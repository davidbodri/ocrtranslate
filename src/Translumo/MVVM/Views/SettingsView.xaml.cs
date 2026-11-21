using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Translumo.Infrastructure.Language;
using Translumo.MVVM.ViewModels;
using Translumo.Services;
using Translumo.Utils;

namespace Translumo.MVVM.Views
{
    /// <summary>
    /// Interaction logic for SettingsView.xaml
    /// </summary>
    public partial class SettingsView : Window
    {
        private SettingsViewModel ViewModel => DataContext as SettingsViewModel;

        private Storyboard _toOpenStoryboard;
        private Storyboard _toCloseStoryboard;

        public SettingsView()
        {
            InitializeComponent();
            this.Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            _toOpenStoryboard = CreateStoryboardToOpenWindow(true);
            _toCloseStoryboard = CreateStoryboardToOpenWindow(false);

            if (NavListBox != null && NavListBox.Items.Count > 0 && NavListBox.SelectedIndex < 0)
            {
                NavListBox.SelectedIndex = 0;
            }

            this.Activate();
        }

        private void ViewModelOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(SettingsViewModel.AdditionPanelOpened))
            {
                if (ViewModel.AdditionPanelOpened)
                {
                    this.BeginStoryboard(_toOpenStoryboard, HandoffBehavior.Compose, true);
                }
                else
                {
                    this.BeginStoryboard(_toCloseStoryboard, HandoffBehavior.Compose, true);
                }
            }
        }

        private Storyboard CreateStoryboardToOpenWindow(bool toOpen)
        {
            const int ADD_WINDOW_WIDTH = 205;
            DoubleAnimation widthAnimation = new DoubleAnimation
            {
                To = this.ActualWidth + (toOpen ? ADD_WINDOW_WIDTH : 0),
                From = this.ActualWidth + (toOpen ? 0 : ADD_WINDOW_WIDTH),
                Duration = TimeSpan.FromMilliseconds(500)
            };

            Storyboard.SetTargetProperty(widthAnimation, new PropertyPath(Window.WidthProperty));
            Storyboard.SetTarget(widthAnimation, this);

            var storyboard = new Storyboard();
            storyboard.Children.Add(widthAnimation);

            return storyboard;
        }

        private void wSettings_SourceInitialized(object sender, EventArgs e)
        {
            if (ViewModel != null)
            {
                ViewModel.PropertyChanged += ViewModelOnPropertyChanged;
            }
        }

        private void wSettings_Activated(object sender, EventArgs e)
        {
        }

        private void wSettings_Deactivated(object sender, EventArgs e)
        {
        }

        [DllImport("user32.dll")]
        public static extern bool ReleaseCapture();

        [DllImport("user32.dll")]
        public static extern IntPtr SendMessage(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);

        private const int WM_NCLBUTTONDOWN = 0xA1;
        private const int HT_CAPTION = 0x2;

        public void DragWindow()
        {
            try
            {
                var helper = new WindowInteropHelper(this);
                if (helper.Handle != IntPtr.Zero)
                {
                    ReleaseCapture();
                    SendMessage(helper.Handle, WM_NCLBUTTONDOWN, (IntPtr)HT_CAPTION, IntPtr.Zero);
                }
            }
            catch
            {
            }
        }

        private static bool IsInteractiveElement(DependencyObject dep)
        {
            while (dep != null)
            {
                if (dep is Button || dep is TextBox || dep is ComboBox || dep is Slider ||
                    dep is ListBox || dep is ListBoxItem || dep is ScrollViewer ||
                    dep is System.Windows.Controls.Primitives.ToggleButton ||
                    dep is System.Windows.Controls.Primitives.ScrollBar)
                {
                    return true;
                }

                dep = VisualTreeHelper.GetParent(dep);
            }
            return false;
        }

        private void Window_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                if (e.OriginalSource is DependencyObject dep && IsInteractiveElement(dep))
                {
                    return;
                }

                DragWindow();
            }
        }

        private void wSettings_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                if (e.OriginalSource is DependencyObject dep && IsInteractiveElement(dep))
                {
                    return;
                }

                DragWindow();
            }
        }

        private void btnMinimize_Click(object sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Minimized;
        }

        private void btnClose_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void NavListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (NavListBox?.SelectedItem is MaterialDesignExtensions.Model.INavigationItem navItem)
            {
                ViewModel?.NavigationItemSelectedCommand?.Execute(navItem);
            }
        }

        private void OnLookupperLinkClick(object sender, RoutedEventArgs e)
        {
            var destinationurl = LocalizationManager.GetValue($"Str.Lookupper.Url");
            var sInfo = new System.Diagnostics.ProcessStartInfo(destinationurl)
            {
                UseShellExecute = true
            };
            System.Diagnostics.Process.Start(sInfo);
        }
    }
}
