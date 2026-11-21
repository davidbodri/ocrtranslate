using System;
using System.Drawing;
using Translumo.Configuration;
using Translumo.Infrastructure;
using Translumo.Processing;
using Translumo.Processing.Interfaces;
using Translumo.Services;

namespace Translumo.MVVM.Models
{
    public class ChatWindowModel
    {
        public ChatWindowConfiguration Configuration { get; set; }
        public ScreenCaptureConfiguration CaptureConfiguration { get; set; }
        public bool TranslationIsRunning => _translationProcessingService.IsStarted;

        public event EventHandler<ChatItemAddedEventArgs> ChatItemAdded;
        public event EventHandler<ChatFirstItemsRemovedEventArgs> ChatFirstItemsRemoved;
        public event EventHandler ChatCleared;

        private const int CHAT_MAX_ITEMS = 50;
        private const int CHAT_ITEMS_BUFFER = 20;

        private int _chatItemsCount;

        private readonly IProcessingService _translationProcessingService;
        private readonly ICapturerFactory _capturerFactory;
        private SubtitleRegionWatcher _regionWatcher;

        public ChatWindowModel(ChatWindowConfiguration configuration, ScreenCaptureConfiguration captureConfiguration,
            IProcessingService translationProcessingService, ICapturerFactory capturerFactory)
        {
            this.Configuration = configuration;
            this.CaptureConfiguration = captureConfiguration;
            this._translationProcessingService = translationProcessingService;
            this._capturerFactory = capturerFactory;
            this._chatItemsCount = 0;
        }

        public void AddChatItem(string text, TextTypes textType)
        {
            ChatItemAdded?.Invoke(this, new ChatItemAddedEventArgs(text, textType));
            _chatItemsCount++;
            if (CHAT_MAX_ITEMS < _chatItemsCount)
            {
                RemoveFirstChatItems(_chatItemsCount - CHAT_MAX_ITEMS + CHAT_ITEMS_BUFFER);
            }
        }

        public void ClearAllChatItems()
        {
            RemoveFirstChatItems(_chatItemsCount);
            ChatCleared?.Invoke(this, EventArgs.Empty);
        }

        public void RemoveFirstChatItems(int count)
        {
            _chatItemsCount -= count;
            ChatFirstItemsRemoved?.Invoke(this, new ChatFirstItemsRemovedEventArgs(count));
        }

        public void StartTranslation()
        {
            if (TranslationIsRunning)
            {
                return;
            }

            ClearAllChatItems();
            _translationProcessingService.StartProcessing();

            if (Configuration?.AutoHideEnabled == true)
            {
                try
                {
                    var capturer = _capturerFactory.CreateCapturer(true);
                    if (capturer != null)
                    {
                        _regionWatcher = new SubtitleRegionWatcher(capturer, Configuration);
                        _regionWatcher.RegionChangedWithText += (s, e) =>
                        {
                            _translationProcessingService.TriggerScan(e.FrameBytes);
                        };
                        _regionWatcher.RegionBecameEmpty += (s, e) =>
                        {
                            _translationProcessingService.TriggerScan(null);
                        };
                        _regionWatcher.Start();
                    }
                }
                catch
                {
                    // Fail safely to standard scan loop
                }
            }
        }

        public void OnceTranslation(RectangleF captureArea)
        {
            _translationProcessingService.ProcessOnce(captureArea);
        }

        public void EndTranslation()
        {
            if (!TranslationIsRunning)
            {
                return;
            }

            _regionWatcher?.Stop();
            _regionWatcher?.Dispose();
            _regionWatcher = null;

            _translationProcessingService.StopProcessing();
            ClearAllChatItems();
        }
    }
}
