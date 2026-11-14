using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;

using Windows.Foundation;

namespace ImageOrganizer
{
    /// <summary>
    /// A line across an image that marks what ought to be level, such as the ground under someone's feet.
    /// </summary>
    /// <param name="Start">The end of the line that belongs on the left, in the coordinate space of the image</param>
    /// <param name="End">The end of the line that belongs on the right, in the coordinate space of the image</param>
    public readonly record struct LevelLine(Vector2 Start, Vector2 End);

    /// <summary>
    /// The outcome of a <see cref="LevelLineMemory"/> looking for the level line in an image.
    /// </summary>
    public sealed class LevelLineSearch
    {
        /// <summary>
        /// Gets the level line as it lies in the image, or null if the image does not show the scene the line was drawn on.
        /// </summary>
        public LevelLine? Line { get; }

        internal Task<SceneFeatures?> Features { get; }

        internal IReadOnlyList<Rect> People { get; }

        internal LevelLineSearch(LevelLine? line, Task<SceneFeatures?> features, IReadOnlyList<Rect> people)
        {
            Line = line;
            Features = features;
            People = people;
        }
    }

    /// <summary>
    /// Remembers a level line as one image after another is displayed, so that it can be found in each of them.
    /// The line is found in an image by working out how the scene has moved since an image the line is known in.
    /// It stays remembered through images that show something else, and is found again once the scene is back.
    /// </summary>
    public sealed class LevelLineMemory
    {
        #region Constants
        // A line that comes out this many times longer or shorter than it was has not been carried over sensibly
        private const float MaximumLengthChange = 5f;
        #endregion

        #region Nested Types
        /// <summary>
        /// An image in which the level line is known.
        /// </summary>
        /// <param name="Features">The distinctive points of the image, which yields null if they could not be worked out</param>
        /// <param name="People">Bounding boxes of the people in the image, who are not part of the scene</param>
        /// <param name="Line">The level line as it lies in the image</param>
        private sealed record Sighting(Task<SceneFeatures?> Features, IReadOnlyList<Rect> People, LevelLine Line);
        #endregion

        #region Fields
        // The image the line was drawn on, and the image it was most recently found in
        private Sighting? _origin;
        private Sighting? _latest;
        #endregion

        #region Properties
        /// <summary>
        /// Gets whether a level line is remembered.
        /// </summary>
        public bool HasLine => _origin is not null;
        #endregion

        #region Public Methods
        /// <summary>
        /// Remembers a level line, in place of any that was remembered before.
        /// </summary>
        /// <param name="features">The distinctive points of the image the line is on</param>
        /// <param name="people">Bounding boxes of the people in the image, in its coordinate space</param>
        /// <param name="line">The level line</param>
        public void Set(Task<SceneFeatures?> features, IReadOnlyList<Rect> people, LevelLine line)
        {
            _origin = _latest = new Sighting(features, people, line);
        }

        public void Clear()
        {
            _origin = _latest = null;
        }

        /// <summary>
        /// Looks for the level line in a newly displayed image. Nothing is remembered differently
        /// as a result, until the outcome is passed to <see cref="Accept(LevelLineSearch)"/>.
        /// </summary>
        /// <param name="features">The distinctive points of the newly displayed image</param>
        /// <param name="people">Bounding boxes of the people in the image, in its coordinate space</param>
        public async Task<LevelLineSearch> LocateAsync(Task<SceneFeatures?> features, IReadOnlyList<Rect> people)
        {
            LevelLine? line = null;
            if (await features is { } target)
            {
                // The image the line was drawn on is the better guide, as the line is exactly known there.
                // The image the line was last found in is the fallback, for when the scene has since moved on.
                line = await LocateAsync(_origin, target, people);
                if (line is null && !ReferenceEquals(_latest, _origin))
                    line = await LocateAsync(_latest, target, people);
            }

            return new LevelLineSearch(line, features, people);
        }

        /// <summary>
        /// Notes that the level line was found in a newly displayed image, given the outcome of looking for it there.
        /// </summary>
        public void Accept(LevelLineSearch search)
        {
            if (_origin is not null && search.Line is { } line)
                _latest = new Sighting(search.Features, search.People, line);
        }
        #endregion

        #region Private Methods
        private static async Task<LevelLine?> LocateAsync(Sighting? sighting, SceneFeatures target, IReadOnlyList<Rect> people)
        {
            if (sighting is null || await sighting.Features is not { } source)
                return null;

            var transform = await Task.Run(() => SceneAlignment.Align(source, target, sighting.People, people));
            if (transform?.Map(sighting.Line.Start) is not { } start || transform.Map(sighting.Line.End) is not { } end)
                return null;

            var lengthChange = Vector2.Distance(start, end) / Vector2.Distance(sighting.Line.Start, sighting.Line.End);
            if (!(lengthChange >= 1 / MaximumLengthChange) || !(lengthChange <= MaximumLengthChange))
                return null;

            return new LevelLine(start, end);
        }
        #endregion
    }
}
