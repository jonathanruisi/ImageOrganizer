using JLR.Utility.NET;
using JLR.Utility.WinUI.ViewModel;

using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

using Windows.Storage;

namespace ImageOrganizer.ViewModel
{
    [ViewModelType(nameof(VideoFile))]
    public sealed partial class VideoFile : ViewModelFile, IMediaMetadata
    {
        #region Fields
        private int _rating;
        #endregion

        #region Properties
        [ViewModelProperty(nameof(Rating), XmlNodeType.Element)]
        public int Rating
        {
            get => _rating;
            set => SetProperty(ref _rating, value);
        }

        public override MimeTypes ContentType => MimeTypes.Video;
        #endregion

        #region Constructors
        public VideoFile() : this(string.Empty) { }

        public VideoFile(string path) : base(path)
        {
            _rating = 0;
        }

        public VideoFile(StorageFile file) : base(file)
        {
            _rating = 0;
        }
        #endregion

        #region Method Overrides (MediaFile)
        public override async Task<bool> MakeReadyAsync()
        {
            IsReady = await base.MakeReadyAsync();
            return IsReady;
        }
        #endregion
    }
}