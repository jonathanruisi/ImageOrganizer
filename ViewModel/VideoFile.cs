using JLR.Utility.NET;
using JLR.Utility.WinUI.ViewModel;

using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

using Windows.Storage;

namespace ImageOrganizer.ViewModel
{
    [ViewModelType(nameof(VideoFile))]
    public sealed partial class VideoFile : ViewModelFile
    {
        #region Fields

        #endregion

        #region Properties
        public override MimeTypes ContentType => MimeTypes.Image;
        #endregion

        #region Constructors
        public VideoFile() : this(string.Empty) { }

        public VideoFile(string path) : base(path)
        {

        }

        public VideoFile(StorageFile file) : base(file)
        {

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