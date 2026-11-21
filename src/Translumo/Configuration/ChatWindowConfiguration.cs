using System;
using System.Windows;
using System.Windows.Media;
using Translumo.MVVM.Models;
using Translumo.Processing.Configuration;
using Translumo.Utils;

namespace Translumo.Configuration
{
    public class ChatWindowConfiguration : BindableBase
    {
        public event EventHandler ExcludeFromCaptueChanged;

        public Color BackgroundColor
        {
            get => _backgroundColor; 
            set
            {
                SetProperty(ref _backgroundColor, value);
            }
        }

        public Color FontColor
        {
            get => _fontColor;
            set
            {
                SetProperty(ref _fontColor, value);
            }
        }

        public float BackgroundOpacity
        {
            get => _backgroundOpacity;
            set
            {
                SetProperty(ref _backgroundOpacity, value);
            }
        }

        public bool BlurEnabled
        {
            get => _blurEnabled;
            set
            {
                SetProperty(ref _blurEnabled, value);
            }
        }

        public bool AutoHideEnabled
        {
            get => _autoHideEnabled;
            set
            {
                SetProperty(ref _autoHideEnabled, value);
            }
        }

        [Obsolete("Use IdleHideMs instead")]
        public int HideDelayMs
        {
            get => _hideDelayMs;
            set
            {
                SetProperty(ref _hideDelayMs, value);
            }
        }

        public int IdleHideMs
        {
            get => _idleHideMs;
            set
            {
                SetProperty(ref _idleHideMs, value);
            }
        }

        public int LineHoldMs
        {
            get => _lineHoldMs;
            set
            {
                SetProperty(ref _lineHoldMs, value);
            }
        }

        public bool KeepEmptySlotSpace
        {
            get => _keepEmptySlotSpace;
            set
            {
                SetProperty(ref _keepEmptySlotSpace, value);
            }
        }

        public bool SplitBySentenceFallback
        {
            get => _splitBySentenceFallback;
            set
            {
                SetProperty(ref _splitBySentenceFallback, value);
            }
        }

        public int EmptyScansBeforeHide
        {
            get => _emptyScansBeforeHide;
            set
            {
                SetProperty(ref _emptyScansBeforeHide, value);
            }
        }

        public int FadeOutMs
        {
            get => _fadeOutMs;
            set
            {
                SetProperty(ref _fadeOutMs, value);
            }
        }

        public int WatchIntervalMs
        {
            get => _watchIntervalMs;
            set
            {
                SetProperty(ref _watchIntervalMs, value);
            }
        }

        public double ChangeThreshold
        {
            get => _changeThreshold;
            set
            {
                SetProperty(ref _changeThreshold, value);
            }
        }

        public int EmptyConfirmFrames
        {
            get => _emptyConfirmFrames;
            set
            {
                SetProperty(ref _emptyConfirmFrames, value);
            }
        }

        public bool ShowOriginalWhileTranslating
        {
            get => _showOriginalWhileTranslating;
            set
            {
                SetProperty(ref _showOriginalWhileTranslating, value);
            }
        }

        public bool KeepOldSubtitleUntilReady
        {
            get => _keepOldSubtitleUntilReady;
            set
            {
                SetProperty(ref _keepOldSubtitleUntilReady, value);
            }
        }

        public int LineMatchThreshold
        {
            get => _lineMatchThreshold;
            set
            {
                SetProperty(ref _lineMatchThreshold, value);
            }
        }

        public int MaxVisibleLines
        {
            get => _maxVisibleLines;
            set
            {
                SetProperty(ref _maxVisibleLines, value);
            }
        }

        public int FontSize
        {
            get => _fontSize;
            set
            {
                SetProperty(ref _fontSize, value);
            }
        }

        public bool FontBold
        {
            get => _fontBold;
            set
            {
                SetProperty(ref _fontBold, value);
            }
        }

        public bool ExcludeFromCaptue
        {
            get => _excludeFromCaptue;
            set
            {
                SetProperty(ref _excludeFromCaptue, value);
                // Only raise the event if the property actually changed
                ExcludeFromCaptueChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        public int LineSpacing
        {
            get => _lineSpacing;
            set
            {
                SetProperty(ref _lineSpacing, value);
            }
        }

        public TextAlignment TextAlignment
        {
            get => _textAlignment;
            set
            {
                SetProperty(ref _textAlignment, value);
            }
        }

        public int StableMs
        {
            get => _stableMs;
            set
            {
                SetProperty(ref _stableMs, value);
            }
        }

        public int EmptyConfirmMs
        {
            get => _emptyConfirmMs;
            set
            {
                SetProperty(ref _emptyConfirmMs, value);
            }
        }

        public bool TranslateSpeakerNames
        {
            get => _translateSpeakerNames;
            set
            {
                SetProperty(ref _translateSpeakerNames, value);
            }
        }

        public bool PerformanceBlur
        {
            get => _performanceBlur;
            set
            {
                SetProperty(ref _performanceBlur, value);
            }
        }

        public bool UseLegacyTracking
        {
            get => _useLegacyTracking;
            set
            {
                SetProperty(ref _useLegacyTracking, value);
            }
        }

        public bool PerfLog
        {
            get => _perfLog;
            set
            {
                SetProperty(ref _perfLog, value);
            }
        }

        public bool LogSubtitleEvents
        {
            get => _logSubtitleEvents;
            set
            {
                SetProperty(ref _logSubtitleEvents, value);
            }
        }

        [MapInternal]
        public TextProcessingConfiguration TextProcessing
        {
            get => _textProcessing;
            set
            {
                SetProperty(ref _textProcessing, value);
            }
        }

        public static ChatWindowConfiguration Default => new()
        {
            BackgroundColor = Color.FromRgb(0, 0, 0),
            FontColor = Color.FromRgb(255, 255, 255),
            BackgroundOpacity = 0.65f,
            BlurEnabled = true,
            AutoHideEnabled = true,
            HideDelayMs = 3000,
            IdleHideMs = 3000,
            LineHoldMs = 3000,
            KeepEmptySlotSpace = false,
            SplitBySentenceFallback = true,
            EmptyScansBeforeHide = 6,
            FadeOutMs = 150,
            WatchIntervalMs = 80,
            ChangeThreshold = 5.0,
            EmptyConfirmFrames = 4,
            ShowOriginalWhileTranslating = false,
            KeepOldSubtitleUntilReady = true,
            LineMatchThreshold = 85,
            MaxVisibleLines = 6,
            FontSize = 15,
            FontBold = true,
            ExcludeFromCaptue = false,
            LineSpacing = 14,
            TextAlignment = TextAlignment.Left,
            StableMs = 200,
            EmptyConfirmMs = 400,
            TranslateSpeakerNames = false,
            PerformanceBlur = true,
            UseLegacyTracking = false,
            PerfLog = false,
            LogSubtitleEvents = false
        };

        private Color _backgroundColor = Color.FromRgb(0, 0, 0);
        private Color _fontColor = Color.FromRgb(255, 255, 255);
        private float _backgroundOpacity = 0.65f;
        private bool _blurEnabled = true;
        private bool _autoHideEnabled = true;
        private int _hideDelayMs = 3000;
        private int _idleHideMs = 3000;
        private int _lineHoldMs = 3000;
        private bool _keepEmptySlotSpace = false;
        private bool _splitBySentenceFallback = true;
        private int _emptyScansBeforeHide = 6;
        private int _fadeOutMs = 150;
        private int _watchIntervalMs = 80;
        private double _changeThreshold = 5.0;
        private int _emptyConfirmFrames = 4;
        private bool _showOriginalWhileTranslating = false;
        private bool _keepOldSubtitleUntilReady = true;
        private int _lineMatchThreshold = 85;
        private int _maxVisibleLines = 6;
        private int _fontSize = 15;
        private bool _fontBold = true;
        private bool _excludeFromCaptue = false;
        private int _lineSpacing = 14;
        private int _stableMs = 200;
        private int _emptyConfirmMs = 400;
        private bool _translateSpeakerNames = false;
        private bool _performanceBlur = true;
        private bool _useLegacyTracking = false;
        private bool _perfLog = false;
        private bool _logSubtitleEvents = false;
        private TextProcessingConfiguration _textProcessing = TextProcessingConfiguration.Default;
        private TextAlignment _textAlignment;
    }
}
