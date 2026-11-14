using System;
using System.Collections.Generic;
using System.Numerics;

using Windows.Foundation;

namespace ImageOrganizer
{
    /// <summary>
    /// How the scene in one image has moved to where it is in another image, as worked out by <see cref="SceneAlignment"/>.
    /// </summary>
    public sealed class SceneTransform
    {
        #region Fields
        // A 3x3 projective transform, in row-major order
        private readonly double[] _matrix;
        #endregion

        #region Properties
        /// <summary>
        /// Gets the number of points the two images were found to have in common, all of which moved in this way.
        /// </summary>
        public int SupportCount { get; }
        #endregion

        #region Constructor
        internal SceneTransform(double[] matrix, int supportCount)
        {
            _matrix = matrix;
            SupportCount = supportCount;
        }
        #endregion

        #region Public Methods
        /// <summary>
        /// Finds where a position in the first image is in the second image.
        /// </summary>
        /// <returns>
        /// The position in the second image, which may lie outside it, or null if the position is not visible from there at all
        /// </returns>
        public Vector2? Map(Vector2 position)
        {
            var w = (_matrix[6] * position.X) + (_matrix[7] * position.Y) + _matrix[8];
            if (!(w > 1e-6))
                return null;

            return new Vector2((float)(((_matrix[0] * position.X) + (_matrix[1] * position.Y) + _matrix[2]) / w),
                               (float)(((_matrix[3] * position.X) + (_matrix[4] * position.Y) + _matrix[5]) / w));
        }
        #endregion
    }

    /// <summary>
    /// Works out how the scene in one image has moved to where it is in another image, from the distinctive points
    /// the two images have in common. This is what allows something marked in one image to be found in the other.
    /// </summary>
    public static class SceneAlignment
    {
        #region Constants
        // Two descriptions are taken to be of the same point if they differ in at most this many bits
        // and if no other candidate comes within the given fraction of being as good a match
        private const int MaximumDescriptorDistance = 80;
        private const double MaximumDistanceRatio = 0.85;

        // How far, in pixels at the size the images are analyzed at, a point can be from where
        // the transform puts it and still be counted as having moved in that way
        private const double Tolerance = 3.0;

        private const int TrialCount = 1500;
        private const int RefinementCount = 3;
        private const int PerspectiveRefinementCount = 6;
        private const int MinimumPerspectiveSeedCount = 12;
        private const double PerspectiveSeedFactor = 2.0;
        private const double MinimumScale = 0.33;
        private const double MaximumScale = 3.0;
        #endregion

        #region Properties
        /// <summary>
        /// Gets or sets the number of points two images must have in common, all moving in the same way,
        /// for them to be accepted as showing the same scene.
        /// </summary>
        public static int MinimumSupportCount { get; set; } = 20;

        /// <summary>
        /// Gets or sets whether the change of perspective between the two images is modeled, as it arises when
        /// the camera is turned to face another way. Otherwise the scene is taken to have only shifted, rotated and scaled.
        /// </summary>
        public static bool ModelPerspective { get; set; } = true;
        #endregion

        #region Public Methods
        /// <summary>
        /// Works out how the scene in one image has moved to where it is in another image.
        /// </summary>
        /// <param name="from">The distinctive points of the first image</param>
        /// <param name="to">The distinctive points of the second image</param>
        /// <param name="excludedFrom">
        /// Areas of the first image to ignore, in its coordinate space. These should cover whatever moves about
        /// within the scene, such as people, as it would otherwise be mistaken for movement of the scene itself.
        /// </param>
        /// <param name="excludedTo">Areas of the second image to ignore, in its coordinate space</param>
        /// <returns>
        /// The movement of the scene, or null if the two images do not appear to show the same scene
        /// </returns>
        public static SceneTransform? Align(SceneFeatures from,
                                            SceneFeatures to,
                                            IReadOnlyList<Rect>? excludedFrom = null,
                                            IReadOnlyList<Rect>? excludedTo = null)
        {
            var matches = Match(from, to, excludedFrom, excludedTo);
            if (matches.Count < MinimumSupportCount)
                return null;

            var tolerance = Tolerance * to.Resolution;
            var squaredTolerance = tolerance * tolerance;

            // Many of the matches are wrong. The movement of the scene is taken to be the one that the most matches
            // agree on, which is searched for by trying out the movement implied by one random pair of matches after another.
            var random = new Random(matches.Count);
            double[]? best = null;
            var bestCount = 0;
            for (var trial = 0; trial < TrialCount; trial++)
            {
                var first = matches[random.Next(matches.Count)];
                var second = matches[random.Next(matches.Count)];
                var fromDelta = second.From - first.From;
                var toDelta = second.To - first.To;
                var squaredLength = fromDelta.LengthSquared();
                if (squaredLength < 100 * squaredTolerance)
                    continue;

                // A shift, rotation and scale is the multiplication of positions (as complex numbers) by a factor, plus an offset
                var a = Vector2.Dot(fromDelta, toDelta) / squaredLength;
                var b = ((fromDelta.X * toDelta.Y) - (fromDelta.Y * toDelta.X)) / squaredLength;
                var scale = Math.Sqrt((a * a) + (b * b));
                if (scale < MinimumScale || scale > MaximumScale)
                    continue;

                double[] candidate =
                [
                    a, -b, first.To.X - (a * first.From.X) + (b * first.From.Y),
                    b, a, first.To.Y - (b * first.From.X) - (a * first.From.Y),
                    0, 0, 1
                ];

                var count = CountSupport(matches, candidate, squaredTolerance, null);
                if (count > bestCount)
                {
                    best = candidate;
                    bestCount = count;
                }
            }

            if (best is null || bestCount < MinimumSupportCount)
                return null;

            // The movement is then fitted to all of the matches that agree with it, which in turn brings more of them into agreement
            var supporters = new List<(Vector2 From, Vector2 To)>();
            for (var i = 0; i < RefinementCount; i++)
            {
                CountSupport(matches, best, squaredTolerance, supporters);
                if (FitSimilarity(supporters) is not { } fitted)
                    break;

                best = fitted;
            }

            bestCount = CountSupport(matches, best, squaredTolerance, supporters);
            if (ModelPerspective)
            {
                // A change of perspective leaves matches further from where a mere shift, rotation and
                // scale puts them, so the fit is started from the matches within a more generous distance
                CountSupport(matches, best, PerspectiveSeedFactor * PerspectiveSeedFactor * squaredTolerance, supporters);
                for (var i = 0; i < PerspectiveRefinementCount && supporters.Count >= MinimumPerspectiveSeedCount; i++)
                {
                    if (FitPerspective(supporters) is not { } fitted)
                        break;

                    var count = CountSupport(matches, fitted, squaredTolerance, supporters);
                    if (count >= bestCount)
                    {
                        best = fitted;
                        bestCount = count;
                    }
                }
            }

            return bestCount >= MinimumSupportCount ? new SceneTransform(best, bestCount) : null;
        }
        #endregion

        #region Private Methods
        /// <summary>
        /// Pairs up the points of two images whose descriptions are each other's closest match.
        /// </summary>
        private static List<(Vector2 From, Vector2 To)> Match(SceneFeatures from,
                                                             SceneFeatures to,
                                                             IReadOnlyList<Rect>? excludedFrom,
                                                             IReadOnlyList<Rect>? excludedTo)
        {
            const int words = SceneFeatures.DescriptorWordCount;
            var fromIndexes = GetIncluded(from, excludedFrom);
            var toIndexes = GetIncluded(to, excludedTo);
            var fromDescriptors = from.Descriptors;
            var toDescriptors = to.Descriptors;

            var closestTo = new int[fromIndexes.Count];
            var closestDistance = new int[fromIndexes.Count];
            var nextDistance = new int[fromIndexes.Count];
            var closestFrom = new int[toIndexes.Count];
            var closestFromDistance = new int[toIndexes.Count];
            Array.Fill(closestFromDistance, int.MaxValue);

            for (var i = 0; i < fromIndexes.Count; i++)
            {
                var f = fromIndexes[i] * words;
                var closest = -1;
                var distance = int.MaxValue;
                var next = int.MaxValue;
                for (var j = 0; j < toIndexes.Count; j++)
                {
                    var t = toIndexes[j] * words;
                    var d = BitOperations.PopCount(fromDescriptors[f] ^ toDescriptors[t])
                          + BitOperations.PopCount(fromDescriptors[f + 1] ^ toDescriptors[t + 1])
                          + BitOperations.PopCount(fromDescriptors[f + 2] ^ toDescriptors[t + 2])
                          + BitOperations.PopCount(fromDescriptors[f + 3] ^ toDescriptors[t + 3]);

                    if (d < distance)
                    {
                        next = distance;
                        distance = d;
                        closest = j;
                    }
                    else if (d < next)
                    {
                        next = d;
                    }

                    if (d < closestFromDistance[j])
                    {
                        closestFromDistance[j] = d;
                        closestFrom[j] = i;
                    }
                }

                closestTo[i] = closest;
                closestDistance[i] = distance;
                nextDistance[i] = next;
            }

            var matches = new List<(Vector2 From, Vector2 To)>();
            for (var i = 0; i < fromIndexes.Count; i++)
            {
                var j = closestTo[i];
                if (j >= 0 &&
                    closestFrom[j] == i &&
                    closestDistance[i] <= MaximumDescriptorDistance &&
                    closestDistance[i] <= MaximumDistanceRatio * nextDistance[i])
                    matches.Add((from.Positions[fromIndexes[i]], to.Positions[toIndexes[j]]));
            }

            return matches;

            static List<int> GetIncluded(SceneFeatures features, IReadOnlyList<Rect>? excluded)
            {
                var indexes = new List<int>(features.Count);
                for (var i = 0; i < features.Count; i++)
                {
                    var position = features.Positions[i];
                    var isExcluded = false;
                    for (var r = 0; excluded is not null && r < excluded.Count && !isExcluded; r++)
                        isExcluded = excluded[r].Contains(new Point(position.X, position.Y));

                    if (!isExcluded)
                        indexes.Add(i);
                }

                return indexes;
            }
        }

        /// <summary>
        /// Counts the matches that a transform puts within the tolerance of where they actually are, optionally gathering them.
        /// </summary>
        private static int CountSupport(List<(Vector2 From, Vector2 To)> matches,
                                        double[] matrix,
                                        double squaredTolerance,
                                        List<(Vector2 From, Vector2 To)>? supporters)
        {
            supporters?.Clear();
            var count = 0;
            foreach (var match in matches)
            {
                var w = (matrix[6] * match.From.X) + (matrix[7] * match.From.Y) + matrix[8];
                if (!(w > 1e-6))
                    continue;

                var dx = ((((matrix[0] * match.From.X) + (matrix[1] * match.From.Y) + matrix[2])) / w) - match.To.X;
                var dy = ((((matrix[3] * match.From.X) + (matrix[4] * match.From.Y) + matrix[5])) / w) - match.To.Y;
                if ((dx * dx) + (dy * dy) <= squaredTolerance)
                {
                    count++;
                    supporters?.Add(match);
                }
            }

            return count;
        }

        /// <summary>
        /// Finds the shift, rotation and scale that best accounts for a set of matches, in the least-squares sense.
        /// </summary>
        private static double[]? FitSimilarity(List<(Vector2 From, Vector2 To)> matches)
        {
            if (matches.Count < 2)
                return null;

            double fromX = 0, fromY = 0, toX = 0, toY = 0;
            foreach (var match in matches)
            {
                fromX += match.From.X;
                fromY += match.From.Y;
                toX += match.To.X;
                toY += match.To.Y;
            }

            fromX /= matches.Count;
            fromY /= matches.Count;
            toX /= matches.Count;
            toY /= matches.Count;

            double dot = 0, cross = 0, spread = 0;
            foreach (var match in matches)
            {
                var px = match.From.X - fromX;
                var py = match.From.Y - fromY;
                var qx = match.To.X - toX;
                var qy = match.To.Y - toY;
                dot += (px * qx) + (py * qy);
                cross += (px * qy) - (py * qx);
                spread += (px * px) + (py * py);
            }

            if (!(spread > 0))
                return null;

            var a = dot / spread;
            var b = cross / spread;
            return
            [
                a, -b, toX - (a * fromX) + (b * fromY),
                b, a, toY - (b * fromX) - (a * fromY),
                0, 0, 1
            ];
        }

        /// <summary>
        /// Finds the projective transform that best accounts for a set of matches, in the least-squares sense.
        /// </summary>
        private static double[]? FitPerspective(List<(Vector2 From, Vector2 To)> matches)
        {
            if (matches.Count < 4)
                return null;

            // The positions are first centered and brought to a common scale, without which the fit would be inaccurate
            var (fromX, fromY, fromScale) = GetNormalization(matches, fromSide: true);
            var (toX, toY, toScale) = GetNormalization(matches, fromSide: false);
            if (!(fromScale > 0) || !(toScale > 0))
                return null;

            // Each match contributes two linear equations in the eight unknowns of the transform
            var system = new double[8, 9];
            Span<double> row = stackalloc double[9];
            foreach (var match in matches)
            {
                var x = (match.From.X - fromX) * fromScale;
                var y = (match.From.Y - fromY) * fromScale;
                var u = (match.To.X - toX) * toScale;
                var v = (match.To.Y - toY) * toScale;

                row.Clear();
                row[0] = x; row[1] = y; row[2] = 1; row[6] = -x * u; row[7] = -y * u; row[8] = u;
                Accumulate(system, row);

                row.Clear();
                row[3] = x; row[4] = y; row[5] = 1; row[6] = -x * v; row[7] = -y * v; row[8] = v;
                Accumulate(system, row);
            }

            if (!Solve(system))
                return null;

            // The normalization is undone: positions are normalized, transformed, then returned to the scale of the second image
            var h = new double[8];
            for (var i = 0; i < 8; i++)
                h[i] = system[i, 8];

            double[] normalized = [h[0], h[1], h[2], h[3], h[4], h[5], h[6], h[7], 1];
            double[] fromNormalization = [fromScale, 0, -fromX * fromScale, 0, fromScale, -fromY * fromScale, 0, 0, 1];
            double[] toDenormalization = [1 / toScale, 0, toX, 0, 1 / toScale, toY, 0, 0, 1];
            var matrix = Multiply(toDenormalization, Multiply(normalized, fromNormalization));
            if (Math.Abs(matrix[8]) < 1e-12)
                return null;

            for (var i = 0; i < 9; i++)
                matrix[i] /= matrix[8];

            return matrix;

            static (double X, double Y, double Scale) GetNormalization(List<(Vector2 From, Vector2 To)> matches, bool fromSide)
            {
                double x = 0, y = 0;
                foreach (var match in matches)
                {
                    var p = fromSide ? match.From : match.To;
                    x += p.X;
                    y += p.Y;
                }

                x /= matches.Count;
                y /= matches.Count;

                var distance = 0.0;
                foreach (var match in matches)
                {
                    var p = fromSide ? match.From : match.To;
                    distance += Math.Sqrt(((p.X - x) * (p.X - x)) + ((p.Y - y) * (p.Y - y)));
                }

                return (x, y, distance > 0 ? Math.Sqrt(2) * matches.Count / distance : 0);
            }

            static void Accumulate(double[,] system, ReadOnlySpan<double> row)
            {
                for (var i = 0; i < 8; i++)
                {
                    if (row[i] == 0)
                        continue;

                    for (var j = 0; j < 9; j++)
                        system[i, j] += row[i] * row[j];
                }
            }

            static double[] Multiply(double[] left, double[] right)
            {
                var product = new double[9];
                for (var i = 0; i < 3; i++)
                {
                    for (var j = 0; j < 3; j++)
                        product[(i * 3) + j] = (left[i * 3] * right[j]) + (left[(i * 3) + 1] * right[3 + j]) + (left[(i * 3) + 2] * right[6 + j]);
                }

                return product;
            }
        }

        /// <summary>
        /// Solves a system of eight linear equations by elimination, leaving the solution in its last column.
        /// </summary>
        private static bool Solve(double[,] system)
        {
            for (var column = 0; column < 8; column++)
            {
                var pivot = column;
                for (var i = column + 1; i < 8; i++)
                {
                    if (Math.Abs(system[i, column]) > Math.Abs(system[pivot, column]))
                        pivot = i;
                }

                if (Math.Abs(system[pivot, column]) < 1e-12)
                    return false;

                if (pivot != column)
                {
                    for (var j = 0; j < 9; j++)
                        (system[column, j], system[pivot, j]) = (system[pivot, j], system[column, j]);
                }

                for (var i = 0; i < 8; i++)
                {
                    if (i == column)
                        continue;

                    var factor = system[i, column] / system[column, column];
                    for (var j = column; j < 9; j++)
                        system[i, j] -= factor * system[column, j];
                }
            }

            for (var i = 0; i < 8; i++)
                system[i, 8] /= system[i, i];

            return true;
        }
        #endregion
    }
}
