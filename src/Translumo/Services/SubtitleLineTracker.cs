using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Translumo.Configuration;
using Translumo.Utils.Extensions;

namespace Translumo.Services
{
    public class TrackedSubtitleLine
    {
        public int SlotIndex { get; set; }
        public string OriginalText { get; set; }
        public string TranslatedText { get; set; }
        public bool IsTranslating { get; set; }
        public DateTime LastSeen { get; set; }
        public int MissingScanCount { get; set; }
        public long Version { get; set; }
        public bool IsActive { get; set; } = true;

        public string DisplayText => TranslatedText;
    }

    public class SubtitleLineTracker
    {
        private readonly ChatWindowConfiguration _configuration;
        private readonly ConcurrentDictionary<string, string> _translationCache = new(StringComparer.OrdinalIgnoreCase);
        private readonly List<TrackedSubtitleLine> _activeLines = new();
        private readonly object _lock = new();

        public SubtitleLineTracker(ChatWindowConfiguration configuration)
        {
            _configuration = configuration;
        }

        public string NormalizeText(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            return Regex.Replace(text.Trim().ToLowerInvariant(), @"[^\w\s]", "");
        }

        public static string[] SplitIntoLinesOrSentences(string rawText, bool fallbackToSentences)
        {
            if (string.IsNullOrWhiteSpace(rawText))
            {
                return Array.Empty<string>();
            }

            var lines = rawText.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries)
                               .Select(l => l.Trim())
                               .Where(l => !string.IsNullOrEmpty(l))
                               .ToArray();

            if (lines.Length > 1 || !fallbackToSentences)
            {
                return lines;
            }

            var single = lines.Length == 1 ? lines[0] : rawText.Trim();
            var sentenceMatches = Regex.Matches(single, @"(?<sentence>.+?([.!?]+(\s+|$)|$))");
            var sentences = new List<string>();
            foreach (Match m in sentenceMatches)
            {
                var s = m.Groups["sentence"].Value.Trim();
                if (!string.IsNullOrEmpty(s))
                {
                    sentences.Add(s);
                }
            }

            return sentences.Count > 1 ? sentences.ToArray() : lines;
        }

        public string GetCachedTranslation(string originalText)
        {
            var normalized = NormalizeText(originalText);
            if (string.IsNullOrEmpty(normalized))
            {
                return null;
            }

            if (_translationCache.TryGetValue(normalized, out var exact))
            {
                return exact;
            }

            // Fuzzy matching with higher threshold
            double minThreshold = (_configuration?.LineMatchThreshold ?? 85) / 100.0;
            foreach (var kvp in _translationCache)
            {
                if (kvp.Key.GetJaroSimilarity(normalized) >= minThreshold)
                {
                    return kvp.Value;
                }
            }

            return null;
        }

        public void CacheTranslation(string originalText, string translatedText)
        {
            var normalized = NormalizeText(originalText);
            if (!string.IsNullOrEmpty(normalized) && !string.IsNullOrWhiteSpace(translatedText))
            {
                _translationCache[normalized] = translatedText;
            }
        }

        public IReadOnlyList<TrackedSubtitleLine> UpdateLines(string[] incomingLines)
        {
            lock (_lock)
            {
                int maxLines = _configuration?.MaxVisibleLines ?? 6;
                double minSimilarity = (_configuration?.LineMatchThreshold ?? 85) / 100.0;
                var now = DateTime.UtcNow;

                var cleanedIncoming = incomingLines
                    .Where(l => !string.IsNullOrWhiteSpace(l))
                    .Take(maxLines)
                    .Select(l => l.Trim())
                    .ToList();

                if (cleanedIncoming.Count == 0)
                {
                    // Screen is empty - increment missing count on active lines
                    foreach (var line in _activeLines)
                    {
                        if (line.IsActive)
                        {
                            line.MissingScanCount++;
                            if (line.MissingScanCount >= 3)
                            {
                                line.IsActive = false;
                            }
                        }
                    }

                    _activeLines.RemoveAll(s => !s.IsActive);
                    return _activeLines;
                }

                var matchedIncomingIndices = new HashSet<int>();
                var matchedActiveLines = new HashSet<TrackedSubtitleLine>();

                // 1. Match incoming lines to existing active lines
                for (int slot = 0; slot < _activeLines.Count; slot++)
                {
                    var existing = _activeLines[slot];
                    if (!existing.IsActive)
                    {
                        continue;
                    }

                    int bestIncomingIdx = -1;
                    double bestScore = 0;

                    for (int i = 0; i < cleanedIncoming.Count; i++)
                    {
                        if (matchedIncomingIndices.Contains(i))
                        {
                            continue;
                        }

                        var incomingNorm = NormalizeText(cleanedIncoming[i]);
                        var existingNorm = NormalizeText(existing.OriginalText);

                        if (incomingNorm == existingNorm)
                        {
                            bestIncomingIdx = i;
                            bestScore = 1.0;
                            break;
                        }

                        double sim = incomingNorm.GetJaroSimilarity(existingNorm);
                        if (sim >= minSimilarity && sim > bestScore)
                        {
                            bestScore = sim;
                            bestIncomingIdx = i;
                        }
                    }

                    if (bestIncomingIdx >= 0)
                    {
                        matchedIncomingIndices.Add(bestIncomingIdx);
                        matchedActiveLines.Add(existing);
                        existing.LastSeen = now;
                        existing.MissingScanCount = 0;
                    }
                }

                // If NONE of the incoming lines matched any existing active line,
                // it means the subtitle on screen has completely changed to a new subtitle!
                // Clear old lines so they don't linger above or underneath the new subtitle.
                if (matchedIncomingIndices.Count == 0 && _activeLines.Count > 0)
                {
                    _activeLines.Clear();
                }
                else
                {
                    // For active lines that were NOT matched in this scan:
                    foreach (var existing in _activeLines)
                    {
                        if (!matchedActiveLines.Contains(existing) && existing.IsActive)
                        {
                            existing.MissingScanCount++;
                            if (existing.MissingScanCount >= 2)
                            {
                                existing.IsActive = false;
                            }
                        }
                    }
                    _activeLines.RemoveAll(s => !s.IsActive);
                }

                // 2. Add unmatched incoming lines
                for (int i = 0; i < cleanedIncoming.Count; i++)
                {
                    if (matchedIncomingIndices.Contains(i))
                    {
                        continue;
                    }

                    var newText = cleanedIncoming[i];
                    var cached = GetCachedTranslation(newText);

                    var newLine = new TrackedSubtitleLine
                    {
                        SlotIndex = _activeLines.Count,
                        OriginalText = newText,
                        TranslatedText = cached,
                        IsTranslating = string.IsNullOrEmpty(cached),
                        LastSeen = now,
                        MissingScanCount = 0,
                        Version = 1,
                        IsActive = true
                    };
                    _activeLines.Add(newLine);
                }

                for (int s = 0; s < _activeLines.Count; s++)
                {
                    _activeLines[s].SlotIndex = s;
                }

                return _activeLines.Where(s => s.IsActive).ToList();
            }
        }

        public void UpdateTranslation(string originalText, string translatedText)
        {
            if (string.IsNullOrWhiteSpace(originalText) || string.IsNullOrWhiteSpace(translatedText))
            {
                return;
            }

            lock (_lock)
            {
                CacheTranslation(originalText, translatedText);

                var active = _activeLines.Where(s => s.IsActive).ToList();
                if (active.Count == 0)
                {
                    return;
                }

                // Split translation into lines
                var transLines = translatedText.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries)
                                               .Select(l => l.Trim())
                                               .Where(l => !string.IsNullOrEmpty(l))
                                               .ToArray();

                if (transLines.Length <= 1 && (_configuration?.SplitBySentenceFallback ?? true))
                {
                    var sentenceSplit = SplitIntoLinesOrSentences(translatedText, true);
                    if (sentenceSplit.Length > 1)
                    {
                        transLines = sentenceSplit;
                    }
                }

                // If only 1 active line on screen, it gets the full translation
                if (active.Count == 1)
                {
                    active[0].TranslatedText = translatedText;
                    active[0].IsTranslating = false;
                    return;
                }

                // Multiple active lines on screen:
                if (transLines.Length == active.Count)
                {
                    // 1-to-1 match between active lines and translated lines
                    for (int i = 0; i < active.Count; i++)
                    {
                        active[i].TranslatedText = transLines[i];
                        active[i].IsTranslating = false;
                        CacheTranslation(active[i].OriginalText, transLines[i]);
                    }
                }
                else if (transLines.Length == 1)
                {
                    // Multi-line original text was translated into a single sentence.
                    // Put the full translation in the first line and deactivate subsequent lines
                    // so no untranslated original text lingers on screen.
                    active[0].TranslatedText = translatedText;
                    active[0].IsTranslating = false;
                    for (int i = 1; i < active.Count; i++)
                    {
                        active[i].TranslatedText = string.Empty;
                        active[i].IsActive = false;
                    }
                }
                else if (transLines.Length < active.Count)
                {
                    for (int i = 0; i < transLines.Length; i++)
                    {
                        active[i].TranslatedText = transLines[i];
                        active[i].IsTranslating = false;
                        CacheTranslation(active[i].OriginalText, transLines[i]);
                    }
                    for (int i = transLines.Length; i < active.Count; i++)
                    {
                        active[i].TranslatedText = string.Empty;
                        active[i].IsActive = false;
                    }
                }
                else // transLines.Length > active.Count
                {
                    for (int i = 0; i < active.Count - 1; i++)
                    {
                        active[i].TranslatedText = transLines[i];
                        active[i].IsTranslating = false;
                        CacheTranslation(active[i].OriginalText, transLines[i]);
                    }
                    active[^1].TranslatedText = string.Join(" ", transLines.Skip(active.Count - 1));
                    active[^1].IsTranslating = false;
                }
            }
        }

        public void Clear()
        {
            lock (_lock)
            {
                _activeLines.Clear();
            }
        }

        public bool HasActiveLines
        {
            get
            {
                lock (_lock)
                {
                    return _activeLines.Any(s => s.IsActive);
                }
            }
        }

        public IReadOnlyList<TrackedSubtitleLine> GetCurrentLines()
        {
            lock (_lock)
            {
                return _activeLines.Where(s => s.IsActive).ToList();
            }
        }
    }
}
