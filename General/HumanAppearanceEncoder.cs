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
    /// What is needed to find a person from one image again in another.
    /// </summary>
    /// <param name="Appearance">What the person looks like, as produced by <see cref="HumanAppearanceEncoder"/></param>
    /// <param name="Bounds">Bounding box of the person, as fractions of the width and height of their image</param>
    public readonly record struct SubjectSignature(float[] Appearance, Rect Bounds);

    /// <summary>
    /// Describes what people in images look like, in a form that allows the same person to be recognized in
    /// another image by their clothing and build, wherever in it they are. The descriptions are produced by a
    /// person re-identification model, which is run on the CPU by the ONNX Runtime that ships with Windows ML.
    /// </summary>
    public sealed partial class HumanAppearanceEncoder : IDisposable
    {
        #region Constants
        private const int InputWidth = 128;
        private const int InputHeight = 256;

        // How much weight where a person is carries, relative to what they look like, when they are being
        // found again. It only needs to be enough to tell apart people who look alike to the model.
        private const float PositionWeight = 0.1f;

        // The model takes RGB values that are normalized with these per-channel statistics
        private static readonly float[] _channelMeans = [123.675f, 116.28f, 103.53f];
        private static readonly float[] _channelDeviations = [58.395f, 57.12f, 57.375f];
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
        public static string DefaultModelPath => Path.Combine(AppContext.BaseDirectory, "Assets", "Models", "youtu_reid.onnx");

        public string ModelPath { get; }
        #endregion

        #region Constructor
        public HumanAppearanceEncoder(string? modelPath = null)
        {
            ModelPath = modelPath ?? DefaultModelPath;
        }
        #endregion

        #region Public Methods
        /// <summary>
        /// Describes what the people in an image look like.
        /// </summary>
        /// <remarks>
        /// The bitmap is only accessed before this method first yields, so the caller
        /// is free to dispose of it while the returned task is still running.
        /// </remarks>
        /// <param name="bitmap">The image the people are in</param>
        /// <param name="bounds">Bounding boxes of the people, in the coordinate space (DIPs) of the bitmap</param>
        /// <returns>
        /// The appearance of each person, in the order their bounding boxes were given.
        /// How alike two people look is given by <see cref="GetSimilarity"/>.
        /// </returns>
        public async Task<float[][]> EncodeAsync(CanvasBitmap bitmap, IReadOnlyList<Rect> bounds)
        {
            if (bounds.Count == 0)
                return [];

            // The model is trained on people who fill its input from edge to edge
            var crops = new List<byte[]>(bounds.Count);
            using (var target = new CanvasRenderTarget(bitmap.Device,
                                                       InputWidth,
                                                       InputHeight,
                                                       96f,
                                                       DirectXPixelFormat.B8G8R8A8UIntNormalized,
                                                       CanvasAlphaMode.Ignore))
            {
                foreach (var personBounds in bounds)
                {
                    if (personBounds.IsEmpty || !(personBounds.Width > 0) || !(personBounds.Height > 0))
                        throw new ArgumentException("A bounding box is empty", nameof(bounds));

                    using (var ds = target.CreateDrawingSession())
                    {
                        ds.Clear(Color.FromArgb(255, 0, 0, 0));
                        ds.DrawImage(bitmap,
                                     new Rect(0, 0, InputWidth, InputHeight),
                                     personBounds,
                                     1f,
                                     CanvasImageInterpolation.HighQualityCubic);
                    }

                    crops.Add(target.GetPixelBytes());
                }
            }

            return await Task.Run(() => Encode(crops));
        }

        /// <summary>
        /// Measures how alike two people look.
        /// </summary>
        /// <returns>
        /// A value of at most 1, which is higher the more alike the two appearances are
        /// </returns>
        public static float GetSimilarity(ReadOnlySpan<float> appearance, ReadOnlySpan<float> otherAppearance)
        {
            if (appearance.Length != otherAppearance.Length)
                throw new ArgumentException("The appearances were not produced by the same model", nameof(otherAppearance));

            // The appearances are unit vectors, so this is the cosine of the angle between them
            var similarity = 0f;
            for (var i = 0; i < appearance.Length; i++)
                similarity += appearance[i] * otherAppearance[i];

            return similarity;
        }

        /// <summary>
        /// Pairs the people in one image with the same people in another, going by how alike they look.
        /// Each person is paired with at most one other, with the pairs that are most alike made first.
        /// Where a person looks much like several others, the one nearest to where they were is preferred.
        /// </summary>
        /// <remarks>
        /// Everyone in the earlier image should be given, not only the people of interest. A person
        /// who is paired with their earlier self cannot then be mistaken for someone else who has left.
        /// </remarks>
        /// <param name="previous">The people in the earlier image, along with any who are remembered from before it</param>
        /// <param name="current">The people in the later image</param>
        /// <param name="minimumSimilarity">How alike two people must look to be taken as the same person</param>
        /// <returns>
        /// The pairs, as indexes within <paramref name="current"/> keyed by indexes within <paramref name="previous"/>
        /// </returns>
        public static IReadOnlyDictionary<int, int> MatchSubjects(IReadOnlyList<SubjectSignature> previous,
                                                                  IReadOnlyList<SubjectSignature> current,
                                                                  float minimumSimilarity)
        {
            var candidates = new List<(int Previous, int Current, double Score)>();
            for (var i = 0; i < previous.Count; i++)
            {
                for (var j = 0; j < current.Count; j++)
                {
                    var similarity = GetSimilarity(previous[i].Appearance, current[j].Appearance);
                    if (similarity >= minimumSimilarity)
                        candidates.Add((i, j, similarity + (PositionWeight * HumanSubjectDetector.GetOverlap(previous[i].Bounds, current[j].Bounds))));
                }
            }

            var pairs = new Dictionary<int, int>();
            var pairedCurrent = new HashSet<int>();
            foreach (var candidate in candidates.OrderByDescending(c => c.Score))
            {
                if (!pairs.ContainsKey(candidate.Previous) && pairedCurrent.Add(candidate.Current))
                    pairs.Add(candidate.Previous, candidate.Current);
            }

            return pairs;
        }
        #endregion

        #region Private Methods
        private float[][] Encode(List<byte[]> bgraCrops)
        {
            var planeLength = InputWidth * InputHeight;
            var input = new float[3 * planeLength];
            var appearances = new float[bgraCrops.Count][];

            lock (_sessionGate)
            {
                ObjectDisposedException.ThrowIf(_disposed, this);

                var session = EnsureSession();
                using var inputValue = OrtValue.CreateTensorValueFromMemory(input, [1, 3, InputHeight, InputWidth]);
                using var runOptions = new RunOptions();

                for (var c = 0; c < bgraCrops.Count; c++)
                {
                    var pixels = bgraCrops[c];
                    for (var i = 0; i < planeLength; i++)
                    {
                        var offset = i * 4;
                        input[i] = (pixels[offset + 2] - _channelMeans[0]) / _channelDeviations[0];
                        input[planeLength + i] = (pixels[offset + 1] - _channelMeans[1]) / _channelDeviations[1];
                        input[(2 * planeLength) + i] = (pixels[offset] - _channelMeans[2]) / _channelDeviations[2];
                    }

                    using var outputs = session.Run(runOptions, _inputNames, new[] { inputValue }, _outputNames);
                    var appearance = outputs[0].GetTensorDataAsSpan<float>().ToArray();

                    // Scaling the appearance to unit length is what allows two of them to be compared by their dot product
                    var length = MathF.Sqrt(GetSimilarity(appearance, appearance));
                    if (length > float.Epsilon)
                    {
                        for (var i = 0; i < appearance.Length; i++)
                            appearance[i] /= length;
                    }

                    appearances[c] = appearance;
                }
            }

            return appearances;
        }

        private InferenceSession EnsureSession()
        {
            if (_session is not null)
                return _session;

            // The model's weights are stored in a compact 8-bit form. This setting has them expanded
            // once, as the model is loaded, where they would otherwise be expanded every time it is run.
            using var options = new SessionOptions();
            options.AddSessionConfigEntry("session.disable_quant_qdq", "1");
            var session = new InferenceSession(ModelPath, options);

            try
            {
                var (inputName, inputMetadata) = session.InputMetadata.Single();
                if (inputMetadata.Dimensions.Length != 4 ||
                    !inputMetadata.Dimensions.AsSpan(1).SequenceEqual([3, InputHeight, InputWidth]))
                {
                    throw new InvalidOperationException(
                        $"The model expects an input of [{string.Join(", ", inputMetadata.Dimensions)}], not [1, 3, {InputHeight}, {InputWidth}]");
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
