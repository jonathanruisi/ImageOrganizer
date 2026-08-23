using CommunityToolkit.Mvvm.Messaging;

using JLR.Utility.WinUI;

using Microsoft.UI;
using Microsoft.UI.Input;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

using Windows.Storage;
using Windows.System;
using Windows.UI.Core;

namespace ImageOrganizer.ViewModel
{
    public sealed partial class ProjectManager
    {
        #region Commands
        public XamlUICommand GeneralPreviousCommand { get; private set; } = new XamlUICommand()
        {
            Label = "Previous",
            Description = "Navigate to the previous item",
            IconSource = new SymbolIconSource { Symbol = Symbol.Previous }
        };

        public XamlUICommand GeneralNextCommand { get; private set; } = new XamlUICommand()
        {
            Label = "Next",
            Description = "Navigate to the next item",
            IconSource = new SymbolIconSource { Symbol = Symbol.Next }
        };

        public XamlUICommand GeneralDeleteCommand { get; private set; } = new XamlUICommand()
        {
            Label = "Delete",
            Description = "Delete the current item",
            IconSource = new SymbolIconSource { Symbol = Symbol.Delete }
        };

        public XamlUICommand BrowserCreateImageSequenceCommand { get; private set; } = new XamlUICommand()
        {
            Label = "Create Image Sequence",
            Description = "Create an image sequence from the selected folder",
            IconSource = new FontIconSource
            {
                Glyph = "\uE786",
                FontFamily = new FontFamily("Segoe Fluent Icons"),
                FontWeight = FontWeights.Bold
            }
        };

        public XamlUICommand ToolsToggleFlag1Command { get; private set; } = new XamlUICommand()
        {
            Label = "Toggle Flag 1",
            Description = "Toggle Flag 1 on the current item",
            IconSource = new FontIconSource
            {
                Glyph = "\uF146",
                FontFamily = new FontFamily("Segoe Fluent Icons"),
                Foreground = new SolidColorBrush(Colors.Gold)
            }
        };

        public XamlUICommand ToolsToggleFlag2Command { get; private set; } = new XamlUICommand()
        {
            Label = "Toggle Flag 2",
            Description = "Toggle Flag 2 on the current item",
            IconSource = new FontIconSource
            {
                Glyph = "\uF147",
                FontFamily = new FontFamily("Segoe Fluent Icons"),
                Foreground = new SolidColorBrush(Colors.CornflowerBlue)
            }
        };

        public XamlUICommand ToolsToggleFlag3Command { get; private set; } = new XamlUICommand()
        {
            Label = "Toggle Flag 3",
            Description = "Toggle Flag 3 on the current item",
            IconSource = new FontIconSource
            {
                Glyph = "\uF148",
                FontFamily = new FontFamily("Segoe Fluent Icons"),
                Foreground = new SolidColorBrush(Colors.IndianRed)
            }
        };

        public XamlUICommand ToolsToggleFlag4Command { get; private set; } = new XamlUICommand()
        {
            Label = "Toggle Flag 4",
            Description = "Toggle Flag 4 on the current item",
            IconSource = new FontIconSource
            {
                Glyph = "\uF149",
                FontFamily = new FontFamily("Segoe Fluent Icons"),
                Foreground = new SolidColorBrush(Colors.ForestGreen)
            }
        };

        public XamlUICommand ToolsDeleteFlag1ItemsCommand { get; private set; } = new XamlUICommand()
        {
            Label = "Delete Flag 1 Items",
            Description = "Delete all items marked with flag 1"
        };

        public XamlUICommand ToolsDeleteFlag2ItemsCommand { get; private set; } = new XamlUICommand()
        {
            Label = "Delete Flag 2 Items",
            Description = "Delete all items marked with flag 2"
        };

        public XamlUICommand ToolsDeleteFlag3ItemsCommand { get; private set; } = new XamlUICommand()
        {
            Label = "Delete Flag 3 Items",
            Description = "Delete all items marked with flag 3"
        };

        public XamlUICommand ToolsDeleteFlag4ItemsCommand { get; private set; } = new XamlUICommand()
        {
            Label = "Delete Flag 4 Items",
            Description = "Delete all items marked with flag 4"
        };

        public XamlUICommand ToolsRunWeeded1Command { get; private set; } = new XamlUICommand()
        {
            Label = "Run Weeded 1",
            Description = "Run Weeded 1 on current directory"
        };

        public XamlUICommand ToolsRunWeeded2Command { get; private set; } = new XamlUICommand()
        {
            Label = "Run Weeded 2",
            Description = "Run Weeded 2 on current directory"
        };

        public XamlUICommand ToolsRunWeeded3Command { get; private set; } = new XamlUICommand()
        {
            Label = "Run Weeded 3",
            Description = "Run Weeded 3 on current directory"
        };

        public XamlUICommand ToolsMarkDuplicatesInCurrentFolderCommand { get; private set; } = new XamlUICommand()
        {
            Label = "Mark Duplicates",
            Description = "Mark duplicates in current folder based on file content"
        };

        public XamlUICommand SystemBrowserUpOneLevelCommand { get; private set; } = new XamlUICommand()
        {
            Label = "Up One Level",
            Description = "Navigate to parent folder",
            IconSource = new FontIconSource
            {
                Glyph = "\uE70E",
                FontFamily = new FontFamily("Segoe Fluent Icons"),
                FontWeight = FontWeights.Bold
            }
        };

        public XamlUICommand SaveImageTransformCommand { get; private set; } = new XamlUICommand()
        {
            Label = "Save Image Transform",
            Description = "Save the current image transform"
        };
        #endregion

        #region Event Handlers (Commands)
        private void GeneralPreviousCommand_CanExecuteRequested(XamlUICommand sender, CanExecuteRequestedEventArgs args)
        {
            args.CanExecute = ActiveElement is ViewModelFile && ActiveElement != ActiveElement.Parent?.Children.First();
        }

        private void GeneralPreviousCommand_ExecuteRequested(XamlUICommand sender, ExecuteRequestedEventArgs args)
        {
            var index = ActiveElement!.Parent.Children.IndexOf(ActiveElement);
            ActiveElement = ActiveElement.Parent.Children[index - 1];
        }

        private void GeneralNextCommand_CanExecuteRequested(XamlUICommand sender, CanExecuteRequestedEventArgs args)
        {
            args.CanExecute = ActiveElement is ViewModelFile && ActiveElement != ActiveElement.Parent?.Children.Last();
        }

        private void GeneralNextCommand_ExecuteRequested(XamlUICommand sender, ExecuteRequestedEventArgs args)
        {
            var index = ActiveElement!.Parent.Children.IndexOf(ActiveElement);
            ActiveElement = ActiveElement.Parent.Children[index + 1];
        }

        private void GeneralDeleteCommand_CanExecuteRequested(XamlUICommand sender, CanExecuteRequestedEventArgs args)
        {
            
        }

        private void GeneralDeleteCommand_ExecuteRequested(XamlUICommand sender, ExecuteRequestedEventArgs args)
        {
            
        }

        private void BrowserCreateImageSequenceCommand_CanExecuteRequested(XamlUICommand sender, CanExecuteRequestedEventArgs args)
        {
            args.CanExecute = SystemBrowserFolder is ViewModelFolder folder &&
                              folder.Children.OfType<ImageFile>().Any();
        }

        private async void BrowserCreateImageSequenceCommand_ExecuteRequested(XamlUICommand sender, ExecuteRequestedEventArgs args)
        {
            if (await(SystemBrowserFolder as ViewModelFolder)!.SaveMetadataAsync() == false)
                App.ShowMessageBoxAsync("Save Error", "Error saving image sequence metadata");
        }

        private void ToolsToggleFlagCommand_CanExecuteRequested(XamlUICommand sender, CanExecuteRequestedEventArgs args)
        {
            args.CanExecute = ActiveElement is not null;
        }

        private void ToolsToggleFlagCommand_ExecuteRequested(XamlUICommand sender, ExecuteRequestedEventArgs args)
        {
            if (!int.TryParse((string)args.Parameter, out int flag))
                return;

            ActiveElement!.ToggleFlag(flag);
            if (InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control).HasFlag(CoreVirtualKeyStates.Down))
            {
                var activeElementFlagState = ActiveElement.CheckFlag(flag);
                for (var i = ActiveElement.Parent.Children.IndexOf(ActiveElement) - 1; i >= 0; i--)
                {
                    if (ActiveElement.Parent.Children[i].CheckFlag(flag) != activeElementFlagState)
                        ActiveElement.Parent.Children[i].ToggleFlag(flag);
                    else break;
                }
            }
        }

        private void ToolsRunWeeded1Command_CanExecuteRequested(XamlUICommand sender, CanExecuteRequestedEventArgs args)
        {
            args.CanExecute = SystemBrowserFolder is not null;
        }

        private async void ToolsRunWeeded1Command_ExecuteRequested(XamlUICommand sender, ExecuteRequestedEventArgs args)
        {
            Directory.CreateDirectory($"{(SystemBrowserFolder as ViewModelFolder)!.Path}\\Unedited");
            ViewModelFolder? targetDirectory = null;
            while (targetDirectory is null)
            {
                if ((SystemBrowserFolder as ViewModelFolder)!.Children.Any(x => x.Name == "Unedited"))
                    targetDirectory = (SystemBrowserFolder as ViewModelFolder)!.Children.First(x => x.Name == "Unedited") as ViewModelFolder;
            }

            var flag1Items = SystemBrowserFolder.Children.OfType<ViewModelFile>().Where(x => x.CheckFlag(1)).ToList();
            var filesToDelete = SystemBrowserFolder.Children.OfType<ViewModelFile>().Where(x => x.CheckFlag(1) == false && !x.File.GetFileExtension().Equals("txt", StringComparison.InvariantCultureIgnoreCase)).ToList();
            for (var i = 0; i < flag1Items.Count; i++)
            {
                Messenger.Send(new SetInfoBarMessage()
                {
                    Title = "Moving File",
                    Message = flag1Items[i].Name,
                    Severity = InfoBarSeverity.Informational,
                    IsCloseable = false
                });
                await flag1Items[i].File?.MoveAsync(targetDirectory.Folder);
            }

            for (var i = 0; i < filesToDelete.Count; i++)
            {
                Messenger.Send(new SetInfoBarMessage()
                {
                    Title = "Deleting File",
                    Message = filesToDelete[i].Name,
                    Severity = InfoBarSeverity.Informational,
                    IsCloseable = false
                });
                await filesToDelete[i].File?.DeleteAsync();
            }

            SystemBrowserFolder = targetDirectory;
            var textFileNameParts = SystemBrowserFolder.Parent.Name.Split('_');
            var textFileName = $"{textFileNameParts[0]}_{textFileNameParts[1]}_weeded1.txt";
            var textFilePath = $"{(SystemBrowserFolder.Parent as ViewModelFolder)?.Path}\\{textFileName}";
            using (var writer = new StreamWriter(textFilePath))
            {
                foreach (var item in flag1Items)
                {
                    if (item.File is not null)
                        writer.WriteLine(item.File.Name);
                }
            }

            Messenger.Send(new SetInfoBarMessage()
            {
                Title = "Done",
                Message = $"Moved {flag1Items.Count} files to {SystemBrowserFolder.Parent.Name}\\{SystemBrowserFolder.Name}, deleted {filesToDelete.Count} files, and created {textFileName}.",
                Severity = InfoBarSeverity.Success,
                IsCloseable = true
            });
        }

        private void ToolsRunWeeded2Command_CanExecuteRequested(XamlUICommand sender, CanExecuteRequestedEventArgs args)
        {
            args.CanExecute = SystemBrowserFolder?.Name == "Unedited";
        }

        private async void ToolsRunWeeded2Command_ExecuteRequested(XamlUICommand sender, ExecuteRequestedEventArgs args)
        {
            var flag1Items = SystemBrowserFolder!.Children.OfType<ViewModelFile>().Where(x => x.CheckFlag(1)).ToList();
            for (var i = 0; i < flag1Items.Count; i++)
            {
                Messenger.Send(new SetInfoBarMessage()
                {
                    Title = "Moving File",
                    Message = flag1Items[i].Name,
                    Severity = InfoBarSeverity.Informational,
                    IsCloseable = false
                });
                await flag1Items[i].File?.MoveAsync((SystemBrowserFolder.Parent as ViewModelFolder)?.Folder);
            }

            SystemBrowserFolder = SystemBrowserFolder.Parent;
            var textFileNameParts = SystemBrowserFolder.Name.Split('_');
            var textFileName = $"{textFileNameParts[0]}_{textFileNameParts[1]}_weeded2.txt";
            var textFilePath = $"{(SystemBrowserFolder as ViewModelFolder)?.Path}\\{textFileName}";
            using (var writer = new StreamWriter(textFilePath))
            {
                foreach (var item in flag1Items)
                {
                    if (item.File is not null)
                        writer.WriteLine(item.File.Name);
                }
            }

            Messenger.Send(new SetInfoBarMessage()
            {
                Title = "Deleting Directory",
                Message = "Unedited",
                Severity = InfoBarSeverity.Informational,
                IsCloseable = false
            });
            var directoryToDelete = SystemBrowserFolder.Children.First(x => x.Name == "Unedited") as ViewModelFolder;
            await directoryToDelete?.Folder?.DeleteAsync();

            Messenger.Send(new SetInfoBarMessage()
            {
                Title = "Done",
                Message = $"Moved {flag1Items.Count} files to {SystemBrowserFolder.Name}, created {textFileName}, and deleted Unedited.",
                Severity = InfoBarSeverity.Success,
                IsCloseable = true
            });
        }

        private void ToolsRunWeeded3Command_CanExecuteRequested(XamlUICommand sender, CanExecuteRequestedEventArgs args)
        {
            args.CanExecute = SystemBrowserFolder?.Name == "Unedited";
        }

        private async void ToolsRunWeeded3Command_ExecuteRequested(XamlUICommand sender, ExecuteRequestedEventArgs args)
        {
            var flag1Items = SystemBrowserFolder!.Children.OfType<ViewModelFile>().Where(x => x.CheckFlag(1)).ToList();
            for (var i = 0; i < flag1Items.Count; i++)
            {
                Messenger.Send(new SetInfoBarMessage()
                {
                    Title = "Moving File",
                    Message = flag1Items[i].Name,
                    Severity = InfoBarSeverity.Informational,
                    IsCloseable = false
                });
                await flag1Items[i].File?.MoveAsync((SystemBrowserFolder.Parent as ViewModelFolder)?.Folder);
            }

            SystemBrowserFolder = SystemBrowserFolder.Parent;
            var textFileNameParts = SystemBrowserFolder.Name.Split('_');
            var textFileName = $"{textFileNameParts[0]}_{textFileNameParts[1]}_weeded3.txt";
            var textFilePath = $"{(SystemBrowserFolder as ViewModelFolder)?.Path}\\{textFileName}";
            using (var writer = new StreamWriter(textFilePath))
            {
                foreach (var item in flag1Items)
                {
                    if (item.File is not null)
                        writer.WriteLine(item.File.Name);
                }
            }

            Messenger.Send(new SetInfoBarMessage()
            {
                Title = "Deleting Directory",
                Message = "Unedited",
                Severity = InfoBarSeverity.Informational,
                IsCloseable = false
            });
            var directoryToDelete = SystemBrowserFolder.Children.First(x => x.Name == "Unedited") as ViewModelFolder;
            await directoryToDelete?.Folder?.DeleteAsync();

            Messenger.Send(new SetInfoBarMessage()
            {
                Title = "Done",
                Message = $"Moved {flag1Items.Count} files to {SystemBrowserFolder.Name}, created {textFileName}, and deleted Unedited.",
                Severity = InfoBarSeverity.Success,
                IsCloseable = true
            });
        }

        private void ToolsDeleteFlaggedItemsCommand_CanExecuteRequested(XamlUICommand sender, CanExecuteRequestedEventArgs args)
        {
            if (!int.TryParse((string)args.Parameter, out int flag))
                args.CanExecute = false;
            args.CanExecute = SystemBrowserFolder is not null && SystemBrowserFolder.Children.OfType<ViewModelFile>().Any(x => x.CheckFlag(flag));
        }

        private async void ToolsDeleteFlaggedItemsCommand_ExecuteRequested(XamlUICommand sender, ExecuteRequestedEventArgs args)
        {
            if (!int.TryParse((string)args.Parameter, out int flag))
                return;

            var flaggedItems = SystemBrowserFolder!.Children.OfType<ViewModelFile>().Where(x => x.CheckFlag(flag)).ToList();
            for (var i = 0; i < flaggedItems.Count; i++)
            {
                Messenger.Send(new SetInfoBarMessage()
                {
                    Title = "Deleting File",
                    Message = flaggedItems[i].Name,
                    Severity = InfoBarSeverity.Informational,
                    IsCloseable = true
                });
                await flaggedItems[i].File?.DeleteAsync(StorageDeleteOption.Default);
            }
        }

        private void ToolsMarkDuplicatesInCurrentFolderCommand_CanExecuteRequested(XamlUICommand sender, CanExecuteRequestedEventArgs args)
        {
            args.CanExecute = SystemBrowserFolder is not null && SystemBrowserFolder.Children.OfType<ImageFile>().Any();
        }

        private async void ToolsMarkDuplicatesInCurrentFolderCommand_ExecuteRequested(XamlUICommand sender, ExecuteRequestedEventArgs args)
        {
            int duplicateCount = 0;
            var checksumBag = new ConcurrentBag<string>();

            foreach (var imageFile in SystemBrowserFolder!.Children.OfType<ImageFile>())
            {
                if (string.IsNullOrWhiteSpace(imageFile.Checksum))
                {
                    if (!imageFile.IsCached)
                    {
                        await imageFile.Cache();
                    }

                    imageFile.ComputeChecksum(10.0);
                    //Debug.WriteLine($"CHECKSUM {imageFile.Name}: {imageFile.Checksum}");
                }

                if (checksumBag.Contains(imageFile.Checksum))
                {
                    imageFile.SetFlag(4);
                    duplicateCount++;

                    Messenger.Send(new SetInfoBarMessage()
                    {
                        Title = $"{duplicateCount} duplicates found in {SystemBrowserFolder.Name}",
                        Message = imageFile.Name,
                        Severity = InfoBarSeverity.Informational,
                        IsCloseable = true
                    });
                }
                else
                {
                    if (imageFile.Checksum is not null)
                        checksumBag.Add(imageFile.Checksum);
                }
            }
        }

        private void SystemBrowserUpOneLevelCommand_CanExecuteRequested(XamlUICommand sender, CanExecuteRequestedEventArgs args)
        {
            args.CanExecute = SystemBrowserFolder?.Parent is not null;
        }

        private void SystemBrowserUpOneLevelCommand_ExecuteRequested(XamlUICommand sender, ExecuteRequestedEventArgs args)
        {
            SystemBrowserFolder = SystemBrowserFolder?.Parent;
        }

        private void SaveImageTransformCommand_CanExecuteRequested(XamlUICommand sender, CanExecuteRequestedEventArgs args)
        {
            args.CanExecute = ActiveElement is not null && ActiveElement is ImageFile;
        }

        private async void SaveImageTransformCommand_ExecuteRequested(XamlUICommand sender, ExecuteRequestedEventArgs args)
        {
            var request = Messenger.Send<ImageTransformRequestMessage>();
            var response = request.Response;
            var imageFile = ActiveElement as ImageFile;
            imageFile?.Transform = new ImageTransform
            {
                TranslationX = response.Item1,
                TranslationY = response.Item2,
                Rotation = response.Item3,
                Scale = response.Item4
            };

            if (imageFile?.Parent is ViewModelFolder folder)
            {
                await folder.SaveMetadataAsync();
            }
        }
        #endregion

        #region Private Methods
        private void InitializeCommands()
        {
            GeneralPreviousCommand.KeyboardAccelerators.Add(new KeyboardAccelerator
            {
                Key = VirtualKey.Left,
                IsEnabled = true
            });

            GeneralNextCommand.KeyboardAccelerators.Add(new KeyboardAccelerator
            {
                Key = VirtualKey.Right,
                IsEnabled = true
            });

            GeneralDeleteCommand.KeyboardAccelerators.Add(new KeyboardAccelerator
            {
                Key = VirtualKey.Delete,
                IsEnabled = true
            });

            BrowserCreateImageSequenceCommand.KeyboardAccelerators.Add(new KeyboardAccelerator
            {
                Key = VirtualKey.I,
                Modifiers = VirtualKeyModifiers.Control | VirtualKeyModifiers.Shift
            });

            ToolsToggleFlag1Command.KeyboardAccelerators.Add(new KeyboardAccelerator
            {
                Key = VirtualKey.Number1,
                IsEnabled = true
            });

            ToolsToggleFlag2Command.KeyboardAccelerators.Add(new KeyboardAccelerator
            {
                Key = VirtualKey.Number2,
                IsEnabled = true
            });

            ToolsToggleFlag3Command.KeyboardAccelerators.Add(new KeyboardAccelerator
            {
                Key = VirtualKey.Number3,
                IsEnabled = true
            });

            ToolsToggleFlag4Command.KeyboardAccelerators.Add(new KeyboardAccelerator
            {
                Key = VirtualKey.Number4,
                IsEnabled = true
            });

            SaveImageTransformCommand.KeyboardAccelerators.Add(new KeyboardAccelerator
            {
                Key = VirtualKey.T,
                Modifiers = VirtualKeyModifiers.Control,
                IsEnabled = true
            });

            GeneralPreviousCommand.CanExecuteRequested +=
                GeneralPreviousCommand_CanExecuteRequested;
            GeneralPreviousCommand.ExecuteRequested +=
                GeneralPreviousCommand_ExecuteRequested;

            GeneralNextCommand.CanExecuteRequested +=
                GeneralNextCommand_CanExecuteRequested;
            GeneralNextCommand.ExecuteRequested +=
                GeneralNextCommand_ExecuteRequested;

            GeneralDeleteCommand.CanExecuteRequested +=
                GeneralDeleteCommand_CanExecuteRequested;
            GeneralDeleteCommand.ExecuteRequested +=
                GeneralDeleteCommand_ExecuteRequested;

            BrowserCreateImageSequenceCommand.CanExecuteRequested +=
                BrowserCreateImageSequenceCommand_CanExecuteRequested;
            BrowserCreateImageSequenceCommand.ExecuteRequested +=
                BrowserCreateImageSequenceCommand_ExecuteRequested;

            ToolsToggleFlag1Command.CanExecuteRequested +=
                ToolsToggleFlagCommand_CanExecuteRequested;
            ToolsToggleFlag1Command.ExecuteRequested +=
                ToolsToggleFlagCommand_ExecuteRequested;

            ToolsToggleFlag2Command.CanExecuteRequested +=
                ToolsToggleFlagCommand_CanExecuteRequested;
            ToolsToggleFlag2Command.ExecuteRequested +=
                ToolsToggleFlagCommand_ExecuteRequested;

            ToolsToggleFlag3Command.CanExecuteRequested +=
                ToolsToggleFlagCommand_CanExecuteRequested;
            ToolsToggleFlag3Command.ExecuteRequested +=
                ToolsToggleFlagCommand_ExecuteRequested;

            ToolsToggleFlag4Command.CanExecuteRequested +=
                ToolsToggleFlagCommand_CanExecuteRequested;
            ToolsToggleFlag4Command.ExecuteRequested +=
                ToolsToggleFlagCommand_ExecuteRequested;

            ToolsDeleteFlag1ItemsCommand.CanExecuteRequested +=
                ToolsDeleteFlaggedItemsCommand_CanExecuteRequested;
            ToolsDeleteFlag1ItemsCommand.ExecuteRequested +=
                ToolsDeleteFlaggedItemsCommand_ExecuteRequested;

            ToolsDeleteFlag2ItemsCommand.CanExecuteRequested +=
                ToolsDeleteFlaggedItemsCommand_CanExecuteRequested;
            ToolsDeleteFlag2ItemsCommand.ExecuteRequested +=
                ToolsDeleteFlaggedItemsCommand_ExecuteRequested;

            ToolsDeleteFlag3ItemsCommand.CanExecuteRequested +=
                ToolsDeleteFlaggedItemsCommand_CanExecuteRequested;
            ToolsDeleteFlag3ItemsCommand.ExecuteRequested +=
                ToolsDeleteFlaggedItemsCommand_ExecuteRequested;

            ToolsDeleteFlag4ItemsCommand.CanExecuteRequested +=
                ToolsDeleteFlaggedItemsCommand_CanExecuteRequested;
            ToolsDeleteFlag4ItemsCommand.ExecuteRequested +=
                ToolsDeleteFlaggedItemsCommand_ExecuteRequested;

            ToolsRunWeeded1Command.CanExecuteRequested +=
                ToolsRunWeeded1Command_CanExecuteRequested;
            ToolsRunWeeded1Command.ExecuteRequested +=
                ToolsRunWeeded1Command_ExecuteRequested;

            ToolsRunWeeded2Command.CanExecuteRequested +=
                ToolsRunWeeded2Command_CanExecuteRequested;
            ToolsRunWeeded2Command.ExecuteRequested +=
                ToolsRunWeeded2Command_ExecuteRequested;

            ToolsRunWeeded3Command.CanExecuteRequested +=
                ToolsRunWeeded3Command_CanExecuteRequested;
            ToolsRunWeeded3Command.ExecuteRequested +=
                ToolsRunWeeded3Command_ExecuteRequested;

            ToolsMarkDuplicatesInCurrentFolderCommand.CanExecuteRequested +=
                ToolsMarkDuplicatesInCurrentFolderCommand_CanExecuteRequested;
            ToolsMarkDuplicatesInCurrentFolderCommand.ExecuteRequested +=
                ToolsMarkDuplicatesInCurrentFolderCommand_ExecuteRequested;

            SystemBrowserUpOneLevelCommand.CanExecuteRequested +=
                SystemBrowserUpOneLevelCommand_CanExecuteRequested;
            SystemBrowserUpOneLevelCommand.ExecuteRequested +=
                SystemBrowserUpOneLevelCommand_ExecuteRequested;

            SaveImageTransformCommand.CanExecuteRequested +=
                SaveImageTransformCommand_CanExecuteRequested;
            SaveImageTransformCommand.ExecuteRequested +=
                SaveImageTransformCommand_ExecuteRequested;
        }
        #endregion
    }
}