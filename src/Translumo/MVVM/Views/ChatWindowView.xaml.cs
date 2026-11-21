using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Translumo.Configuration;
using Translumo.Infrastructure;
using Translumo.MVVM.Models;
using Translumo.MVVM.ViewModels;
using Translumo.Services;
using Translumo.Utils;

namespace Translumo.MVVM.Views
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class ChatWindowView : Window
    {
        [DllImport("user32.dll")]
        private static extern bool SetWindowDisplayAffinity(IntPtr hwnd, uint affinity);

        private const uint WDA_EXCLUDEFROMCAPTURE = 0x11;
        private ChatWindowConfiguration _configuration;
        private DispatcherTimer _hideTimer;
        private bool _isFadingOut;
        private SubtitleLineTracker _lineTracker;

        public ChatWindowView()
        {
            InitializeComponent();
        }

        private void ModelOnChatFirstItemsRemoved(object sender, ChatFirstItemsRemovedEventArgs e)
        {
            RemoveFirstTextBlocks(e.NumberRemoved);
            if (_configuration?.AutoHideEnabled == true && rtbChat.Document.Blocks.Count == 0)
            {
                _lineTracker?.Clear();
                _hideTimer?.Stop();
                StartFadeOut();
            }
        }

        private void ModelOnChatCleared(object sender, EventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                _lineTracker?.Clear();
                rtbChat.Document.Blocks.Clear();
                if (_configuration?.AutoHideEnabled == true)
                {
                    HideImmediately();
                }
            });
        }

        private void ModelOnChatItemAdded(object sender, ChatItemAddedEventArgs e)
        {
            if (_configuration?.AutoHideEnabled == true)
            {
                // In subtitle mode (AutoHide), non-empty translated text is handled via TextWithOriginalRaised.
                // But empty text notifications must reach DisplaySubtitle to retire missing lines when text disappears.
                if (e.TextType == TextTypes.Translation && !string.IsNullOrWhiteSpace(e.Text))
                {
                    return;
                }

                var viewModel = DataContext as ChatWindowViewModel;
                if (e.TextType == TextTypes.Info && viewModel?.Model?.TranslationIsRunning != true)
                {
                    return;
                }
                DisplaySubtitle(e.Text, e.TextType);
            }
            {
                AppendTextBlock(e.Text, e.TextType);
            }
        }

        private void DisplaySubtitle(string text, TextTypes textType)
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(() => DisplaySubtitle(text, textType));
                return;
            }

            var rawLines = SubtitleLineTracker.SplitIntoLinesOrSentences(text, _configuration?.SplitBySentenceFallback ?? true);
            var trackedLines = _lineTracker?.UpdateLines(rawLines);

            if (_lineTracker == null || !_lineTracker.HasActiveLines)
            {
                if (!_isFadingOut && this.Opacity > 0.0)
                {
                    StartFadeOut();
                }
                return;
            }

            if (_isFadingOut)
            {
                this.BeginAnimation(UIElement.OpacityProperty, null);
                _isFadingOut = false;
            }
            this.Opacity = 1.0;

            RenderTrackedLines(trackedLines, textType);
            ResetHideTimer();
        }

        private void RenderTrackedLines(IReadOnlyList<TrackedSubtitleLine> trackedLines, TextTypes textType)
        {
            rtbChat.Document.Blocks.Clear();
            if (trackedLines == null || trackedLines.Count == 0)
            {
                return;
            }

            string previousText = null;
            foreach (var line in trackedLines)
            {
                if (!line.IsActive)
                {
                    continue;
                }

                string displayText = line.DisplayText;
                if (string.IsNullOrWhiteSpace(displayText))
                {
                    if (_configuration?.ShowOriginalWhileTranslating == true)
                    {
                        displayText = line.OriginalText;
                    }
                    else if (_configuration?.KeepEmptySlotSpace == true)
                    {
                        displayText = " ";
                    }
                    else
                    {
                        continue;
                    }
                }

                if (string.IsNullOrWhiteSpace(displayText))
                {
                    continue;
                }

                // Prevent rendering duplicate lines consecutively
                if (!string.IsNullOrWhiteSpace(previousText) &&
                    string.Equals(displayText?.Trim(), previousText.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                previousText = displayText;

                var paragraph = new Paragraph { LineHeight = fontTextBlockInstance.LineHeight, TextAlignment = fontTextBlockInstance.TextAlignment, Margin = new Thickness(0, 0, 0, 4) };
                var run = GetRunInstance(textType).Clone(displayText);
                paragraph.Inlines.Add(run);
                rtbChat.Document.Blocks.Add(paragraph);
            }
        }

        private void ViewModelOnTextWithOriginalRaised(object sender, TranslatedWithOriginalEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                if (string.IsNullOrEmpty(e.Translated))
                {
                    if (_configuration?.ShowOriginalWhileTranslating == true)
                    {
                        DisplaySubtitleOriginal(e.Original);
                    }
                }
                else
                {
                    var rawLines = SubtitleLineTracker.SplitIntoLinesOrSentences(e.Original, _configuration?.SplitBySentenceFallback ?? true);
                    _lineTracker?.UpdateLines(rawLines);

                    _lineTracker?.UpdateTranslation(e.Original, e.Translated);
                    if (_lineTracker != null && _lineTracker.HasActiveLines)
                    {
                        if (_isFadingOut)
                        {
                            this.BeginAnimation(UIElement.OpacityProperty, null);
                            _isFadingOut = false;
                        }
                        this.Opacity = 1.0;
                        RenderTrackedLines(_lineTracker.GetCurrentLines(), TextTypes.Translation);
                        ResetHideTimer();
                    }
                }
            });
        }

        private void DisplaySubtitleOriginal(string originalText)
        {
            if (string.IsNullOrWhiteSpace(originalText))
            {
                return;
            }

            var rawLines = SubtitleLineTracker.SplitIntoLinesOrSentences(originalText, _configuration?.SplitBySentenceFallback ?? true);
            var trackedLines = _lineTracker?.UpdateLines(rawLines);

            if (_lineTracker == null || !_lineTracker.HasActiveLines)
            {
                return;
            }

            if (_isFadingOut)
            {
                this.BeginAnimation(UIElement.OpacityProperty, null);
                _isFadingOut = false;
            }
            this.Opacity = 1.0;

            RenderTrackedLines(trackedLines, TextTypes.Translation);
            ResetHideTimer();
        }

        private void ResetHideTimer()
        {
            if (_hideTimer == null || _configuration == null)
            {
                return;
            }

            int delay = Math.Max(100, _configuration.IdleHideMs);
            _hideTimer.Stop();
            _hideTimer.Interval = TimeSpan.FromMilliseconds(delay);
            _hideTimer.Start();
        }

        private void HideTimerOnTick(object sender, EventArgs e)
        {
            _hideTimer.Stop();

            if (_configuration?.AutoHideEnabled != true)
            {
                return;
            }

            if (_lineTracker != null && _lineTracker.HasActiveLines)
            {
                ResetHideTimer();
                return;
            }

            StartFadeOut();
        }

        public void HideImmediately()
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(HideImmediately);
                return;
            }

            this.BeginAnimation(UIElement.OpacityProperty, null);
            this.Opacity = 0.0;
            _isFadingOut = false;
            _hideTimer?.Stop();
            _lineTracker?.Clear();
            rtbChat.Document.Blocks.Clear();
        }

        private void StartFadeOut()
        {
            if (_isFadingOut || this.Opacity == 0.0)
            {
                return;
            }

            int fadeMs = _configuration?.FadeOutMs ?? 150;
            if (fadeMs <= 0)
            {
                HideImmediately();
                return;
            }

            _isFadingOut = true;
            var fadeAnimation = new DoubleAnimation
            {
                From = this.Opacity,
                To = 0.0,
                Duration = TimeSpan.FromMilliseconds(fadeMs)
            };

            fadeAnimation.Completed += (s, args) =>
            {
                if (_isFadingOut)
                {
                    this.BeginAnimation(UIElement.OpacityProperty, null);
                    this.Opacity = 0.0;
                    _isFadingOut = false;
                    _lineTracker?.Clear();
                    rtbChat.Document.Blocks.Clear();
                }
            };

            this.BeginAnimation(UIElement.OpacityProperty, fadeAnimation);
        }

        private void RemoveFirstTextBlocks(int numberBlocks)
        {
            var blocksCollection = rtbChat.Document.Blocks;
            var toDelete = Math.Min(numberBlocks, blocksCollection.Count);
            for (var i = 0; i < toDelete; i++)
            {
                blocksCollection.Remove(blocksCollection.FirstBlock);
            }
        }

        private void AppendTextBlock(string text, TextTypes textType)
        {
            if (string.IsNullOrEmpty(text))
                return;

            rtbChat.CaretPosition = rtbChat.CaretPosition.DocumentEnd;

            var paragraph = new Paragraph { LineHeight = fontTextBlockInstance.LineHeight, TextAlignment = fontTextBlockInstance.TextAlignment};
            var run = GetRunInstance(textType).Clone(text);
            paragraph.Inlines.Add(run);
            rtbChat.Document.Blocks.Add(paragraph);

            rtbChat.ScrollToEnd();
        }

        private void rtbChat_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                if (_configuration?.AutoHideEnabled == true)
                {
                    _hideTimer?.Stop();
                    if (_isFadingOut)
                    {
                        this.BeginAnimation(UIElement.OpacityProperty, null);
                        this.Opacity = 1.0;
                        _isFadingOut = false;
                        UpdateBlur();
                    }
                }

                this.DragMove();

                if (_configuration?.AutoHideEnabled == true && rtbChat.Document.Blocks.Count > 0)
                {
                    ResetHideTimer();
                }
            }
        }

        private void Window_Activated(object sender, EventArgs e)
        {
            this.Topmost = true;
        }

        private void Window_Deactivated(object sender, EventArgs e)
        {
            this.Topmost = true;
            this.Activate();
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);

            var viewModel = DataContext as ChatWindowViewModel;
            if (viewModel?.Model?.Configuration != null)
            {
                _configuration = viewModel.Model.Configuration;
                _configuration.PropertyChanged += ConfigurationOnPropertyChanged;
                this.Closed += Window_Closed;

                viewModel.Model.ChatCleared += ModelOnChatCleared;
                _lineTracker = new SubtitleLineTracker(_configuration);
                viewModel.TextWithOriginalRaised += ViewModelOnTextWithOriginalRaised;

                _hideTimer = new DispatcherTimer(DispatcherPriority.Normal, this.Dispatcher);
                _hideTimer.Tick += HideTimerOnTick;

                UpdateBlur();
                if (_configuration.AutoHideEnabled)
                {
                    this.Opacity = 0.0;
                }
            }
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            var viewModel = DataContext as ChatWindowViewModel;
            if (viewModel != null)
            {
                viewModel.TextWithOriginalRaised -= ViewModelOnTextWithOriginalRaised;
                if (viewModel.Model != null)
                {
                    viewModel.Model.ChatCleared -= ModelOnChatCleared;
                }
            }

            if (_hideTimer != null)
            {
                _hideTimer.Stop();
                _hideTimer.Tick -= HideTimerOnTick;
                _hideTimer = null;
            }

            if (_configuration != null)
            {
                _configuration.PropertyChanged -= ConfigurationOnPropertyChanged;
                _configuration = null;
            }
        }

        private void ConfigurationOnPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ChatWindowConfiguration.BlurEnabled) ||
                e.PropertyName == nameof(ChatWindowConfiguration.BackgroundColor) ||
                e.PropertyName == nameof(ChatWindowConfiguration.BackgroundOpacity) ||
                e.PropertyName == nameof(ChatWindowConfiguration.PerformanceBlur))
            {
                Dispatcher.Invoke(UpdateBlur);
            }
            else if (e.PropertyName == nameof(ChatWindowConfiguration.AutoHideEnabled))
            {
                Dispatcher.Invoke(() =>
                {
                    if (!_configuration.AutoHideEnabled)
                    {
                        _hideTimer?.Stop();
                        this.BeginAnimation(UIElement.OpacityProperty, null);
                        this.Opacity = 1.0;
                        _isFadingOut = false;
                        UpdateBlur();
                    }
                    else if (rtbChat.Document.Blocks.Count == 0)
                    {
                        this.Opacity = 0.0;
                    }
                });
            }
            else if (e.PropertyName == nameof(ChatWindowConfiguration.HideDelayMs) ||
                     e.PropertyName == nameof(ChatWindowConfiguration.IdleHideMs))
            {
                Dispatcher.Invoke(() =>
                {
                    if (_hideTimer != null && _hideTimer.IsEnabled)
                    {
                        _hideTimer.Interval = TimeSpan.FromMilliseconds(Math.Max(50, _configuration.IdleHideMs));
                    }
                });
            }
        }

        private void UpdateBlur()
        {
            if (_configuration == null)
            {
                return;
            }

            if (_configuration.BlurEnabled)
            {
                bool perf = _configuration.PerformanceBlur;
                if (WindowBlurHelper.Enable(this, _configuration.BackgroundColor, _configuration.BackgroundOpacity, perf))
                {
                    this.Background = Brushes.Transparent;
                }
                else
                {
                    this.Background = new SolidColorBrush(_configuration.BackgroundColor)
                    {
                        Opacity = _configuration.BackgroundOpacity
                    };
                }
            }
            else
            {
                WindowBlurHelper.Disable(this);
                this.Background = new SolidColorBrush(_configuration.BackgroundColor)
                {
                    Opacity = _configuration.BackgroundOpacity
                };
            }
        }

        private void Window_SourceInitialized(object sender, EventArgs e)
        {
            var viewModel = DataContext as ChatWindowViewModel;

            viewModel.Model.ChatItemAdded += ModelOnChatItemAdded;
            viewModel.Model.ChatFirstItemsRemoved += ModelOnChatFirstItemsRemoved;
            viewModel.Model.Configuration.ExcludeFromCaptueChanged += Configuration_ExcludeFromCaptueChanged;

            ExcludeFromCapture(viewModel.Model.Configuration.ExcludeFromCaptue);
        }

        private void Configuration_ExcludeFromCaptueChanged(object sender, EventArgs e)
        {
            if (sender is ChatWindowConfiguration config)
            {
                bool exclude = config.ExcludeFromCaptue;
                ExcludeFromCapture(exclude);
            }
        }

        // Delay setting display affinity until window handle is created
        // Exclude ChatWindow to be captured in screenshot
        // Requires build 10.0.19041
        private void ExcludeFromCapture(bool exclude)
        {
            var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;

            if (exclude)
                SetWindowDisplayAffinity(hwnd, WDA_EXCLUDEFROMCAPTURE);
            else
                SetWindowDisplayAffinity(hwnd, 0); // allow capture
        }

        private Run GetRunInstance(TextTypes textType)
        {
            return textType switch
            {
                TextTypes.Info => fontRunInfoInstance,
                TextTypes.Error => fontRunErrorInstance,
                _ => fontRunInstance,
            };
        }
    }
}
