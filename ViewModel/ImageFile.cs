using JLR.Utility.NET;
using JLR.Utility.WinUI.ViewModel;

using Microsoft.Graphics.Canvas;

using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

using Windows.Foundation;
using Windows.Storage;

namespace ImageOrganizer.ViewModel
{
    public struct ImageTransform(double translationX,
                                 double translationY,
                                 double rotation,
                                 double scale) : IEquatable<ImageTransform>
    {
        public double TranslationX = translationX;
        public double TranslationY = translationY;
        public double Rotation = rotation;
        public double Scale = scale;

        public ImageTransform() : this(double.NaN, double.NaN, double.NaN, double.NaN) { }

        public readonly bool Equals(ImageTransform other)
        {
            return TranslationX == other.TranslationX &&
                   TranslationY == other.TranslationY &&
                   Rotation == other.Rotation &&
                   Scale == other.Scale;
        }

        public override readonly bool Equals(object? obj)
        {
            return obj is ImageTransform other && Equals(other);
        }

        public override readonly int GetHashCode()
        {
            return HashCode.Combine(TranslationX, TranslationY, Rotation, Scale);
        }

        public static bool operator ==(ImageTransform left, ImageTransform right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(ImageTransform left, ImageTransform right)
        {
            return !left.Equals(right);
        }
    }

    [ViewModelType(nameof(ImageFile))]
    public sealed partial class ImageFile : ViewModelFile
    {
        #region Fields
        private bool _isCached;
        private CanvasBitmap? _bitmap;
        private double _blurScore = -1;
        private string? _checksum;
        private Size _originalSize;
        private Rect _boundingRect;
        private ImageTransform _transform;
        #endregion

        #region Properties
        public Size OriginalSize
        {
            get => _originalSize;
            private set => SetProperty(ref _originalSize, value);
        }

        [ViewModelProperty(nameof(BoundingRect), XmlNodeType.Element, true, true)]
        public Rect BoundingRect
        {
            get => _boundingRect;
            set => SetProperty(ref _boundingRect, value);
        }

        [ViewModelProperty(nameof(Transform), XmlNodeType.Element, true, true)]
        public ImageTransform Transform
        {
            get => _transform;
            set => SetProperty(ref _transform, value, true);
        }

        public bool IsCached
        {
            get => _isCached;
            set => SetProperty(ref _isCached, value);
        }

        public CanvasBitmap? Bitmap
        {
            get => _bitmap;
            set => SetProperty(ref _bitmap, value);
        }

        /// <summary>
        /// Gets the blur score for this image computed via the Variance of Laplacian method.
        /// A lower value indicates a blurrier image; a higher value indicates a sharper image.
        /// A value of -1 means the score has not been computed.
        /// </summary>
        public double BlurScore
        {
            get => _blurScore;
            private set => SetProperty(ref _blurScore, value);
        }

        /// <summary>
        /// Gets the MD5 checksum of the cached bitmap's pixel data, represented as a
        /// lowercase hexadecimal string. A value of null means the checksum has not
        /// been computed.
        /// </summary>
        public string? Checksum
        {
            get => _checksum;
            private set => SetProperty(ref _checksum, value);
        }

        public override MimeTypes ContentType => MimeTypes.Image;
        #endregion

        #region Constructors
        public ImageFile() : this(string.Empty) { }

        public ImageFile(string path) : base(path)
        {
            _isCached = false;
            _bitmap = null;
            _originalSize = Size.Empty;
            _boundingRect = Rect.Empty;
            _transform = default;
        }

        public ImageFile(StorageFile file) : base(file)
        {
            _isCached = false;
            _bitmap = null;
            _originalSize = Size.Empty;
            _boundingRect = Rect.Empty;
            _transform = default;
        }
        #endregion

        #region Public Methods
        public async Task<bool> CropAsync(Rect cropRect, ICanvasResourceCreator? resourceCreator = null, float dpi = 96f)
        {
            BoundingRect = new Rect(cropRect.Left, cropRect.Top, cropRect.Width, cropRect.Height);
            return await Render(resourceCreator, dpi);
        }

        public async Task<bool> Render(ICanvasResourceCreator? resourceCreator = null, float dpi = 96f)
        {
            if (!IsReady && await MakeReadyAsync() == false)
            {
                IsCached = false;
                return false;
            }

            if (File is null)
            {
                IsCached = false;
                return false;
            }

            if (Bitmap is not null && Bitmap.Size.Width == (int)BoundingRect.Width && Bitmap.Size.Height == (int)BoundingRect.Height)
            {
                IsCached = true;
                return true;
            }

            try
            {
                resourceCreator ??= CanvasDevice.GetSharedDevice();
                using var stream = await File.OpenReadAsync();
                using var sourceBitmap = await CanvasBitmap.LoadAsync(resourceCreator, stream, dpi);

                var rt = new CanvasRenderTarget(
                    resourceCreator,
                    (float)BoundingRect.Width,
                    (float)BoundingRect.Height,
                    dpi,
                    sourceBitmap.Format,
                    sourceBitmap.AlphaMode);

                using (var ds = rt.CreateDrawingSession())
                {
                    ds.DrawImage(sourceBitmap, 0, 0, BoundingRect);
                }

                if (Bitmap is not null)
                {
                    Bitmap.Dispose();
                    Bitmap = null;
                }

                Bitmap = rt;
            }
            catch
            {
                Bitmap = null;
                IsCached = false;
                return false;
            }

            IsCached = true;
            return true;
        }

        public void ReleaseCache()
        {
            //Debug.WriteLine($"RELEASED {Name}");
            IsCached = false;
            Bitmap?.Dispose();
            Bitmap = null;
        }

        /// <summary>
        /// Computes a sharpness/blur score for the cached bitmap using the Variance of Laplacian method.
        /// The Laplacian highlights regions of rapid intensity change (edges). In a blurry image there
        /// are fewer sharp edges, so the variance of the Laplacian response is low.
        /// </summary>
        /// <returns>
        /// The variance of the Laplacian across all interior pixels, or -1 if no bitmap is cached
        /// or the image is too small to evaluate.
        /// </returns>
        public double ComputeBlurScore()
        {
            if (Bitmap is null)
                return -1;

            var width = (int)Bitmap.SizeInPixels.Width;
            var height = (int)Bitmap.SizeInPixels.Height;

            if (width < 3 || height < 3)
                return -1;

            // GetPixelBytes returns BGRA8 data
            var pixels = Bitmap.GetPixelBytes();

            // Convert to grayscale using standard luminance weights
            var gray = new double[width * height];
            for (var i = 0; i < gray.Length; i++)
            {
                var offset = i * 4;
                var b = pixels[offset];
                var g = pixels[offset + 1];
                var r = pixels[offset + 2];
                gray[i] = 0.299 * r + 0.587 * g + 0.114 * b;
            }

            // Apply Laplacian kernel [0,1,0; 1,-4,1; 0,1,0] and accumulate for variance
            var count = (long)(width - 2) * (height - 2);
            var sum = 0.0;
            var sumSq = 0.0;

            for (var y = 1; y < height - 1; y++)
            {
                for (var x = 1; x < width - 1; x++)
                {
                    var idx = y * width + x;
                    var laplacian =
                        gray[idx - width] +
                        gray[idx - 1] +
                        -4.0 * gray[idx] +
                        gray[idx + 1] +
                        gray[idx + width];

                    sum += laplacian;
                    sumSq += laplacian * laplacian;
                }
            }

            var mean = sum / count;
            BlurScore = (sumSq / count) - (mean * mean);
            return BlurScore;
        }

        /// <summary>
        /// Computes an MD5 checksum of the cached bitmap's raw pixel data.
        /// </summary>
        /// <returns>
        /// A lowercase hexadecimal string representing the MD5 hash of the pixel bytes,
        /// or null if no bitmap is cached.
        /// <paramref name="percentageOfImage">The percentage of the image to use for the checksum calculation.</paramref>
        /// </returns>
        public string? ComputeChecksum(double percentageOfImage = 100.0)
        {
            if (Bitmap is null)
                return null;

            var imageWidth = (int)Bitmap.SizeInPixels.Width;
            var imageHeight = (int)Bitmap.SizeInPixels.Height;
            var cornerFraction = percentageOfImage / 4.0 / 100.0;
            var width = (int)(imageWidth * cornerFraction);
            var height = (int)(imageHeight * cornerFraction);

            using var md5 = MD5.Create();
            md5.TransformBlock(Bitmap.GetPixelBytes(0, 0, width, height), 0, width * height * 4, null, 0);
            md5.TransformBlock(Bitmap.GetPixelBytes(imageWidth - width, 0, width, height), 0, width * height * 4, null, 0);
            md5.TransformBlock(Bitmap.GetPixelBytes(0, imageHeight - height, width, height), 0, width * height * 4, null, 0);
            md5.TransformFinalBlock(Bitmap.GetPixelBytes(imageWidth - width, imageHeight - height, width, height), 0, width * height * 4);

            if (md5 is null || md5.Hash is null)
                return null;
            Checksum = Convert.ToHexStringLower(md5.Hash);
            return Checksum;
        }
        #endregion

        #region Method Overrides (MediaFile)
        public override async Task<bool> MakeReadyAsync()
        {
            // Load file from path
            if (await base.MakeReadyAsync() == false)
            {
                IsReady = false;
                return false;
            }

            // Read image file properties
            try
            {
                var strWidth = "System.Image.HorizontalSize";
                var strHeight = "System.Image.VerticalSize";
                var propRequestList = new List<string> { strWidth, strHeight };
                var propResultList = await File?.Properties.RetrievePropertiesAsync(propRequestList);

                var width = (uint)propResultList[strWidth];
                var height = (uint)propResultList[strHeight];
                OriginalSize = new Size(width, height);
                if (BoundingRect == Rect.Empty) // Don't overwrite if already set (e.g. from XML)
                    BoundingRect = new Rect(0, 0, width, height);
            }
            catch (Exception)
            {
                IsReady = false;
                return false;
            }

            IsReady = true;
            return true;
        }
        #endregion

        #region Method Overrides (ViewModelElement)
        protected override object? CustomPropertyParser(string propertyName, string content, params string[] args)
        {
            if (propertyName == nameof(Transform))
            {
                if (content == "None")
                    return default(ImageTransform);

                var values = content.Split(',');
                var translationX = double.Parse(values[0]);
                var translationY = double.Parse(values[1]);
                var rotation = double.Parse(values[2]);
                var scale = double.Parse(values[3]);
                return new ImageTransform
                {
                    TranslationX = translationX,
                    TranslationY = translationY,
                    Rotation = rotation,
                    Scale = scale
                };
            }

            if (propertyName == nameof(BoundingRect))
            {
                if(content=="None")
                    return default(Rect);

                var values = content.Split(',');
                var x = double.Parse(values[0]);
                var y = double.Parse(values[1]);
                var width = double.Parse(values[2]);
                var height = double.Parse(values[3]);
                return new Rect(x, y, width, height);
            }

            return null;
        }

        protected override string? CustomPropertyWriter(string propertyName, object value, params string[] args)
        {
            if (propertyName == nameof(Transform))
            {
                if (value is ImageTransform transform)
                {
                    if (transform == default)
                        return "None";
                    else
                        return $"{transform.TranslationX},{transform.TranslationY},{transform.Rotation},{transform.Scale}";
                }
            }

            if (propertyName == nameof(BoundingRect))
            {
                if (value is Rect rect)
                {
                    if (rect == default)
                        return "None";
                    else
                        return $"{rect.X},{rect.Y},{rect.Width},{rect.Height}";
                }
            }
            return null;
        }
        #endregion
    }
}