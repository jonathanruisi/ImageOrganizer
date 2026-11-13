using Microsoft.Graphics.Canvas;
using Microsoft.ML.OnnxRuntime;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;

using Windows.Foundation;
using Windows.Graphics.DirectX;
using Windows.UI;

namespace ImageOrganizer
{
    /// <summary>
    /// Determines how far a person in an image is rotated away from upright. The person's joints are
    /// located with an RTMPose model, which is run on the CPU by the ONNX Runtime that ships with Windows ML.
    /// </summary>
    public sealed partial class HumanPoseEstimator : IDisposable
    {
        #region Constants
        private const int InputWidth = 192;
        private const int InputHeight = 256;
        private const int JointCount = 17;

        // Indexes of the joints (in COCO order) that indicate which way up a person is
        private const int Nose = 0;
        private const int LeftEye = 1;
        private const int RightEye = 2;
        private const int LeftShoulder = 5;
        private const int RightShoulder = 6;
        private const int LeftHip = 11;
        private const int RightHip = 12;

        // The model is given this much more than the person's bounding box, for context
        private const double BoundsPadding = 1.25;

        // The model reports each coordinate as a distribution over bins, of which there are this many per input pixel
        private const float BinsPerPixel = 2f;

        // An estimate is accepted once it is within this many degrees of the tilt the person was assumed to have
        private const double TiltTolerance = 15.0;
        private const int MaximumPassCount = 6;

        // The model takes RGB values that are normalized with these per-channel statistics
        private static readonly float[] _channelMeans = [123.675f, 116.28f, 103.53f];
        private static readonly float[] _channelDeviations = [58.395f, 57.12f, 57.375f];
        #endregion

        #region Nested Types
        private readonly record struct Joint(Vector2 Position, float Confidence);
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
        public static string DefaultModelPath => Path.Combine(AppContext.BaseDirectory, "Assets", "Models", "rtmpose_s.onnx");

        public string ModelPath { get; }

        /// <summary>
        /// Gets or sets the minimum confidence a joint needs in order to be used.
        /// </summary>
        public float JointConfidenceThreshold { get; set; } = 0.3f;
        #endregion

        #region Constructor
        public HumanPoseEstimator(string? modelPath = null)
        {
            ModelPath = modelPath ?? DefaultModelPath;
        }
        #endregion

        #region Public Methods
        /// <summary>
        /// Measures how far a person is rotated away from upright.
        /// </summary>
        /// <remarks>
        /// The bitmap must not be disposed of until the returned task has completed.
        /// When awaited on a UI thread, the bitmap is only accessed from that thread.
        /// </remarks>
        /// <param name="bitmap">The image the person is in</param>
        /// <param name="bounds">Bounding box of the person, in the coordinate space (DIPs) of the bitmap</param>
        /// <returns>
        /// The clockwise angle, in degrees within (-180, 180], from the top of the image to the direction
        /// the top of the person's body points in, or null if the pose of the person could not be made out
        /// </returns>
        public async Task<double?> EstimateTiltAsync(CanvasBitmap bitmap, Rect bounds)
        {
            // The model is at its most reliable when a person is presented to it upright. An estimate is
            // therefore only accepted once the model, shown the person as they would appear with that tilt
            // cancelled out, agrees that they are upright. A person may be so far from upright that the
            // model cannot make them out at all, in which case they are presented at other orientations.
            var assumedTilt = 0.0;
            var alternativeTilts = new Queue<double>(new[] { 180.0, 90.0, -90.0 });

            for (var pass = 0; pass < MaximumPassCount; pass++)
            {
                if (await EstimateTiltAsync(bitmap, bounds, assumedTilt) is not double tilt)
                {
                    if (!alternativeTilts.TryDequeue(out assumedTilt))
                        return null;
                }
                else if (Math.Abs(Math.IEEERemainder(tilt - assumedTilt, 360.0)) <= TiltTolerance)
                {
                    return tilt;
                }
                else
                {
                    assumedTilt = tilt;
                }
            }

            return null;
        }
        #endregion

        #region Private Methods
        /// <summary>
        /// Measures the tilt of a person by presenting them to the model as they
        /// would appear if the image were rotated to cancel out an assumed tilt.
        /// </summary>
        internal async Task<double?> EstimateTiltAsync(CanvasBitmap bitmap, Rect bounds, double assumedTilt)
        {
            if (bounds.IsEmpty || bounds.Width <= 0 || bounds.Height <= 0)
                return null;

            // The area occupied by the person once the assumed tilt is cancelled out is widened or
            // heightened to the shape of the model's input, and then padded, before being scaled to fit
            var radians = assumedTilt * Math.PI / 180.0;
            var width = (bounds.Width * Math.Abs(Math.Cos(radians))) + (bounds.Height * Math.Abs(Math.Sin(radians)));
            var height = (bounds.Width * Math.Abs(Math.Sin(radians))) + (bounds.Height * Math.Abs(Math.Cos(radians)));
            width = Math.Max(width, height * InputWidth / InputHeight) * BoundsPadding;

            var center = new Vector2((float)(bounds.X + (bounds.Width / 2)), (float)(bounds.Y + (bounds.Height / 2)));
            var imageToInput = Matrix3x2.CreateTranslation(-center) *
                               Matrix3x2.CreateRotation((float)-radians) *
                               Matrix3x2.CreateScale((float)(InputWidth / width)) *
                               Matrix3x2.CreateTranslation(InputWidth / 2f, InputHeight / 2f);
            if (!Matrix3x2.Invert(imageToInput, out var inputToImage))
                return null;

            byte[] pixels;
            using (var target = new CanvasRenderTarget(bitmap.Device,
                                                       InputWidth,
                                                       InputHeight,
                                                       96f,
                                                       DirectXPixelFormat.B8G8R8A8UIntNormalized,
                                                       CanvasAlphaMode.Ignore))
            {
                using (var ds = target.CreateDrawingSession())
                {
                    ds.Clear(Color.FromArgb(255, 0, 0, 0));
                    ds.Transform = imageToInput;
                    ds.DrawImage(bitmap,
                                 0,
                                 0,
                                 new Rect(0, 0, bitmap.Size.Width, bitmap.Size.Height),
                                 1f,
                                 CanvasImageInterpolation.HighQualityCubic);
                }

                pixels = target.GetPixelBytes();
            }

            var joints = await Task.Run(() => LocateJoints(pixels));
            if (joints is null)
                return null;

            for (var i = 0; i < joints.Length; i++)
                joints[i] = joints[i] with { Position = Vector2.Transform(joints[i].Position, inputToImage) };

            return GetTilt(joints, JointConfidenceThreshold);
        }

        /// <summary>
        /// Locates the joints of the person in an image prepared for the model.
        /// </summary>
        /// <returns>
        /// The joints, positioned in the coordinate space of the model's input, or null if this instance has been disposed
        /// </returns>
        private Joint[]? LocateJoints(byte[] bgraPixels)
        {
            var planeLength = InputWidth * InputHeight;
            var input = new float[3 * planeLength];
            for (var i = 0; i < planeLength; i++)
            {
                var offset = i * 4;
                input[i] = (bgraPixels[offset + 2] - _channelMeans[0]) / _channelDeviations[0];
                input[planeLength + i] = (bgraPixels[offset + 1] - _channelMeans[1]) / _channelDeviations[1];
                input[(2 * planeLength) + i] = (bgraPixels[offset] - _channelMeans[2]) / _channelDeviations[2];
            }

            lock (_sessionGate)
            {
                if (_disposed)
                    return null;

                var session = EnsureSession();
                using var inputValue = OrtValue.CreateTensorValueFromMemory(input, [1, 3, InputHeight, InputWidth]);
                using var runOptions = new RunOptions();
                using var outputs = session.Run(runOptions, _inputNames, new[] { inputValue }, _outputNames);

                // For each joint, the most likely bin along each axis gives its position
                var horizontal = outputs[0].GetTensorDataAsSpan<float>();
                var vertical = outputs[1].GetTensorDataAsSpan<float>();
                var horizontalBinCount = horizontal.Length / JointCount;
                var verticalBinCount = vertical.Length / JointCount;

                var joints = new Joint[JointCount];
                for (var i = 0; i < JointCount; i++)
                {
                    var (x, horizontalConfidence) = FindPeak(horizontal.Slice(i * horizontalBinCount, horizontalBinCount));
                    var (y, verticalConfidence) = FindPeak(vertical.Slice(i * verticalBinCount, verticalBinCount));
                    joints[i] = new Joint(new Vector2(x / BinsPerPixel, y / BinsPerPixel),
                                          Math.Min(horizontalConfidence, verticalConfidence));
                }

                return joints;
            }

            static (int Index, float Value) FindPeak(ReadOnlySpan<float> values)
            {
                var index = 0;
                for (var i = 1; i < values.Length; i++)
                {
                    if (values[i] > values[index])
                        index = i;
                }

                return (index, values[index]);
            }
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
                if (inputMetadata.Dimensions.Length != 4 ||
                    !inputMetadata.Dimensions.AsSpan(1).SequenceEqual([3, InputHeight, InputWidth]) ||
                    !session.OutputMetadata.ContainsKey("simcc_x") ||
                    !session.OutputMetadata.ContainsKey("simcc_y"))
                {
                    throw new InvalidOperationException(
                        $"The model is not an RTMPose model with an input of [3, {InputHeight}, {InputWidth}]");
                }

                _inputNames = [inputName];
                _outputNames = ["simcc_x", "simcc_y"];
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
        /// Works out which way up a person is from the positions of their joints.
        /// </summary>
        /// <returns>
        /// The clockwise angle, in degrees, from the top of the image to the direction the top
        /// of the person's body points in, or null if too few joints were found to tell
        /// </returns>
        private static double? GetTilt(ReadOnlySpan<Joint> joints, float minimumConfidence)
        {
            var leftShoulder = joints[LeftShoulder];
            var rightShoulder = joints[RightShoulder];
            var leftHip = joints[LeftHip];
            var rightHip = joints[RightHip];
            var leftEye = joints[LeftEye];
            var rightEye = joints[RightEye];
            var nose = joints[Nose];

            var hasShoulders = Math.Min(leftShoulder.Confidence, rightShoulder.Confidence) >= minimumConfidence;
            var hasHips = Math.Min(leftHip.Confidence, rightHip.Confidence) >= minimumConfidence;
            var hasEyes = Math.Min(leftEye.Confidence, rightEye.Confidence) >= minimumConfidence;
            var hasNose = nose.Confidence >= minimumConfidence;
            var shoulderCenter = (leftShoulder.Position + rightShoulder.Position) / 2;
            var eyeCenter = (leftEye.Position + rightEye.Position) / 2;

            Vector2 up;
            if (hasShoulders && hasHips)
            {
                // The torso runs from the middle of the hips up to the middle of the shoulders
                up = shoulderCenter - ((leftHip.Position + rightHip.Position) / 2);
            }
            else if (hasShoulders && hasNose)
            {
                // Without the hips, the body is taken to be square to the shoulders, on the side of them the head is on
                up = GetPerpendicularTowards(rightShoulder.Position - leftShoulder.Position, nose.Position - shoulderCenter);
            }
            else if (hasEyes && hasNose)
            {
                // With only a face to go by, it is taken to be square to the eyes, which are above the nose
                up = GetPerpendicularTowards(rightEye.Position - leftEye.Position, eyeCenter - nose.Position);
            }
            else
            {
                return null;
            }

            if (up.LengthSquared() < float.Epsilon)
                return null;

            return Math.Atan2(up.X, -up.Y) * 180.0 / Math.PI;

            static Vector2 GetPerpendicularTowards(Vector2 line, Vector2 direction)
            {
                var perpendicular = new Vector2(-line.Y, line.X);
                return Vector2.Dot(perpendicular, direction) < 0 ? -perpendicular : perpendicular;
            }
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
