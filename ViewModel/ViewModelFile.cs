using JLR.Utility.NET;
using JLR.Utility.WinUI.ViewModel;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;

using Windows.Storage;

namespace ImageOrganizer.ViewModel
{
    public abstract class ViewModelFile : ViewModelElement, IViewModelStorageItem
    {
        #region Fields
        private string _path;
        private StorageFile? _file;
        private bool _isReady;
        #endregion

        #region Properties
        [ViewModelProperty(nameof(Path), XmlNodeType.Element)]
        public string Path
        {
            get => _path;
            set => SetProperty(ref _path, value);
        }

        /// <summary>
        /// Gets or sets the <see cref="StorageFile"/> associated with this instance.
        /// </summary>
        public StorageFile? File
        {
            get => _file;
            protected set
            {
                SetProperty(ref _file, value);
                if (File is not null && File.IsAvailable)
                {
                    Path = File.Path;
                    Id = File.FolderRelativeId;
                }
            }
        }

        public bool IsReady
        {
            get => _isReady;
            protected set => SetProperty(ref _isReady, value);
        }

        public abstract MimeTypes ContentType { get; }
        #endregion

        #region Constructors
        protected ViewModelFile() : this(string.Empty) { }

        protected ViewModelFile(string path)
        {
            _isReady = false;
            _file = null;
            _path = path;
            Id = string.Empty;
            Name = string.Empty;
        }

        protected ViewModelFile(StorageFile file)
        {
            _isReady = false;
            _file = file;
            _path = file?.Path ?? string.Empty;
            Id = file?.FolderRelativeId ?? string.Empty;
            Name = file?.DisplayName ?? string.Empty;
        }
        #endregion

        #region Public Methods
        public virtual async Task<bool> MakeReadyAsync()
        {
            if (IsReady)
                return true;

            if (string.IsNullOrWhiteSpace(Path) && File is null)
                return false;

            if (string.IsNullOrWhiteSpace(Path) && File?.IsAvailable == true)
            {
                Path = File.Path;
                Id = File.FolderRelativeId;
            }

            if (File?.Path == Path && File?.FolderRelativeId == Id)
                return true;

            try
            {
                File = await StorageFile.GetFileFromPathAsync(Path);
                Name = File.DisplayName;
                Id = File.FolderRelativeId;
            }
            catch
            {
                File = null;
                Name = string.Empty;
                Id = string.Empty;
                return false;
            }

            var contentTypeString = Enum.GetName(ContentType) ?? Enum.GetName(MimeTypes.Unknown);
            if (!File.ContentType.Contains(contentTypeString!, StringComparison.CurrentCultureIgnoreCase))
                throw new InvalidOperationException($"{contentTypeString} file expected");

            return true;
        }
        #endregion
    }
}