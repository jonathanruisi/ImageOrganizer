using System;
using System.Collections.Generic;
using System.Numerics;

using Windows.Foundation;

namespace ImageOrganizer
{
    /// <summary>
    /// A view of an image, expressed the way <see cref="Controls.ImagePresenterControl"/> positions an image:
    /// rotated about its center, then scaled, then centered in the viewport and offset by a translation.
    /// </summary>
    /// <param name="Rotation">The clockwise rotation of the image, in degrees within [0, 360)</param>
    /// <param name="Scale">The scale factor applied to the image</param>
    /// <param name="TranslationX">The horizontal offset, in DIPs, of the image's center from the center of the viewport</param>
    /// <param name="TranslationY">The vertical offset, in DIPs, of the image's center from the center of the viewport</param>
    public readonly record struct SubjectFrame(double Rotation, double Scale, double TranslationX, double TranslationY);

    public static class SubjectFraming
    {
        /// <summary>
        /// Works out the view of an image in which a group of subjects is upright, centered and as large as will fit.
        /// </summary>
        /// <param name="subjectBounds">Bounding boxes of the subjects, relative to the image</param>
        /// <param name="subjectTilts">
        /// The clockwise angles, in degrees, by which the subjects are known to be rotated away from upright.
        /// The image is counter-rotated by their average. Subjects whose tilt is unknown are simply left out.
        /// </param>
        /// <param name="imageSize">The size of the image</param>
        /// <param name="viewportSize">The size of the area the image is displayed in</param>
        /// <param name="fill">
        /// The fraction of the viewport that the subjects should span, in whichever dimension is the tighter fit
        /// </param>
        /// <returns>
        /// The view, or null if there is nothing to frame or nowhere to display it
        /// </returns>
        public static SubjectFrame? GetFrame(IEnumerable<Rect> subjectBounds,
                                             IEnumerable<double> subjectTilts,
                                             Size imageSize,
                                             Size viewportSize,
                                             double fill)
        {
            // Rotating the image about its center, as the control does, leaves the subjects somewhere
            // new. The extents they then cover determine the scale and what is brought to the center.
            var rotation = -GetAverageAngle(subjectTilts);
            var imageCenter = new Vector2((float)(imageSize.Width / 2), (float)(imageSize.Height / 2));
            var rotationMatrix = Matrix3x2.CreateRotation((float)(rotation * Math.PI / 180.0), imageCenter);

            var minimum = new Vector2(float.PositiveInfinity);
            var maximum = new Vector2(float.NegativeInfinity);
            foreach (var bounds in subjectBounds)
            {
                ReadOnlySpan<Vector2> corners =
                [
                    new((float)bounds.Left, (float)bounds.Top),
                    new((float)bounds.Right, (float)bounds.Top),
                    new((float)bounds.Right, (float)bounds.Bottom),
                    new((float)bounds.Left, (float)bounds.Bottom)
                ];

                foreach (var corner in corners)
                {
                    var rotated = Vector2.Transform(corner, rotationMatrix);
                    minimum = Vector2.Min(minimum, rotated);
                    maximum = Vector2.Max(maximum, rotated);
                }
            }

            var extent = maximum - minimum;
            if (!(extent.X > 0) || !(extent.Y > 0) || !(viewportSize.Width > 0) || !(viewportSize.Height > 0))
                return null;

            var scale = fill * Math.Min(viewportSize.Width / extent.X, viewportSize.Height / extent.Y);
            var subjectCenter = (minimum + maximum) / 2;

            return new SubjectFrame(((rotation % 360.0) + 360.0) % 360.0,
                                    scale,
                                    scale * (imageCenter.X - subjectCenter.X),
                                    scale * (imageCenter.Y - subjectCenter.Y));
        }

        /// <summary>
        /// Averages angles in a way that respects their wrapping around,
        /// so that the average of 350 and 10 degrees is 0 rather than 180.
        /// </summary>
        /// <returns>
        /// The average, in degrees, or 0 if there are no angles or they cancel each other out
        /// </returns>
        private static double GetAverageAngle(IEnumerable<double> angles)
        {
            var x = 0.0;
            var y = 0.0;
            foreach (var angle in angles)
            {
                var radians = angle * Math.PI / 180.0;
                x += Math.Cos(radians);
                y += Math.Sin(radians);
            }

            return Math.Sqrt((x * x) + (y * y)) < 1e-6
                ? 0
                : Math.Atan2(y, x) * 180.0 / Math.PI;
        }
    }
}
