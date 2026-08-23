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
        private MimeTypes _contentType;
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

        public MimeTypes ContentType
        {
            get => _contentType;
            protected set => SetProperty(ref _contentType, value);
        }
        #endregion

        #region Constructors
        protected ViewModelFile() : this(string.Empty) { }

        protected ViewModelFile(string path)
        {
            Id = string.Empty;
            _path = path;
            _file = null;
            _isReady = false;
            _contentType = MimeTypes.Unknown;
            Name = string.Empty;
        }

        protected ViewModelFile(StorageFile file)
        {
            Id = file?.FolderRelativeId ?? string.Empty;
            _path = file?.Path ?? string.Empty;
            _file = file;
            _isReady = false;
            Name = file?.DisplayName ?? string.Empty;
            var mimeTypeString = file?.ContentType.Split('/', StringSplitOptions.RemoveEmptyEntries).First().ToLowerInvariant();
            mimeTypeString ??= MimeTypes.Unknown.ToString();
            _contentType = Enum.Parse<MimeTypes>(mimeTypeString, true);
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

            //var contentTypeString = Enum.GetName(ContentType) ?? Enum.GetName(MimeTypes.Unknown);
            //if (!File.ContentType.Contains(contentTypeString, StringComparison.CurrentCultureIgnoreCase))
            //    throw new InvalidOperationException($"{contentTypeString} file expected");

            return true;
        }
        #endregion
    }
}