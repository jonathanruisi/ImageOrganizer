using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

namespace ImageOrganizer.Controls
{
    public sealed partial class ImagePresenterControl
    {
        #region Transform Properties
        /// <summary>
        /// Gets or sets the horizontal translation offset applied to the image, in device-independent units.
        /// </summary>
        public double ImageTranslationX
        {
            get => (double)GetValue(ImageTranslationXProperty);
            set => SetValue(ImageTranslationXProperty, value);
        }

        public static readonly DependencyProperty ImageTranslationXProperty =
            DependencyProperty.Register(nameof(ImageTranslationX),
                                        typeof(double),
                                        typeof(ImagePresenterControl),
                                        new PropertyMetadata(0.0, OnImageTransformChanged));

        /// <summary>
        /// Gets or sets the vertical translation offset applied to the image, in device-independent units.
        /// </summary>
        public double ImageTranslationY
        {
            get => (double)GetValue(ImageTranslationYProperty);
            set => SetValue(ImageTranslationYProperty, value);
        }

        public static readonly DependencyProperty ImageTranslationYProperty =
            DependencyProperty.Register(nameof(ImageTranslationY),
                                        typeof(double),
                                        typeof(ImagePresenterControl),
                                        new PropertyMetadata(0.0, OnImageTransformChanged));

        /// <summary>
        /// Gets or sets the image rotation in degrees.
        /// </summary>
        public double ImageRotation
        {
            get => (double)GetValue(ImageRotationProperty);
            set => SetValue(ImageRotationProperty, value);
        }

        public static readonly DependencyProperty ImageRotationProperty =
            DependencyProperty.Register(nameof(ImageRotation),
                                        typeof(double),
                                        typeof(ImagePresenterControl),
                                        new PropertyMetadata(0.0, OnImageTransformChanged));

        /// <summary>
        /// Gets or sets the scale factor applied to the displayed image.
        /// </summary>
        /// <remarks>A value greater than 1.0 enlarges the image, while a value less than 1.0 reduces its size.
        /// The default scale is typically 1.0, representing the image's original size.</remarks>
        public double ImageScale
        {
            get => (double)GetValue(ImageScaleProperty);
            set => SetValue(ImageScaleProperty, value);
        }

        public static readonly DependencyProperty ImageScaleProperty =
            DependencyProperty.Register(nameof(ImageScale),
                                        typeof(double),
                                        typeof(ImagePresenterControl),
                                        new PropertyMetadata(1.0, OnImageTransformChanged));

        public double RelativeImageScale
        {
            get => (double)GetValue(RelativeImageScaleProperty);
            set => SetValue(RelativeImageScaleProperty, value);
        }

        public static readonly DependencyProperty RelativeImageScaleProperty =
            DependencyProperty.Register(nameof(RelativeImageScale),
                                        typeof(double),
                                        typeof(ImagePresenterControl),
                                        new PropertyMetadata(1.0, OnImageTransformChanged));

        public double LinearImageScale
        {
            get => (double)GetValue(LinearImageScaleProperty);
            set => SetValue(LinearImageScaleProperty, value);
        }

        public static readonly DependencyProperty LinearImageScaleProperty =
            DependencyProperty.Register(nameof(LinearImageScale),
                                        typeof(double),
                                        typeof(ImagePresenterControl),
                                        new PropertyMetadata(1.0, OnImageTransformChanged));
        #endregion

        #region Pointer Manipulation Properties
        public bool AllowManualTranslation
        {
            get => (bool)GetValue(AllowManualTranslationProperty);
            set => SetValue(AllowManualTranslationProperty, value);
        }

        public static readonly DependencyProperty AllowManualTranslationProperty =
            DependencyProperty.Register(nameof(AllowManualTranslation),
                                        typeof(bool),
                                        typeof(ImagePresenterControl),
                                        new PropertyMetadata(false));

        public bool AllowManualScaling
        {
            get => (bool)GetValue(AllowManualScalingProperty);
            set => SetValue(AllowManualScalingProperty, value);
        }

        public static readonly DependencyProperty AllowManualScalingProperty =
            DependencyProperty.Register(nameof(AllowManualScaling),
                                        typeof(bool),
                                        typeof(ImagePresenterControl),
                                        new PropertyMetadata(false));

        public bool AllowManualRotation
        {
            get => (bool)GetValue(AllowManualRotationProperty);
            set => SetValue(AllowManualRotationProperty, value);
        }

        public static readonly DependencyProperty AllowManualRotationProperty =
            DependencyProperty.Register(nameof(AllowManualRotation),
                                        typeof(bool),
                                        typeof(ImagePresenterControl),
                                        new PropertyMetadata(false));

        public bool LockTransform
        {
            get => (bool)GetValue(LockTransformProperty);
            set => SetValue(LockTransformProperty, value);
        }

        public static readonly DependencyProperty LockTransformProperty =
            DependencyProperty.Register(nameof(LockTransform),
                                        typeof(bool),
                                        typeof(ImagePresenterControl),
                                        new PropertyMetadata(false));

        public double SnapDistance
        {
            get => (double)GetValue(SnapDistanceProperty);
            set => SetValue(SnapDistanceProperty, value);
        }

        public static readonly DependencyProperty SnapDistanceProperty =
            DependencyProperty.Register(nameof(SnapDistance),
                                        typeof(double),
                                        typeof(ImagePresenterControl),
                                        new PropertyMetadata(10.0));

        public int RotationRate
        {
            get => (int)GetValue(RotationRateProperty);
            set => SetValue(RotationRateProperty, value);
        }

        public static readonly DependencyProperty RotationRateProperty =
            DependencyProperty.Register(nameof(RotationRate),
                                        typeof(int),
                                        typeof(ImagePresenterControl),
                                        new PropertyMetadata(5));
        #endregion

        #region Tool Properties
        public bool EnableCropMode
        {
            get => (bool)GetValue(EnableCropModeProperty);
            set => SetValue(EnableCropModeProperty, value);
        }

        public static readonly DependencyProperty EnableCropModeProperty =
            DependencyProperty.Register(nameof(EnableCropMode),
                                        typeof(bool),
                                        typeof(ImagePresenterControl),
                                        new PropertyMetadata(false, OnEnableCropModeChanged));

        public bool OverlayPreviousImage
        {
            get => (bool)GetValue(OverlayPreviousImageProperty);
            set => SetValue(OverlayPreviousImageProperty, value);
        }

        public static readonly DependencyProperty OverlayPreviousImageProperty =
            DependencyProperty.Register(nameof(OverlayPreviousImage),
                                        typeof(bool),
                                        typeof(ImagePresenterControl),
                                        new PropertyMetadata(false, OnOverlayPreviousImageChanged));
        #endregion

        #region Visual Properties
        public bool ShowAlignmentGrid
        {
            get => (bool)GetValue(ShowAlignmentGridProperty);
            set => SetValue(ShowAlignmentGridProperty, value);
        }

        public static readonly DependencyProperty ShowAlignmentGridProperty =
            DependencyProperty.Register(nameof(ShowAlignmentGrid),
                                        typeof(bool),
                                        typeof(ImagePresenterControl),
                                        new PropertyMetadata(false));

        public Brush AlignmentGridPrimaryBrush
        {
            get => (Brush)GetValue(AlignmentGridPrimaryBrushProperty);
            set => SetValue(AlignmentGridPrimaryBrushProperty, value);
        }

        public static readonly DependencyProperty AlignmentGridPrimaryBrushProperty =
            DependencyProperty.Register(nameof(AlignmentGridPrimaryBrush),
                                        typeof(Brush),
                                        typeof(ImagePresenterControl),
                                        new PropertyMetadata(null, OnCanvasBrushChanged));

        public Brush AlignmentGridSecondaryBrush
        {
            get => (Brush)GetValue(AlignmentGridSecondaryBrushProperty);
            set => SetValue(AlignmentGridSecondaryBrushProperty, value);
        }

        public static readonly DependencyProperty AlignmentGridSecondaryBrushProperty =
            DependencyProperty.Register(nameof(AlignmentGridSecondaryBrush),
                                        typeof(Brush),
                                        typeof(ImagePresenterControl),
                                        new PropertyMetadata(null, OnCanvasBrushChanged));

        public double AlignmentGridThickness
        {
            get => (double)GetValue(AlignmentGridThicknessProperty);
            set => SetValue(AlignmentGridThicknessProperty, value);
        }

        public static readonly DependencyProperty AlignmentGridThicknessProperty =
            DependencyProperty.Register(nameof(AlignmentGridThickness),
                                        typeof(double),
                                        typeof(ImagePresenterControl),
                                        new PropertyMetadata(3.0));

        public Brush CropRectangleBrush
        {
            get => (Brush)GetValue(CropRectangleBrushProperty);
            set => SetValue(CropRectangleBrushProperty, value);
        }

        public static readonly DependencyProperty CropRectangleBrushProperty =
            DependencyProperty.Register(nameof(CropRectangleBrush),
                                        typeof(Brush),
                                        typeof(ImagePresenterControl),
                                        new PropertyMetadata(null, OnCanvasBrushChanged));

        public double CropRectangleThickness
        {
            get => (double)GetValue(CropRectangleThicknessProperty);
            set => SetValue(CropRectangleThicknessProperty, value);
        }

        public static readonly DependencyProperty CropRectangleThicknessProperty =
            DependencyProperty.Register(nameof(CropRectangleThickness),
                                        typeof(double),
                                        typeof(ImagePresenterControl),
                                        new PropertyMetadata(5.0));

        public float MaskOpacity
        {
            get => (float)GetValue(MaskOpacityProperty);
            set => SetValue(MaskOpacityProperty, value);
        }

        public static readonly DependencyProperty MaskOpacityProperty =
            DependencyProperty.Register(nameof(MaskOpacity),
                                        typeof(float),
                                        typeof(ImagePresenterControl),
                                        new PropertyMetadata(0.5f));

        public Visibility UpperToolbarVisibility
        {
            get => (Visibility)GetValue(UpperToolbarVisibilityProperty);
            set => SetValue(UpperToolbarVisibilityProperty, value);
        }

        public static readonly DependencyProperty UpperToolbarVisibilityProperty =
            DependencyProperty.Register(nameof(UpperToolbarVisibility),
                                        typeof(Visibility),
                                        typeof(ImagePresenterControl),
                                        new PropertyMetadata(Visibility.Visible));

        public Visibility LowerToolbarVisibility
        {
            get => (Visibility)GetValue(LowerToolbarVisibilityProperty);
            set => SetValue(LowerToolbarVisibilityProperty, value);
        }

        public static readonly DependencyProperty LowerToolbarVisibilityProperty =
            DependencyProperty.Register(nameof(LowerToolbarVisibility),
                                        typeof(Visibility),
                                        typeof(ImagePresenterControl),
                                        new PropertyMetadata(Visibility.Visible));
        #endregion

        #region Performance Properties
        public int CacheCapacity
        {
            get => (int)GetValue(CacheCapacityProperty);
            set => SetValue(CacheCapacityProperty, value);
        }

        public static readonly DependencyProperty CacheCapacityProperty =
            DependencyProperty.Register(nameof(CacheCapacity),
                                        typeof(int),
                                        typeof(ImagePresenterControl),
                                        new PropertyMetadata(500, OnCacheCapacityChanged));

        public int AutoCacheThreshold
        {
            get => (int)GetValue(AutoCacheThresholdProperty);
            set => SetValue(AutoCacheThresholdProperty, value);
        }

        public static readonly DependencyProperty AutoCacheThresholdProperty =
            DependencyProperty.Register(nameof(AutoCacheThreshold),
                                        typeof(int),
                                        typeof(ImagePresenterControl),
                                        new PropertyMetadata(25));

        public double WindowDpi
        {
            get => (double)GetValue(WindowDpiProperty);
            set => SetValue(WindowDpiProperty, value);
        }

        public static readonly DependencyProperty WindowDpiProperty =
            DependencyProperty.Register(nameof(WindowDpi),
                                        typeof(double),
                                        typeof(ImagePresenterControl),
                                        new PropertyMetadata(0.0, OnWindowDpiChanged));

        public double WindowRefreshRate
        {
            get => (double)GetValue(WindowRefreshRateProperty);
            set => SetValue(WindowRefreshRateProperty, value);
        }

        public static readonly DependencyProperty WindowRefreshRateProperty =
            DependencyProperty.Register(nameof(WindowRefreshRate),
                                        typeof(double),
                                        typeof(ImagePresenterControl),
                                        new PropertyMetadata(0.0, OnWindowRefreshRateChanged));
        #endregion
    }
}