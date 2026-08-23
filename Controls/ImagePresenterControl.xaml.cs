using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkit.Mvvm.Messaging.Messages;

using ImageOrganizer.ViewModel;

using JLR.Utility.WinUI;
using JLR.Utility.WinUI.Graphics;
using JLR.Utility.WinUI.ViewModel;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Graphics.Canvas;
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
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;

using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Win32;
using Windows.Win32.Foundation;

namespace ImageOrganizer.Controls
{
    public enum InteractionMode
    {
        None,
        Transform,
        Crop
    }

    public sealed partial class ImagePresenterControl : UserControl
    {
        #region Fields
        private readonly Microsoft.UI.Dispatching.DispatcherQueueTimer _renderTimer;
        private CanvasBitmap? _bitmap;
        private readonly Lock _bitmapLock = new();
        private Matrix3x2 _transform = Matrix3x2.Identity;
        private readonly Lock _transformLock = new();
        private readonly LruBitmapCache _bitmapCache;
        private readonly InputCursor _primaryCursor;
        private readonly InputCursor _secondaryCursor;
        private readonly InputCursor _hoverCursor;
        private readonly InputCursor _dragCursor;
        private bool _isScaling = false;
        private bool _isPointerCapturedForImage = false;
        private bool _isPreCaching = false;
        private Point _lastPointerPosition;
        private Rect _imageSourceRect;
        private Quadrilateral _scaledImageSourceQuadrilateral = Quadrilateral.Zero;
        private int _mediaIndex, _mediaTotal;
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
            _secondaryCursor = InputSystemCursor.Create(InputSystemCursorShape.UpArrow);
            _hoverCursor = InputSystemCursor.Create(InputSystemCursorShape.Cross);
            _dragCursor = InputSystemCursor.Create(InputSystemCursorShape.SizeAll);

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
        public async Task DisplayImageFileAsync(ImageFile imageFile)
        {
            //Debug.WriteLine("Load Image");
            var bitmap = await _bitmapCache.GetOrLoadImageAsync(SwapChainPanel.SwapChain.Device, imageFile);

            if (bitmap is null)
            {
                ClearImage();
                return;
            }

            _imageSourceRect = new Rect(0, 0, bitmap.Size.Width, bitmap.Size.Height);

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

            lock (_bitmapLock)
            {
                _bitmap = bitmap;
            }

            InteractionMode = InteractionMode.Transform;
        }

        public void ClearImage()
        {
            //Debug.WriteLine("Clear Image");
            lock (_bitmapLock)
            {
                _bitmap = null;
            }

            _imageSourceRect = Rect.Zero;
            _scaledImageSourceQuadrilateral = Quadrilateral.Zero;
            ImageTranslationX = 0;
            ImageTranslationY = 0;
            ImageRotation = 0;
            ImageScale = 1;
            InteractionMode = InteractionMode.None;
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

        private static void OnCacheCapacityChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not ImagePresenterControl ip)
                return;

            ip._bitmapCache.Capacity = ((int)e.NewValue);
        }
        #endregion

        #region Event Handlers (UserControl)
        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            EnsureSwapChainDpi();
            EnsureRefreshRate();
        }

        private void UserControl_Unloaded(object sender, RoutedEventArgs e)
        {
            _renderTimer.Stop();
            SwapChainPanel.RemoveFromVisualTree();
            SwapChainPanel.SwapChain = null;

            CanvasBitmap? bitmapToDispose;
            lock (_bitmapLock)
            {
                bitmapToDispose = _bitmap;
                _bitmap = null;
            }
            bitmapToDispose?.Dispose();
            _bitmapCache.Dispose();
        }
        #endregion

        #region Event Handlers (Timers)
        private void RenderTimer_Tick(Microsoft.UI.Dispatching.DispatcherQueueTimer sender, object args)
        {
            using var ds = SwapChainPanel.SwapChain.CreateDrawingSession(Colors.Transparent);

            CanvasBitmap? bitmap;
            lock (_bitmapLock)
            {
                bitmap = _bitmap;
            }

            if (bitmap is null)
            {
                SwapChainPanel.SwapChain.Present();
                return;
            }

            Matrix3x2 transform;
            lock (_transformLock)
            {
                transform = _transform;
            }

            ds.Transform = transform;
            ds.DrawImage(bitmap, 0, 0);
            ds.Transform = Matrix3x2.Identity;

            // Determine the number of different flag adornments needed
            const float adornDistFromEdge = 25f;
            const float adornSpacing = 10f;
            const float adornSize = 100f;
            const float adornOutlineThickness = 5f;
            var numAdorn = 0;
            var adornOffset = 0;

            if (ViewModel.ActiveElement?.CheckFlag(4) == true)
                numAdorn++;
            if (ViewModel.ActiveElement?.CheckFlag(3) == true)
                numAdorn++;
            if (ViewModel.ActiveElement?.CheckFlag(2) == true)
                numAdorn++;
            if (ViewModel.ActiveElement?.CheckFlag(1) == true)
                numAdorn++;

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

            SwapChainPanel.SwapChain.Present();
        }
        #endregion

        #region Event Handlers (SwapChain)
        private void SwapChainPanel_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            //Debug.WriteLine("Size Changed");
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
            if (_isPointerCapturedForImage)
                return;

            ProtectedCursor = _primaryCursor;
        }

        private void PresentationBorder_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            if (_isPointerCapturedForImage)
                return;

            ProtectedCursor = _primaryCursor;
        }

        private void PresentationBorder_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            var point = e.GetCurrentPoint(PresentationBorder);
            if (point.Properties.PointerUpdateKind == PointerUpdateKind.LeftButtonPressed &&
                _scaledImageSourceQuadrilateral.Contains(point.Position) &&
                InteractionMode == InteractionMode.Transform)
            {
                if (e.KeyModifiers.HasFlag(Windows.System.VirtualKeyModifiers.Control))
                {
                    var centerX = SwapChainPanel.ActualWidth / 2;
                    var centerY = SwapChainPanel.ActualHeight / 2;
                    ImageTranslationX += centerX - point.Position.X;
                    ImageTranslationY += centerY - point.Position.Y;
                }
                else
                {
                    _isPointerCapturedForImage = PresentationBorder.CapturePointer(e.Pointer);
                    ProtectedCursor = _dragCursor;
                }
            }

            _lastPointerPosition = point.Position;
        }

        private void PresentationBorder_PointerReleased(object sender, PointerRoutedEventArgs e)
        {
            var point = e.GetCurrentPoint(PresentationBorder);
            if (point.Properties.PointerUpdateKind == PointerUpdateKind.LeftButtonReleased)
            {
                PresentationBorder.ReleasePointerCapture(e.Pointer);
                ProtectedCursor = _scaledImageSourceQuadrilateral.Contains(point.Position) && InteractionMode == InteractionMode.Transform
                    ? _hoverCursor
                    : _secondaryCursor;
            }

            _lastPointerPosition = point.Position;
        }

        private void PresentationBorder_PointerCanceled(object sender, PointerRoutedEventArgs e)
        {
            _isPointerCapturedForImage = false;
            _lastPointerPosition = new Point(double.NaN, double.NaN);
        }

        private void PresentationBorder_PointerCaptureLost(object sender, PointerRoutedEventArgs e)
        {
            _isPointerCapturedForImage = false;
        }

        private void PresentationBorder_PointerMoved(object sender, PointerRoutedEventArgs e)
        {
            var point = e.GetCurrentPoint(PresentationBorder);

            if (_scaledImageSourceQuadrilateral.Contains(point.Position)
                && InteractionMode == InteractionMode.Transform
                && ProtectedCursor != _hoverCursor)
                ProtectedCursor = _hoverCursor;
            else if ((!_scaledImageSourceQuadrilateral.Contains(point.Position)
                      || InteractionMode == InteractionMode.None)
                     && ProtectedCursor != _primaryCursor)
                ProtectedCursor = _primaryCursor;

            if (point.Properties.IsLeftButtonPressed
                && InteractionMode == InteractionMode.Transform
                && _isPointerCapturedForImage)
            {
                if (ProtectedCursor != _dragCursor)
                    ProtectedCursor = _dragCursor;

                ImageTranslationX += point.Position.X - _lastPointerPosition.X;
                ImageTranslationY += point.Position.Y - _lastPointerPosition.Y;
            }

            _lastPointerPosition = point.Position;
        }

        private void PresentationBorder_PointerWheelChanged(object sender, PointerRoutedEventArgs e)
        {
            if (InteractionMode == InteractionMode.None || _isPointerCapturedForImage)
                return;

            var point = e.GetCurrentPoint(PresentationBorder);
            var delta = point.Properties.MouseWheelDelta / 120.0;
            var prevScaledImageSourceRect = _scaledImageSourceQuadrilateral.BoundingBox;

            if (e.KeyModifiers.HasFlag(Windows.System.VirtualKeyModifiers.Shift))
            {
                var magnitude = e.KeyModifiers.HasFlag(Windows.System.VirtualKeyModifiers.Control) ? 1 : 5;
                ImageRotation -= delta * magnitude;
            }
            else
            {
                LinearImageScale += delta;
                if (prevScaledImageSourceRect.Contains(point.Position))
                {
                    var boundingBox = _scaledImageSourceQuadrilateral.BoundingBox;
                    var widthDelta = boundingBox.Width - prevScaledImageSourceRect.Width;
                    var heightDelta = boundingBox.Height - prevScaledImageSourceRect.Height;
                    var pointerOffsetX = point.Position.X - prevScaledImageSourceRect.GetCenterPoint().X;
                    var pointerOffsetY = point.Position.Y - prevScaledImageSourceRect.GetCenterPoint().Y;

                    ImageTranslationX -= (widthDelta * (pointerOffsetX / (prevScaledImageSourceRect.Width / 2))) / 2;
                    ImageTranslationY -= (heightDelta * (pointerOffsetY / (prevScaledImageSourceRect.Height / 2))) / 2;
                }
            }
        }
        #endregion

        #region Private Methods
        private void UpdateTransform()
        {
            // Create transform matrix
            var offsetX = (SwapChainPanel.ActualWidth - _imageSourceRect.Width * ImageScale) / 2.0;
            var offsetY = (SwapChainPanel.ActualHeight - _imageSourceRect.Height * ImageScale) / 2.0;
            var translation = Matrix3x2.CreateTranslation((float)(ImageTranslationX + offsetX), (float)(ImageTranslationY + offsetY));
            var rotation = Matrix3x2.CreateRotation((float)(ImageRotation * Math.PI / 180.0), _imageSourceRect.GetCenterPoint().ToVector2());
            var scale = Matrix3x2.CreateScale((float)ImageScale);
            var transform = rotation * scale * translation;

            // Get bounding rectangle for the transformed source image
            var topLeft = Vector2.Transform(new Vector2((float)_imageSourceRect.Left, (float)_imageSourceRect.Top), transform);
            var topRight = Vector2.Transform(new Vector2((float)_imageSourceRect.Right, (float)_imageSourceRect.Top), transform);
            var bottomLeft = Vector2.Transform(new Vector2((float)_imageSourceRect.Left, (float)_imageSourceRect.Bottom), transform);
            var bottomRight = Vector2.Transform(new Vector2((float)_imageSourceRect.Right, (float)_imageSourceRect.Bottom), transform);
            _scaledImageSourceQuadrilateral = new Quadrilateral(topLeft, topRight, bottomRight, bottomLeft);

            lock (_transformLock)
            {
                _transform = transform;
            }
        }

        private double GetFitScale()
        {
            if (SwapChainPanel is null ||
                _imageSourceRect.IsEmpty ||
                _imageSourceRect.IsZero ||
                _imageSourceRect.Width <= 0 ||
                _imageSourceRect.Height <= 0)
                return 1.0;

            return Math.Min(SwapChainPanel.ActualWidth / _imageSourceRect.Width,
                            SwapChainPanel.ActualHeight / _imageSourceRect.Height);
        }

        private void EnsureSwapChainDpi()
        {
            var dpi = PInvoke.GetDpiForWindow((HWND)App.WindowHandle);
            if (dpi == 0)
                throw new InvalidOperationException("Unable to determine display DPI");
            WindowDpi = dpi;

            if (SwapChainPanel is not null && (SwapChainPanel.SwapChain is null || Math.Abs(SwapChainPanel.SwapChain.Dpi - WindowDpi) > 0.1))
            {
                //Debug.WriteLine($"DPI CHANGED ({(SwapChainPanel.SwapChain == null ? "XX" : SwapChainPanel.SwapChain.Dpi):0} --> {WindowDpi:0})");
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

        private void RegisterMessages()
        {
            var services = App.Current.Services
                ?? throw new InvalidOperationException("Services not configured");
            var messenger = services.GetService<IMessenger>()
                ?? throw new InvalidOperationException("IMessenger service is not registered");

            messenger.Register<ValueChangedMessage<(int dpiX, int dpiY)>>(this, (r, m) =>
            {
                WindowDpi = m.Value.dpiX;
            });

            messenger.Register<ValueChangedMessage<double?>>(this, (r, m) =>
            {
                if (m.Value is null)
                    return;

                WindowRefreshRate = (double)m.Value;
            });

            messenger.Register<PropertyChangedMessage<ViewModelElement>>(this, async (r, m) =>
            {
                if (m.Sender != ViewModel || m.PropertyName != nameof(ViewModel.ActiveElement))
                    return;

                if (m.NewValue is null)
                {
                    _mediaIndex = 0;
                    _mediaTotal = 0;
                    ((ImagePresenterControl)r).ClearImage();
                }
                else if (m.NewValue is ImageFile imageFile)
                {
                    await ((ImagePresenterControl)r).DisplayImageFileAsync(imageFile);

                    _mediaIndex = imageFile.Parent.Children.IndexOf(imageFile);
                    _mediaTotal = imageFile.Parent.Children.Count;

                    var prevIndex = 0;
                    if (m.OldValue is ImageFile prevImage && prevImage.Parent == imageFile.Parent)
                        prevIndex = prevImage.Parent.Children.IndexOf(prevImage);

                    if (!_isPreCaching && (prevIndex == 0 || prevIndex < _mediaIndex))
                    {
                        var proceed = false;
                        var startIndex = _mediaIndex + 1;
                        for (var i = startIndex; i < startIndex + AutoCacheThreshold && i < _mediaTotal; i++)
                        {
                            if (imageFile.Parent.Children[i] is ImageFile nextImage && !nextImage.IsCached)
                            {
                                proceed = true;
                                startIndex = i;
                                break;
                            }
                        }

                        if (proceed)
                        {
                            _isPreCaching = true;
                            for (var i = startIndex; i < startIndex + AutoCacheThreshold && i < _mediaTotal; i++)
                            {
                                if (imageFile.Parent.Children[i] is ImageFile nextImage && !nextImage.IsCached)
                                {
                                    await _bitmapCache.GetOrLoadImageAsync(SwapChainPanel.SwapChain.Device, nextImage);
                                }
                            }
                            _isPreCaching = false;
                        }
                    }
                }
            });

            messenger.Register<RemoveImageFromCacheMessage>(this, (r, m) =>
            {
                ((ImagePresenterControl)r)._bitmapCache.RemoveImage(m.Path);
            });

            messenger.Register<ImageTransformRequestMessage>(this, (r, m) =>
            {
                var translationX = ((ImagePresenterControl)r).ImageTranslationX;
                var translationY = ((ImagePresenterControl)r).ImageTranslationY;
                var rotation = ((ImagePresenterControl)r).ImageRotation;
                var scale = ((ImagePresenterControl)r).RelativeImageScale;
                m.Reply((translationX, translationY, rotation, scale));
            });
        }
        #endregion
    }
}