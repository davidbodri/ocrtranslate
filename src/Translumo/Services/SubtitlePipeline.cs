using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OpenCvSharp;
using Translumo.Configuration;
using Translumo.OCR;
using Translumo.OCR.Configuration;
using Translumo.Processing.Exceptions;
using Translumo.Processing.Interfaces;
using Translumo.Translation;
using Translumo.Translation.Configuration;
using Translumo.Utils.Extensions;

namespace Translumo.Services
{
    public class SubtitleBlockEventArgs : EventArgs
    {
        public string Speaker { get; }
        public string Dialogue { get; }
        public string SourceText { get; }
        public bool SpeakerOnOwnLine { get; }

        public SubtitleBlockEventArgs(string speaker, string dialogue, string sourceText, bool speakerOnOwnLine)
        {
            Speaker = speaker;
            Dialogue = dialogue;
            SourceText = sourceText;
            SpeakerOnOwnLine = speakerOnOwnLine;
        }
    }

    public class SubtitlePipeline : IDisposable
    {
        public event EventHandler<SubtitleBlockEventArgs> SubtitleReady;
        public event EventHandler HideRequested;

        public bool IsRunning => _cts != null && !_cts.IsCancellationRequested;

        private readonly ICapturerFactory _capturerFactory;
        private readonly OcrEnginesFactory _ocrEnginesFactory;
        private readonly TranslatorFactory _translatorFactory;
        private readonly ChatWindowConfiguration _configuration;
        private readonly TranslationConfiguration _translationConfiguration;
        private readonly OcrGeneralConfiguration _ocrGeneralConfiguration;
        private readonly ScreenCaptureConfiguration _screenCaptureConfiguration;
        private readonly GlossaryService _glossaryService;
        private readonly ILogger _logger;

        private readonly ConcurrentDictionary<string, string> _translationCache = new(StringComparer.OrdinalIgnoreCase);

        private volatile ITranslator _translator;
        private volatile IOCREngine _cachedPrimaryOcr;

        private CancellationTokenSource _cts;
        private Task _loopTask;
        private IScreenCapturer _capturer;
        private long _currentSequence;
        private string _lastShownSourceNormalized = string.Empty;

        private readonly OpenCvSharp.Size _scaledSize = new(200, 50);

        public SubtitlePipeline(
            ICapturerFactory capturerFactory,
            OcrEnginesFactory ocrEnginesFactory,
            TranslatorFactory translatorFactory,
            ChatWindowConfiguration configuration,
            TranslationConfiguration translationConfiguration,
            OcrGeneralConfiguration ocrGeneralConfiguration,
            ScreenCaptureConfiguration screenCaptureConfiguration,
            GlossaryService glossaryService,
            ILogger<SubtitlePipeline> logger = null)
        {
            _capturerFactory = capturerFactory;
            _ocrEnginesFactory = ocrEnginesFactory;
            _translatorFactory = translatorFactory;
            _configuration = configuration;
            _translationConfiguration = translationConfiguration;
            _ocrGeneralConfiguration = ocrGeneralConfiguration;
            _screenCaptureConfiguration = screenCaptureConfiguration;
            _glossaryService = glossaryService;
            _logger = logger;

            EnsureEnginesAndTranslator();

            _translationConfiguration.PropertyChanged += OnTranslationConfigChanged;
            _ocrGeneralConfiguration.PropertyChanged += OnOcrConfigChanged;
        }

        private void OnTranslationConfigChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(TranslationConfiguration.Translator) ||
                e.PropertyName == nameof(TranslationConfiguration.TranslateFromLang) ||
                e.PropertyName == nameof(TranslationConfiguration.TranslateToLang))
            {
                try
                {
                    _translator = _translatorFactory.CreateTranslator(_translationConfiguration);
                    _cachedPrimaryOcr = null;
                    _translationCache.Clear();
                    LogInfo("Translator refreshed on configuration change");
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Failed to refresh translator");
                }
            }
        }

        private void OnOcrConfigChanged(object sender, PropertyChangedEventArgs e)
        {
            _cachedPrimaryOcr = null;
            LogInfo("OCR engines cache invalidated on configuration change");
        }

        private void EnsureEnginesAndTranslator()
        {
            if (_translator == null)
            {
                try
                {
                    _translator = _translatorFactory.CreateTranslator(_translationConfiguration);
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Failed to create translator");
                }
            }

            if (_cachedPrimaryOcr == null)
            {
                try
                {
                    var engines = _ocrEnginesFactory
                        .GetEngines(_ocrGeneralConfiguration.OcrConfigurations, _translationConfiguration.TranslateFromLang)
                        .ToArray();
                    _cachedPrimaryOcr = engines.OrderByDescending(e => e.PrimaryPriority).FirstOrDefault();
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Failed to initialize OCR engine");
                }
            }
        }

        public void Start()
        {
            if (IsRunning)
            {
                return;
            }

            _cts = new CancellationTokenSource();
            _loopTask = Task.Run(() => PipelineLoopAsync(_cts.Token));
            LogInfo("SubtitlePipeline started");
        }

        public void Stop()
        {
            if (!IsRunning)
            {
                return;
            }

            _cts?.Cancel();
            try
            {
                _loopTask?.Wait(500);
            }
            catch { }

            _cts?.Dispose();
            _cts = null;
            _loopTask = null;

            _capturer?.Dispose();
            _capturer = null;

            _lastShownSourceNormalized = string.Empty;
            HideRequested?.Invoke(this, EventArgs.Empty);
            LogInfo("SubtitlePipeline stopped");
        }

        private async Task PipelineLoopAsync(CancellationToken token)
        {
            _capturer = _capturerFactory.CreateCapturer(false);
            if (_capturer == null)
            {
                _logger?.LogError("Failed to create capturer for SubtitlePipeline");
                SubtitleReady?.Invoke(this, new SubtitleBlockEventArgs("bdstudio.hu", "Nem sikerült a képernyőrögzítőt elindítani.", string.Empty, false));
                return;
            }

            var scaledMat = new Mat(_scaledSize, MatType.CV_8UC1);
            var prevMat = new Mat(_scaledSize, MatType.CV_8UC1);
            var diffMat = new Mat(_scaledSize, MatType.CV_8UC1);

            bool isFirstFrame = true;
            var swWatchdog = Stopwatch.StartNew();

            try
            {
                while (!token.IsCancellationRequested)
                {
                    await Task.Delay(60, token).ConfigureAwait(false);

                    if (token.IsCancellationRequested)
                    {
                        break;
                    }

                    if (_screenCaptureConfiguration.CaptureArea.IsEmpty)
                    {
                        SubtitleReady?.Invoke(this, new SubtitleBlockEventArgs(
                            "bdstudio.hu",
                            "Kérjük, jelölje ki a feliratterületet (Alt + Q)",
                            string.Empty,
                            false));
                        await Task.Delay(1000, token).ConfigureAwait(false);
                        continue;
                    }

                    byte[] screenBytes = null;
                    try
                    {
                        screenBytes = _capturer.CaptureScreen();
                    }
                    catch (CaptureException ex)
                    {
                        _logger?.LogWarning("CaptureScreen failed: {Message}", ex.Message);
                        await Task.Delay(300, token).ConfigureAwait(false);
                        continue;
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogTrace("CaptureScreen exception: {Message}", ex.Message);
                        continue;
                    }

                    if (screenBytes == null || screenBytes.Length == 0)
                    {
                        continue;
                    }

                    using (var rawMat = Cv2.ImDecode(screenBytes, ImreadModes.Grayscale))
                    {
                        if (rawMat.Empty())
                        {
                            continue;
                        }
                        Cv2.Resize(rawMat, scaledMat, _scaledSize, 0, 0, InterpolationFlags.Area);
                    }

                    if (isFirstFrame)
                    {
                        scaledMat.CopyTo(prevMat);
                        isFirstFrame = false;
                        await ProcessFrameAsync(screenBytes, token).ConfigureAwait(false);
                        swWatchdog.Restart();
                        continue;
                    }

                    // Compute frame difference
                    Cv2.Absdiff(scaledMat, prevMat, diffMat);
                    double changeDiff = Cv2.Mean(diffMat).Val0;
                    scaledMat.CopyTo(prevMat);

                    bool isShowing = !string.IsNullOrEmpty(_lastShownSourceNormalized);

                    // Run OCR if:
                    // 1. Pixels changed (changeDiff >= 0.5)
                    // 2. Or watchdog check (if subtitle is currently showing, verify every 300ms)
                    if (changeDiff >= 0.5 || (isShowing && swWatchdog.ElapsedMilliseconds >= 300))
                    {
                        swWatchdog.Restart();
                        await ProcessFrameAsync(screenBytes, token).ConfigureAwait(false);
                    }
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Unexpected error in SubtitlePipeline loop");
            }
            finally
            {
                scaledMat.Dispose();
                prevMat.Dispose();
                diffMat.Dispose();
            }
        }

        private async Task ProcessFrameAsync(byte[] screenBytes, CancellationToken token)
        {
            EnsureEnginesAndTranslator();
            var primaryOcr = _cachedPrimaryOcr;
            if (primaryOcr == null)
            {
                return;
            }

            string[] ocrLines;
            try
            {
                ocrLines = primaryOcr.GetTextLines(screenBytes);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "OCR failed in SubtitlePipeline");
                return;
            }

            var cleanLines = ocrLines?
                .Select(l => l?.Trim())
                .Where(l => !string.IsNullOrWhiteSpace(l))
                .ToArray();

            bool hasLetters = cleanLines != null && cleanLines.Any(line => line.Any(char.IsLetter));

            // 1. If NO text/letters on screen:
            if (!hasLetters)
            {
                if (!string.IsNullOrEmpty(_lastShownSourceNormalized))
                {
                    _lastShownSourceNormalized = string.Empty;
                    LogSubtitleEvent("Dialogue disappeared from screen, hiding immediately");
                    HideRequested?.Invoke(this, EventArgs.Empty);
                }
                return;
            }

            // 2. Parse speaker and dialogue
            ParseSpeakerAndDialogue(cleanLines, out string speaker, out string dialogue, out bool speakerOnOwnLine);

            if (string.IsNullOrWhiteSpace(dialogue) || !dialogue.Any(char.IsLetter))
            {
                if (!string.IsNullOrEmpty(_lastShownSourceNormalized))
                {
                    _lastShownSourceNormalized = string.Empty;
                    HideRequested?.Invoke(this, EventArgs.Empty);
                }
                return;
            }

            string normalizedDialogue = NormalizeForComparison(dialogue);
            if (string.IsNullOrEmpty(normalizedDialogue))
            {
                return;
            }

            // 3. Noise / Identity check against currently shown text
            if (IsSameTextOrOcrNoise(normalizedDialogue, _lastShownSourceNormalized))
            {
                return;
            }

            // 4. New text appeared! Translate immediately!
            long seq = Interlocked.Increment(ref _currentSequence);

            string translatedDialogue = null;
            if (_translationCache.TryGetValue(normalizedDialogue, out var cached))
            {
                translatedDialogue = cached;
                LogPerf("Translation cache hit");
            }
            else
            {
                if (_translator == null)
                {
                    return;
                }

                try
                {
                    var transTask = _translator.TranslateTextAsync(dialogue);
                    var delayTask = Task.Delay(5000, token);
                    var completedTask = await Task.WhenAny(transTask, delayTask).ConfigureAwait(false);

                    if (completedTask == transTask)
                    {
                        translatedDialogue = await transTask.ConfigureAwait(false);
                    }
                    else
                    {
                        LogSubtitleEvent("Translation timed out after 5000ms");
                        return;
                    }
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Translation failed in SubtitlePipeline");
                    return;
                }
            }

            if (token.IsCancellationRequested || seq < Volatile.Read(ref _currentSequence))
            {
                // Stale result discarded
                return;
            }

            if (string.IsNullOrWhiteSpace(translatedDialogue))
            {
                return;
            }

            // Apply Glossary post-translation replacements
            translatedDialogue = _glossaryService.ApplyPostTranslation(translatedDialogue);

            // Cache result
            _translationCache[normalizedDialogue] = translatedDialogue;
            _lastShownSourceNormalized = normalizedDialogue;

            // Optional speaker translation
            string finalSpeaker = speaker;
            if (_configuration.TranslateSpeakerNames && !string.IsNullOrWhiteSpace(speaker) && _translator != null)
            {
                try
                {
                    var speakerTask = _translator.TranslateTextAsync(speaker);
                    var delayTask = Task.Delay(1500, token);
                    if (await Task.WhenAny(speakerTask, delayTask).ConfigureAwait(false) == speakerTask)
                    {
                        string transSpeaker = await speakerTask.ConfigureAwait(false);
                        if (!string.IsNullOrWhiteSpace(transSpeaker))
                        {
                            finalSpeaker = transSpeaker;
                        }
                    }
                }
                catch { }
            }

            LogSubtitleEvent($"Subtitle ready: [{finalSpeaker}] \"{translatedDialogue}\"");

            SubtitleReady?.Invoke(this, new SubtitleBlockEventArgs(finalSpeaker, translatedDialogue, dialogue, speakerOnOwnLine));
        }

        private static void ParseSpeakerAndDialogue(
            string[] lines,
            out string speaker,
            out string dialogue,
            out bool speakerOnOwnLine)
        {
            speaker = null;
            speakerOnOwnLine = false;

            if (lines == null || lines.Length == 0)
            {
                dialogue = string.Empty;
                return;
            }

            var colonRegex = new Regex(@"^(?<speaker>[A-Za-z0-9 _'.-]{1,30}):\s*(?<rest>.*)$", RegexOptions.Compiled);
            var bracketRegex = new Regex(@"^[\[\(](?<speaker>[A-Za-z0-9 _'.-]{1,30})[\]\)]\s*:?\s*(?<rest>.*)$", RegexOptions.Compiled);

            var match = colonRegex.Match(lines[0]);
            if (!match.Success)
            {
                match = bracketRegex.Match(lines[0]);
            }

            if (match.Success)
            {
                speaker = match.Groups["speaker"].Value.Trim();
                string rest = match.Groups["rest"].Value.Trim();

                if (string.IsNullOrEmpty(rest))
                {
                    speakerOnOwnLine = true;
                    dialogue = string.Join(" ", lines.Skip(1));
                }
                else
                {
                    speakerOnOwnLine = false;
                    var remaining = new List<string> { rest };
                    remaining.AddRange(lines.Skip(1));
                    dialogue = string.Join(" ", remaining);
                }
            }
            else
            {
                dialogue = string.Join(" ", lines);
            }
        }

        public static string NormalizeForComparison(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            string s = text.Trim().ToLowerInvariant();
            s = s.Replace('1', 'l').Replace('i', 'l').Replace('0', 'o');
            s = Regex.Replace(s, @"[^\w\s]", "");
            s = Regex.Replace(s, @"\s+", " ");

            return s.Trim();
        }

        private static bool IsSameTextOrOcrNoise(string newText, string shownText)
        {
            if (string.Equals(newText, shownText, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (string.IsNullOrEmpty(shownText) || string.IsNullOrEmpty(newText))
            {
                return false;
            }

            // If length differs by more than 3 characters or 15%, it's new/different dialogue
            int lenDiff = Math.Abs(newText.Length - shownText.Length);
            if (lenDiff > Math.Max(3, (int)(shownText.Length * 0.15)))
            {
                return false;
            }

            double sim = newText.GetJaroSimilarity(shownText);
            return sim >= 0.88;
        }

        private void LogPerf(string message)
        {
            if (_configuration.PerfLog)
            {
                _logger?.LogInformation("[SubtitlePipeline-PERF] {Message}", message);
            }
        }

        private void LogSubtitleEvent(string message)
        {
            if (_configuration.LogSubtitleEvents || _configuration.PerfLog)
            {
                _logger?.LogInformation("[SubtitlePipeline-EVENT] {Message}", message);
            }
        }

        private void LogInfo(string message)
        {
            _logger?.LogInformation("[SubtitlePipeline] {Message}", message);
        }

        public void Dispose()
        {
            _translationConfiguration.PropertyChanged -= OnTranslationConfigChanged;
            _ocrGeneralConfiguration.PropertyChanged -= OnOcrConfigChanged;
            Stop();
        }
    }
}
