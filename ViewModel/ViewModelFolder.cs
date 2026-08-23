using CommunityToolkit.Mvvm.Messaging;

using JLR.Utility.WinUI.ViewModel;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;

using Windows.Storage;
using Windows.System;

namespace ImageOrganizer.ViewModel
{
    [ViewModelType(nameof(ViewModelFolder))]
    public sealed partial class ViewModelFolder : ViewModelNode, IViewModelStorageItem
    {
        #region Constants
        public static readonly string MetadataFileName = "folder_metadata";
        #endregion

        #region Fields
        private string _path;
        private bool _isReady;
        private bool _hasMetadata;
        private bool _hasUnrealizedChildren;
        private readonly DispatcherQueue _dispatcher = DispatcherQueue.GetForCurrentThread();
        private StorageFolder? _folder;
        private StorageFile? _metadataFile;
        private FileSystemWatcher? _fileSystemWatcher;
        #endregion

        #region Properties
        [ViewModelProperty(nameof(Path), XmlNodeType.Element)]
        public string Path
        {
            get => _path;
            set => SetProperty(ref _path, value);
        }

        /// <summary>
        /// Gets or sets the <see cref="StorageFolder"/> associated with this instance.
        /// </summary>
        public StorageFolder? Folder
        {
            get => _folder;
            private set
            {
                SetProperty(ref _folder, value);
                if (Folder is not null)
                {
                    Path = Folder.Path;
                    Id = Folder.FolderRelativeId;
                }
            }
        }

        [ViewModelProperty(nameof(HasMetadata), XmlNodeType.Element, true)]
        public bool HasMetadata
        {
            get => _hasMetadata;
            set => SetProperty(ref _hasMetadata, value);
        }

        public StorageFile? MetadataFile
        {
            get => _metadataFile;
            private set => SetProperty(ref _metadataFile, value);
        }

        public bool IsReady
        {
            get => _isReady;
            private set => SetProperty(ref _isReady, value);
        }

        public bool HasUnrealizedChildren
        {
            get => _hasUnrealizedChildren;
            set
            {
                if (SetProperty(ref _hasUnrealizedChildren, value))
                {
                    if (!_hasUnrealizedChildren && _fileSystemWatcher is null)
                    {
                        EnableFileSystemWatcher();
                    }
                }
            }
        }
        #endregion

        #region Constructors
        public ViewModelFolder() : this(string.Empty) { }

        public ViewModelFolder(string path)
        {
            Id = string.Empty;
            _path = path;
            _hasMetadata = false;
            _folder = null;
            _metadataFile = null;
            _isReady = false;
            _hasUnrealizedChildren = true;
            Name = path.Split(@"\", StringSplitOptions.RemoveEmptyEntries).LastOrDefault() ?? string.Empty;
        }

        public ViewModelFolder(StorageFolder folder)
        {
            Id = folder?.FolderRelativeId ?? string.Empty;
            _path = folder?.Path ?? string.Empty;
            _hasMetadata = false;
            _folder = folder;
            _metadataFile = null;
            _isReady = false;
            _hasUnrealizedChildren = true;
            Name = folder?.DisplayName ?? string.Empty;
        }
        #endregion

        #region Public Methods
        public async Task<bool> MakeReadyAsync()
        {
            if (IsReady)
                return true;

            if (string.IsNullOrWhiteSpace(Path))
            {
                if (Folder is null)
                {
                    IsReady = false;
                    return false;
                }

                Path = Folder.Path;
                Id = Folder.FolderRelativeId;
            }

            var metadataPath = System.IO.Path.Combine(Path, MetadataFileName);
            HasMetadata = File.Exists(metadataPath);

            if (HasMetadata && MetadataFile is null)
            {
                try
                {
                    MetadataFile = await StorageFile.GetFileFromPathAsync(metadataPath);
                }
                catch
                {
                    HasMetadata = false;
                    MetadataFile = null;
                    return false;
                }
            }

            if (HasMetadata)
            {
                var reader = await GetXmlReaderForFileAsync(MetadataFile);
                ReadXml(reader);
            }

            if (Folder?.Path == Path && Folder?.FolderRelativeId == Id && (!HasMetadata || MetadataFile?.Path == metadataPath))
            {
                IsReady = true;
                return true;
            }

            try
            {
                Folder = await StorageFolder.GetFolderFromPathAsync(Path);
                Name = Folder.DisplayName;
                Id = Folder.FolderRelativeId;
            }
            catch
            {
                Folder = null;
                Name = string.Empty;
                Id = string.Empty;
                IsReady = false;
                return false;
            }

            IsReady = true;
            return true;
        }

        public async Task<bool> RealizeChildrenAsync()
        {
            if (await MakeReadyAsync() == false)
                return false;

            if (!HasUnrealizedChildren)
                return true;

            //Debug.WriteLine($"Realizing Children: {Name}");
            if (Folder is null)
                return false;
            var items = await Folder.GetItemsAsync();

            foreach (var subFolder in items.OfType<StorageFolder>())
            {
                var newFolder = new ViewModelFolder(subFolder);
                await newFolder.MakeReadyAsync();
                //Debug.WriteLine($"Adding Directory: {newFolder.Name}");
                if (Children.Where(c => c is ViewModelFolder vf && vf.Id == newFolder.Id).Any() == false)
                    Children.Add(newFolder);
            }

            foreach (var file in items.OfType<StorageFile>())
            {
                if (Children.Where(c => c is ViewModelFile vf && vf.Id == file.FolderRelativeId).Any())
                    continue;

                var contentType = file.ContentType.ToLowerInvariant();
                if (contentType.Contains("image"))
                {
                    var imageFile = new ImageFile(file);
                    //Debug.WriteLine($"Adding Image: {imageFile.Name}");
                    Children.Add(imageFile);
                }
                else if (contentType.Contains("video"))
                {
                    var videoFile = new VideoFile(file);
                    //Debug.WriteLine($"Adding Video: {videoFile.Name}");
                    Children.Add(videoFile);
                }
            }

            var taskList = new List<Task<bool>>();
            foreach (var file in Children.OfType<ViewModelFile>())
            {
                taskList.Add(file.MakeReadyAsync());
            }

            await foreach (var task in Task.WhenEach(taskList))
            {
                await task;
            }

            HasUnrealizedChildren = false;
            return true;
        }

        public async Task<bool> SaveMetadataAsync()
        {
            if (HasUnrealizedChildren)
                await RealizeChildrenAsync();

            if (Folder is null)
                return false;

            try
            {
                MetadataFile = await Folder.CreateFileAsync(MetadataFileName, CreationCollisionOption.ReplaceExisting);
                HasMetadata = true;
            }
            catch
            {
                HasMetadata = false;
                return false;
            }

            await SaveAsync(MetadataFile);
            return true;
        }
        #endregion

        #region Event Handlers (FileSystemWatcher)
        private void FileSystemWatcher_Created(object sender, FileSystemEventArgs e)
        {
            _dispatcher.TryEnqueue(async () =>
            {
                try
                {
                    //Debug.WriteLine("FSW Created");
                    if (Directory.Exists(e.FullPath))
                    {
                        //Debug.WriteLine($"Adding Directory: {e.FullPath}");
                        Children.Add(new ViewModelFolder(e.FullPath));
                        return;
                    }

                    if (File.Exists(e.FullPath) &&
                        (e.FullPath.EndsWith(".bmp") ||
                         e.FullPath.EndsWith(".jpg") ||
                         e.FullPath.EndsWith(".png") ||
                         e.FullPath.EndsWith(".mp4")))
                    {
                        StorageFile? file = null;
                        try { file = await StorageFile.GetFileFromPathAsync(e.FullPath); }
                        catch
                        {
                            //Debug.WriteLine($"Error accessing file: {e.Name}");
                        }

                        if (file is not null)
                        {
                            var contentType = file.ContentType.ToLowerInvariant();
                            if (contentType.Contains("image"))
                            {
                                var imageFile = new ImageFile(file);
                                //Debug.WriteLine($"Adding Image: {imageFile.Name}");
                                Children.Add(imageFile);
                                await imageFile.MakeReadyAsync();
                            }
                            else if (contentType.Contains("video"))
                            {
                                var videoFile = new VideoFile(file);
                                //Debug.WriteLine($"Adding Video: {videoFile.Name}");
                                Children.Add(videoFile);
                                await videoFile.MakeReadyAsync();
                            }
                        }
                    }
                }
                catch { }
            });
        }

        private void FileSystemWatcher_Deleted(object sender, FileSystemEventArgs e)
        {
            _dispatcher.TryEnqueue(async () =>
            {
                try
                {
                    //Debug.WriteLine("FSW Deleted");
                    var item = Children.OfType<IViewModelStorageItem>().First(x => x.Path == e.FullPath);
                    if (item is ViewModelFolder folder)
                    {
                        foreach (var image in folder.Children.OfType<ImageFile>().Where(x => x.IsCached))
                            Messenger.Send(new RemoveImageFromCacheMessage(image.Path));
                        //Debug.WriteLine($"Removing Directory: {folder.Name}");
                        Messenger.Send(new ViewModelFolderRemovedMessage(folder));
                        Children.Remove(folder);
                        folder.DisableFileSystemWatcher();
                    }
                    else if (item is ViewModelFile file)
                    {
                        if (file is ImageFile imageFile && imageFile.IsCached)
                            Messenger.Send(new RemoveImageFromCacheMessage(imageFile.Path));
                        //Debug.WriteLine($"Removing File: {file.Name}");
                        Children.Remove(file);
                    }
                }
                catch { }
            });
        }

        private void FileSystemWatcher_Changed(object sender, FileSystemEventArgs e)
        {
            _dispatcher.TryEnqueue(async () =>
            {
                try
                {
                    //Debug.WriteLine("FSW Changed");
                    var item = Children.OfType<IViewModelStorageItem>().First(x => x.Path == e.FullPath);
                    if (item is ViewModelFolder folder)
                    {
                        //Debug.WriteLine($"Changing Directory: {folder.Name}");
                        await folder.MakeReadyAsync();
                    }
                    else if (item is ViewModelFile file)
                    {
                        //Debug.WriteLine($"Changing File: {file.Name}");
                        await file.MakeReadyAsync();
                    }
                }
                catch { }
            });
        }

        private void FileSystemWatcher_Renamed(object sender, RenamedEventArgs e)
        {
            _dispatcher.TryEnqueue(async () =>
            {
                try
                {
                    //Debug.WriteLine("FSW Renamed");
                    var item = Children.OfType<IViewModelStorageItem>().First(x => x.Path == e.OldFullPath);
                    if (item is ViewModelFolder folder)
                    {
                        folder.Path = e.FullPath;
                        folder.Name = e.Name;
                        //Debug.WriteLine($"Renaming Directory: {folder.Name}");
                        if (folder.Parent is ViewModelFolder parentFolder)
                        {
                            var index = parentFolder.Children.IndexOf(folder);
                            if (index >= 0)
                            {
                                parentFolder.Children.RemoveAt(index);
                                parentFolder.Children.Insert(index, folder);
                            }
                        }
                    }
                    else if (item is ViewModelFile file)
                    {
                        file.Path = e.FullPath;
                        file.Name = e.Name;
                        //Debug.WriteLine($"Renaming File: {file.Name}");
                        if (file.Parent is ViewModelFolder parentFolder)
                        {
                            var index = parentFolder.Children.IndexOf(file);
                            if (index >= 0)
                            {
                                parentFolder.Children.RemoveAt(index);
                                parentFolder.Children.Insert(index, file);
                            }
                        }
                    }
                }
                catch { }
            });
            //Debug.WriteLine("FSW Renamed");
        }
        #endregion

        #region Method Overrides (ViewModelElement)
        protected override object? CustomPropertyParser(string propertyName, string content, params string[] args)
        {
            if (propertyName == nameof(HasMetadata))
                return bool.Parse(content);

            return null;
        }
        #endregion

        #region Private Methods
        private void EnableFileSystemWatcher()
        {
            //Debug.WriteLine($"Enabling FSW for {Name}");
            _fileSystemWatcher = new FileSystemWatcher(Path)
            {
                IncludeSubdirectories = false,
                EnableRaisingEvents = true,
                NotifyFilter = NotifyFilters.FileName |
                               NotifyFilters.DirectoryName |
                               NotifyFilters.Size |
                               NotifyFilters.LastWrite
            };

            _fileSystemWatcher.Created += FileSystemWatcher_Created;
            _fileSystemWatcher.Deleted += FileSystemWatcher_Deleted;
            _fileSystemWatcher.Changed += FileSystemWatcher_Changed;
            _fileSystemWatcher.Renamed += FileSystemWatcher_Renamed;
            _fileSystemWatcher.EnableRaisingEvents = true;
        }

        private void DisableFileSystemWatcher()
        {
            //Debug.WriteLine($"Disabling FSW for {Name}");
            if (_fileSystemWatcher is not null)
            {
                _fileSystemWatcher.EnableRaisingEvents = false;
                _fileSystemWatcher.Created -= FileSystemWatcher_Created;
                _fileSystemWatcher.Deleted -= FileSystemWatcher_Deleted;
                _fileSystemWatcher.Changed -= FileSystemWatcher_Changed;
                _fileSystemWatcher.Renamed -= FileSystemWatcher_Renamed;
                _fileSystemWatcher.Dispose();
                _fileSystemWatcher = null;
            }
        }
        #endregion
    }
}