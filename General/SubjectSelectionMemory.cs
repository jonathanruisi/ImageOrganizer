using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Windows.Foundation;

namespace ImageOrganizer
{
    /// <summary>
    /// A person in an image, as noted by a <see cref="SubjectSelectionMemory"/>.
    /// </summary>
    /// <param name="Appearance">
    /// What the person looks like, as produced by <see cref="HumanAppearanceEncoder"/>, which yields null if it could not be worked out
    /// </param>
    /// <param name="Bounds">Bounding box of the person, as fractions of the width and height of their image</param>
    /// <param name="IsSelected">Whether the person is selected</param>
    public readonly record struct RememberedSubject(Task<float[]?> Appearance, Rect Bounds, bool IsSelected);

    /// <summary>
    /// The outcome of a <see cref="SubjectSelectionMemory"/> looking for the selected people in an image.
    /// </summary>
    public sealed class SubjectRecognition
    {
        /// <summary>
        /// Gets the selected people who are in the image, as indexes within the people who were looked among.
        /// </summary>
        public IReadOnlySet<int> Selected { get; }

        /// <summary>
        /// Gets the selected people who are not in the image.
        /// </summary>
        internal IReadOnlyList<RememberedSubject> Missing { get; }

        internal SubjectRecognition(IReadOnlySet<int> selected, IReadOnlyList<RememberedSubject> missing)
        {
            Selected = selected;
            Missing = missing;
        }
    }

    /// <summary>
    /// Remembers which people are selected as one image after another is displayed, recognizing them
    /// by what they look like. A selected person who is absent from an image stays remembered,
    /// so that they are selected again in whichever later image they reappear in.
    /// </summary>
    public sealed class SubjectSelectionMemory
    {
        #region Fields
        // Everyone in the image that was last noted, and the selected people who were not in it
        private readonly List<RememberedSubject> _present = [];
        private readonly List<RememberedSubject> _absent = [];
        #endregion

        #region Properties
        /// <summary>
        /// Gets whether anyone at all is remembered as being selected.
        /// </summary>
        public bool HasSelection => IsSelectionPresent || _absent.Count > 0;

        /// <summary>
        /// Gets whether anyone in the image that was last noted is selected.
        /// </summary>
        public bool IsSelectionPresent => _present.Any(p => p.IsSelected);

        /// <summary>
        /// Gets the number of selected people who were not in the image that was last noted.
        /// </summary>
        public int AbsentCount => _absent.Count;
        #endregion

        #region Public Methods
        /// <summary>
        /// Notes the people in the image being displayed, in place of those of the image noted before.
        /// The selected people who are absent stay remembered.
        /// </summary>
        /// <remarks>
        /// Everyone in the image should be given, selected or not. The people who are
        /// not selected are how a selected person is later told apart from them.
        /// </remarks>
        public void SetPresent(IEnumerable<RememberedSubject> people)
        {
            // The new people are gathered first, in case they are derived from the current ones
            var newPeople = people.ToList();
            _present.Clear();
            _present.AddRange(newPeople);
        }

        /// <summary>
        /// Forgets the selected people who are not in the image being displayed.
        /// </summary>
        public void ForgetAbsent()
        {
            _absent.Clear();
        }

        /// <summary>
        /// Forgets everyone.
        /// </summary>
        public void Clear()
        {
            _present.Clear();
            _absent.Clear();
        }

        /// <summary>
        /// Looks for the selected people among the people in a newly displayed image. Nothing is remembered
        /// differently as a result, until the outcome is passed to <see cref="Accept(SubjectRecognition)"/>.
        /// </summary>
        /// <param name="people">Everyone in the newly displayed image</param>
        /// <param name="minimumSimilarity">How alike two people must look to be taken as the same person</param>
        public async Task<SubjectRecognition> RecognizeAsync(IReadOnlyList<SubjectSignature> people, float minimumSimilarity)
        {
            var remembered = _present.Concat(_absent).ToList();
            var appearances = await Task.WhenAll(remembered.Select(p => p.Appearance));

            // Someone whose appearance could not be worked out cannot be recognized, so is not remembered further
            var previous = new List<SubjectSignature>();
            var previousPeople = new List<RememberedSubject>();
            for (var i = 0; i < remembered.Count; i++)
            {
                if (appearances[i] is float[] appearance)
                {
                    previous.Add(new SubjectSignature(appearance, remembered[i].Bounds));
                    previousPeople.Add(remembered[i]);
                }
            }

            // Everyone is paired up, not only the selected people, so that a person who is not selected
            // is recognized as themselves instead of being mistaken for a selected person who is absent
            var pairs = HumanAppearanceEncoder.MatchSubjects(previous, people, minimumSimilarity);

            var selected = new HashSet<int>();
            var missing = new List<RememberedSubject>();
            for (var i = 0; i < previousPeople.Count; i++)
            {
                if (!previousPeople[i].IsSelected)
                    continue;

                if (pairs.TryGetValue(i, out var j))
                    selected.Add(j);
                else
                    missing.Add(previousPeople[i]);
            }

            return new SubjectRecognition(selected, missing);
        }

        /// <summary>
        /// Moves on to a newly displayed image, given the outcome of looking for the selected people in it.
        /// Those who were not found are remembered as absent, and the people of the image noted before are forgotten.
        /// The people in the new image then need to be noted with <see cref="SetPresent(IEnumerable{RememberedSubject})"/>.
        /// </summary>
        public void Accept(SubjectRecognition recognition)
        {
            // Some of them may have been forgotten in the time it took to look for them
            var missing = recognition.Missing.Where(p => _present.Contains(p) || _absent.Contains(p)).ToList();

            _present.Clear();
            _absent.Clear();
            _absent.AddRange(missing);
        }
        #endregion
    }
}
