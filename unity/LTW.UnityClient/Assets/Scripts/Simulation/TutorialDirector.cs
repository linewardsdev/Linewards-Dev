#nullable enable

using System;
using LTW.Simulation.Economy;
using LTW.Simulation.Events;
using LTW.UnityClient.UI;
using UnityEngine;

namespace LTW.UnityClient.Simulation
{
    /// <summary>
    /// Walks a first-time player through one practice match, one coach-strip step at a time.
    /// </summary>
    /// <remarks>
    /// A plain class rather than a MonoBehaviour, and owned by <see cref="LocalSessionFlowOverlay"/>,
    /// which ticks it from its own Update. The overlay is the single owner of session state
    /// (docs/GAME_MENU_AND_RUNTIME_FLOW.md), and the tutorial is session state — it has to know
    /// when practice starts, when the player leaves, and when a shell screen is up. Giving it its
    /// own Update would put a second component in charge of "what is the session doing", which is
    /// the drift the flow doc exists to prevent.
    ///
    /// It only ever READS the simulation, through the driver's snapshot, events and route lengths.
    /// It never places, sends, pauses or spends: a tutorial that nudged the board to make its own
    /// steps come true would break principle 3 (menus do not mutate simulation state) in the one
    /// place a new player is least equipped to notice.
    ///
    /// Every predicate is evaluated on the frame path, so nothing below allocates per tick: the
    /// step table is static, the snapshot lists are indexed rather than enumerated through
    /// LINQ, and the copy is only pushed to the strip on a transition, never re-sent each frame.
    /// </remarks>
    internal sealed class TutorialDirector : IDisposable
    {
        /// <summary>Wards the BUILD step asks for. Three is enough to bend a route without sealing it.</summary>
        private const int BuildTarget = 3;

        /// <summary>
        /// How many cells longer than the fly-over route counts as "bent". One or two extra cells
        /// happen by accident when a single ward sits on the direct line; a real detour costs more.
        /// </summary>
        private const int MazeMargin = 2;

        /// <summary>The SURVIVE step dismisses itself after this long so the strip cannot outstay its welcome.</summary>
        private const float SurviveHoldSeconds = 6f;

        private readonly struct StepCopy
        {
            public StepCopy(string eyebrow, string body, bool showNext)
            {
                Eyebrow = eyebrow;
                Body = body;
                ShowNext = showNext;
            }

            public string Eyebrow { get; }

            public string Body { get; }

            public bool ShowNext { get; }
        }

        private static readonly StepCopy[] Steps =
        {
            new StepCopy("BUILD", "Tap BUILD and place three wards on your platforms.", showNext: false),
            new StepCopy("MAZE", "See the path bend? Creeps take the long way round. Build across it — but never seal it.", showNext: true),
            new StepCopy("SEND", "Sends unlock when LIVE begins. Tap SEND and send a Runner — it attacks the next lane and raises your income.", showNext: false),
            new StepCopy("GROW", "Income just paid out. Tiers unlock on income — open a line and buy Tier 2 when it lights up.", showNext: true),
            new StepCopy("SURVIVE", "40 lives. A leak costs one. You're set — this match keeps going as practice; PRACTICE on the title brings you back any time.", showNext: true)
        };

        private const int BuildStep = 0;
        private const int MazeStep = 1;
        private const int SendStep = 2;
        private const int GrowStep = 3;
        private const int SurviveStep = 4;

        private readonly UnitySimulationDriver driver;
        private readonly CoachStripView strip;

        private bool active;
        private int stepIndex;
        private int startingIncome;
        private float surviveShownAt;

        public TutorialDirector(UnitySimulationDriver simulationDriver, CoachStripView coachStrip)
        {
            driver = simulationDriver;
            strip = coachStrip;
            strip.NextRequested += OnNextRequested;
            strip.SkipRequested += OnSkipRequested;
        }

        /// <summary>Whether a practice walkthrough is in progress — steps left and neither finished nor skipped.</summary>
        public bool IsActive => active;

        /// <summary>Whether the strip is on screen right now, so IMGUI panels can keep out from under it.</summary>
        public bool StripVisible => active && strip.IsVisible;

        /// <summary>Starts from the BUILD step against the match the driver holds right now.</summary>
        /// <remarks>
        /// Reads the starting income here rather than assuming 10: the SEND step's fallback
        /// predicate is "income rose", and the number it rose from belongs to the simulation.
        /// </remarks>
        public void Begin()
        {
            active = true;
            stepIndex = BuildStep;
            startingIncome = LocalIncome();
            surviveShownAt = 0f;
            ShowCurrentStep();
        }

        /// <summary>
        /// Abandons the walkthrough without marking the tutorial seen — the player left, they did
        /// not finish. Used when practice ends by reset, rematch or exit to title.
        /// </summary>
        public void Stop()
        {
            if (!active)
            {
                return;
            }

            active = false;
            strip.Hide();
        }

        /// <summary>
        /// One frame of the walkthrough.
        /// </summary>
        /// <param name="suppressed">
        /// True while a session panel owns the display — pause, results, settings. The strip hides
        /// underneath those and comes back when they close; the step itself is not lost.
        /// </param>
        /// <remarks>
        /// The build countdown is NOT suppressed, on purpose: it is playable (see the overlay's
        /// OwnsDisplay remark) and the BUILD and MAZE steps are exactly what the player should be
        /// doing during it.
        /// </remarks>
        public void Tick(bool suppressed)
        {
            if (!active)
            {
                return;
            }

            if (suppressed)
            {
                if (strip.IsVisible)
                {
                    strip.Hide();
                }

                return;
            }

            if (!strip.IsVisible)
            {
                ShowCurrentStep();
            }

            if (CurrentStepComplete())
            {
                Advance();
            }
        }

        public void Dispose()
        {
            strip.NextRequested -= OnNextRequested;
            strip.SkipRequested -= OnSkipRequested;
        }

        private void OnNextRequested()
        {
            if (active)
            {
                Advance();
            }
        }

        private void OnSkipRequested()
        {
            if (active)
            {
                Finish();
            }
        }

        private void Advance()
        {
            stepIndex++;
            if (stepIndex >= Steps.Length)
            {
                Finish();
                return;
            }

            ShowCurrentStep();
        }

        /// <summary>
        /// Completion and SKIP end the same way: the strip goes, the tutorial is marked seen, and
        /// the match carries on with its passive bots. Skipping never ends the match — a player who
        /// dismissed the coaching still wanted to play.
        /// </summary>
        private void Finish()
        {
            active = false;
            PresentationPreferences.TutorialSeen = true;
            strip.Hide();
        }

        private void ShowCurrentStep()
        {
            var step = Steps[stepIndex];
            strip.Show(step.Eyebrow, step.Body, stepIndex, Steps.Length, step.ShowNext);
            if (stepIndex == SurviveStep)
            {
                // Unscaled, like the driver's own opening countdown: the strip is presentation and
                // should not stretch if something slows the game clock.
                surviveShownAt = Time.unscaledTime;
            }
        }

        private bool CurrentStepComplete()
        {
            switch (stepIndex)
            {
                case BuildStep:
                    return LocalTowerCount() >= BuildTarget;
                case MazeStep:
                    var lane = driver.LocalPlayerLaneId;
                    return driver.RouteLength(lane) > driver.DirectRouteLength(lane) + MazeMargin;
                case SendStep:
                    return LocalSendQueuedThisFrame() || LocalIncome() > startingIncome;
                case GrowStep:
                    return LocalHasSecondTier();
                case SurviveStep:
                    return Time.unscaledTime - surviveShownAt >= SurviveHoldSeconds;
                default:
                    return false;
            }
        }

        private int LocalTowerCount()
        {
            var snapshot = driver.LatestSnapshot;
            if (snapshot is null)
            {
                return 0;
            }

            var owner = driver.LocalPlayerId;
            var lane = driver.LocalPlayerLaneId;
            var towers = snapshot.Towers;
            var count = 0;
            for (var index = 0; index < towers.Count; index++)
            {
                var tower = towers[index];
                if (tower.OwnerId.Equals(owner) && tower.LaneId.Equals(lane))
                {
                    count++;
                }
            }

            return count;
        }

        /// <summary>
        /// Whether a send from the local seat was accepted this frame.
        /// </summary>
        /// <remarks>
        /// <see cref="CreepQueuedEvent"/> rather than <see cref="CreepSpawnedEvent"/>: queued is the
        /// moment the player's tap was accepted, which is what the step is teaching. Spawned comes
        /// later and would leave the strip sitting on SEND after the player had already done it.
        /// </remarks>
        private bool LocalSendQueuedThisFrame()
        {
            var events = driver.LatestEvents;
            var local = driver.LocalPlayerId;
            for (var index = 0; index < events.Count; index++)
            {
                if (events[index] is CreepQueuedEvent queued && queued.SenderId.Equals(local))
                {
                    return true;
                }
            }

            return false;
        }

        private bool LocalHasSecondTier()
        {
            var seat = LocalSeat();
            if (seat is null)
            {
                return false;
            }

            for (var category = 0; category < PlayerEconomyState.CategoryCount; category++)
            {
                if (seat.TowerLineTier(category) > PlayerEconomyState.BaseTier || seat.SendCategoryTier(category) > PlayerEconomyState.BaseTier)
                {
                    return true;
                }
            }

            return false;
        }

        private int LocalIncome() => LocalSeat()?.Income.Amount ?? 0;

        private PlayerEconomyState? LocalSeat()
        {
            var snapshot = driver.LatestSnapshot;
            return snapshot is null ? null : snapshot.Players.Get(driver.LocalPlayerId);
        }
    }
}
