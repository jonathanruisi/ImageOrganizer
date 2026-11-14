using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

using System.Collections.ObjectModel;

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

        public bool IsAutoAdvanceActive
        {
            get => (bool)GetValue(IsAutoAdvanceActiveProperty);
            set => SetValue(IsAutoAdvanceActiveProperty, value);
        }

        public static readonly DependencyProperty IsAutoAdvanceActiveProperty =
            DependencyProperty.Register(nameof(IsAutoAdvanceActive),
                                        typeof(bool),
                                        typeof(ImagePresenterControl),
                                        new PropertyMetadata(false, OnIsAutoAdvanceActiveChanged));

        public int AutoAdvanceRate
        {
            get => (int)GetValue(AutoAdvanceRateProperty);
            set => SetValue(AutoAdvanceRateProperty, value);
        }

        public static readonly DependencyProperty AutoAdvanceRateProperty =
            DependencyProperty.Register(nameof(AutoAdvanceRate),
                                        typeof(int),
                                        typeof(ImagePresenterControl),
                                        new PropertyMetadata(500, OnAutoAdvanceRateChanged));

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
        public int ActiveLayer
        {
            get => (int)GetValue(ActiveLayerProperty);
            set => SetValue(ActiveLayerProperty, value);
        }

        public static readonly DependencyProperty ActiveLayerProperty =
            DependencyProperty.Register(nameof(ActiveLayer),
                                        typeof(int),
                                        typeof(ImagePresenterControl),
                                        new PropertyMetadata(0, OnActiveLayerChanged));

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

        /// <summary>
        /// Gets or sets whether the people in the displayed image are detected and can be selected.
        /// </summary>
        /// <remarks>Subject selection mode and crop mode are mutually exclusive.</remarks>
        public bool EnableSubjectSelectionMode
        {
            get => (bool)GetValue(EnableSubjectSelectionModeProperty);
            set => SetValue(EnableSubjectSelectionModeProperty, value);
        }

        public static readonly DependencyProperty EnableSubjectSelectionModeProperty =
            DependencyProperty.Register(nameof(EnableSubjectSelectionMode),
                                        typeof(bool),
                                        typeof(ImagePresenterControl),
                                        new PropertyMetadata(false, OnEnableSubjectSelectionModeChanged));

        /// <summary>
        /// Gets or sets whether selected subjects remain selected when another image is displayed.
        /// </summary>
        /// <remarks>
        /// A subject remains selected if a person who looks the same, going by
        /// their clothing and build, is detected anywhere in the new image.
        /// A selected person who is absent from an image is remembered, and selected again in whichever
        /// later image they reappear in, unless forgotten with <see cref="ForgetAbsentSubjects"/>.
        /// Clearing this property forgets them too.
        /// </remarks>
        public bool KeepSubjectSelection
        {
            get => (bool)GetValue(KeepSubjectSelectionProperty);
            set => SetValue(KeepSubjectSelectionProperty, value);
        }

        public static readonly DependencyProperty KeepSubjectSelectionProperty =
            DependencyProperty.Register(nameof(KeepSubjectSelection),
                                        typeof(bool),
                                        typeof(ImagePresenterControl),
                                        new PropertyMetadata(false, OnKeepSubjectSelectionChanged));

        /// <summary>
        /// Gets or sets how alike a person must look to a subject of the previous image, on a scale that
        /// reaches 1 for an identical appearance, for them to be taken as the same person when the selection
        /// is carried over. See <see cref="KeepSubjectSelection"/>.
        /// </summary>
        /// <remarks>
        /// A higher value makes it less likely that a selection passes to someone who looks similar, such as
        /// a person in the same uniform, and more likely that it is lost when a person's appearance changes,
        /// such as by their turning around. Useful values lie between about 0.4 and 0.7.
        /// </remarks>
        public double SubjectMatchSimilarity
        {
            get => (double)GetValue(SubjectMatchSimilarityProperty);
            set => SetValue(SubjectMatchSimilarityProperty, value);
        }

        public static readonly DependencyProperty SubjectMatchSimilarityProperty =
            DependencyProperty.Register(nameof(SubjectMatchSimilarity),
                                        typeof(double),
                                        typeof(ImagePresenterControl),
                                        new PropertyMetadata(0.55));

        /// <summary>
        /// Gets or sets whether the view zooms in on the selected subjects
        /// and rotates the image so that they appear upright.
        /// </summary>
        public bool FrameSelectedSubjects
        {
            get => (bool)GetValue(FrameSelectedSubjectsProperty);
            set => SetValue(FrameSelectedSubjectsProperty, value);
        }

        public static readonly DependencyProperty FrameSelectedSubjectsProperty =
            DependencyProperty.Register(nameof(FrameSelectedSubjects),
                                        typeof(bool),
                                        typeof(ImagePresenterControl),
                                        new PropertyMetadata(false, OnFrameSelectedSubjectsChanged));

        /// <summary>
        /// Gets or sets whether dragging across the image draws a level line: a line marking what ought
        /// to be level, such as the ground under a subject's feet. The image is then rotated to make it so.
        /// </summary>
        /// <remarks>
        /// This is cleared once a line has been drawn. Setting it also sets <see cref="EnableSubjectSelectionMode"/>.
        /// A level line takes the place of the subjects' pose in deciding the rotation when subjects are framed.
        /// While <see cref="KeepSubjectSelection"/> is set, the line is looked for in each image displayed afterwards,
        /// by how the scene has moved, until it is discarded with <see cref="ClearLevelLine"/>.
        /// </remarks>
        public bool EnableLevelLineDrawing
        {
            get => (bool)GetValue(EnableLevelLineDrawingProperty);
            set => SetValue(EnableLevelLineDrawingProperty, value);
        }

        public static readonly DependencyProperty EnableLevelLineDrawingProperty =
            DependencyProperty.Register(nameof(EnableLevelLineDrawing),
                                        typeof(bool),
                                        typeof(ImagePresenterControl),
                                        new PropertyMetadata(false, OnEnableLevelLineDrawingChanged));

        /// <summary>
        /// Gets a description of the current state of subject detection and selection.
        /// </summary>
        public string SubjectStatusText
        {
            get => (string)GetValue(SubjectStatusTextProperty);
            private set => SetValue(SubjectStatusTextProperty, value);
        }

        public static readonly DependencyProperty SubjectStatusTextProperty =
            DependencyProperty.Register(nameof(SubjectStatusText),
                                        typeof(string),
                                        typeof(ImagePresenterControl),
                                        new PropertyMetadata(string.Empty));

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

        /// <summary>
        /// Gets or sets the brush used to outline detected subjects. White is used if no brush is set.
        /// </summary>
        public Brush SubjectBrush
        {
            get => (Brush)GetValue(SubjectBrushProperty);
            set => SetValue(SubjectBrushProperty, value);
        }

        public static readonly DependencyProperty SubjectBrushProperty =
            DependencyProperty.Register(nameof(SubjectBrush),
                                        typeof(Brush),
                                        typeof(ImagePresenterControl),
                                        new PropertyMetadata(null, OnCanvasBrushChanged));

        /// <summary>
        /// Gets or sets the brush used to highlight selected subjects. Blue is used if no brush is set.
        /// </summary>
        public Brush SelectedSubjectBrush
        {
            get => (Brush)GetValue(SelectedSubjectBrushProperty);
            set => SetValue(SelectedSubjectBrushProperty, value);
        }

        public static readonly DependencyProperty SelectedSubjectBrushProperty =
            DependencyProperty.Register(nameof(SelectedSubjectBrush),
                                        typeof(Brush),
                                        typeof(ImagePresenterControl),
                                        new PropertyMetadata(null, OnCanvasBrushChanged));

        public double SubjectOutlineThickness
        {
            get => (double)GetValue(SubjectOutlineThicknessProperty);
            set => SetValue(SubjectOutlineThicknessProperty, value);
        }

        public static readonly DependencyProperty SubjectOutlineThicknessProperty =
            DependencyProperty.Register(nameof(SubjectOutlineThickness),
                                        typeof(double),
                                        typeof(ImagePresenterControl),
                                        new PropertyMetadata(3.0));

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

        /// <summary>
        /// Shadow adjustment applied to the displayed image, in the range [-1, 1]. 0 leaves shadows unchanged.
        /// </summary>
        public double ImageShadows
        {
            get => (double)GetValue(ImageShadowsProperty);
            set => SetValue(ImageShadowsProperty, value);
        }

        public static readonly DependencyProperty ImageShadowsProperty =
            DependencyProperty.Register(nameof(ImageShadows),
                                        typeof(double),
                                        typeof(ImagePresenterControl),
                                        new PropertyMetadata(0.0, OnImageToneAdjustmentChanged));

        /// <summary>
        /// Highlight adjustment applied to the displayed image, in the range [-1, 1]. 0 leaves highlights unchanged.
        /// </summary>
        public double ImageHighlights
        {
            get => (double)GetValue(ImageHighlightsProperty);
            set => SetValue(ImageHighlightsProperty, value);
        }

        public static readonly DependencyProperty ImageHighlightsProperty =
            DependencyProperty.Register(nameof(ImageHighlights),
                                        typeof(double),
                                        typeof(ImagePresenterControl),
                                        new PropertyMetadata(0.0, OnImageToneAdjustmentChanged));

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
                                        new PropertyMetadata(75));

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