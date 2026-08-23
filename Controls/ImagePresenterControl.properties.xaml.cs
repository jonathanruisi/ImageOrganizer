using Microsoft.UI.Xaml;

namespace ImageOrganizer.Controls
{
    public sealed partial class ImagePresenterControl
    {
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

        public InteractionMode InteractionMode
        {
            get => (InteractionMode)GetValue(InteractionModeProperty);
            set => SetValue(InteractionModeProperty, value);
        }

        public static readonly DependencyProperty InteractionModeProperty =
            DependencyProperty.Register(nameof(InteractionMode),
                                        typeof(InteractionMode),
                                        typeof(ImagePresenterControl),
                                        new PropertyMetadata(InteractionMode.Transform));
    }
}