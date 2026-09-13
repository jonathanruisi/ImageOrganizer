using ImageOrganizer.ViewModel;

using JLR.Utility.WinUI;
using JLR.Utility.WinUI.Graphics;

using Microsoft.UI.Xaml.Media;

using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;

using Windows.Foundation;

namespace ImageOrganizer.Controls
{
    public sealed partial class ImagePresenterControl
    {
        #region Nested Types
        private sealed class ImagePresenterLayer
        {
            #region Fields
            private readonly ImagePresenterControl _parent;
            #endregion

            #region Properties
            public ImageFile? ImageFile { get; set; }
            public Matrix3x2 Transform { get; set; }
            public Quadrilateral TransformedImageQuadrilateral { get; set; }
            public Rect CropRectangle { get; set; }
            public bool IsDragging { get; set; }
            public bool IsCropping { get; set; }
            public RectLocations CropRectAdjustmentLocation { get; set; }
            #endregion

            #region Constructor
            public ImagePresenterLayer(ImagePresenterControl parent, ImageFile? imageFile = null)
            {
                _parent = parent;
                ImageFile = imageFile;
                Transform = Matrix3x2.Identity;
                TransformedImageQuadrilateral = Quadrilateral.Zero;
                CropRectangle = Rect.Empty;
                IsDragging = false;
                IsCropping = false;
                CropRectAdjustmentLocation = RectLocations.Outside;
            }
            #endregion

            #region Public Methods
            /// <summary>
            /// Calculates the scale factor needed to fit the image within the SwapChainPanel while maintaining its aspect ratio
            /// </summary>
            /// <returns>
            /// The scale factor needed to fit the image within the SwapChainPanel
            /// </returns>
            public double GetFitScale(double panelWidth, double panelHeight)
            {
                if (ImageFile is null ||
                    ImageFile.BoundingRect.IsEmpty ||
                    ImageFile.BoundingRect.IsZero ||
                    ImageFile.BoundingRect.Width <= 0 ||
                    ImageFile.BoundingRect.Height <= 0)
                    return 1.0;

                return Math.Min(panelWidth / ImageFile.BoundingRect.Width,
                                panelHeight / ImageFile.BoundingRect.Height);
            }

            /// <summary>
            /// Updates the transformation matrix based on the current image scale, rotation, and translation values
            /// </summary>
            public void UpdateTransform()
            {
                if (ImageFile is null)
                    return;

                var imageRect = new Rect(0, 0, ImageFile.BoundingRect.Width, ImageFile.BoundingRect.Height);

                // Create transform matrix
                var offsetX = (_parent.SwapChainPanel.ActualWidth - ImageFile.BoundingRect.Width * _parent.ImageScale) / 2.0;
                var offsetY = (_parent.SwapChainPanel.ActualHeight - ImageFile.BoundingRect.Height * _parent.ImageScale) / 2.0;
                var translation = Matrix3x2.CreateTranslation((float)(_parent.ImageTranslationX + offsetX), (float)(_parent.ImageTranslationY + offsetY));
                var rotation = Matrix3x2.CreateRotation((float)(_parent.ImageRotation * Math.PI / 180.0), imageRect.GetCenterPoint().ToVector2());
                var scale = Matrix3x2.CreateScale((float)_parent.ImageScale);
                var transform = rotation * scale * translation;

                // Get bounding rectangle for the transformed source image
                var topLeft = Vector2.Transform(new Vector2((float)imageRect.Left, (float)imageRect.Top), transform);
                var topRight = Vector2.Transform(new Vector2((float)imageRect.Right, (float)imageRect.Top), transform);
                var bottomLeft = Vector2.Transform(new Vector2((float)imageRect.Left, (float)imageRect.Bottom), transform);
                var bottomRight = Vector2.Transform(new Vector2((float)imageRect.Right, (float)imageRect.Bottom), transform);

                // Adjust crop rectangle so that it stays aligned when the image is translated or scaled
                var previousImageBounds = TransformedImageQuadrilateral.BoundingBox;
                TransformedImageQuadrilateral = new Quadrilateral(topLeft, topRight, bottomRight, bottomLeft);
                AdjustCropRectForImageBoundsChange(previousImageBounds, TransformedImageQuadrilateral.BoundingBox);

                Transform = transform;
            }

            /// <summary>
            /// Calculates the crop rectangle in the source image's coordinate space
            /// </summary>
            /// <returns>
            /// The crop rectangle in the source image's coordinate space
            /// </returns>
            public Rect GetSourceCropRect()
            {
                if (ImageFile is null || CropRectangle.IsEmpty)
                    return Rect.Empty;

                if (!Matrix3x2.Invert(Transform, out var inverseTransform))
                    return Rect.Empty;

                var topLeft = Vector2.Transform(new Vector2((float)CropRectangle.Left, (float)CropRectangle.Top), inverseTransform);
                var topRight = Vector2.Transform(new Vector2((float)CropRectangle.Right, (float)CropRectangle.Top), inverseTransform);
                var bottomRight = Vector2.Transform(new Vector2((float)CropRectangle.Right, (float)CropRectangle.Bottom), inverseTransform);
                var bottomLeft = Vector2.Transform(new Vector2((float)CropRectangle.Left, (float)CropRectangle.Bottom), inverseTransform);

                var sourceBounds = ImageFile.BoundingRect;
                var left = Math.Clamp(sourceBounds.Left + Math.Min(Math.Min(topLeft.X, topRight.X), Math.Min(bottomRight.X, bottomLeft.X)), sourceBounds.Left, sourceBounds.Right);
                var top = Math.Clamp(sourceBounds.Top + Math.Min(Math.Min(topLeft.Y, topRight.Y), Math.Min(bottomRight.Y, bottomLeft.Y)), sourceBounds.Top, sourceBounds.Bottom);
                var right = Math.Clamp(sourceBounds.Left + Math.Max(Math.Max(topLeft.X, topRight.X), Math.Max(bottomRight.X, bottomLeft.X)), sourceBounds.Left, sourceBounds.Right);
                var bottom = Math.Clamp(sourceBounds.Top + Math.Max(Math.Max(topLeft.Y, topRight.Y), Math.Max(bottomRight.Y, bottomLeft.Y)), sourceBounds.Top, sourceBounds.Bottom);

                if (right <= left || bottom <= top)
                    return Rect.Empty;

                return new Rect(left, top, right - left, bottom - top);
            }
            #endregion

            #region Private Methods
            /// <summary>
            /// Adjusts the crop rectangle to maintain its relative position and size when the image bounds change due to translation or scaling
            /// </summary>
            /// <param name="previousImageBounds">The bounding rectangle of the image before the transformation</param>
            /// <param name="currentImageBounds">The bounding rectangle of the image after the transformation</param>
            private void AdjustCropRectForImageBoundsChange(Rect previousImageBounds, Rect currentImageBounds)
            {
                if (!_parent.EnableCropMode ||
                    CropRectangle.IsEmpty ||
                    previousImageBounds.IsEmpty ||
                    currentImageBounds.IsEmpty ||
                    previousImageBounds.Width <= 0 ||
                    previousImageBounds.Height <= 0 ||
                    currentImageBounds.Width <= 0 ||
                    currentImageBounds.Height <= 0)
                    return;

                var left = currentImageBounds.Left + ((CropRectangle.Left - previousImageBounds.Left) / previousImageBounds.Width * currentImageBounds.Width);
                var top = currentImageBounds.Top + ((CropRectangle.Top - previousImageBounds.Top) / previousImageBounds.Height * currentImageBounds.Height);
                var right = currentImageBounds.Left + ((CropRectangle.Right - previousImageBounds.Left) / previousImageBounds.Width * currentImageBounds.Width);
                var bottom = currentImageBounds.Top + ((CropRectangle.Bottom - previousImageBounds.Top) / previousImageBounds.Height * currentImageBounds.Height);

                CropRectangle = new Rect(left, top, right - left, bottom - top);
            }
            #endregion
        }
        #endregion
    }
}