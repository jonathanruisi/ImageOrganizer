using CommunityToolkit.Mvvm.Messaging;

using ImageOrganizer;
using ImageOrganizer.ViewModel;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Input;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;

using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Graphics;

namespace ImageOrganizer
{
    public sealed partial class MainWindow : Window
    {
        #region Fields
        private readonly Microsoft.UI.Dispatching.DispatcherQueueTimer? _vramCheckTimer;
        #endregion

        #region Properties
        public ProjectManager ViewModel { get; private set; }
        public AppWindowPresenterKind PresenterKind { get; private set; }
        #endregion

        #region Constructor
        public MainWindow()
        {
            InitializeComponent();

            ExtendsContentIntoTitleBar = true;

            // Install hook to handle DPI changes
            InstallDpiHook();
            Closed += (s, e) => UninstallDpiHook();

            // Register event handlers
            Activated += MainWindow_Activated;
            Closed += MainWindow_Closed;
            AppWindow.Changed += AppWindow_Changed;
            MainWindowTitleBar.Loaded += MainWindowTitleBar_Loaded;
            MainWindowTitleBar.SizeChanged += MainWindowTitleBar_SizeChanged;

            // Load ViewModel
            ViewModel = App.Current.Services.GetService<ProjectManager>()
                ?? throw new InvalidOperationException("ProjectManager service is not registered.");

            // Initialize VRAM check timer
            _vramCheckTimer = DispatcherQueue.CreateTimer();
            _vramCheckTimer.IsRepeating = true;
            _vramCheckTimer.Tick += VramCheckTimer_Tick;

            // Set initial presenter kind
            PresenterKind = AppWindowPresenterKind.Default;

            // Register for messages
            RegisterMessages();
        }
        #endregion

        #region Event Handlers
        private void MainWindow_Activated(object sender, WindowActivatedEventArgs args)
        {
            if (args.WindowActivationState == WindowActivationState.Deactivated)
            {
                TitleBarTitle.Foreground =
                    (SolidColorBrush)App.Current.Resources["WindowCaptionForegroundDisabled"];
                SettingsButton.Foreground =
                    (SolidColorBrush)App.Current.Resources["WindowCaptionForegroundDisabled"];
            }
            else
            {
                TitleBarTitle.Foreground =
                    (SolidColorBrush)App.Current.Resources["WindowCaptionForeground"];
                SettingsButton.Foreground =
                    (SolidColorBrush)App.Current.Resources["WindowCaptionForeground"];
            }
        }

        private void MainWindow_Closed(object sender, WindowEventArgs args)
        {
            if (_vramCheckTimer?.IsRunning == true)
                _vramCheckTimer.Stop();
        }

        private void AppWindow_Changed(AppWindow sender, AppWindowChangedEventArgs args)
        {
            if (args.DidPresenterChange)
            {
                switch (sender.Presenter.Kind)
                {
                    case AppWindowPresenterKind.CompactOverlay:
                        MainWindowTitleBar.Visibility = Visibility.Collapsed;
                        sender.TitleBar.ResetToDefault();
                        break;

                    case AppWindowPresenterKind.FullScreen:
                        MainWindowTitleBar.Visibility = Visibility.Collapsed;
                        sender.TitleBar.ExtendsContentIntoTitleBar = true;
                        break;

                    case AppWindowPresenterKind.Overlapped:
                        MainWindowTitleBar.Visibility = Visibility.Visible;
                        sender.TitleBar.ExtendsContentIntoTitleBar = true;
                        break;

                    default:
                        sender.TitleBar.ResetToDefault();
                        break;
                }
            }
        }

        private void MainWindowTitleBar_Loaded(object sender, RoutedEventArgs e)
        {
            if (ExtendsContentIntoTitleBar)
            {
                SetRegionsForTitleBar();
            }

            if (_vramCheckTimer is not null)
            {
                _vramCheckTimer.Interval = TimeSpan.FromMilliseconds(500);
                _vramCheckTimer.Start();
            }
        }

        private void MainWindowTitleBar_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (ExtendsContentIntoTitleBar)
            {
                SetRegionsForTitleBar();
            }
        }

        private void VramCheckTimer_Tick(Microsoft.UI.Dispatching.DispatcherQueueTimer sender, object args)
        {
            var vramInfo = VramHelper.GetVideoMemoryInfoForPrimaryAdapter();
            if (vramInfo is null)
            {
                VramUsageIndicator.Width = 0;
                return;
            }

            var percentUsed = (double)vramInfo.CurrentUsageBytes / vramInfo.BudgetBytes;
            var mbUsed = decimal.Round(vramInfo.CurrentUsageBytes / 1073741824M, 2);
            var mbAvailable = decimal.Round(vramInfo.BudgetBytes / 1073741824M, 2);

            var maxWidth = VramUsageIndicatorBackground.ActualWidth - (VramUsageIndicatorBackground.Margin.Left * 2);
            VramUsageIndicator.Width = maxWidth * percentUsed;
            VramUsedText.Text = $"{mbUsed} GB";
            VramAvailableText.Text = $"{mbAvailable} GB";
        }
        #endregion

        #region Private Methods
        private void SetRegionsForTitleBar()
        {
            var scaleFactor = MainWindowTitleBar.XamlRoot.RasterizationScale;

            RightPaddingColumn.Width = new GridLength(AppWindow.TitleBar.RightInset / scaleFactor);
            LeftPaddingColumn.Width = new GridLength(AppWindow.TitleBar.LeftInset / scaleFactor);

            GeneralTransform transform = MainMenuBar.TransformToVisual(null);
            var bounds = transform.TransformBounds(
                new Rect(0, 0, MainMenuBar.ActualWidth, MainMenuBar.ActualHeight));
            var menuRect = GetRect(bounds);

            transform = SettingsButton.TransformToVisual(null);
            bounds = transform.TransformBounds(
                new Rect(0, 0, SettingsButton.ActualWidth, SettingsButton.ActualHeight));
            var settingsRect = GetRect(bounds);

            var rectArray = new RectInt32[] { menuRect, settingsRect };
            var nonClientInputSource = InputNonClientPointerSource.GetForWindowId(AppWindow.Id);
            nonClientInputSource.SetRegionRects(NonClientRegionKind.Passthrough, rectArray);

            RectInt32 GetRect(Rect bounds)
            {
                return new RectInt32((int)Math.Round(bounds.X * scaleFactor),
                                                      (int)Math.Round(bounds.Y * scaleFactor),
                                                      (int)Math.Round(bounds.Width * scaleFactor),
                                                      (int)Math.Round(bounds.Height * scaleFactor));
            }
        }

        public void KeyboardAcceleratorInvoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs e)
        {
            e.Handled = true;
            if (PresenterKind != AppWindowPresenterKind.FullScreen)
            {
                SwitchPresenter(AppWindowPresenterKind.FullScreen);
                PresenterKind = AppWindowPresenterKind.FullScreen;
                SystemBrowser.Visibility = Visibility.Collapsed;
                SystemBrowserSizer.Visibility = Visibility.Collapsed;
                ImagePresenter.UpperToolbarVisibility = Visibility.Collapsed;
                ImagePresenter.LowerToolbarVisibility = Visibility.Collapsed;
            }
            else
            {
                SwitchPresenter(AppWindowPresenterKind.Default);
                PresenterKind = AppWindowPresenterKind.Default;
                SystemBrowser.Visibility = Visibility.Visible;
                SystemBrowserSizer.Visibility = Visibility.Visible;
                ImagePresenter.UpperToolbarVisibility = Visibility.Visible;
                ImagePresenter.LowerToolbarVisibility = Visibility.Visible;
            }
        }

        private void SwitchPresenter(AppWindowPresenterKind presenterKind)
        {
            if (AppWindow is null)
                return;

            if (presenterKind != AppWindow.Presenter.Kind)
                AppWindow.SetPresenter(presenterKind);
        }

        private void RegisterMessages()
        {
            var services = App.Current.Services
                ?? throw new InvalidOperationException("Services not configured");
            var messenger = services.GetService<IMessenger>()
                ?? throw new InvalidOperationException("IMessenger service is not registered");

            messenger.Register<MainWindow, SetInfoBarMessage>(this, (r, m) =>
            {
                r.MainInfoBar.Title = m.Title;
                r.MainInfoBar.Message = m.Message;
                r.MainInfoBar.Severity = m.Severity;
                r.MainInfoBar.IsClosable = m.IsCloseable;
                r.MainInfoBar.IsOpen = true;
            });

            messenger.Register<MainWindow, ToggleFullscreenMessage>(this, (r, m) =>
            {
                if (r.PresenterKind != AppWindowPresenterKind.FullScreen)
                    r.SwitchPresenter(AppWindowPresenterKind.FullScreen);
                else
                    r.SwitchPresenter(AppWindowPresenterKind.Default);
            });
        }
        #endregion
    }
}