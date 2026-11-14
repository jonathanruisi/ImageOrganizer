using Microsoft.Graphics.Canvas;

using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;

using Windows.Foundation;
using Windows.Graphics.DirectX;

namespace ImageOrganizer
{
    /// <summary>
    /// The distinctive points of an image, such as the corners of objects, each with a compact description of
    /// its surroundings. Two images of the same scene have many of these points in common, which is what
    /// allows <see cref="SceneAlignment"/> to work out how the scene has moved from one image to the other.
    /// </summary>
    public sealed class SceneFeatures
    {
        #region Constants
        internal const int DescriptorWordCount = 4;

        // The image is analyzed at this size (its longer side, in pixels) and at a few smaller sizes,
        // each smaller than the last by the given factor, so that points are found again after a change of zoom
        private const int WorkingSize = 640;
        private const int LevelCount = 4;
        private const double LevelFactor = 1.3;
        private const int MinimumLevelSize = 64;

        // Points are picked evenly, cell by cell, so that strongly textured areas do not crowd out the rest
        private const int CellSize = 32;
        private const int CellCapacity = 4;
        private const float MinimumCornerStrength = 100f;

        // The surroundings of a point are described by comparing the brightness of pairs of places within this radius
        private const int PatchRadius = 13;
        private const int OrientationRadius = 12;
        private const int BorderMargin = 20;
        private const int DescriptorBitCount = DescriptorWordCount * 64;

        private static readonly sbyte[] _comparisonPattern = CreateComparisonPattern();
        #endregion

        #region Properties
        /// <summary>
        /// Gets the size (in DIPs) of the image the points belong to.
        /// </summary>
        public Size ImageSize { get; }

        public int Count => Positions.Length;

        /// <summary>
        /// Gets the size (in DIPs) of a pixel of the image at the size it was analyzed at.
        /// </summary>
        internal double Resolution { get; }

        /// <summary>
        /// Gets the positions of the points, in the coordinate space (DIPs) of the image.
        /// </summary>
        internal Vector2[] Positions { get; }

        internal ulong[] Descriptors { get; }
        #endregion

        #region Constructor
        private SceneFeatures(Size imageSize, double resolution, Vector2[] positions, ulong[] descriptors)
        {
            ImageSize = imageSize;
            Resolution = resolution;
            Positions = positions;
            Descriptors = descriptors;
        }
        #endregion

        #region Public Methods
        /// <summary>
        /// Finds and describes the distinctive points of an image.
        /// </summary>
        /// <remarks>
        /// The bitmap is only accessed before this method first yields, so the caller
        /// is free to dispose of it while the returned task is still running.
        /// </remarks>
        public static async Task<SceneFeatures> ExtractAsync(CanvasBitmap bitmap)
        {
            var imageSize = bitmap.Size;
            var pixelSize = bitmap.SizeInPixels;
            if (imageSize.Width <= 0 || imageSize.Height <= 0)
                return new SceneFeatures(imageSize, 1, [], []);

            // The GPU does the resampling, so only small bitmaps need to be read back
            var workingScale = Math.Min(WorkingSize, Math.Max(pixelSize.Width, pixelSize.Height)) / Math.Max(imageSize.Width, imageSize.Height);
            var levels = new List<(byte[] Pixels, int Width, int Height)>();
            for (var i = 0; i < LevelCount; i++)
            {
                var scale = workingScale / Math.Pow(LevelFactor, i);
                var width = (int)Math.Round(imageSize.Width * scale);
                var height = (int)Math.Round(imageSize.Height * scale);
                if (Math.Min(width, height) < MinimumLevelSize)
                    break;

                using var target = new CanvasRenderTarget(bitmap.Device,
                                                          width,
                                                          height,
                                                          96f,
                                                          DirectXPixelFormat.B8G8R8A8UIntNormalized,
                                                          CanvasAlphaMode.Ignore);
                using (var ds = target.CreateDrawingSession())
                {
                    ds.DrawImage(bitmap,
                                 new Rect(0, 0, width, height),
                                 new Rect(0, 0, imageSize.Width, imageSize.Height),
                                 1f,
                                 CanvasImageInterpolation.HighQualityCubic);
                }

                levels.Add((target.GetPixelBytes(), width, height));
            }

            return await Task.Run(() =>
            {
                var positions = new List<Vector2>();
                var descriptors = new List<ulong>();
                foreach (var (pixels, width, height) in levels)
                    ExtractLevel(pixels, width, height, imageSize, positions, descriptors);

                return new SceneFeatures(imageSize, 1 / workingScale, [.. positions], [.. descriptors]);
            });
        }
        #endregion

        #region Private Methods
        private static void ExtractLevel(byte[] bgraPixels, int width, int height, Size imageSize, List<Vector2> positions, List<ulong> descriptors)
        {
            var count = width * height;
            var image = new float[count];
            for (var i = 0; i < count; i++)
            {
                var offset = i * 4;
                image[i] = (0.114f * bgraPixels[offset]) + (0.587f * bgraPixels[offset + 1]) + (0.299f * bgraPixels[offset + 2]);
            }

            // A point is distinctive where the brightness changes in every direction, which is measured
            // by the smaller eigenvalue of the matrix of brightness gradients gathered around it
            var xx = new float[count];
            var yy = new float[count];
            var xy = new float[count];
            for (var y = 1; y < height - 1; y++)
            {
                for (var x = 1; x < width - 1; x++)
                {
                    var i = (y * width) + x;
                    var gx = image[i - width + 1] + (2 * image[i + 1]) + image[i + width + 1]
                           - image[i - width - 1] - (2 * image[i - 1]) - image[i + width - 1];
                    var gy = image[i + width - 1] + (2 * image[i + width]) + image[i + width + 1]
                           - image[i - width - 1] - (2 * image[i - width]) - image[i - width + 1];
                    xx[i] = gx * gx;
                    yy[i] = gy * gy;
                    xy[i] = gx * gy;
                }
            }

            var scratch = new float[count];
            Blur(xx, scratch, width, height, 1.5f);
            Blur(yy, scratch, width, height, 1.5f);
            Blur(xy, scratch, width, height, 1.5f);

            var strength = xx;
            for (var i = 0; i < count; i++)
            {
                var mean = (xx[i] + yy[i]) / 2;
                var difference = (xx[i] - yy[i]) / 2;
                strength[i] = mean - MathF.Sqrt((difference * difference) + (xy[i] * xy[i]));
            }

            // The strongest points of each cell are kept, provided they stand out from their neighbors
            var columns = (width + CellSize - 1) / CellSize;
            var rows = (height + CellSize - 1) / CellSize;
            var cells = new List<(float Strength, int Index)>[columns * rows];
            for (var y = BorderMargin; y < height - BorderMargin; y++)
            {
                for (var x = BorderMargin; x < width - BorderMargin; x++)
                {
                    var i = (y * width) + x;
                    var value = strength[i];
                    if (value < MinimumCornerStrength ||
                        value <= strength[i - 1] || value < strength[i + 1] ||
                        value <= strength[i - width] || value < strength[i + width] ||
                        value <= strength[i - width - 1] || value < strength[i + width + 1] ||
                        value <= strength[i - width + 1] || value < strength[i + width - 1])
                        continue;

                    var cell = cells[((y / CellSize) * columns) + (x / CellSize)] ??= [];
                    cell.Add((value, i));
                }
            }

            // Descriptions are taken from a smoothed image, which makes them insensitive to noise
            var smooth = image;
            image = (float[])image.Clone();
            Blur(smooth, scratch, width, height, 2f);

            var scaleX = (float)(imageSize.Width / width);
            var scaleY = (float)(imageSize.Height / height);
            Span<ulong> descriptor = stackalloc ulong[DescriptorWordCount];
            foreach (var cell in cells)
            {
                if (cell is null)
                    continue;

                cell.Sort((a, b) => b.Strength.CompareTo(a.Strength));
                for (var c = 0; c < cell.Count && c < CellCapacity; c++)
                {
                    var i = cell[c].Index;
                    var x = i % width;
                    var y = i / width;

                    Describe(image, smooth, width, x, y, descriptor);
                    descriptors.AddRange(descriptor);

                    // The position is refined to a fraction of a pixel by fitting a parabola through the strengths around it
                    var offsetX = GetPeakOffset(strength[i - 1], strength[i], strength[i + 1]);
                    var offsetY = GetPeakOffset(strength[i - width], strength[i], strength[i + width]);
                    positions.Add(new Vector2((x + 0.5f + offsetX) * scaleX, (y + 0.5f + offsetY) * scaleY));
                }
            }

            static float GetPeakOffset(float before, float peak, float after)
            {
                var curvature = (2 * peak) - before - after;
                return curvature > 0 ? Math.Clamp((after - before) / (2 * curvature), -0.5f, 0.5f) : 0;
            }
        }

        /// <summary>
        /// Describes the surroundings of a point as a string of bits, each of which records which of two places
        /// near the point is the brighter. The places are laid out relative to the direction in which
        /// the surroundings get brighter, so that the description is unaffected by the image being rotated.
        /// </summary>
        private static void Describe(float[] image, float[] smooth, int width, int x, int y, Span<ulong> descriptor)
        {
            var center = (y * width) + x;
            float momentX = 0, momentY = 0;
            for (var dy = -OrientationRadius; dy <= OrientationRadius; dy++)
            {
                var extent = (int)MathF.Sqrt((OrientationRadius * OrientationRadius) - (dy * dy));
                for (var dx = -extent; dx <= extent; dx++)
                {
                    var value = image[center + (dy * width) + dx];
                    momentX += dx * value;
                    momentY += dy * value;
                }
            }

            var length = MathF.Sqrt((momentX * momentX) + (momentY * momentY));
            var cos = length > 0 ? momentX / length : 1;
            var sin = length > 0 ? momentY / length : 0;

            descriptor.Clear();
            for (var bit = 0; bit < DescriptorBitCount; bit++)
            {
                var p = bit * 4;
                var x1 = (int)MathF.Round((cos * _comparisonPattern[p]) - (sin * _comparisonPattern[p + 1]));
                var y1 = (int)MathF.Round((sin * _comparisonPattern[p]) + (cos * _comparisonPattern[p + 1]));
                var x2 = (int)MathF.Round((cos * _comparisonPattern[p + 2]) - (sin * _comparisonPattern[p + 3]));
                var y2 = (int)MathF.Round((sin * _comparisonPattern[p + 2]) + (cos * _comparisonPattern[p + 3]));

                if (smooth[center + (y1 * width) + x1] < smooth[center + (y2 * width) + x2])
                    descriptor[bit >> 6] |= 1UL << (bit & 63);
            }
        }

        /// <summary>
        /// Lays out the pairs of places that are compared to describe a point. They are scattered at random,
        /// more densely towards the point, and are the same every time so that descriptions can be compared.
        /// </summary>
        private static sbyte[] CreateComparisonPattern()
        {
            var random = new Random(20261113);
            var pattern = new sbyte[DescriptorBitCount * 4];
            for (var i = 0; i < pattern.Length; i += 2)
            {
                double x, y;
                do
                {
                    var radius = (PatchRadius / 2.5) * Math.Sqrt(-2 * Math.Log(1 - random.NextDouble()));
                    var angle = 2 * Math.PI * random.NextDouble();
                    x = Math.Round(radius * Math.Cos(angle));
                    y = Math.Round(radius * Math.Sin(angle));
                }
                while ((x * x) + (y * y) > PatchRadius * PatchRadius);

                pattern[i] = (sbyte)x;
                pattern[i + 1] = (sbyte)y;
            }

            return pattern;
        }

        /// <summary>
        /// Smooths an image in place with a Gaussian of the given standard deviation.
        /// </summary>
        private static void Blur(float[] image, float[] scratch, int width, int height, float sigma)
        {
            var radius = (int)MathF.Ceiling(sigma * 2.5f);
            var kernel = new float[(2 * radius) + 1];
            var sum = 0f;
            for (var i = -radius; i <= radius; i++)
                sum += kernel[i + radius] = MathF.Exp(-(i * i) / (2 * sigma * sigma));
            for (var i = 0; i < kernel.Length; i++)
                kernel[i] /= sum;

            for (var y = 0; y < height; y++)
            {
                var row = y * width;
                for (var x = 0; x < width; x++)
                {
                    var value = 0f;
                    for (var k = -radius; k <= radius; k++)
                        value += kernel[k + radius] * image[row + Math.Clamp(x + k, 0, width - 1)];
                    scratch[row + x] = value;
                }
            }

            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var value = 0f;
                    for (var k = -radius; k <= radius; k++)
                        value += kernel[k + radius] * scratch[(Math.Clamp(y + k, 0, height - 1) * width) + x];
                    image[(y * width) + x] = value;
                }
            }
        }
        #endregion
    }
}
