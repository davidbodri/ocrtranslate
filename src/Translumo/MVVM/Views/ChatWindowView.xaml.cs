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

        [DllImport("user32.dll")]
        public static extern bool ReleaseCapture();

        [DllImport("user32.dll")]
        public static extern IntPtr SendMessage(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);

        private const int WM_NCLBUTTONDOWN = 0xA1;
        private const int HT_CAPTION = 0x2;

        private const uint WDA_EXCLUDEFROMCAPTURE = 0x11;
        private ChatWindowConfiguration _configuration;
        private DispatcherTimer _hideTimer;
        private bool _isFadingOut;
        private bool _isObsSetupMode;
        private string _lastDisplayedSubtitle;

        public ChatWindowView()
        {
            InitializeComponent();
        }

        private void ModelOnChatFirstItemsRemoved(object sender, ChatFirstItemsRemovedEventArgs e)
        {
            if (_isObsSetupMode)
            {
                return;
            }

            RemoveFirstTextBlocks(e.NumberRemoved);
            if (_configuration?.AutoHideEnabled == true && rtbChat.Document.Blocks.Count == 0)
            {
                _lastDisplayedSubtitle = null;
                _hideTimer?.Stop();
                StartFadeOut();
            }
        }

        private void ModelOnChatCleared(object sender, EventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                _lastDisplayedSubtitle = null;
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
                if (e.TextType == TextTypes.Info || e.TextType == TextTypes.Error)
                {
                    ShowBlackBar();
                    AppendTextBlock(e.Text, e.TextType);
                    ResetHideTimer();
                    return;
                }

                if (e.TextType == TextTypes.Translation)
                {
                    return;
                }
            }
            else
            {
                AppendTextBlock(e.Text, e.TextType);
            }
        }

        private void ShowBlackBar()
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(ShowBlackBar);
                return;
            }

            if (_isFadingOut)
            {
                this.BeginAnimation(UIElement.OpacityProperty, null);
                _isFadingOut = false;
            }
            UpdateBlur();
            this.Opacity = 1.0;
        }

        private void DisplayTranslatedSubtitle(string text)
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(() => DisplayTranslatedSubtitle(text));
                return;
            }

            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            if (string.Equals(_lastDisplayedSubtitle, text, StringComparison.Ordinal))
            {
                ResetHideTimer();
                return;
            }

            _lastDisplayedSubtitle = text;

            ShowBlackBar();

            rtbChat.Document.Blocks.Clear();

            var lines = text.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                if (string.IsNullOrEmpty(trimmed))
                {
                    continue;
                }

                var paragraph = new Paragraph
                {
                    LineHeight = fontTextBlockInstance.LineHeight,
                    TextAlignment = fontTextBlockInstance.TextAlignment,
                    Margin = new Thickness(0, 0, 0, 4)
                };
                var run = GetRunInstance(TextTypes.Translation).Clone(trimmed);
                paragraph.Inlines.Add(run);
                rtbChat.Document.Blocks.Add(paragraph);
            }

            ResetHideTimer();
        }

        private void ViewModelOnTextWithOriginalRaised(object sender, TranslatedWithOriginalEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                if (!string.IsNullOrWhiteSpace(e.Translated))
                {
                    DisplayTranslatedSubtitle(e.Translated);
                }
                else if (_configuration?.ShowOriginalWhileTranslating == true && !string.IsNullOrWhiteSpace(e.Original))
                {
                    DisplayTranslatedSubtitle(e.Original);
                }
            });
        }

        private void ResetHideTimer()
        {
            if (_isObsSetupMode)
            {
                return;
            }

            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(ResetHideTimer);
                return;
            }

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

            if (_isObsSetupMode || _configuration?.AutoHideEnabled != true)
            {
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
            _lastDisplayedSubtitle = null;
            rtbChat.Document.Blocks.Clear();
            WindowBlurHelper.Disable(this);
            this.Background = Brushes.Transparent;
        }

        private void StartFadeOut()
        {
            if (_isFadingOut || this.Opacity == 0.0)
            {
                return;
            }

            int fadeMs = _configuration?.FadeOutMs ?? 200;
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
                    _lastDisplayedSubtitle = null;
                    rtbChat.Document.Blocks.Clear();
                    WindowBlurHelper.Disable(this);
                    this.Background = Brushes.Transparent;
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

            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(() => AppendTextBlock(text, textType));
                return;
            }

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
                if (_configuration?.AutoHideEnabled == true && !_isObsSetupMode)
                {
                    _hideTimer?.Stop();
                    ShowBlackBar();
                }

                try
                {
                    var helper = new System.Windows.Interop.WindowInteropHelper(this);
                    if (helper.Handle != IntPtr.Zero)
                    {
                        ReleaseCapture();
                        SendMessage(helper.Handle, WM_NCLBUTTONDOWN, (IntPtr)HT_CAPTION, IntPtr.Zero);
                    }
                }
                catch
                {
                }

                if (_configuration?.AutoHideEnabled == true && !_isObsSetupMode)
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
                viewModel.TextWithOriginalRaised += ViewModelOnTextWithOriginalRaised;
                viewModel.ObsSetupModeChanged += ViewModelOnObsSetupModeChanged;

                _hideTimer = new DispatcherTimer(DispatcherPriority.Normal, this.Dispatcher);
                _hideTimer.Tick += HideTimerOnTick;

                UpdateBlur();
                this.Opacity = 1.0;
                if (_configuration.AutoHideEnabled)
                {
                    ResetHideTimer();
                }
            }
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            var viewModel = DataContext as ChatWindowViewModel;
            if (viewModel != null)
            {
                viewModel.ObsSetupModeChanged -= ViewModelOnObsSetupModeChanged;
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
                if (_isObsSetupMode)
                {
                    return;
                }

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
                        HideImmediately();
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
                WindowBlurHelper.Enable(this, _configuration.BackgroundColor, _configuration.BackgroundOpacity, perf);
            }
            else
            {
                WindowBlurHelper.Disable(this);
            }

            this.Background = Brushes.Transparent;
        }

        private void Window_SourceInitialized(object sender, EventArgs e)
        {
            WindowHelper.MakeCapturableInObs(this);

            var viewModel = DataContext as ChatWindowViewModel;

            viewModel.Model.ChatItemAdded += ModelOnChatItemAdded;
            viewModel.Model.ChatFirstItemsRemoved += ModelOnChatFirstItemsRemoved;
            viewModel.Model.Configuration.ExcludeFromCaptueChanged += Configuration_ExcludeFromCaptueChanged;

            ExcludeFromCapture(viewModel.Model.Configuration.ExcludeFromCaptue);
        }

        private void ViewModelOnObsSetupModeChanged(object sender, bool isSetupMode)
        {
            Dispatcher.Invoke(() =>
            {
                _isObsSetupMode = isSetupMode;
                if (isSetupMode)
                {
                    _hideTimer?.Stop();
                    if (_isFadingOut)
                    {
                        this.BeginAnimation(UIElement.OpacityProperty, null);
                        _isFadingOut = false;
                    }
                    UpdateBlur();
                    this.Opacity = 1.0;

                    if (!this.IsVisible)
                    {
                        this.Show();
                    }

                    rtbChat.Document.Blocks.Clear();

                    var p1 = new Paragraph
                    {
                        LineHeight = fontTextBlockInstance.LineHeight,
                        TextAlignment = fontTextBlockInstance.TextAlignment,
                        Margin = new Thickness(0, 0, 0, 4)
                    };
                    var run1 = GetRunInstance(TextTypes.Translation).Clone("BDSTUDIO — Streamlabs / OBS Felirat Minta");
                    p1.Inlines.Add(run1);
                    rtbChat.Document.Blocks.Add(p1);

                    var p2 = new Paragraph
                    {
                        LineHeight = fontTextBlockInstance.LineHeight,
                        TextAlignment = fontTextBlockInstance.TextAlignment,
                        Margin = new Thickness(0, 0, 0, 2)
                    };
                    var run2 = GetRunInstance(TextTypes.Info).Clone("Helyezd el és méretezd ezt az ablakot a közvetítésedben!");
                    p2.Inlines.Add(run2);
                    rtbChat.Document.Blocks.Add(p2);

                    var p3 = new Paragraph
                    {
                        LineHeight = fontTextBlockInstance.LineHeight,
                        TextAlignment = fontTextBlockInstance.TextAlignment,
                        Margin = new Thickness(0, 0, 0, 0)
                    };
                    var run3 = GetRunInstance(TextTypes.Info).Clone("Rögzítési mód: Windows 10 (1903 és újabb)");
                    p3.Inlines.Add(run3);
                    rtbChat.Document.Blocks.Add(p3);
                }
                else
                {
                    _lastDisplayedSubtitle = null;
                    rtbChat.Document.Blocks.Clear();
                    if (_configuration?.AutoHideEnabled == true)
                    {
                        HideImmediately();
                    }
                }
            });
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
