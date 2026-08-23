using JLR.Utility.WinUI.ViewModel;

using Microsoft.UI;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;

using System;
using System.Collections.Generic;
using System.Text;

namespace ImageOrganizer.ViewModel
{
    [ViewModelType("Workspace")]
    public sealed partial class ProjectManager : ViewModelElement
    {
        #region Constants
        public static readonly string DefaultName = "Workspace";
        #endregion

        #region Fields
        private ViewModelNode? _systemBrowserFolder;
        private ViewModelElement? _activeElement;
        #endregion

        #region Properties
        public ViewModelNode? SystemBrowserFolder
        {
            get => _systemBrowserFolder;
            set
            {
                if (SetProperty(ref _systemBrowserFolder, value, true))
                {
                    GeneralPreviousCommand.NotifyCanExecuteChanged();
                    GeneralNextCommand.NotifyCanExecuteChanged();
                    GeneralDeleteCommand.NotifyCanExecuteChanged();
                    SystemBrowserUpOneLevelCommand.NotifyCanExecuteChanged();
                }
            }
        }

        public ViewModelElement? ActiveElement
        {
            get => _activeElement;
            set
            {
                if (SetProperty(ref _activeElement, value, true))
                {
                    GeneralPreviousCommand.NotifyCanExecuteChanged();
                    GeneralNextCommand.NotifyCanExecuteChanged();
                    GeneralDeleteCommand.NotifyCanExecuteChanged();
                    ToolsToggleFlag1Command.NotifyCanExecuteChanged();
                    ToolsToggleFlag2Command.NotifyCanExecuteChanged();
                    ToolsToggleFlag3Command.NotifyCanExecuteChanged();
                    ToolsToggleFlag4Command.NotifyCanExecuteChanged();
                }
            }
        }
        #endregion

        #region Constructor
        public ProjectManager()
        {
            Name = DefaultName;
            _systemBrowserFolder = new LogicalFolder("Root");

            InitializeCommands();
        }
        #endregion

        #region Private Methods
 
        #endregion
    }
}