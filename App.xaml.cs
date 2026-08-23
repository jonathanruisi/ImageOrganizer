using System;

using CommunityToolkit.Mvvm.Messaging;

using ImageOrganizer.ViewModel;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Windows.UI.Popups;

using WinRT.Interop;

namespace ImageOrganizer
{
    public partial class App : Application
    {
        #region Properties
        public new static App Current => (App)Application.Current;
        public static MainWindow? Window { get; private set; }
        public static IntPtr WindowHandle { get; private set; }
        public IServiceProvider Services { get; }
        #endregion

        #region Constructor
        public App()
        {
            Services = ConfigureServices();
            InitializeComponent();
        }
        #endregion

        #region Public Methods
        public static async void ShowMessageBoxAsync(string content, string title)
        {
            var messageDialog = new MessageDialog(content, title);
            InitializeWithWindow.Initialize(messageDialog, WindowHandle);
            await messageDialog.ShowAsync();
        }
        #endregion

        #region Event Handlers
        protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
            Window = new MainWindow();
            WindowHandle = WindowNative.GetWindowHandle(Window);
            Window.Activate();
        }
        #endregion

        #region Private Methods
        private static ServiceProvider ConfigureServices()
        {
            var services = new ServiceCollection();
            services.AddSingleton<IMessenger>(StrongReferenceMessenger.Default);
            services.AddSingleton<ProjectManager>();
            return services.BuildServiceProvider();
        }
        #endregion
    }
}
