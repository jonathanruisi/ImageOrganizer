using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkit.Mvvm.Messaging.Messages;

using ImageOrganizer.ViewModel;

using JLR.Utility.WinUI.ViewModel;

using Microsoft.Extensions.DependencyInjection;
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
using Windows.Storage;

namespace ImageOrganizer.Controls
{
    public sealed partial class SystemBrowserControl : UserControl
    {
        #region Properties
        public ProjectManager ViewModel => (ProjectManager)DataContext;
        #endregion

        #region Constructor
        public SystemBrowserControl()
        {
            InitializeComponent();
            DataContext = App.Current.Services.GetService<ProjectManager>();

            RegisterMessages();
        }
        #endregion

        #region Event Handlers (UserControl)
        private async void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            // Add the user's desktop folder to the browser
            var desktopFolder = await StorageFolder.GetFolderFromPathAsync(Environment.GetFolderPath(Environment.SpecialFolder.Desktop));
            ViewModel.SystemBrowserFolder?.Children.Add(new ViewModelFolder(desktopFolder));

            // Add the user's personal folder to the browser
            var personalFolder = await StorageFolder.GetFolderFromPathAsync(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
            ViewModel.SystemBrowserFolder?.Children.Add(new ViewModelFolder(personalFolder));

            // Add all logical drives to the browser
            var drives = Environment.GetLogicalDrives();
            foreach (var drive in drives)
            {
                var driveFolder = await StorageFolder.GetFolderFromPathAsync(drive);
                ViewModel.SystemBrowserFolder?.Children.Add(new ViewModelFolder(driveFolder));
            }
        }
        #endregion

        #region Event Handlers (ListView)
        private async void SystemBrowserListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var removedItems = e.RemovedItems.OfType<ViewModelElement>();
            var addedItems = e.AddedItems.OfType<ViewModelElement>();

            foreach (var item in removedItems)
                item.IsSelected = false;

            foreach (var item in addedItems)
                item.IsSelected = true;

            var selectedItems = SystemBrowserListView.SelectedItems.OfType<ViewModelElement>().ToList();

            if (selectedItems.Count == 1)
            {
                if (selectedItems[0] is ImageFile image)
                {
                    ViewModel.ActiveElement = image;
                }
                else if (selectedItems[0] is ViewModelFolder folder)
                {

                }
                SystemBrowserListView.ScrollIntoView(selectedItems[0]);
            }
            else
            {
                ViewModel.ActiveElement = null;
            }
        }

        private async void SystemBrowserListView_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
        {
            if ((e.OriginalSource as FrameworkElement)?.DataContext is not ViewModelFolder folder)
                return;

            ViewModel.SystemBrowserFolder = folder;
        }

        private void SystemBrowserListView_RightTapped(object sender, RightTappedRoutedEventArgs e)
        {

        }
        #endregion

        #region Private Methods
        private void RegisterMessages()
        {
            var services = App.Current.Services
                ?? throw new InvalidOperationException("Services not configured");
            var messenger = services.GetService<IMessenger>()
                ?? throw new InvalidOperationException("IMessenger service is not registered");

            messenger.Register<SystemBrowserControl, PropertyChangedMessage<ViewModelElement>>(this, (r, m) =>
            {
                if (m.Sender != ViewModel || m.PropertyName != nameof(ViewModel.ActiveElement))
                    return;

                if (r.SystemBrowserListView.SelectedItems.Count == 1)
                    r.SystemBrowserListView.SelectedItem = m.NewValue;
            });

            messenger.Register<SystemBrowserControl, PropertyChangedMessage<ViewModelNode>>(this, async (r, m) =>
            {
                if (m.Sender != ViewModel || m.PropertyName != nameof(ViewModel.SystemBrowserFolder))
                    return;

                if (m.NewValue is ViewModelFolder folder && folder.Name != "Root")
                {
                    await folder.RealizeChildrenAsync();
                    if (ViewModel.SystemBrowserFolder?.Children.Contains(m.OldValue) == true)
                    {
                        SystemBrowserListView.SelectedItem = m.OldValue;
                    }
                }
            });

            messenger.Register<SystemBrowserControl, ViewModelFolderRemovedMessage>(this, (r, m) =>
            {
                if (m.Folder == ViewModel.SystemBrowserFolder)
                {
                    ViewModel.SystemBrowserFolder = m.Folder.Parent;
                }
            });
        }
        #endregion
    }
}