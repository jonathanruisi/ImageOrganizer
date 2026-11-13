using Microsoft.Graphics.Canvas;
using Microsoft.ML.OnnxRuntime;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Windows.Foundation;
using Windows.Graphics.DirectX;
using Windows.UI;

namespace ImageOrganizer
{
    /// <summary>
    /// A person located in an image by <see cref="HumanSubjectDetector"/>.
    /// </summary>
    /// <param name="Bounds">Bounding box of the person, in the coordinate space (DIPs) of the analyzed bitmap</param>
    /// <param name="Confidence">Detection confidence in the range [0, 1]</param>
    public readonly record struct SubjectDetection(Rect Bounds, float Confidence);

    /// <summary>
    /// Locates people in images using a YOLOX object detection model, which is run on the CPU
    /// by the ONNX Runtime that ships with Windows ML (part of the Windows App SDK).
    /// </summary>
    public sealed partial class HumanSubjectDetector : IDisposable
    {
        #region Constants
        // Layout of a YOLOX output row: center x, center y, width, height, objectness, then one score per COCO class
        private const int BoxValueCount = 5;
        private const int ClassCount = 80;
        private const int PersonClassIndex = 0;
        private const byte LetterboxFill = 114;
        private static readonly int[] _strides = [8, 16, 32];
        #endregion

        #region Fields
        private readonly Lock _sessionGate = new();
        private InferenceSession? _session;
        private string[] _inputNames = [];
        private string[] _outputNames = [];
        private bool _disposed;
        #endregion

        #region Properties
        /// <summary>
        /// Gets the path of the model that is deployed with the application.
        /// </summary>
        public static string DefaultModelPath => Path.Combine(AppContext.BaseDirectory, "Assets", "Models", "yolox_s.onnx");

        public string ModelPath { get; }

        /// <summary>
        /// Gets the width and height, in pixels, of the square image the model expects.
        /// </summary>
        public int InputSize { get; }

        /// <summary>
        /// Gets or sets the minimum confidence a detection needs in order to be reported.
        /// </summary>
        /// <remarks>
        /// The default leans towards finding partly hidden people at the cost of the
        /// occasional spurious detection, because a missed person cannot be selected.
        /// </remarks>
        public float ConfidenceThreshold { get; set; } = 0.4f;

        /// <summary>
        /// Gets or sets the overlap (intersection over union) above which
        /// the weaker of two detections is treated as a duplicate and discarded.
        /// </summary>
        public float OverlapThreshold { get; set; } = 0.45f;
        #endregion

        #region Constructor
        public HumanSubjectDetector(string? modelPath = null, int inputSize = 640)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(inputSize, _strides[^1]);
            if (inputSize % _strides[^1] != 0)
                throw new ArgumentException("The input size must be a multiple of the largest model stride", nameof(inputSize));

            ModelPath = modelPath ?? DefaultModelPath;
            InputSize = inputSize;
        }
        #endregion

        #region Public Methods
        /// <summary>
        /// Locates the people in a bitmap.
        /// </summary>
        /// <remarks>
        /// The bitmap is only accessed before this method first yields, so the caller
        /// is free to dispose of it while the returned task is still running.
        /// </remarks>
        /// <param name="bitmap">The image to analyze</param>
        /// <param name="confidenceThreshold">
        /// The minimum confidence of the detections to report, or null to use <see cref="ConfidenceThreshold"/>
        /// </param>
        /// <returns>
        /// The people found, ordered from the most to the least confident detection
        /// </returns>
        public async Task<IReadOnlyList<SubjectDetection>> DetectAsync(CanvasBitmap bitmap, float? confidenceThreshold = null)
        {
            var imageSize = bitmap.Size;
            if (imageSize.Width <= 0 || imageSize.Height <= 0)
                return [];

            // The model expects the image scaled to fit a square, anchored at the top-left
            // corner, with the unused area filled with gray. The GPU does the resampling,
            // so only a small bitmap needs to be read back regardless of the source size.
            var scale = InputSize / Math.Max(imageSize.Width, imageSize.Height);
            byte[] pixels;
            using (var target = new CanvasRenderTarget(bitmap.Device,
                                                       InputSize,
                                                       InputSize,
                                                       96f,
                                                       DirectXPixelFormat.B8G8R8A8UIntNormalized,
                                                       CanvasAlphaMode.Ignore))
            {
                using (var ds = target.CreateDrawingSession())
                {
                    ds.Clear(Color.FromArgb(255, LetterboxFill, LetterboxFill, LetterboxFill));
                    ds.DrawImage(bitmap,
                                 new Rect(0, 0, imageSize.Width * scale, imageSize.Height * scale),
                                 new Rect(0, 0, imageSize.Width, imageSize.Height),
                                 1f,
                                 CanvasImageInterpolation.HighQualityCubic);
                }

                pixels = target.GetPixelBytes();
            }

            var threshold = confidenceThreshold ?? ConfidenceThreshold;
            return await Task.Run(() => Detect(pixels, scale, imageSize, threshold));
        }
        #endregion

        #region Private Methods
        private IReadOnlyList<SubjectDetection> Detect(byte[] bgraPixels, double scale, Size imageSize, float confidenceThreshold)
        {
            // The model takes planar BGR values in the range [0, 255]
            var planeLength = InputSize * InputSize;
            var input = new float[3 * planeLength];
            for (var i = 0; i < planeLength; i++)
            {
                var offset = i * 4;
                input[i] = bgraPixels[offset];
                input[planeLength + i] = bgraPixels[offset + 1];
                input[(2 * planeLength) + i] = bgraPixels[offset + 2];
            }

            List<SubjectDetection> candidates;
            lock (_sessionGate)
            {
                if (_disposed)
                    return [];

                var session = EnsureSession();
                using var inputValue = OrtValue.CreateTensorValueFromMemory(input, [1, 3, InputSize, InputSize]);
                using var runOptions = new RunOptions();
                using var outputs = session.Run(runOptions, _inputNames, new[] { inputValue }, _outputNames);
                candidates = Decode(outputs[0].GetTensorDataAsSpan<float>(), InputSize, confidenceThreshold);
            }

            var subjects = new List<SubjectDetection>();
            foreach (var candidate in SuppressOverlaps(candidates, OverlapThreshold))
            {
                // Map the box from the model's input back onto the image
                var left = Math.Clamp(candidate.Bounds.Left / scale, 0, imageSize.Width);
                var top = Math.Clamp(candidate.Bounds.Top / scale, 0, imageSize.Height);
                var right = Math.Clamp(candidate.Bounds.Right / scale, 0, imageSize.Width);
                var bottom = Math.Clamp(candidate.Bounds.Bottom / scale, 0, imageSize.Height);

                if (right - left >= 1 && bottom - top >= 1)
                    subjects.Add(new SubjectDetection(new Rect(left, top, right - left, bottom - top), candidate.Confidence));
            }

            return subjects;
        }

        private InferenceSession EnsureSession()
        {
            if (_session is not null)
                return _session;

            using var options = new SessionOptions();
            var session = new InferenceSession(ModelPath, options);

            try
            {
                var (inputName, inputMetadata) = session.InputMetadata.Single();
                if (!inputMetadata.Dimensions.SequenceEqual([1, 3, InputSize, InputSize]))
                {
                    throw new InvalidOperationException(
                        $"The model expects an input of [{string.Join(", ", inputMetadata.Dimensions)}], not [1, 3, {InputSize}, {InputSize}]");
                }

                _inputNames = [inputName];
                _outputNames = [session.OutputMetadata.Single().Key];
            }
            catch
            {
                session.Dispose();
                throw;
            }

            _session = session;
            return session;
        }

        /// <summary>
        /// Extracts the people from the raw output of a YOLOX model.
        /// </summary>
        /// <param name="output">The model output: one row of box, objectness and class values per prediction</param>
        /// <param name="inputSize">The width and height of the model's input</param>
        /// <param name="confidenceThreshold">The minimum confidence of the predictions to keep</param>
        /// <returns>
        /// The predicted people, in the coordinate space of the model's input
        /// </returns>
        internal static List<SubjectDetection> Decode(ReadOnlySpan<float> output, int inputSize, float confidenceThreshold)
        {
            const int rowLength = BoxValueCount + ClassCount;
            var rowCount = _strides.Sum(stride => (inputSize / stride) * (inputSize / stride));
            if (output.Length != rowCount * rowLength)
                throw new InvalidOperationException($"The model produced {output.Length} values where {rowCount * rowLength} were expected");

            // The model predicts on three grids of decreasing resolution. Box centers are
            // relative to their grid cell and box sizes are logarithmic, both in units of the stride.
            var detections = new List<SubjectDetection>();
            var rowIndex = 0;
            foreach (var stride in _strides)
            {
                var gridSize = inputSize / stride;
                for (var gridY = 0; gridY < gridSize; gridY++)
                {
                    for (var gridX = 0; gridX < gridSize; gridX++)
                    {
                        var row = output.Slice(rowIndex * rowLength, rowLength);
                        rowIndex++;

                        var confidence = row[4] * row[BoxValueCount + PersonClassIndex];
                        if (confidence < confidenceThreshold)
                            continue;

                        var centerX = (row[0] + gridX) * stride;
                        var centerY = (row[1] + gridY) * stride;
                        var width = MathF.Exp(row[2]) * stride;
                        var height = MathF.Exp(row[3]) * stride;
                        detections.Add(new SubjectDetection(new Rect(centerX - (width / 2), centerY - (height / 2), width, height),
                                                            confidence));
                    }
                }
            }

            return detections;
        }

        /// <summary>
        /// Performs non-maximum suppression: where detections overlap by more than
        /// the given threshold, only the most confident one is kept.
        /// </summary>
        internal static List<SubjectDetection> SuppressOverlaps(IEnumerable<SubjectDetection> detections, float overlapThreshold)
        {
            var kept = new List<SubjectDetection>();
            foreach (var detection in detections.OrderByDescending(d => d.Confidence))
            {
                if (kept.TrueForAll(k => GetOverlap(k.Bounds, detection.Bounds) <= overlapThreshold))
                    kept.Add(detection);
            }

            return kept;
        }

        /// <summary>
        /// Calculates how much two rectangles overlap, as the ratio of
        /// the area they share to the area they cover (intersection over union).
        /// </summary>
        internal static double GetOverlap(Rect a, Rect b)
        {
            var width = Math.Min(a.Right, b.Right) - Math.Max(a.Left, b.Left);
            var height = Math.Min(a.Bottom, b.Bottom) - Math.Max(a.Top, b.Top);
            if (width <= 0 || height <= 0)
                return 0;

            var intersection = width * height;
            return intersection / ((a.Width * a.Height) + (b.Width * b.Height) - intersection);
        }
        #endregion

        #region Interface Implementation (IDisposable)
        public void Dispose()
        {
            // Taking the lock makes this wait for any inference that is in progress
            lock (_sessionGate)
            {
                if (_disposed)
                    return;

                _disposed = true;
                _session?.Dispose();
                _session = null;
            }
        }
        #endregion
    }
}
