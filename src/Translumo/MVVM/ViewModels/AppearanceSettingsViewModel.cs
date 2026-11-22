using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Toolkit.Mvvm.Input;
using Translumo.Configuration;
using Translumo.MVVM.Common;
using Translumo.MVVM.Models;
using Translumo.Services;
using Translumo.Utils;
using RelayCommand = Microsoft.Toolkit.Mvvm.Input.RelayCommand;

namespace Translumo.MVVM.ViewModels
{
    public sealed class AppearanceSettingsViewModel : BindableBase, IAdditionalPanelController, IDisposable
    {
        public event EventHandler<bool> PanelStateIsChanged;

        public ChatWindowConfiguration Model
        {
            get => _model;
            set => SetProperty(ref _model, value);
        }

        public IList<DisplayAlignment> AvailableAlignments { get; set; }

        public bool ColorPickerIsOpened
        {
            get => _colorPickerIsOpened;
            set
            {
                SetProperty(ref _colorPickerIsOpened, value);
                PanelStateIsChanged?.Invoke(this, value);
            }
        }

        public Color SelectedColor
        {
            get => _selectedColor;
            set => SetProperty(ref _selectedColor, value);
        }

        public bool IsObsSetupMode
        {
            get => _chatMediator.IsObsSetupMode;
            set
            {
                if (_chatMediator.IsObsSetupMode != value)
                {
                    if (value && Model.ExcludeFromCaptue)
                    {
                        Model.ExcludeFromCaptue = false;
                    }
                    _chatMediator.SetObsSetupMode(value);
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(ObsButtonText));
                }
            }
        }

        public string ObsButtonText => IsObsSetupMode ? "Mód leállítása" : "OBS / Streamlabs mód";

        public ICommand ChangeBackColorClickedCommand => new RelayCommand<Control>(OnChangeBackColorClicked);
        public ICommand ChangeFontColorClickedCommand => new RelayCommand<Control>(OnChangeFontColorClicked);
        public ICommand SendTestTextCommand => new RelayCommand(OnSendTestTextCommand);
        public ICommand ToggleObsSetupModeCommand => new RelayCommand(OnToggleObsSetupMode);
        public ICommand ColorIsPickedCommand => new RelayCommand<bool>(OnColorPickedCommand);

        private ChatWindowConfiguration _model;
       
        private Action<ChatWindowConfiguration, Color> _newColorSetter;
        private string _additionalPanelTriggerName;
        private bool _colorPickerIsOpened;
        private Color _selectedColor;

        private readonly ChatUITextMediator _chatMediator;
        public AppearanceSettingsViewModel(ChatWindowConfiguration model, ChatUITextMediator chatMediator)
        {
            this.Model = model;
            this._chatMediator = chatMediator;
            this._chatMediator.ObsSetupModeChanged += ChatMediatorOnObsSetupModeChanged;
            this.AvailableAlignments = Enum.GetValues<TextAlignment>()
                .Select(alignment => new DisplayAlignment(alignment, GetDisplayAlignmentText(alignment)))
                .ToList();
        }

        private void OnToggleObsSetupMode()
        {
            IsObsSetupMode = !IsObsSetupMode;
        }

        private void ChatMediatorOnObsSetupModeChanged(object sender, bool e)
        {
            OnPropertyChanged(nameof(IsObsSetupMode));
            OnPropertyChanged(nameof(ObsButtonText));
        }

        private string GetDisplayAlignmentText(TextAlignment alignment)
        {
            return LocalizationManager.GetValue($"Str.AppearanceSettings.{alignment}", false,
                OnLocalizedValueChanged, this);
        }

        private void OnChangeBackColorClicked(Control sender)
        {
            OnChangeColorClicked(sender, (model, color) => model.BackgroundColor = color, Model.BackgroundColor);
        }

        private void OnChangeFontColorClicked(Control sender)
        {
            OnChangeColorClicked(sender, (model, color) => model.FontColor = color, Model.FontColor);
        }

        private void OnSendTestTextCommand()
        {
            _chatMediator.SendText($"Translated test text was sent", true);
        }

        private void OnColorPickedCommand(bool applyColor)
        {
            ClosePanel();
            if (applyColor)
            {
                _newColorSetter.Invoke(Model, SelectedColor);
            }

            SelectedColor = default(Color);
        }

        private void OnChangeColorClicked(Control sender, Action<ChatWindowConfiguration, Color> newColorSetter, Color currentColor)
        {
            OpenCloseColorPanel(sender.Name);

            _newColorSetter = newColorSetter;
            SelectedColor = currentColor;
        }

        private void OpenCloseColorPanel(string triggerControlName)
        {
            var toOpen = _additionalPanelTriggerName != triggerControlName;
            _additionalPanelTriggerName = toOpen ? triggerControlName : null;
            ColorPickerIsOpened = toOpen;
        }

        public void ClosePanel()
        {
            _additionalPanelTriggerName = null;
            ColorPickerIsOpened = false;
        }

        private void OnLocalizedValueChanged(string key, string oldValue)
        {
            var availableAlignment = AvailableAlignments.First(alig => alig.DisplayText == oldValue);
            availableAlignment.DisplayText = LocalizationManager.GetValue(key, false, OnLocalizedValueChanged, this);
        }

        public void Dispose()
        {
            if (_chatMediator != null)
            {
                _chatMediator.ObsSetupModeChanged -= ChatMediatorOnObsSetupModeChanged;
            }
            LocalizationManager.ReleaseChangedValuesCallbacks(this);
        }
    }
}
