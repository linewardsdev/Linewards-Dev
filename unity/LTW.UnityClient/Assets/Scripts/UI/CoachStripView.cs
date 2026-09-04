#nullable enable

using UnityEngine;
using UnityEngine.UIElements;

namespace LTW.UnityClient.UI
{
    /// <summary>
    /// The slim coaching strip that rides over the board during a practice match.
    /// </summary>
    /// <remarks>
    /// Not a shell screen. A screen owns the display; this shares it with a live board, so it has
    /// no field behind it, it never gates input, and every container in it ignores picks — only
    /// NEXT and SKIP take a pointer, so a tap in the strip's margin still lands on the board.
    ///
    /// A plain class over elements <see cref="ShellScreenView"/> already owns, driven by the
    /// tutorial director through <see cref="Show"/> and <see cref="Hide"/> and reporting taps back
    /// through the two events. It reads nothing from the simulation and holds no step state of its
    /// own beyond what it was last told to draw, which is what keeps the director the single owner
    /// of where the practice match is — the same one-way dependency the shell screens keep with
    /// the overlay.
    /// </remarks>
    internal sealed class CoachStripView
    {
        private readonly VisualElement? strip;
        private readonly Label? eyebrow;
        private readonly Label? body;
        private readonly VisualElement? dots;
        private readonly Button? next;
        private readonly Button? skip;

        private VisualElement[] dotElements = System.Array.Empty<VisualElement>();

        internal CoachStripView(VisualElement root)
        {
            strip = root.Q<VisualElement>("coach-strip");
            eyebrow = root.Q<Label>("coach-eyebrow");
            body = root.Q<Label>("coach-body");
            dots = root.Q<VisualElement>("coach-dots");
            next = root.Q<Button>("coach-next");
            skip = root.Q<Button>("coach-skip");

            if (strip is null || eyebrow is null || body is null || dots is null || next is null || skip is null)
            {
                Debug.LogError("COACH the shell document did not contain the expected coach-strip elements.");
                return;
            }

            next.clicked += () => NextRequested?.Invoke();
            skip.clicked += () => SkipRequested?.Invoke();
        }

        /// <summary>Raised when the player taps NEXT on a step that showed one.</summary>
        internal event System.Action? NextRequested;

        /// <summary>Raised when the player taps SKIP. What skipping means is the director's call.</summary>
        internal event System.Action? SkipRequested;

        /// <summary>Whether the strip is currently up.</summary>
        internal bool IsVisible => strip != null && strip.ClassListContains("is-shown");

        /// <summary>
        /// Puts the strip up, or updates it in place if it already is.
        /// </summary>
        /// <param name="eyebrow">The small line, e.g. "PRACTICE · STEP 2 OF 5".</param>
        /// <param name="body">One line of instruction.</param>
        /// <param name="stepIndex">Zero-based index of the current step; dots before it read as done.</param>
        /// <param name="stepCount">How many dots to draw.</param>
        /// <param name="showNext">Whether this step advances on a tap of NEXT rather than on play.</param>
        /// <remarks>
        /// Idempotent per visibility: calling this every frame while the strip is up only rewrites
        /// text and classes, and does not restart the enter transition. The dots are rebuilt only
        /// when the count changes, so a per-frame call costs nothing measurable.
        /// </remarks>
        internal void Show(string eyebrow, string body, int stepIndex, int stepCount, bool showNext)
        {
            if (strip is null)
            {
                return;
            }

            if (this.eyebrow != null)
            {
                this.eyebrow.text = eyebrow;
            }

            if (this.body != null)
            {
                this.body.text = body;
            }

            next?.EnableInClassList("is-hidden", !showNext);
            RenderDots(stepIndex, stepCount);

            if (IsVisible)
            {
                return;
            }

            // Same deferral as ShellScreenView.Enter, for the same reason: an element that was
            // display:none has no resolved opacity to transition from, so both classes in one
            // frame would snap the strip in rather than lift it.
            strip.AddToClassList("is-shown");
            strip.schedule.Execute(() => strip.AddToClassList("is-entered")).ExecuteLater(1);
        }

        internal void Hide()
        {
            if (strip is null)
            {
                return;
            }

            strip.RemoveFromClassList("is-shown");
            strip.RemoveFromClassList("is-entered");
        }

        private void RenderDots(int stepIndex, int stepCount)
        {
            if (dots is null)
            {
                return;
            }

            var count = Mathf.Max(0, stepCount);
            if (dotElements.Length != count)
            {
                dots.Clear();
                dotElements = new VisualElement[count];
                for (var slot = 0; slot < count; slot++)
                {
                    var dot = new VisualElement { name = $"coach-dot-{slot}", pickingMode = PickingMode.Ignore };
                    dot.AddToClassList("ltw-coach__dot");
                    dots.Add(dot);
                    dotElements[slot] = dot;
                }
            }

            for (var slot = 0; slot < count; slot++)
            {
                dotElements[slot].EnableInClassList("ltw-coach__dot--done", slot < stepIndex);
                dotElements[slot].EnableInClassList("ltw-coach__dot--current", slot == stepIndex);
            }
        }
    }
}
