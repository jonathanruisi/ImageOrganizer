using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkit.Mvvm.Messaging.Messages;

using ImageOrganizer.ViewModel;

using JLR.Utility.WinUI;
using JLR.Utility.WinUI.Graphics;
using JLR.Utility.WinUI.ViewModel;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Brushes;
using Microsoft.Graphics.Canvas.Geometry;
using Microsoft.UI;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;

using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.System;
using Windows.Win32;
using Windows.Win32.Foundation;

namespace ImageOrganizer.Controls
{
    public sealed partial class ImagePresenterControl : UserControl
    {
        #region Fields
        private readonly Microsoft.UI.Dispatching.DispatcherQueueTimer _renderTimer;
        private readonly LruBitmapCache _bitmapCache;
        private readonly InputCursor _primaryCursor, _hoverCursor, _dragCursor;
        private readonly InputCursor _dragWECursor, _dragNSCursor, _dragNESWCursor, _dragNWSECursor;
        private CanvasBitmap? _bitmap;
        private int _mediaIndex, _mediaTotal;
        private Matrix3x2 _transform = Matrix3x2.Identity;
        private int _isPreCaching;
        private bool _isScaling = false;
        private Rect _cropRect;
        private Quadrilateral _transformedImageQuadrilateral = Quadrilateral.Zero;
        private bool _isPointerCapturedForImage = false;
        private bool _isPointerCapturedForCrop = false;
        private Point _previousPointerPosition = new(0, 0);
        private RectLocations _capturedCropRectLocation = RectLocations.Outside;
        private ICanvasBrush? _alignmentGridPrimaryBrush, _alignmentGridSecondaryBrush, _cropRectangleBrush;
        private CanvasGeometry? _alignmentGridPrimaryGeometry, _alignmentGridSecondaryGeometry;
        private Size _alignmentGridGeometrySize;
        private float _alignmentGridGeometryThickness;
        #endregion

        #region Properties
        public ProjectManager ViewModel => (ProjectManager)DataContext;
        #endregion

        #region Constructor
        public ImagePresenterControl()
        {
            InitializeComponent();
            DataContext = App.Current.Services.GetService<ProjectManager>();

            // Initialize cursors
            _primaryCursor = InputSystemCursor.Create(InputSystemCursorShape.Hand);
            _hoverCursor = InputSystemCursor.Create(InputSystemCursorShape.Cross);
            _dragCursor = InputSystemCursor.Create(InputSystemCursorShape.SizeAll);
            _dragWECursor = InputSystemCursor.Create(InputSystemCursorShape.SizeWestEast);
            _dragNSCursor = InputSystemCursor.Create(InputSystemCursorShape.SizeNorthSouth);
            _dragNESWCursor = InputSystemCursor.Create(InputSystemCursorShape.SizeNortheastSouthwest);
            _dragNWSECursor = InputSystemCursor.Create(InputSystemCursorShape.SizeNorthwestSoutheast);

            // Initialize render timer
            _renderTimer = DispatcherQueue.CreateTimer();
            _renderTimer.IsRepeating = true;
            _renderTimer.Tick += RenderTimer_Tick;

            // Initialize bitmap cache
            _bitmapCache = new(CacheCapacity);

            // Register for messages
            RegisterMessages();
        }
        #endregion

        #region Public Methods
        public void DisplayCurrentImageFile()
        {
            if (ViewModel.ActiveElement is not ImageFile imageFile || imageFile is null)
            {
                ClearImage();
                return;
            }

            if (!LockTransform)
            {
                if (imageFile.Transform == default)
                {
                    ImageRotation = 0;
                    ScaleImageToFit();
                }
                else
                {
                    RelativeImageScale = imageFile.Transform.Scale;
                    ImageRotation = imageFile.Transform.Rotation;
                    ImageTranslationX = imageFile.Transform.TranslationX;
                    ImageTranslationY = imageFile.Transform.TranslationY;
                }
            }

            // All _bitmap/_transform access occurs on the UI thread
            // (dispatcher timer, messenger handlers, and DP callbacks), so no locking is needed.
            _bitmap = imageFile.Bitmap;

            AllowManualTranslation = true;
            AllowManualScaling = true;
            AllowManualRotation = true;
            EnableCropMode = false;
        }

        public void ClearImage()
        {
            _bitmap = null;

            _transformedImageQuadrilateral = Quadrilateral.Zero;
            _isPointerCapturedForImage = false;
            _isPointerCapturedForCrop = false;
            _previousPointerPosition = new(0, 0);
            _capturedCropRectLocation = RectLocations.Outside;
            ImageTranslationX = 0;
            ImageTranslationY = 0;
            ImageRotation = 0;
            ImageScale = 1;

            AllowManualTranslation = false;
            AllowManualScaling = false;
            AllowManualRotation = false;
            EnableCropMode = false;
        }

        public void ScaleImageToFit()
        {
            ImageScale = GetFitScale();
            ImageTranslationX = 0;
            ImageTranslationY = 0;
        }
        #endregion

        #region Dependency Property Callbacks
        private static void OnWindowDpiChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not ImagePresenterControl ip)
                return;

            ip.EnsureSwapChainDpi();
        }

        private static void OnWindowRefreshRateChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not ImagePresenterControl ip)
                return;

            ip.EnsureRefreshRate();
        }

        private static void OnImageTransformChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not ImagePresenterControl ip)
                return;

            if (e.Property == ImageScaleProperty && !ip._isScaling)
            {
                if (ip.ImageScale < 0)
                {
                    ip.ImageScale = 0;
                    return;
                }

                ip._isScaling = true;
                ip.LinearImageScale = Math.Log2(ip.ImageScale) * 10;
                ip.RelativeImageScale = ip.ImageScale / ip.GetFitScale();
                ip._isScaling = false;
            }
            else if (e.Property == LinearImageScaleProperty && !ip._isScaling)
            {
                ip._isScaling = true;
                ip.ImageScale = Math.Pow(2, 0.1 * ip.LinearImageScale);
                ip.RelativeImageScale = ip.ImageScale / ip.GetFitScale();
                ip._isScaling = false;
            }
            else if (e.Property == RelativeImageScaleProperty && !ip._isScaling)
            {
                if (ip.RelativeImageScale < 0)
                {
                    ip.RelativeImageScale = 0;
                    return;
                }

                ip._isScaling = true;
                ip.ImageScale = ip.RelativeImageScale * ip.GetFitScale();
                ip.LinearImageScale = ip.ImageScale > 0 ? Math.Log2(ip.ImageScale) * 10 : 0;
                ip._isScaling = false;
            }
            else if (e.Property == ImageRotationProperty)
            {
                ip.ImageRotation %= 360.0;
                if (ip.ImageRotation < 0)
                    ip.ImageRotation += 360.0;
            }

            if (!ip._isScaling)
                ip.UpdateTransform();
        }

        private static void OnCanvasBrushChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not ImagePresenterControl ip || ip.SwapChainPanel.SwapChain is null)
                return;

            if (e.Property == AlignmentGridPrimaryBrushProperty)
            {
                ip._alignmentGridPrimaryBrush?.Dispose();
                ip._alignmentGridPrimaryBrush = null;
                ip._alignmentGridPrimaryBrush = ip.AlignmentGridPrimaryBrush.CreateCanvasBrush(ip.SwapChainPanel.SwapChain.Device);
            }
            else if (e.Property == AlignmentGridSecondaryBrushProperty)
            {
                ip._alignmentGridSecondaryBrush?.Dispose();
                ip._alignmentGridSecondaryBrush = null;
                ip._alignmentGridSecondaryBrush = ip.AlignmentGridSecondaryBrush.CreateCanvasBrush(ip.SwapChainPanel.SwapChain.Device);
            }
            else if (e.Property == CropRectangleBrushProperty)
            {
                ip._cropRectangleBrush?.Dispose();
                ip._cropRectangleBrush = null;
                ip._cropRectangleBrush = ip.CropRectangleBrush.CreateCanvasBrush(ip.SwapChainPanel.SwapChain.Device);
            }
        }

        private static void OnCacheCapacityChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not ImagePresenterControl ip)
                return;

            ip._bitmapCache.Capacity = ((int)e.NewValue);
        }

        private static void OnEnableCropModeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not ImagePresenterControl ip)
                return;

            if (ip.EnableCropMode && ip.ViewModel.ActiveElement is ImageFile)
            {
                ip.AllowManualRotation = false;
                ip.ToggleButtonAllowRotation.IsEnabled = false;
                ip.ImageRotation = 0;
                ip._cropRect = ip._transformedImageQuadrilateral.BoundingBox;
            }
            else
            {
                ip.AllowManualRotation = true;
                ip.ToggleButtonAllowRotation.IsEnabled = true;
                ip._cropRect = Rect.Empty;
            }
        }

        private static void OnOverlayPreviousImageChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not ImagePresenterControl ip)
                return;
        }
        #endregion

        #region Event Handlers (UserControl)
        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            EnsureSwapChainDpi();
            EnsureRefreshRate();

            _alignmentGridPrimaryBrush?.Dispose();
            _alignmentGridPrimaryBrush = null;
            _alignmentGridPrimaryBrush = AlignmentGridPrimaryBrush.CreateCanvasBrush(SwapChainPanel.SwapChain.Device);

            _alignmentGridSecondaryBrush?.Dispose();
            _alignmentGridSecondaryBrush = null;
            _alignmentGridSecondaryBrush = AlignmentGridSecondaryBrush.CreateCanvasBrush(SwapChainPanel.SwapChain.Device);

            _cropRectangleBrush?.Dispose();
            _cropRectangleBrush = null;
            _cropRectangleBrush = CropRectangleBrush.CreateCanvasBrush(SwapChainPanel.SwapChain.Device);
        }

        private void UserControl_Unloaded(object sender, RoutedEventArgs e)
        {
            _renderTimer.Stop();
            SwapChainPanel.RemoveFromVisualTree();
            SwapChainPanel.SwapChain = null;

            var bitmapToDispose = _bitmap;
            _bitmap = null;
            bitmapToDispose?.Dispose();
            _bitmapCache.Dispose();

            _alignmentGridPrimaryGeometry?.Dispose();
            _alignmentGridPrimaryGeometry = null;
            _alignmentGridSecondaryGeometry?.Dispose();
            _alignmentGridSecondaryGeometry = null;
        }

        private async void UserControl_KeyboardAcceleratorInvoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs e)
        {
            if (e.Handled)
                return;

            if (sender.Key == VirtualKey.Escape)
            {
                if (EnableCropMode)
                {
                    EnableCropMode = false;
                    e.Handled = true;
                }
            }
            else if (sender.Key == VirtualKey.Enter)
            {
                if (EnableCropMode && ViewModel.ActiveElement is ImageFile imageFile && imageFile is not null)
                {
                    var sourceCropRect = GetSourceCropRect();
                    if (sourceCropRect.IsEmpty)
                        return;

                    await imageFile.CropAsync(sourceCropRect);
                    EnableCropMode = false;
                    DisplayCurrentImageFile();
                    e.Handled = true;
                }
            }
            else if (sender.Key == VirtualKey.F)
            {
                if (ViewModel.ActiveElement is ImageFile imageFile && imageFile is not null)
                {
                    if (sender.Modifiers.HasFlag(VirtualKeyModifiers.Control))
                        ImageScale = 1;
                    else
                        ScaleImageToFit();

                    e.Handled = true;
                }
            }
            else if (sender.Key == VirtualKey.L)
            {
                if (ViewModel.ActiveElement is ImageFile imageFile && imageFile is not null)
                {
                    LockTransform = !LockTransform;

                    e.Handled = true;
                }
            }
            else if (sender.Key == VirtualKey.O)
            {
                if (ViewModel.ActiveElement is ImageFile imageFile && imageFile is not null)
                {
                    OverlayPreviousImage = !OverlayPreviousImage;
                    e.Handled = true;
                }
            }
            else if (sender.Key == VirtualKey.G)
            {
                if (ViewModel.ActiveElement is ImageFile imageFile && imageFile is not null)
                {
                    ShowAlignmentGrid = !ShowAlignmentGrid;
                    e.Handled = true;
                }
            }
        }
        #endregion

        #region Event Handlers (Timers)
        private void RenderTimer_Tick(Microsoft.UI.Dispatching.DispatcherQueueTimer sender, object args)
        {
            using var ds = SwapChainPanel.SwapChain.CreateDrawingSession(Colors.Transparent);

            var bitmap = _bitmap;

            if (bitmap is null)
            {
                SwapChainPanel.SwapChain.Present();
                return;
            }

            ds.Transform = _transform;
            ds.DrawImage(bitmap, 0, 0);
            ds.Transform = Matrix3x2.Identity;

            // Determine the number of different flag adornments needed
            const float adornDistFromEdge = 25f;
            const float adornSpacing = 10f;
            const float adornSize = 100f;
            const float adornOutlineThickness = 5f;
            var adornOffset = 0;

            for (var i = 1; i <= 4; i++)
            {
                if (ViewModel.ActiveElement?.CheckFlag(i) == false)
                    continue;

                var color = i switch
                {
                    1 => Colors.Gold,
                    2 => Colors.CornflowerBlue,
                    3 => Colors.IndianRed,
                    4 => Colors.ForestGreen,
                    _ => throw new NotImplementedException()
                };

                var xOffset = adornDistFromEdge + adornSpacing + (adornSpacing * adornOffset) + (adornOffset * adornSize);
                var yOffset = adornDistFromEdge + adornSpacing;

                ds.FillEllipse(xOffset + (adornSize / 2),
                               yOffset + (adornSize / 2),
                               adornSize / 2,
                               adornSize / 2,
                               color);

                ds.DrawEllipse(xOffset + (adornSize / 2),
                               yOffset + (adornSize / 2),
                               adornSize / 2,
                               adornSize / 2,
                               Colors.Black,
                               adornOutlineThickness);

                adornOffset++;
            }

            // If in cropping mode, shade the area outside the crop rectangle
            if (EnableCropMode && !_cropRect.IsEmpty)
            {
                var cropRect = _cropRect;
                var panelWidth = SwapChainPanel.ActualWidth;
                var panelHeight = SwapChainPanel.ActualHeight;

                using (ds.CreateLayer(MaskOpacity))
                {
                    // Draw shaded rectangles outside the crop rectangle
                    ds.FillRectangle(0, 0, (float)panelWidth, (float)cropRect.Top, Colors.Black); // Top
                    ds.FillRectangle(0, (float)cropRect.Bottom, (float)panelWidth, (float)(panelHeight - cropRect.Bottom), Colors.Black); // Bottom
                    ds.FillRectangle(0, (float)cropRect.Top, (float)cropRect.Left, (float)cropRect.Height, Colors.Black); // Left
                    ds.FillRectangle((float)cropRect.Right, (float)cropRect.Top, (float)(panelWidth - cropRect.Right), (float)cropRect.Height, Colors.Black); // Right
                }

                // Draw the crop rectangle border
                ds.DrawRectangle(cropRect, _cropRectangleBrush, (float)CropRectangleThickness);
            }

            // Draw the alignment grid if enabled
            if (ShowAlignmentGrid)
            {
                var strokeWidth = (float)(AlignmentGridThickness / 3);
                EnsureAlignmentGridGeometry((float)SwapChainPanel.ActualWidth, (float)SwapChainPanel.ActualHeight);
                ds.DrawGeometry(_alignmentGridSecondaryGeometry, _alignmentGridSecondaryBrush, strokeWidth);
                ds.DrawGeometry(_alignmentGridPrimaryGeometry, _alignmentGridPrimaryBrush, strokeWidth);
            }

            SwapChainPanel.SwapChain.Present();
        }
        #endregion

        #region Event Handlers (SwapChain)
        private void SwapChainPanel_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            SwapChainPanel?.SwapChain?.ResizeBuffers(e.NewSize);

            // Re-apply relative scale so it stays consistent with the new panel size
            var oldScale = ImageScale;
            var relative = RelativeImageScale;

            _isScaling = true;
            ImageScale = relative * GetFitScale();
            LinearImageScale = ImageScale > 0 ? Math.Log2(ImageScale) * 10 : 0;

            // Keep the image point at the viewport center fixed:
            // the point displayed at the panel center is offset from the image center
            // by -Translation / scale (in image space), so scaling the translation by
            // the scale ratio preserves that point at the new center.
            if (oldScale > 0)
            {
                var scaleRatio = ImageScale / oldScale;
                ImageTranslationX *= scaleRatio;
                ImageTranslationY *= scaleRatio;
            }
            _isScaling = false;

            UpdateTransform();
        }
        #endregion

        #region Event Handlers (Pointer)
        private void PresentationBorder_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            if (_isPointerCapturedForImage || _isPointerCapturedForCrop)
                return;

            ProtectedCursor = _primaryCursor;
        }

        private void PresentationBorder_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            if (_isPointerCapturedForImage || _isPointerCapturedForCrop)
                return;

            ProtectedCursor = _primaryCursor;
        }

        private void PresentationBorder_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            var point = e.GetCurrentPoint(PresentationBorder);
            var isWithinImage = _transformedImageQuadrilateral.Contains(point.Position);
            var cropRectProximity = _cropRect.IsPointOnOrNear(point.Position, SnapDistance);

            if (point.Properties.IsLeftButtonPressed)
            {
                // Left-click occured on or within the crop rectangle,
                // so capture the pointer for cropping
                if (EnableCropMode && cropRectProximity != RectLocations.Outside)
                {
                    _isPointerCapturedForCrop = PresentationBorder.CapturePointer(e.Pointer);
                    _capturedCropRectLocation = cropRectProximity;
                }
                // Left-click occured within the image bounds
                else if (isWithinImage && AllowManualTranslation)
                {
                    // If the Control key is held down, center the image on the pointer position
                    if (e.KeyModifiers.HasFlag(VirtualKeyModifiers.Control))
                    {
                        var centerX = SwapChainPanel.ActualWidth / 2;
                        var centerY = SwapChainPanel.ActualHeight / 2;
                        ImageTranslationX += centerX - point.Position.X;
                        ImageTranslationY += centerY - point.Position.Y;
                    }
                    // Otherwise, capture the pointer for dragging the image
                    else
                    {
                        _isPointerCapturedForImage = PresentationBorder.CapturePointer(e.Pointer);
                        ProtectedCursor = _dragCursor;
                    }
                }
            }
            else if (point.Properties.IsRightButtonPressed)
            {
                // Reset the crop rectangle to the bounding box of the image
                if (EnableCropMode && cropRectProximity != RectLocations.Outside)
                {
                    _cropRect = _transformedImageQuadrilateral.BoundingBox;
                }
            }
        }

        private void PresentationBorder_PointerReleased(object sender, PointerRoutedEventArgs e)
        {
            var point = e.GetCurrentPoint(PresentationBorder);
            var isWithinImage = _transformedImageQuadrilateral.Contains(point.Position);
            var cropRectProximity = _cropRect.IsPointOnOrNear(point.Position, SnapDistance);

            SetCursor(isWithinImage, cropRectProximity);
            _isPointerCapturedForImage = false;
            _isPointerCapturedForCrop = false;
        }

        private void PresentationBorder_PointerCanceled(object sender, PointerRoutedEventArgs e)
        {
            var point = e.GetCurrentPoint(PresentationBorder);
            var isWithinImage = _transformedImageQuadrilateral.Contains(point.Position);
            var cropRectProximity = _cropRect.IsPointOnOrNear(point.Position, SnapDistance);

            SetCursor(isWithinImage, cropRectProximity);
            _isPointerCapturedForImage = false;
            _isPointerCapturedForCrop = false;
        }

        private void PresentationBorder_PointerCaptureLost(object sender, PointerRoutedEventArgs e)
        {
            var point = e.GetCurrentPoint(PresentationBorder);
            var isWithinImage = _transformedImageQuadrilateral.Contains(point.Position);
            var cropRectProximity = _cropRect.IsPointOnOrNear(point.Position, SnapDistance);

            SetCursor(isWithinImage, cropRectProximity);
            _isPointerCapturedForImage = false;
            _isPointerCapturedForCrop = false;
        }

        private void PresentationBorder_PointerMoved(object sender, PointerRoutedEventArgs e)
        {
            var point = e.GetCurrentPoint(PresentationBorder);
            var isWithinImage = _transformedImageQuadrilateral.Contains(point.Position);
            var cropRectProximity = _cropRect.IsPointOnOrNear(point.Position, SnapDistance);
            var deltaX = point.Position.X - _previousPointerPosition.X;
            var deltaY = point.Position.Y - _previousPointerPosition.Y;

            // Resize the crop rectangle if the pointer is on or near one of its edges or corners,
            // or move the crop rectangle if the pointer is inside it.
            if (_isPointerCapturedForCrop && point.Properties.IsLeftButtonPressed)
            {
                switch (_capturedCropRectLocation)
                {
                    case RectLocations.Left:
                        AdjustX();
                        AdjustWidthMinus();
                        break;
                    case RectLocations.Top:
                        AdjustY();
                        AdjustHeightMinus();
                        break;
                    case RectLocations.Bottom:
                        AdjustHeightPlus();
                        break;
                    case RectLocations.Right:
                        AdjustWidthPlus();
                        break;
                    case RectLocations.TopLeft:
                        AdjustX();
                        AdjustWidthMinus();
                        AdjustY();
                        AdjustHeightMinus();
                        break;
                    case RectLocations.TopRight:
                        AdjustWidthPlus();
                        AdjustY();
                        AdjustHeightMinus();
                        break;
                    case RectLocations.BottomRight:
                        AdjustWidthPlus();
                        AdjustHeightPlus();
                        break;
                    case RectLocations.BottomLeft:
                        AdjustX();
                        AdjustWidthMinus();
                        AdjustHeightPlus();
                        break;
                    case RectLocations.Inside:
                        AdjustX(true);
                        AdjustY(true);
                        break;
                }

                void AdjustX(bool includeWidth = false)
                {
                    if (_cropRect.X + deltaX < _transformedImageQuadrilateral.BoundingBox.Left)
                        _cropRect.X = _transformedImageQuadrilateral.BoundingBox.Left;
                    else if (includeWidth && _cropRect.X + _cropRect.Width + deltaX > _transformedImageQuadrilateral.BoundingBox.Right)
                        _cropRect.X = _transformedImageQuadrilateral.BoundingBox.Right - _cropRect.Width;
                    else if (_cropRect.X + deltaX > _cropRect.Right - 1)
                        _cropRect.X = _cropRect.Right - 1;
                    else
                        _cropRect.X += deltaX;
                }

                void AdjustY(bool includeHeight = false)
                {
                    if (_cropRect.Y + deltaY < _transformedImageQuadrilateral.BoundingBox.Top)
                        _cropRect.Y = _transformedImageQuadrilateral.BoundingBox.Top;
                    else if (includeHeight && _cropRect.Y + _cropRect.Height + deltaY > _transformedImageQuadrilateral.BoundingBox.Bottom)
                        _cropRect.Y = _transformedImageQuadrilateral.BoundingBox.Bottom - _cropRect.Height;
                    else if (_cropRect.Y + deltaY > _cropRect.Bottom - 1)
                        _cropRect.Y = _cropRect.Bottom - 1;
                    else
                        _cropRect.Y += deltaY;
                }

                void AdjustWidthPlus()
                {
                    if (_cropRect.Width + deltaX < 1)
                        _cropRect.Width = 1;
                    else if (_cropRect.Width + deltaX > _transformedImageQuadrilateral.BoundingBox.Width)
                        _cropRect.Width = _transformedImageQuadrilateral.BoundingBox.Width;
                    else
                        _cropRect.Width += deltaX;
                }

                void AdjustWidthMinus()
                {
                    if (_cropRect.Width - deltaX < 1)
                        _cropRect.Width = 1;
                    else if (_cropRect.Width - deltaX > _transformedImageQuadrilateral.BoundingBox.Width)
                        _cropRect.Width = _transformedImageQuadrilateral.BoundingBox.Width;
                    else
                        _cropRect.Width -= deltaX;
                }

                void AdjustHeightPlus()
                {
                    if (_cropRect.Height + deltaY < 1)
                        _cropRect.Height = 1;
                    else if (_cropRect.Height + deltaY > _transformedImageQuadrilateral.BoundingBox.Height)
                        _cropRect.Height = _transformedImageQuadrilateral.BoundingBox.Height;
                    else
                        _cropRect.Height += deltaY;
                }

                void AdjustHeightMinus()
                {
                    if (_cropRect.Height - deltaY < 1)
                        _cropRect.Height = 1;
                    else if (_cropRect.Height - deltaY > _transformedImageQuadrilateral.BoundingBox.Height)
                        _cropRect.Height = _transformedImageQuadrilateral.BoundingBox.Height;
                    else
                        _cropRect.Height -= deltaY;
                }
            }
            // If the pointer is captured for image translation,
            // move the image by the pointer delta.
            else if (_isPointerCapturedForImage && point.Properties.IsLeftButtonPressed)
            {
                ImageTranslationX += deltaX;
                ImageTranslationY += deltaY;
            }
            // If the pointer is not captured,
            // update the cursor based on its position relative to the image and crop rectangle.
            else
            {
                SetCursor(isWithinImage, cropRectProximity);
            }

            _previousPointerPosition = point.Position;
        }

        private void PresentationBorder_PointerWheelChanged(object sender, PointerRoutedEventArgs e)
        {
            if (_isPointerCapturedForImage || _isPointerCapturedForCrop)
                return;

            var point = e.GetCurrentPoint(PresentationBorder);
            var delta = point.Properties.MouseWheelDelta / 120.0; // Each notch of the wheel is 120 units
            var prevScaledImageSourceRect = _transformedImageQuadrilateral.BoundingBox;

            // Adjust the image rotation based on the mouse wheel delta
            if (AllowManualRotation && e.KeyModifiers.HasFlag(VirtualKeyModifiers.Shift))
            {
                var magnitude = e.KeyModifiers.HasFlag(VirtualKeyModifiers.Control) ? 1 : RotationRate;
                ImageRotation -= delta * magnitude;
            }
            // Adjust the image scale based on the mouse wheel delta
            else if (AllowManualScaling && prevScaledImageSourceRect.Contains(point.Position))
            {
                LinearImageScale += delta;
                var boundingBox = _transformedImageQuadrilateral.BoundingBox;
                var widthDelta = boundingBox.Width - prevScaledImageSourceRect.Width;
                var heightDelta = boundingBox.Height - prevScaledImageSourceRect.Height;
                var pointerOffsetX = point.Position.X - prevScaledImageSourceRect.GetCenterPoint().X;
                var pointerOffsetY = point.Position.Y - prevScaledImageSourceRect.GetCenterPoint().Y;

                ImageTranslationX -= (widthDelta * (pointerOffsetX / (prevScaledImageSourceRect.Width / 2))) / 2;
                ImageTranslationY -= (heightDelta * (pointerOffsetY / (prevScaledImageSourceRect.Height / 2))) / 2;
            }
        }
        #endregion

        #region Private Methods
        /// <summary>
        /// Updates the transformation matrix based on the current image scale, rotation, and translation values
        /// </summary>
        private void UpdateTransform()
        {
            if (ViewModel.ActiveElement is not ImageFile imageFile || imageFile is null)
                return;

            var imageRect = new Rect(0, 0, imageFile.BoundingRect.Width, imageFile.BoundingRect.Height);

            // Create transform matrix
            var offsetX = (SwapChainPanel.ActualWidth - imageFile.BoundingRect.Width * ImageScale) / 2.0;
            var offsetY = (SwapChainPanel.ActualHeight - imageFile.BoundingRect.Height * ImageScale) / 2.0;
            var translation = Matrix3x2.CreateTranslation((float)(ImageTranslationX + offsetX), (float)(ImageTranslationY + offsetY));
            var rotation = Matrix3x2.CreateRotation((float)(ImageRotation * Math.PI / 180.0), imageRect.GetCenterPoint().ToVector2());
            var scale = Matrix3x2.CreateScale((float)ImageScale);
            var transform = rotation * scale * translation;

            // Get bounding rectangle for the transformed source image
            var topLeft = Vector2.Transform(new Vector2((float)imageRect.Left, (float)imageRect.Top), transform);
            var topRight = Vector2.Transform(new Vector2((float)imageRect.Right, (float)imageRect.Top), transform);
            var bottomLeft = Vector2.Transform(new Vector2((float)imageRect.Left, (float)imageRect.Bottom), transform);
            var bottomRight = Vector2.Transform(new Vector2((float)imageRect.Right, (float)imageRect.Bottom), transform);

            // Adjust crop rectangle so that it stays aligned when the image is translated or scaled
            var previousImageBounds = _transformedImageQuadrilateral.BoundingBox;
            _transformedImageQuadrilateral = new Quadrilateral(topLeft, topRight, bottomRight, bottomLeft);
            AdjustCropRectForImageBoundsChange(previousImageBounds, _transformedImageQuadrilateral.BoundingBox);

            _transform = transform;
        }

        /// <summary>
        /// Adjusts the crop rectangle to maintain its relative position and size when the image bounds change due to translation or scaling
        /// </summary>
        /// <param name="previousImageBounds">The bounding rectangle of the image before the transformation</param>
        /// <param name="currentImageBounds">The bounding rectangle of the image after the transformation</param>
        private void AdjustCropRectForImageBoundsChange(Rect previousImageBounds, Rect currentImageBounds)
        {
            if (!EnableCropMode ||
                _cropRect.IsEmpty ||
                previousImageBounds.IsEmpty ||
                currentImageBounds.IsEmpty ||
                previousImageBounds.Width <= 0 ||
                previousImageBounds.Height <= 0 ||
                currentImageBounds.Width <= 0 ||
                currentImageBounds.Height <= 0)
                return;

            var left = currentImageBounds.Left + ((_cropRect.Left - previousImageBounds.Left) / previousImageBounds.Width * currentImageBounds.Width);
            var top = currentImageBounds.Top + ((_cropRect.Top - previousImageBounds.Top) / previousImageBounds.Height * currentImageBounds.Height);
            var right = currentImageBounds.Left + ((_cropRect.Right - previousImageBounds.Left) / previousImageBounds.Width * currentImageBounds.Width);
            var bottom = currentImageBounds.Top + ((_cropRect.Bottom - previousImageBounds.Top) / previousImageBounds.Height * currentImageBounds.Height);

            _cropRect = new Rect(left, top, right - left, bottom - top);
        }

        /// <summary>
        /// Calculates the crop rectangle in the source image's coordinate space
        /// </summary>
        /// <returns>
        /// The crop rectangle in the source image's coordinate space
        /// </returns>
        private Rect GetSourceCropRect()
        {
            if (ViewModel.ActiveElement is not ImageFile imageFile || imageFile is null || _cropRect.IsEmpty)
                return Rect.Empty;

            if (!Matrix3x2.Invert(_transform, out var inverseTransform))
                return Rect.Empty;

            var topLeft = Vector2.Transform(new Vector2((float)_cropRect.Left, (float)_cropRect.Top), inverseTransform);
            var topRight = Vector2.Transform(new Vector2((float)_cropRect.Right, (float)_cropRect.Top), inverseTransform);
            var bottomRight = Vector2.Transform(new Vector2((float)_cropRect.Right, (float)_cropRect.Bottom), inverseTransform);
            var bottomLeft = Vector2.Transform(new Vector2((float)_cropRect.Left, (float)_cropRect.Bottom), inverseTransform);

            var sourceBounds = imageFile.BoundingRect;
            var left = Math.Clamp(sourceBounds.Left + Math.Min(Math.Min(topLeft.X, topRight.X), Math.Min(bottomRight.X, bottomLeft.X)), sourceBounds.Left, sourceBounds.Right);
            var top = Math.Clamp(sourceBounds.Top + Math.Min(Math.Min(topLeft.Y, topRight.Y), Math.Min(bottomRight.Y, bottomLeft.Y)), sourceBounds.Top, sourceBounds.Bottom);
            var right = Math.Clamp(sourceBounds.Left + Math.Max(Math.Max(topLeft.X, topRight.X), Math.Max(bottomRight.X, bottomLeft.X)), sourceBounds.Left, sourceBounds.Right);
            var bottom = Math.Clamp(sourceBounds.Top + Math.Max(Math.Max(topLeft.Y, topRight.Y), Math.Max(bottomRight.Y, bottomLeft.Y)), sourceBounds.Top, sourceBounds.Bottom);

            if (right <= left || bottom <= top)
                return Rect.Empty;

            return new Rect(left, top, right - left, bottom - top);
        }

        /// <summary>
        /// Calculates the scale factor needed to fit the image within the SwapChainPanel while maintaining its aspect ratio
        /// </summary>
        /// <returns>
        /// The scale factor needed to fit the image within the SwapChainPanel
        /// </returns>
        private double GetFitScale()
        {
            if (SwapChainPanel is null ||
                ViewModel.ActiveElement is not ImageFile imageFile || imageFile is null ||
                imageFile.BoundingRect.IsEmpty ||
                imageFile.BoundingRect.IsZero ||
                imageFile.BoundingRect.Width <= 0 ||
                imageFile.BoundingRect.Height <= 0)
                return 1.0;

            return Math.Min(SwapChainPanel.ActualWidth / imageFile.BoundingRect.Width,
                            SwapChainPanel.ActualHeight / imageFile.BoundingRect.Height);
        }

        private void SetCursor(bool isWithinImage, RectLocations cropRectProximity)
        {
            // Crop mode is enabled and the pointer is inside the crop rectangle,
            // so set the cursor to the drag cursor
            if (EnableCropMode && cropRectProximity == RectLocations.Inside)
            {
                if (ProtectedCursor != _dragCursor)
                    ProtectedCursor = _dragCursor;
            }
            // Crop mode is enabled and the pointer is near the crop rectangle,
            // so set the cursor to the appropriate resize cursor
            else if (EnableCropMode && cropRectProximity != RectLocations.Outside)
            {
                switch (cropRectProximity)
                {
                    case RectLocations.Top:
                    case RectLocations.Bottom:
                        if (ProtectedCursor != _dragNSCursor)
                            ProtectedCursor = _dragNSCursor;
                        break;
                    case RectLocations.Left:
                    case RectLocations.Right:
                        if (ProtectedCursor != _dragWECursor)
                            ProtectedCursor = _dragWECursor;
                        break;
                    case RectLocations.TopLeft:
                    case RectLocations.BottomRight:
                        if (ProtectedCursor != _dragNWSECursor)
                            ProtectedCursor = _dragNWSECursor;
                        break;
                    case RectLocations.TopRight:
                    case RectLocations.BottomLeft:
                        if (ProtectedCursor != _dragNESWCursor)
                            ProtectedCursor = _dragNESWCursor;
                        break;
                }
            }
            // The pointer is within the image bounds,
            // so set the cursor to the hover cursor if manual translation or scaling is allowed,
            // otherwise set it to the primary cursor
            else if (isWithinImage)
            {
                if (AllowManualTranslation || AllowManualScaling)
                {
                    if (ProtectedCursor != _hoverCursor)
                        ProtectedCursor = _hoverCursor;
                }
                else
                {
                    if (ProtectedCursor != _primaryCursor)
                        ProtectedCursor = _primaryCursor;
                }
            }
            // The pointer is outside the image bounds,
            // so set the cursor to the primary cursor
            else
            {
                if (ProtectedCursor != _primaryCursor)
                    ProtectedCursor = _primaryCursor;
            }
        }

        private void EnsureSwapChainDpi()
        {
            var dpi = PInvoke.GetDpiForWindow((HWND)App.WindowHandle);
            if (dpi == 0)
                throw new InvalidOperationException("Unable to determine display DPI");
            WindowDpi = dpi;

            if (SwapChainPanel is not null && (SwapChainPanel.SwapChain is null || Math.Abs(SwapChainPanel.SwapChain.Dpi - WindowDpi) > 0.1))
            {
                CanvasSwapChain? newSwapChain = null;
                CanvasSwapChain? oldSwapChain = SwapChainPanel.SwapChain;

                try
                {
                    newSwapChain = new CanvasSwapChain(CanvasDevice.GetSharedDevice(),
                                                       (float)Math.Max(1, SwapChainPanel.ActualWidth),
                                                       (float)Math.Max(1, SwapChainPanel.ActualHeight),
                                                       (float)WindowDpi);
                    SwapChainPanel.SwapChain = newSwapChain;
                }
                catch
                {
                    newSwapChain?.Dispose();
                    newSwapChain = null;
                    throw;
                }
                finally
                {
                    oldSwapChain?.Dispose();
                }

                ClearImage();
                _bitmapCache.Dpi = SwapChainPanel.SwapChain.Dpi;
            }
        }

        private void EnsureRefreshRate()
        {
            var refreshRate = DisplayHelper.GetRefreshRateForWindow(App.WindowHandle)
                ?? throw new InvalidOperationException("Unable to determine display refresh rate");
            WindowRefreshRate = refreshRate;

            var targetTimerInterval = 1000.0 / WindowRefreshRate;
            if (!_renderTimer.IsRunning || _renderTimer.Interval.TotalMilliseconds != targetTimerInterval)
            {
                _renderTimer.Interval = TimeSpan.FromMilliseconds(targetTimerInterval);
                _renderTimer.Start();
            }
        }

        /// <summary>
        /// Builds (or rebuilds) the cached rule-of-thirds grid geometry.
        /// The geometry is only regenerated when the panel size or grid thickness changes.
        /// </summary>
        /// <param name="panelWidth">Current width of the swap chain panel</param>
        /// <param name="panelHeight">Current height of the swap chain panel</param>
        private void EnsureAlignmentGridGeometry(float panelWidth, float panelHeight)
        {
            var thickness = (float)(AlignmentGridThickness / 3);
            if (_alignmentGridPrimaryGeometry is not null &&
                _alignmentGridSecondaryGeometry is not null &&
                _alignmentGridGeometrySize.Width == panelWidth &&
                _alignmentGridGeometrySize.Height == panelHeight &&
                _alignmentGridGeometryThickness == thickness)
                return;

            _alignmentGridPrimaryGeometry?.Dispose();
            _alignmentGridPrimaryGeometry = null;
            _alignmentGridSecondaryGeometry?.Dispose();
            _alignmentGridSecondaryGeometry = null;

            var device = SwapChainPanel.SwapChain.Device;
            var x1 = panelWidth / 3;
            var x2 = 2 * panelWidth / 3;
            var y1 = panelHeight / 3;
            var y2 = 2 * panelHeight / 3;

            static void AddLine(CanvasPathBuilder pb, float startX, float startY, float endX, float endY)
            {
                pb.BeginFigure(startX, startY);
                pb.AddLine(endX, endY);
                pb.EndFigure(CanvasFigureLoop.Open);
            }

            using (var pb = new CanvasPathBuilder(device))
            {
                AddLine(pb, x1, 0, x1, panelHeight);
                AddLine(pb, x2, 0, x2, panelHeight);
                AddLine(pb, 0, y1, panelWidth, y1);
                AddLine(pb, 0, y2, panelWidth, y2);
                _alignmentGridPrimaryGeometry = CanvasGeometry.CreatePath(pb);
            }

            using (var pb = new CanvasPathBuilder(device))
            {
                AddLine(pb, x1 - thickness, 0, x1 - thickness, panelHeight);
                AddLine(pb, x1 + thickness, 0, x1 + thickness, panelHeight);
                AddLine(pb, x2 - thickness, 0, x2 - thickness, panelHeight);
                AddLine(pb, x2 + thickness, 0, x2 + thickness, panelHeight);
                AddLine(pb, 0, y1 - thickness, panelWidth, y1 - thickness);
                AddLine(pb, 0, y1 + thickness, panelWidth, y1 + thickness);
                AddLine(pb, 0, y2 - thickness, panelWidth, y2 - thickness);
                AddLine(pb, 0, y2 + thickness, panelWidth, y2 + thickness);
                _alignmentGridSecondaryGeometry = CanvasGeometry.CreatePath(pb);
            }

            _alignmentGridGeometrySize = new Size(panelWidth, panelHeight);
            _alignmentGridGeometryThickness = thickness;
        }

        /// <summary>
        /// Pre-caches upcoming sibling images without blocking the caller.
        /// Reentrancy-safe: only one pre-caching pass runs at a time.
        /// </summary>
        /// <param name="imageFile">The currently displayed image whose siblings should be pre-cached</param>
        private async Task PreCacheUpcomingImagesAsync(ImageFile imageFile)
        {
            if (Interlocked.CompareExchange(ref _isPreCaching, 1, 0) != 0)
                return;

            try
            {
                var startIndex = _mediaIndex + 1;
                for (var i = startIndex; i < startIndex + AutoCacheThreshold && i < _mediaTotal; i++)
                {
                    if (imageFile.Parent.Children[i] is ImageFile nextImage && !nextImage.IsCached)
                    {
                        await _bitmapCache.LoadImageAsync(SwapChainPanel.SwapChain.Device, nextImage);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Pre-caching failed: {ex}");
            }
            finally
            {
                Interlocked.Exchange(ref _isPreCaching, 0);
            }
        }

        private void RegisterMessages()
        {
            var services = App.Current.Services
                ?? throw new InvalidOperationException("Services not configured");
            var messenger = services.GetService<IMessenger>()
                ?? throw new InvalidOperationException("IMessenger service is not registered");

            messenger.Register<ImagePresenterControl, ValueChangedMessage<(int dpiX, int dpiY)>>(this, (r, m) =>
            {
                r.WindowDpi = m.Value.dpiX;
            });

            messenger.Register<ImagePresenterControl, ValueChangedMessage<double?>>(this, (r, m) =>
            {
                if (m.Value is null)
                    return;

                r.WindowRefreshRate = (double)m.Value;
            });

            messenger.Register<ImagePresenterControl, PropertyChangedMessage<ViewModelElement>>(this, static async (r, m) =>
            {
                if (m.Sender != r.ViewModel || m.PropertyName != nameof(ViewModel.ActiveElement))
                    return;

                if (m.NewValue is null)
                {
                    r._mediaIndex = 0;
                    r._mediaTotal = 0;
                    r.ClearImage();
                }
                else if (m.NewValue is ImageFile imageFile)
                {
                    var isAvailable = await r._bitmapCache.LoadImageAsync(r.SwapChainPanel.SwapChain.Device, imageFile);
                    if (isAvailable)
                        r.DisplayCurrentImageFile();
                    else
                    {
                        r.ClearImage();
                        return;
                    }

                    r._mediaIndex = imageFile.Parent.Children.IndexOf(imageFile);
                    r._mediaTotal = imageFile.Parent.Children.Count;

                    var prevIndex = 0;
                    if (m.OldValue is ImageFile prevImage && prevImage.Parent == imageFile.Parent)
                        prevIndex = prevImage.Parent.Children.IndexOf(prevImage);

                    if (prevIndex == 0 || prevIndex < r._mediaIndex)
                    {
                        // Fire-and-forget by design: pre-caching must not delay display of the
                        // current image, and the method handles its own errors and reentrancy.
                        _ = r.PreCacheUpcomingImagesAsync(imageFile);
                    }
                }
            });

            messenger.Register<ImagePresenterControl, RemoveImageFromCacheMessage>(this, (r, m) =>
            {
                r._bitmapCache.RemoveImage(m.Path);
            });

            messenger.Register<ImagePresenterControl, ImageTransformRequestMessage>(this, (r, m) =>
            {
                var translationX = r.ImageTranslationX;
                var translationY = r.ImageTranslationY;
                var rotation = r.ImageRotation;
                var scale = r.RelativeImageScale;
                m.Reply((translationX, translationY, rotation, scale));
            });
        }
        #endregion
    }
}