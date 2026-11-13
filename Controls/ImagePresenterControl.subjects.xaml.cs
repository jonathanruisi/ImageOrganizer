using ImageOrganizer.ViewModel;

using JLR.Utility.WinUI;

using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Brushes;
using Microsoft.UI;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;

using Windows.Foundation;
using Windows.System;
using Windows.UI;

namespace ImageOrganizer.Controls
{
    /// <summary>
    /// A person detected in the image displayed by an <see cref="ImagePresenterControl"/>.
    /// </summary>
    public sealed class DetectedSubject
    {
        #region Properties
        /// <summary>
        /// Gets the bounding box of the person, in pixels of the source image file.
        /// </summary>
        public Rect Bounds { get; }

        /// <summary>
        /// Gets the confidence of the detection, in the range [0, 1].
        /// </summary>
        public float Confidence { get; }

        public bool IsSelected { get; internal set; }

        /// <summary>
        /// Gets the bounding box of the person relative to the displayed (cropped) image,
        /// which is the coordinate space that a layer's transform maps onto the screen.
        /// </summary>
        internal Rect LayerBounds { get; }

        /// <summary>
        /// Gets the bounding box of the person as fractions of the width and height of the source
        /// image file, which allows it to be compared with subjects in images of other sizes.
        /// </summary>
        internal Rect RelativeBounds { get; }

        /// <summary>
        /// Gets or sets the clockwise angle, in degrees, by which the person is
        /// rotated away from upright, or null if that is not known or could not be told.
        /// </summary>
        internal double? Tilt { get; set; }

        internal bool IsTiltEstimated { get; set; }

        /// <summary>
        /// Gets or sets what the person looks like, which is what they are recognized by in another image.
        /// This is null until it has been asked for, and yields null if it could not be worked out.
        /// </summary>
        internal Task<float[]?>? Appearance { get; set; }
        #endregion

        #region Constructor
        internal DetectedSubject(Rect bounds, Rect layerBounds, Rect relativeBounds, float confidence)
        {
            Bounds = bounds;
            LayerBounds = layerBounds;
            RelativeBounds = relativeBounds;
            Confidence = confidence;
        }
        #endregion
    }

    public sealed partial class ImagePresenterControl
    {
        #region Constants
        // The furthest the pointer can move, in DIPs, between being pressed
        // and released for the gesture to be treated as a click rather than a drag
        private const double SubjectClickTolerance = 4.0;

        // A detection too uncertain to be offered for selection is still accepted,
        // down to this confidence, as a subject whose selection is being carried over
        private const float SubjectTrackingConfidence = 0.2f;

        // The fraction of the render area that framed subjects span, in whichever dimension is the tighter fit
        private const double SubjectFramingFill = 0.85;
        #endregion

        #region Fields
        private readonly List<DetectedSubject> _subjects = [];
        private readonly SubjectSelectionMemory _subjectMemory = new();
        private HumanSubjectDetector? _subjectDetector;
        private HumanPoseEstimator? _subjectPoseEstimator;
        private HumanAppearanceEncoder? _subjectAppearanceEncoder;
        private DetectedSubject? _hoveredSubject, _pressedSubject;
        private Point _subjectPressPosition;
        private ICanvasBrush? _subjectBrush, _selectedSubjectBrush;
        private int _subjectDetectionId, _subjectFramingId;
        private bool _isDetectingSubjects, _subjectDetectionFailed, _isSubjectViewDeferred;
        #endregion

        #region Events
        /// <summary>
        /// Occurs when a subject is selected or deselected, including when
        /// the selection is lost because the subjects themselves were discarded.
        /// </summary>
        public event EventHandler? SubjectSelectionChanged;
        #endregion

        #region Properties
        /// <summary>
        /// Gets the people detected in the image of the active layer.
        /// This is empty unless <see cref="EnableSubjectSelectionMode"/> is set.
        /// </summary>
        public IReadOnlyList<DetectedSubject> Subjects => _subjects.AsReadOnly();

        /// <summary>
        /// Gets the detected people that are currently selected.
        /// </summary>
        public IReadOnlyList<DetectedSubject> SelectedSubjects => _subjects.Where(s => s.IsSelected).ToList();

        /// <summary>
        /// Gets the number of people who were selected in an earlier image and are not in the one being displayed.
        /// While <see cref="KeepSubjectSelection"/> is set, they are selected again in whichever image they reappear in.
        /// </summary>
        public int AbsentSubjectCount => _subjectMemory.AbsentCount;
        #endregion

        #region Public Methods
        /// <summary>
        /// Forgets the people who were selected in an earlier image and are not in the
        /// one being displayed, so that they are not selected again if they reappear.
        /// </summary>
        public void ForgetAbsentSubjects()
        {
            _subjectMemory.ForgetAbsent();
            UpdateSubjectStatus();
        }
        #endregion

        #region Private Methods (Subject Detection)
        /// <summary>
        /// Discards the current subjects and, if subject selection mode is enabled,
        /// starts detecting the subjects in the image of the active layer.
        /// </summary>
        private void RefreshSubjects()
        {
            // Advancing the IDs causes the results of any detection or framing still in progress to be ignored
            var detectionId = ++_subjectDetectionId;
            _subjectFramingId++;
            var hadSelection = _subjects.Any(s => s.IsSelected);

            _subjects.Clear();
            _hoveredSubject = null;
            _pressedSubject = null;
            _isDetectingSubjects = false;
            _subjectDetectionFailed = false;

            if (EnableSubjectSelectionMode && ActiveLayerInstance is { ImageFile: { Bitmap: CanvasBitmap bitmap } imageFile })
            {
                // Fire-and-forget by design: detection must not delay display of the image,
                // and the method handles its own errors and discards superseded results.
                _ = DetectSubjectsAsync(detectionId, imageFile, bitmap);
            }
            else
            {
                // No subjects are on their way, so a view that was framing some has nothing left to frame
                UpdateSubjectFraming(restoreView: hadSelection && FrameSelectedSubjects);
            }

            UpdateSubjectStatus();
            if (hadSelection)
                SubjectSelectionChanged?.Invoke(this, EventArgs.Empty);
        }

        private async Task DetectSubjectsAsync(int detectionId, ImageFile imageFile, CanvasBitmap bitmap)
        {
            _isDetectingSubjects = true;

            List<DetectedSubject>? candidates = null;
            SubjectRecognition? recognition = null;
            var confidenceThreshold = 0f;
            try
            {
                // Subjects whose selection is being carried over are looked for harder than new ones
                var detector = _subjectDetector ??= new HumanSubjectDetector();
                confidenceThreshold = detector.ConfidenceThreshold;
                var detections = await detector.DetectAsync(bitmap, _subjectMemory.HasSelection ? SubjectTrackingConfidence : null);

                // The bitmap holds the part of the source image inside the bounding rectangle,
                // so offsetting a detection by the rectangle's origin locates it in the source image
                var origin = imageFile.BoundingRect;
                var sourceWidth = imageFile.OriginalSize.Width > 0 ? imageFile.OriginalSize.Width : origin.Right;
                var sourceHeight = imageFile.OriginalSize.Height > 0 ? imageFile.OriginalSize.Height : origin.Bottom;
                candidates = [];
                foreach (var detection in detections)
                {
                    var bounds = new Rect(origin.X + detection.Bounds.X, origin.Y + detection.Bounds.Y, detection.Bounds.Width, detection.Bounds.Height);
                    candidates.Add(new DetectedSubject(bounds,
                                                       detection.Bounds,
                                                       new Rect(bounds.X / sourceWidth, bounds.Y / sourceHeight, bounds.Width / sourceWidth, bounds.Height / sourceHeight),
                                                       detection.Confidence));
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Subject detection failed: {ex}");
            }

            // People who were selected in earlier images are selected in this one if they are recognized in it
            if (candidates is not null && detectionId == _subjectDetectionId && _subjectMemory.HasSelection)
                recognition = await RecognizeSubjectsAsync(bitmap, candidates);

            if (detectionId != _subjectDetectionId)
                return;

            _isDetectingSubjects = false;
            if (candidates is null)
            {
                _subjectDetectionFailed = true;
            }
            else
            {
                // Selection may have stopped being carried over while the people were being recognized
                if (!_subjectMemory.HasSelection)
                    recognition = null;

                if (recognition is not null)
                    _subjectMemory.Accept(recognition);

                for (var i = 0; i < candidates.Count; i++)
                {
                    candidates[i].IsSelected = recognition?.Selected.Contains(i) == true;
                    if (candidates[i].IsSelected || candidates[i].Confidence >= confidenceThreshold)
                        _subjects.Add(candidates[i]);
                }

                UpdateTrackedSubjects();
            }

            UpdateSubjectFraming(restoreView: false);
            UpdateSubjectStatus();
            if (_subjects.Any(s => s.IsSelected))
                SubjectSelectionChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Looks, among the subjects detected in an image, for the people who were selected in
        /// earlier images. They are recognized by what they look like, wherever they are.
        /// </summary>
        private async Task<SubjectRecognition> RecognizeSubjectsAsync(CanvasBitmap bitmap, List<DetectedSubject> candidates)
        {
            // If what the subjects look like cannot be worked out, then nobody is recognized among them
            var people = new List<SubjectSignature>();
            if (candidates.Count > 0 &&
                await EncodeSubjectsAsync(bitmap, candidates.Select(c => c.LayerBounds).ToList()) is float[][] appearances)
            {
                for (var i = 0; i < candidates.Count; i++)
                {
                    candidates[i].Appearance = Task.FromResult<float[]?>(appearances[i]);
                    people.Add(new SubjectSignature(appearances[i], candidates[i].RelativeBounds));
                }
            }

            return await _subjectMemory.RecognizeAsync(people, (float)SubjectMatchSimilarity);
        }

        /// <summary>
        /// Works out what subjects look like. The bitmap is only accessed before this method first yields.
        /// </summary>
        /// <returns>
        /// The appearance of each subject, or null if their appearances could not be worked out
        /// </returns>
        private async Task<float[][]?> EncodeSubjectsAsync(CanvasBitmap bitmap, IReadOnlyList<Rect> bounds)
        {
            try
            {
                var encoder = _subjectAppearanceEncoder ??= new HumanAppearanceEncoder();
                return await encoder.EncodeAsync(bitmap, bounds);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Subject appearance encoding failed: {ex}");
                return null;
            }
        }

        private void UpdateSubjectStatus()
        {
            var absentCount = _subjectMemory.AbsentCount;
            ButtonForgetAbsentSubjects.IsEnabled = absentCount > 0;

            if (!EnableSubjectSelectionMode || ActiveLayerInstance?.ImageFile?.Bitmap is null)
                SubjectStatusText = string.Empty;
            else if (_isDetectingSubjects)
                SubjectStatusText = "Detecting people…";
            else if (_subjectDetectionFailed)
                SubjectStatusText = "People detection is unavailable";
            else if (_subjects.Count == 0)
                SubjectStatusText = "No people found";
            else
                SubjectStatusText = $"{_subjects.Count(s => s.IsSelected)} of {_subjects.Count} selected";

            if (absentCount > 0 && !_isDetectingSubjects && SubjectStatusText.Length > 0)
                SubjectStatusText += $" ({absentCount} selected {(absentCount == 1 ? "person" : "people")} not in this image)";
        }
        #endregion

        #region Private Methods (Subject Selection)
        /// <summary>
        /// Finds the subject at a position within the control. Where subjects overlap,
        /// the smallest one is chosen so that a subject enclosed by another remains reachable.
        /// </summary>
        private DetectedSubject? GetSubjectAt(ImagePresenterLayer layer, Point position)
        {
            if (!EnableSubjectSelectionMode ||
                _subjects.Count == 0 ||
                !Matrix3x2.Invert(layer.Transform, out var inverseTransform))
                return null;

            var layerPosition = Vector2.Transform(position.ToVector2(), inverseTransform).ToPoint();
            DetectedSubject? result = null;
            foreach (var subject in _subjects)
            {
                if (subject.LayerBounds.Contains(layerPosition) &&
                    (result is null || GetArea(subject) < GetArea(result)))
                    result = subject;
            }

            return result;

            static double GetArea(DetectedSubject subject) => subject.LayerBounds.Width * subject.LayerBounds.Height;
        }

        private void UpdateHoveredSubject(ImagePresenterLayer layer, Point position)
        {
            _hoveredSubject = GetSubjectAt(layer, position);
        }

        /// <summary>
        /// Notes the subject under the pointer when the left button is pressed, so that releasing
        /// the button without dragging toggles its selection. The image can still be dragged
        /// by a subject, and Control-click remains reserved for centering the image.
        /// </summary>
        private void BeginSubjectClick(ImagePresenterLayer layer, Point position, VirtualKeyModifiers keyModifiers)
        {
            _pressedSubject = keyModifiers.HasFlag(VirtualKeyModifiers.Control)
                ? null
                : GetSubjectAt(layer, position);
            _subjectPressPosition = position;
        }

        private void CompleteSubjectClick(ImagePresenterLayer layer, Point position)
        {
            if (_pressedSubject is not { } subject)
                return;

            _pressedSubject = null;
            if (Math.Abs(position.X - _subjectPressPosition.X) > SubjectClickTolerance ||
                Math.Abs(position.Y - _subjectPressPosition.Y) > SubjectClickTolerance ||
                !ReferenceEquals(GetSubjectAt(layer, position), subject))
                return;

            subject.IsSelected = !subject.IsSelected;
            UpdateTrackedSubjects();
            if (FrameSelectedSubjects)
                UpdateSubjectFraming(restoreView: true);

            UpdateSubjectStatus();
            SubjectSelectionChanged?.Invoke(this, EventArgs.Empty);
        }
        #endregion

        #region Private Methods (Subject Tracking and Framing)
        /// <summary>
        /// Gets whether the view is framing subjects that will be looked for in the next image displayed.
        /// </summary>
        private bool IsFramingTrackedSubjects => EnableSubjectSelectionMode && FrameSelectedSubjects && _subjectMemory.IsSelectionPresent;

        /// <summary>
        /// Notes who is in the image being displayed and what they look like,
        /// so that the selected people can be recognized in the images displayed after it.
        /// </summary>
        private void UpdateTrackedSubjects()
        {
            if (!EnableSubjectSelectionMode || !KeepSubjectSelection)
            {
                _subjectMemory.Clear();
                return;
            }

            // Unless someone is selected, in this image or an earlier one, there is nobody to be recognized later
            var people = new List<RememberedSubject>();
            if ((_subjects.Any(s => s.IsSelected) || _subjectMemory.AbsentCount > 0) &&
                ActiveLayerInstance?.ImageFile?.Bitmap is CanvasBitmap bitmap)
            {
                // Their appearance has to be captured here, while their image is still the one displayed.
                // The subjects that are not selected are needed too, to tell them from the ones that are.
                var newSubjects = _subjects.Where(s => s.Appearance is null).ToList();
                if (newSubjects.Count > 0)
                {
                    var encoding = EncodeSubjectsAsync(bitmap, newSubjects.Select(s => s.LayerBounds).ToList());
                    for (var i = 0; i < newSubjects.Count; i++)
                        newSubjects[i].Appearance = GetAppearanceAsync(encoding, i);
                }

                foreach (var subject in _subjects)
                {
                    if (subject.Appearance is { } appearance)
                        people.Add(new RememberedSubject(appearance, subject.RelativeBounds, subject.IsSelected));
                }
            }

            _subjectMemory.SetPresent(people);

            static async Task<float[]?> GetAppearanceAsync(Task<float[][]?> encoding, int index)
            {
                return (await encoding)?[index];
            }
        }

        /// <summary>
        /// Brings the view into line with the selection. If framing is enabled and subjects are selected,
        /// they are framed. Otherwise, if the view had been given over to framing, the image's own view is restored.
        /// </summary>
        /// <param name="restoreView">Whether the view is known to have been framing subjects</param>
        private void UpdateSubjectFraming(bool restoreView)
        {
            restoreView |= _isSubjectViewDeferred;
            _isSubjectViewDeferred = false;

            if (EnableSubjectSelectionMode && FrameSelectedSubjects && _subjects.Any(s => s.IsSelected))
            {
                // Fire-and-forget by design: the method handles its own errors and gives way to later requests
                _ = FrameSelectedSubjectsAsync();
            }
            else if (restoreView)
            {
                _subjectFramingId++;
                ApplyTransformPropertiesFromLayer(ActiveLayer);
            }
        }

        /// <summary>
        /// Zooms in on the selected subjects, and rotates the image so that they are upright.
        /// </summary>
        private async Task FrameSelectedSubjectsAsync()
        {
            var framingId = ++_subjectFramingId;
            if (ActiveLayerInstance is not { ImageFile: { Bitmap: CanvasBitmap bitmap } imageFile })
                return;

            var selectedSubjects = _subjects.Where(s => s.IsSelected).ToList();
            foreach (var subject in selectedSubjects.Where(s => !s.IsTiltEstimated))
            {
                double? tilt = null;
                try
                {
                    var poseEstimator = _subjectPoseEstimator ??= new HumanPoseEstimator();
                    tilt = await poseEstimator.EstimateTiltAsync(bitmap, subject.LayerBounds);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Subject pose estimation failed: {ex}");
                }

                if (framingId != _subjectFramingId)
                    return;

                subject.Tilt = tilt;
                subject.IsTiltEstimated = true;
            }

            // A subject whose tilt could not be told has no say in the rotation, but is still kept in view
            var frame = SubjectFraming.GetFrame(selectedSubjects.Select(s => s.LayerBounds),
                                                selectedSubjects.Where(s => s.Tilt.HasValue).Select(s => s.Tilt.GetValueOrDefault()),
                                                new Size(imageFile.BoundingRect.Width, imageFile.BoundingRect.Height),
                                                new Size(SwapChainPanel.ActualWidth, SwapChainPanel.ActualHeight),
                                                SubjectFramingFill);
            if (frame is not { } view)
                return;

            ImageRotation = view.Rotation;
            ImageScale = view.Scale;
            ImageTranslationX = view.TranslationX;
            ImageTranslationY = view.TranslationY;
        }
        #endregion

        #region Private Methods (Subject Rendering)
        private void EnsureSubjectBrushes(bool recreate = false)
        {
            if (SwapChainPanel.SwapChain is null)
                return;

            if (recreate)
            {
                _subjectBrush?.Dispose();
                _subjectBrush = null;
                _selectedSubjectBrush?.Dispose();
                _selectedSubjectBrush = null;
            }

            var device = SwapChainPanel.SwapChain.Device;
            _subjectBrush ??= SubjectBrush?.CreateCanvasBrush(device)
                ?? new CanvasSolidColorBrush(device, Colors.White);
            _selectedSubjectBrush ??= SelectedSubjectBrush?.CreateCanvasBrush(device)
                ?? new CanvasSolidColorBrush(device, Colors.DodgerBlue);
        }

        private void DrawSubjects(CanvasDrawingSession ds, ImagePresenterLayer layer)
        {
            if (!EnableSubjectSelectionMode || _subjects.Count == 0 || ImageScale <= 0)
                return;

            EnsureSubjectBrushes();
            if (_subjectBrush is null || _selectedSubjectBrush is null)
                return;

            // Drawing in the image's coordinate space keeps the boxes attached to the image however
            // it is moved, scaled or rotated. Stroke widths are divided by the scale to cancel it out.
            var thickness = (float)(SubjectOutlineThickness / ImageScale);
            var contrastThickness = thickness + (float)(2 / ImageScale);
            var contrastColor = Color.FromArgb(128, 0, 0, 0);

            ds.Transform = layer.Transform;
            foreach (var subject in _subjects)
            {
                var brush = subject.IsSelected ? _selectedSubjectBrush : _subjectBrush;
                var isHovered = ReferenceEquals(subject, _hoveredSubject);

                if (subject.IsSelected || isHovered)
                {
                    brush.Opacity = subject.IsSelected && isHovered ? 0.4f
                                  : subject.IsSelected ? 0.3f
                                  : 0.15f;
                    ds.FillRectangle(subject.LayerBounds, brush);
                    brush.Opacity = 1f;
                }

                // A dark edge keeps the outline visible against areas of the image that match the brush
                ds.DrawRectangle(subject.LayerBounds, contrastColor, contrastThickness);
                ds.DrawRectangle(subject.LayerBounds, brush, thickness);
            }
            ds.Transform = Matrix3x2.Identity;
        }
        #endregion
    }
}
