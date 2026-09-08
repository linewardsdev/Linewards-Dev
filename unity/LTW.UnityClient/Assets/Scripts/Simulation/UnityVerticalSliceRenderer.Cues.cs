using System;
using System.Collections.Generic;
using LTW.Simulation.Combat;
using LTW.Simulation.Events;
using LTW.Simulation.Primitives;
using LTW.UnityClient.UI;
using UnityEngine;

namespace LTW.UnityClient.Simulation
{
    /// <summary>
    /// Event feedback: the board labels and the per-event cues (attack, hit, death, leak,
    /// income, send) that <see cref="RenderEvents"/> fires.
    /// </summary>
    public sealed partial class UnityVerticalSliceRenderer
    {
        /// <summary>
        /// Sorting order for world-space floating text. Kept above every board decoration
        /// SpriteRenderer so send banners and damage numbers are never covered by board furniture.
        /// </summary>
        private const int FloatingTextSortingOrder = 100;

        /// <summary>World units a board label rises over its life.</summary>
        private const float FloatingTextRise = 0.5f;

        /// <summary>World units a board label starts above the event it marks.</summary>
        private const float FloatingTextLift = 0.55f;

        /// <summary>
        /// TMP point size every board label draws at, before <see cref="PresentationPreferences.TextScale"/>.
        /// One number for every kind: "+441 income" is a longer string than "+4", not a bigger one.
        /// </summary>
        private const float BoardLabelFontSize = 3.4f;

        /// <summary>
        /// Horizontal distance, in world units, within which two labels count as being in the same
        /// place — the reach of both stacking and merging.
        /// </summary>
        /// <remarks>
        /// 0.6 is a little over half a cell. A leak puts its "-1 LIFE" and its "+N" bounty at the
        /// same creep, and a kill puts its bounty at the creep while the relay tower that got the
        /// last hit puts its "+1" a cell away; the first pair should share a column, the second
        /// should not.
        /// </remarks>
        private const float FloatingTextNeighbourRadius = 0.6f;

        /// <summary>Vertical step between labels stacked over one spot.</summary>
        /// <remarks>
        /// Raised from 0.35 (OPEN_ITEMS.md item 53): a leak's "-N LIVES" and its bounty "+N" still
        /// overlapped at one step — confirmed in a fresh `37-leak-row-labels-seq` capture, "+25"
        /// sitting close enough above "-5 LIVES" to read as one smear rather than two numbers, the
        /// same shape of defect the 2026-09-01 review already found and this step was raised once
        /// for. One step is a touch under half of BoardLabelFontSize's actual rendered line height
        /// at PresentationPreferences.TextScale 1, measured directly from that capture rather than
        /// assumed from the TMP point size, which is not a reliable proxy for a custom font asset's
        /// real glyph metrics.
        /// </remarks>
        private const float FloatingTextStackStep = 0.55f;

        /// <summary>Most steps a stacked label can be lifted by. Beyond four it is off the cell.</summary>
        private const int FloatingTextStackCap = 4;

        /// <summary>
        /// Seconds after a label spawns during which an identical-kind label at the same spot is
        /// folded into it rather than drawn beside it.
        /// </summary>
        /// <remarks>
        /// Short on purpose: it is for events the simulation raised on the same tick — two creeps
        /// leaking together, a bounty and a refund landing at once — not for aggregating a stream.
        /// At 4 ticks a second, 0.2s is less than one tick, so two labels from consecutive ticks
        /// still draw as two. <see cref="BoardLabelKind.Income"/> is the exception and merges for
        /// as long as the earlier label is alive, which is what keeps it to one per lane.
        /// </remarks>
        private const float FloatingTextMergeWindow = 0.2f;

        /// <summary>
        /// Most board labels alive at once. Beyond this, new ones are dropped.
        /// </summary>
        /// <remarks>
        /// TextMeshPro regenerates a mesh every time a label's text is assigned, which is far more
        /// expensive than the legacy TextMesh this replaced — measured on the batch playtest, which
        /// runs the simulation at roughly 300x: 183s with board text off against a >420s timeout with
        /// it on, so the labels alone cost more than the entire rest of the match.
        ///
        /// That ratio is an artefact of the harness rather than of play — at the shipped 4 ticks a
        /// second the same match produces labels at 1/300th the rate — but "only slow in the
        /// harness" is not a property worth relying on, because it is also what a real device sees
        /// under heavy pressure with eight lanes leaking at once.
        ///
        /// A cap rather than a cheaper label: 24 is far above anything a player can read in the
        /// half-second a label lives, so in normal play nothing is ever dropped, while the worst
        /// case stays bounded instead of scaling with how much is happening off screen.
        /// </remarks>
        private const int MaxLiveFloatingLabels = 24;

        /// <summary>Board labels currently animating. See SpawnBoardLabel and UpdateFloatingLabels.</summary>
        private readonly List<FloatingLabel> floatingLabels = new List<FloatingLabel>();

        /// <summary>
        /// What a board label is about. Decides whether two labels landing on the same spot fold
        /// into one, and how the folded amount is written.
        /// </summary>
        /// <remarks>
        /// Passed by the caller, never recovered from the text: "+4" is a bounty and "+4 LIFE" is a
        /// stolen life, and the only thing that knows which is the event that raised it. Parsing
        /// the string back would work until the next wording change, silently.
        /// </remarks>
        private enum BoardLabelKind
        {
            /// <summary>Free text that never merges: the elimination and victory banners, and the reduced-effects cue words.</summary>
            Text,

            /// <summary>"+N" gold — kill bounty, leak bounty, sell refund, relay earnings.</summary>
            Gold,

            /// <summary>"+N income" at the lane's income point. At most one alive per lane.</summary>
            Income,

            /// <summary>"-N LIFE" / "-N LIVES" at the leak.</summary>
            LifeLost,

            /// <summary>"+N LIFE" / "+N LIVES" on the lane of the seat that gained them.</summary>
            LifeStolen,

            /// <summary>"SEND" at the local player's own gate. Merges to keep one banner under a burst of taps.</summary>
            Send,
        }

        /// <summary>A free-text board label: banners, cue words, SEND.</summary>
        private void SpawnFloatingText(Vector3 position, BoardLabelKind kind, string text, Color color, float duration) =>
            SpawnBoardLabel(position, kind, 0, text, color, duration);

        /// <summary>A board label for a number: gold, income, lives. The kind decides the wording.</summary>
        private void SpawnFloatingAmount(Vector3 position, BoardLabelKind kind, int amount, Color color, float duration) =>
            SpawnBoardLabel(position, kind, amount, null, color, duration);

        /// <summary>Wording for an amount label, including the aggregate after a merge.</summary>
        private static string BoardLabelText(BoardLabelKind kind, int amount, string text) => kind switch
        {
            BoardLabelKind.Gold => $"+{amount}",
            BoardLabelKind.Income => $"+{amount} income",
            BoardLabelKind.LifeLost => amount == 1 ? "-1 LIFE" : $"-{amount} LIVES",
            BoardLabelKind.LifeStolen => amount == 1 ? "+1 LIFE" : $"+{amount} LIVES",
            _ => text,
        };

        /// <summary>
        /// Whether two labels of this kind at one spot fold into one. Free text never does: "PLAYER
        /// 3 OUT" and "PLAYER 5 OUT" are two facts, and a cue word repeated is still one word.
        /// </summary>
        private static bool BoardLabelMerges(BoardLabelKind kind) => kind != BoardLabelKind.Text;

        /// <summary>How long after spawning a label of this kind still accepts a merge.</summary>
        private static float BoardLabelMergeWindow(BoardLabelKind kind) =>
            kind == BoardLabelKind.Income ? float.PositiveInfinity : FloatingTextMergeWindow;

        /// <summary>
        /// Orientation that presents flat world-space text square-on to the gameplay camera.
        /// Falls back to the old board-flat orientation only if no camera is resolvable.
        /// </summary>
        private Quaternion FloatingTextRotation()
        {
            var camera = presentationCamera != null ? presentationCamera : Camera.main;
            return camera != null ? camera.transform.rotation : Quaternion.Euler(90f, 0f, 0f);
        }

        /// <summary>Distance across the board between two points, ignoring how high each sits.</summary>
        /// <remarks>
        /// Height is ignored because stacking puts height in: a third label over a cell has to be
        /// measured against the anchors of the two already lifted there, not against where they
        /// have risen to.
        /// </remarks>
        private static float PlanarDistance(Vector3 a, Vector3 b)
        {
            var dx = a.x - b.x;
            var dz = a.z - b.z;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        /// <summary>
        /// A board label: SDF text with an outline, rising and fading over its life.
        /// </summary>
        /// <remarks>
        /// Was a legacy TextMesh that never assigned a font, so every label on the board rendered in
        /// Unity's built-in Arial — unlit, flat, no outline, over a busy board. That is the "blocky
        /// and plain" read, and the improvement cycle scores it as C10 Typography: "default engine
        /// font". TextMeshPro is used for the SDF rendering rather than for the typeface: it stays
        /// crisp at any distance and gives a real outline, which is what makes small text legible
        /// against the board instead of dissolving into it.
        ///
        /// Motion carries the rest. Text used to appear, hold and vanish, which reads as a label
        /// switching on. It now rises, fades out, and punches up in scale over its first frames, so
        /// it reads as an event that happened.
        ///
        /// Layout is the other half, added 2026-09-01 after the live render review found labels
        /// colliding: a leak drew "-1 LIFE", "+4" and a cue word in one cell on top of each other,
        /// and two creeps leaking on the same tick drew two "-1 LIFE" through each other. Two rules,
        /// both scoped to <see cref="FloatingTextNeighbourRadius"/> of a label that is still alive:
        ///
        /// Merge first. Same kind, same spot, within <see cref="BoardLabelMergeWindow"/> of the
        /// earlier label's spawn — the earlier label's amount absorbs the new one, its text is
        /// rewritten as the aggregate ("-2 LIVES", "+7"), and its clock restarts so the scale punch
        /// replays as the number ticks up. Nothing new is spawned, so this also does not count
        /// against <see cref="MaxLiveFloatingLabels"/>.
        ///
        /// Otherwise stack. Every live label within reach lifts the new one by
        /// <see cref="FloatingTextStackStep"/>, capped at <see cref="FloatingTextStackCap"/> steps,
        /// so simultaneous labels at one cell read as a column rather than a pile. Stacking has no
        /// kind test — a cue word and a bounty at the same creep still want separate lines.
        /// </remarks>
        private void SpawnBoardLabel(Vector3 position, BoardLabelKind kind, int amount, string text, Color color, float duration)
        {
            if (!IsOnActiveLane(position))
            {
                return;
            }

            if (BoardLabelMerges(kind))
            {
                var window = BoardLabelMergeWindow(kind);
                for (var index = 0; index < floatingLabels.Count; index++)
                {
                    var existing = floatingLabels[index];
                    if (existing.Kind != kind || existing.Label == null || existing.Object == null)
                    {
                        continue;
                    }

                    if (Time.time - existing.SpawnedAt > window || PlanarDistance(existing.Anchor, position) > FloatingTextNeighbourRadius)
                    {
                        continue;
                    }

                    existing.Absorb(amount, BoardLabelText(kind, existing.Amount + amount, text), Time.time, duration);
                    ExtendTimedPresentation(existing.Object, existing.SpawnedAt + existing.Duration);
                    return;
                }
            }

            // Dropped rather than queued: a label that cannot be shown now is worthless a second
            // later, and queueing would keep the cost while losing the timing.
            if (floatingLabels.Count >= MaxLiveFloatingLabels)
            {
                return;
            }

            var neighbours = 0;
            for (var index = 0; index < floatingLabels.Count; index++)
            {
                if (PlanarDistance(floatingLabels[index].Anchor, position) <= FloatingTextNeighbourRadius)
                {
                    neighbours++;
                }
            }

            var stackLift = Mathf.Min(neighbours, FloatingTextStackCap) * FloatingTextStackStep;

            var textObject = GetTextObject();
            textObject.transform.position = position + Vector3.up * (FloatingTextLift + stackLift);
            // Face the camera rather than lying flat on the board. The old fixed Euler(90,0,0) was
            // only legible because the camera was nearly straight down; at any real tilt the text
            // slants away and loses readability. Billboarding keeps it face-on at any camera angle.
            textObject.transform.rotation = FloatingTextRotation();
            textObject.transform.localScale = Vector3.one;

            var label = textObject.GetComponent<TMPro.TextMeshPro>();
            if (label == null)
            {
                label = textObject.AddComponent<TMPro.TextMeshPro>();
                label.alignment = TMPro.TextAlignmentOptions.Center;
                label.textWrappingMode = TMPro.TextWrappingModes.NoWrap;
                label.raycastTarget = false;
                // One typeface and one outlined material for every label, resolved in one place
                // (RuntimeUiChrome, beside the HUD's SharedFont — same LiberationSans family, so
                // board and HUD text agree). Font first, then material: assigning `font` resets
                // the component's material to the font's plain default, so the outlined one has
                // to land after it or the outline silently disappears. See
                // RuntimeUiChrome.SharedBoardTextMaterial for why it is a shared material and not
                // a per-label property.
                label.font = RuntimeUiChrome.SharedBoardFont;
                var material = RuntimeUiChrome.SharedBoardTextMaterial;
                if (material != null)
                {
                    label.fontSharedMaterial = material;
                }
            }

            label.text = BoardLabelText(kind, amount, text);
            label.color = color;
            label.fontSize = BoardLabelFontSize * PresentationPreferences.TextScale;

            // Board furniture such as the endpoint gate plates draws through SpriteRenderers with
            // sorting orders up to 3, so board text sorts above all board decoration. Depth against
            // units is handled by the shared material (ZTest Always, queue 3100 — see
            // RuntimeUiChrome.SharedBoardTextMaterial), not here: sortingOrder orders labels among
            // their transparent-queue neighbours, it cannot stop a creep model cutting them.
            var textRenderer = textObject.GetComponent<MeshRenderer>();
            if (textRenderer != null)
            {
                textRenderer.sortingOrder = FloatingTextSortingOrder;
            }

            floatingLabels.Add(new FloatingLabel(textObject, label, kind, amount, position, Time.time, duration));
            timedPresentations.Add(new TimedPresentation(textObject, Time.time + duration, textPool));
        }

        /// <summary>Pushes back the pool release of an object already in <see cref="timedPresentations"/>.</summary>
        /// <remarks>
        /// A merged label restarts its clock, and its pooled text object has to outlive the new
        /// clock or the pool hands it to the next label while this one is still writing to it.
        /// A linear scan: the list also holds every live beam, but a merge only happens when two
        /// events land on one spot in a fifth of a second, which is rare enough that indexing the
        /// list for it would cost more than it saves.
        /// </remarks>
        private void ExtendTimedPresentation(GameObject target, float releaseAt)
        {
            for (var index = 0; index < timedPresentations.Count; index++)
            {
                var presentation = timedPresentations[index];
                if (!ReferenceEquals(presentation.Object, target))
                {
                    continue;
                }

                if (releaseAt > presentation.ReleaseAt)
                {
                    timedPresentations[index] = new TimedPresentation(target, releaseAt, presentation.Pool);
                }

                return;
            }
        }

        /// <summary>
        /// Whether a world position is in the lane the camera is actually framing.
        /// </summary>
        /// <remarks>
        /// Board text was drawn for all eight lanes while the camera frames one, so roughly seven
        /// eighths of every label spawned was instantiated, positioned, billboarded, sorted and
        /// pooled for a lane nobody could see. Measured at 36 text objects a second across a match.
        /// Costs nothing in design to skip: the player is looking at their own lane.
        /// </remarks>
        private bool IsOnActiveLane(Vector3 position)
        {
            if (cameraFraming != LaneCameraFraming.ActiveLane)
            {
                return true;
            }

            var laneCentreX = LaneOffset(ActiveLaneCameraId) + BoardCenterX;
            return Mathf.Abs(position.x - laneCentreX) <= LaneSpacing * 0.5f;
        }

        /// <summary>Whether this is the seat the client is playing.</summary>
        /// <remarks>
        /// The companion to <see cref="IsOnActiveLane"/>: that one asks whether a cue is somewhere
        /// the player can see, this one whether it is about something the player can answer. Most
        /// per-seat events fire once for every seat on the same tick, so without this the board
        /// spends seven eighths of its cues narrating other people's matches.
        ///
        /// <c>!= null</c> rather than <c>?.</c> — simulationDriver is a UnityEngine.Object and only
        /// the overloaded comparison treats a destroyed one as null; the null-conditional operator
        /// sails straight past it. With no driver this returns false, which stands a cue down
        /// rather than showing every seat's.
        /// </remarks>
        private bool IsLocalSeat(PlayerId playerId) =>
            simulationDriver != null && playerId.Equals(simulationDriver.LocalPlayerId);

        /// <summary>Rises, fades and punches in scale over its life. See SpawnBoardLabel.</summary>
        private void UpdateFloatingLabels()
        {
            for (var index = floatingLabels.Count - 1; index >= 0; index--)
            {
                var entry = floatingLabels[index];
                if (entry.Object == null || entry.Label == null)
                {
                    floatingLabels.RemoveAt(index);
                    continue;
                }

                var age = (Time.time - entry.SpawnedAt) / Mathf.Max(0.01f, entry.Duration);
                if (age >= 1f)
                {
                    floatingLabels.RemoveAt(index);
                    continue;
                }

                entry.Object.transform.position = entry.Origin + Vector3.up * (age * FloatingTextRise);
                // Held solid for the first half, then faded, so a short label is legible for most of
                // its life instead of being half-transparent the whole way.
                var alpha = age < 0.5f ? 1f : 1f - (age - 0.5f) * 2f;
                var colour = entry.Label.color;
                entry.Label.color = new Color(colour.r, colour.g, colour.b, alpha);
                // A quick overshoot on arrival, settling to 1.
                var punch = age < 0.18f ? Mathf.Lerp(0.72f, 1.06f, age / 0.18f) : Mathf.Lerp(1.06f, 1f, Mathf.InverseLerp(0.18f, 0.34f, age));
                entry.Object.transform.localScale = Vector3.one * Mathf.Min(punch, 1.06f);
            }
        }

        private static void TriggerHapticFeedback()
        {
            if (!PresentationPreferences.ReducedEffects)
            {
#if UNITY_IOS || UNITY_ANDROID
                Handheld.Vibrate();
#endif
            }
        }

        /// <summary>Marks the gate cell a creep has just walked into.</summary>
        /// <remarks>
        /// The cell pulse is the whole cue (a ring since Wave 5, a beam square before that — see
        /// SpawnCellFrameCue). It used to also draw two beams across the diagonals, and a
        /// square outline with an X through it is the single most recognisable "this asset failed
        /// to load" glyph there is — it was reported as one. The diagonals reached ±0.54 against a
        /// cell about a unit across, so the X overhung the frame it was drawn in and read as a
        /// symbol stamped on the board rather than as anything happening in the world.
        ///
        /// Nothing is lost by dropping them: the caller already raises a <c>BurstShape.Rise</c> at
        /// the same position, which is the part that reads as an arrival, and the frame still says
        /// which cell. Two overlapping tells for one event, one of which looked like an error, is
        /// how the board ended up feeling marked up.
        /// </remarks>
        private void SpawnCreepArrivalCue(int laneId, Color color) =>
            SpawnCellFrameCue(SpawnPosition(laneId), color, 0.22f);

        /// <summary>
        /// All the offsets below are expressed relative to the tower's base — the same numeric
        /// vectors that used to be added to <paramref name="towerPosition"/> directly. When a live
        /// Body transform is available they instead go through <paramref name="bodyTransform"/>'s
        /// TransformPoint, so the whole attack cue rotates and drifts with the tower's actual
        /// current animated state (aim rotation, idle drift, recoil) rather than assuming the tower
        /// still sits at its rest pose. Falls back to the old flat world-space offset if the tower
        /// GameObject could not be resolved (e.g. it was removed the same frame).
        /// </summary>
        /// <remarks>
        /// Wave 5 (finding #8 / R4, render review 2026-09-01, re-judged 2026-09-02). The frames
        /// showed every ranged shot as a one-frame straight line whatever its width, colour or
        /// duration — a 0.1s beam sampled at 0.1s IS a line — so every branch here was re-read
        /// and sorted one of two ways. Anything that was an instantaneous straight SpawnBeam from
        /// muzzle to target is now a <see cref="SpawnProjectile"/> dart with a trail: Arrow,
        /// Control, Relay, Prism, Repair Drone, Spore/Bloomheart, Sapling and the line tail here,
        /// Gatling and Barricade inside their own helpers. Anything already shaped and
        /// non-instant is kept as it was: Tesla's forked arc, the Thorn/Canopy vine lashes,
        /// Pulse's and Control's expanding rings, every muzzle burst.
        ///
        /// Also gone: the short beam pairs that crossed at the tower body (Arrow's limb and
        /// string, Relay's two body strokes, Prism's head cross, Pulse's four spokes) — from
        /// above they were an X on the tower every frame — and every SpawnCellFrameCue on a hit
        /// cell, which was the square bracket. The hit's impact is raised by the dart when it
        /// lands (UpdateProjectiles), not here, so it arrives with the shot.
        /// </remarks>
        private void SpawnTowerAttackCue(Vector3 towerPosition, Vector3 hitPosition, string towerId, int damage, Transform bodyTransform, int tier = 1)
        {
            Vector3 At(Vector3 localOffset) =>
                bodyTransform != null ? bodyTransform.TransformPoint(localOffset) : towerPosition + localOffset;

            lastAttackCueLaunchedProjectile = false;

            var shotColor = TowerShotColor(towerId, damage);
            var muzzle = At(Vector3.up * 0.62f);
            // The five bespoke branches below are all Arcane, and take the same thin, bright, quick
            // grammar as every other Arcane tower — they keep their own choreography and colours,
            // which are already tuned, but no longer their own arbitrary beam widths.
            var line = LineFor(towerId);
            var style = StyleFor(line, damage, tier);
            var shape = ProjectileFor(line, style);
            var impact = hitPosition + Vector3.up * 0.12f;
            // What the dart raises when it lands: a ring this wide with this many sparks.
            var impactScale = damage >= 5 ? 0.62f : 0.46f;
            var impactSparks = damage >= 5 ? 6 : 5;

            if (IsArrowTower(towerId))
            {
                // The bolt leaves the nock, a touch forward of the body centre, as a longer and
                // slightly thinner dart than the line default. The limb and string flashes that
                // used to frame it were two short beams at right angles on the body: a cross.
                var nock = At(new Vector3(0f, 0.62f, 0.12f));
                LaunchProjectile(nock, impact, shotColor, style.Intensity, shape.Scaled(0.9f, 1.15f), impactScale, impactSparks);
                SpawnEffect(nock, shotColor, damage >= 5 ? 0.28f : 0.2f, 0.08f, BurstShape.Muzzle, hitPosition - nock);
                return;
            }

            if (IsControlTower(towerId))
            {
                // Rings kept — they are Control's own portal shape. The aimed beam from the core
                // is a fatter, slightly slower bolt so the ring at the tower has opened before the
                // bolt lands.
                LaunchProjectile(muzzle, impact, shotColor, style.Intensity, shape.Scaled(1.3f, 0.9f), impactScale * 1.1f, impactSparks);
                SpawnExpandingRing(At(Vector3.up * 0.28f), shotColor, 0.15f, 1.4f, 0.35f);
                SpawnExpandingRing(hitPosition + Vector3.up * 0.18f, shotColor, 0.1f, 0.85f, 0.22f);
                return;
            }

            if (IsRelayTower(towerId))
            {
                // The frames' "green square bracket on the tower that got the kill" was this
                // branch's SpawnCellFrameCue at the tower base, in Relay's mint, drawn on every
                // hit beside the "+N" earnings label RenderEvents raises for TowerEarnedGoldEvent.
                // The label is the information; the bracket is now a 0.15s base flash so the
                // tower still visibly acts. The two body strokes that crossed at the core are gone.
                LaunchProjectile(muzzle, hitPosition + Vector3.up * 0.2f, shotColor, style.Intensity, shape, impactScale, impactSparks);
                SpawnTowerBaseFlash(towerPosition, shotColor);
                SpawnEffect(muzzle, shotColor, 0.26f, 0.12f, BurstShape.Muzzle, hitPosition - muzzle);
                return;
            }

            if (IsPulseTower(towerId))
            {
                // Omnidirectional splash, so no dart. The four radial beam spokes — kept before
                // because rings and bursts both arrive late and this tower had nothing on the frame
                // it fired — are now six sparks thrown outward from the ring part: still geometry
                // on the firing frame, but moving and fading rather than a static asterisk.
                var ringHeight = At(Vector3.up * 0.5f);
                for (var spoke = 0; spoke < 6; spoke++)
                {
                    var heading = Quaternion.Euler(0f, 30f + spoke * 60f, 0f) * Vector3.forward;
                    SpawnSpark(ringHeight, (heading + Vector3.up * 0.15f).normalized, shotColor, style.Width * 0.8f, 0.42f, 3.2f, style.Duration * 2f, style.Intensity);
                }

                SpawnExpandingRing(ringHeight, shotColor, 0.15f, 1.6f, 0.4f);
                SpawnEffect(ringHeight, SignalGold, 0.16f, 0.1f);
                // A ring on the splashed cell, no sparks: the splash is the tower's, not the creep's.
                SpawnImpactBurst(impact, shotColor, damage >= 5 ? 0.8f : 0.6f, 0, 0.24f);
                SpawnEffect(hitPosition, shotColor, damage >= 5 ? 0.5f : 0.36f, 0.16f);
                return;
            }

            if (IsPrismTower(towerId))
            {
                // The head glints with four short sparks where two crossing beams used to draw an X.
                var head = At(Vector3.up * 0.7f);
                for (var facet = 0; facet < 4; facet++)
                {
                    var heading = Quaternion.Euler(0f, 45f + facet * 90f, 0f) * Vector3.forward;
                    SpawnSpark(head, (heading + Vector3.up * 0.6f).normalized, SignalGold, 0.05f, 0.2f, 1.4f, 0.14f, 1.6f);
                }

                SpawnEffect(head, SignalGold, 0.22f, 0.12f);
                LaunchProjectile(head, hitPosition + Vector3.up * 0.16f, shotColor, style.Intensity, shape.Scaled(0.85f, 1.2f), impactScale, impactSparks);
                return;
            }

            // Per-tower tells. Each of these towers has a mechanic that was invisible while it drew
            // the shared fallback below. Several read their own mechanic straight off `damage`,
            // which is the honest source: Grovebond, Crowd Bloom and Tesla's halving chain all
            // express themselves as damage the simulation already computed.
            if (ContainsRole(towerId, "tesla"))
            {
                // Chain Arc hops backward down the queue, halving each time, and each hop arrives
                // as its own event — so a thinner, dimmer arc for a weaker hop shows the decay.
                // Kept as beams: a jagged multi-segment arc is not a straight line, and it is the
                // one shot here that SHOULD connect the two ends for its whole duration.
                var arcColor = Color.Lerp(new Color(0.55f, 0.76f, 1f), new Color(0.86f, 0.95f, 1f), Mathf.Clamp01(damage / 8f));
                SpawnForkedArc(muzzle, impact, arcColor, style, 4);
                SpawnEffect(impact, arcColor, damage >= 5 ? 0.34f : 0.24f, 0.1f);
                return;
            }

            if (ContainsRole(towerId, "gatling"))
            {
                SpawnTracerShot(muzzle, impact, shotColor, style);
                return;
            }

            if (ContainsRole(towerId, "barricade"))
            {
                SpawnSlugShot(muzzle, impact, shotColor, style);
                return;
            }

            if (IsRepairDroneTower(towerId))
            {
                // A support tower, not a weapon: a maintenance pulse rather than a shot. The
                // servicing tether to its neighbours is drawn continuously elsewhere. The thin
                // service beam is now a slim, dim dart with a ring and no sparks on arrival — it
                // still says WHEN the tower acted without looking like a weapon.
                LaunchProjectile(muzzle, impact, shotColor, style.Intensity * 0.8f, shape.Scaled(0.6f, 0.8f), 0.36f, 0);
                SpawnExpandingRing(muzzle, shotColor, 0.3f, 0.95f, style.Duration * 1.6f);
                SpawnExpandingRing(impact, shotColor, 0.2f, 0.6f, style.Duration);
                return;
            }

            if (ContainsRole(towerId, "thorn"))
            {
                // Bramble Hold halves speed in a zone. The vines snap taut and release quickly —
                // the brake itself stays shown by the persistent bramble zone decal, so this does
                // not need to hold for the whole duration of the slow.
                SpawnVineLash(muzzle, impact, shotColor, style, 2, 0.34f);
                SpawnEffect(impact, shotColor, damage >= 5 ? 0.34f : 0.26f, style.Duration * 0.5f);
                return;
            }

            if (ContainsRole(towerId, "canopy"))
            {
                // Deep Roots uniquely targets the REARMOST creep, so this lash deliberately reads as
                // heavy and long — it is reaching past nearer creeps to the back of the lane.
                SpawnVineLash(muzzle, impact, shotColor, style.Scaled(1.35f, 1f), 3, 0.42f);
                SpawnExpandingRing(impact, shotColor, 0.14f, 0.8f, style.Duration);
                return;
            }

            if (ContainsRole(towerId, "spore") || ContainsRole(towerId, "bloomheart"))
            {
                // Rot scales off the target's max health and Crowd Bloom off how many creeps share
                // the cell — both arrive as bigger damage, so a bloom sized by damage shows a fat
                // target or a big stack being punished specifically. The seed is a fatter dart for
                // a bigger bloom; the ring and burst on the target are unchanged.
                var bloom = Mathf.Lerp(0.75f, 1.8f, Mathf.Clamp01(damage / 10f));
                LaunchProjectile(muzzle, impact, shotColor, style.Intensity, shape.Scaled(Mathf.Lerp(0.9f, 1.4f, (bloom - 0.75f) / 1.05f), 1f), bloom * 0.55f, impactSparks);
                // A particle burst carries the bloom, with the ring only underneath it. Measured at
                // the real camera, SpawnExpandingRing draws a soft low-contrast glow rather than a
                // crisp ring — legible for Control, whose ring is a slow deliberate beat, but far
                // too weak to be the whole tell for two towers whose entire mechanic is "this got
                // bigger because the target was fat / the stack was deep".
                SpawnEffect(hitPosition, shotColor, 0.34f * bloom, style.Duration * 0.7f, BurstShape.Impact);
                SpawnExpandingRing(impact, shotColor, bloom * 0.34f, bloom, style.Duration);
                return;
            }

            if (ContainsRole(towerId, "sapling"))
            {
                // Grovebond adds damage per bonded neighbour, so a seed that visibly thickens with
                // damage is the bond paying off.
                var bonded = style.Scaled(Mathf.Lerp(0.7f, 1.6f, Mathf.Clamp01(damage / 8f)), 1f);
                LaunchProjectile(muzzle, impact, shotColor, bonded.Intensity, ProjectileFor(line, bonded), impactScale, impactSparks);
                SpawnEffect(impact, shotColor, damage >= 5 ? 0.32f : 0.22f, style.Duration * 0.5f);
                return;
            }

            // The line grammar. Whatever is left reaches this tail — it is where a GROVE spore
            // bloom and a FOUNDRY gatling used to fire the exact same blue box.

            // A burst thrown along the firing direction reads as the weapon actually discharging.
            SpawnEffect(muzzle, shotColor, damage >= 5 ? 0.3f : 0.22f, 0.1f, BurstShape.Muzzle, impact - muzzle);
            LaunchProjectile(muzzle, impact, shotColor, style.Intensity, shape, impactScale, impactSparks);

            if (line == TowerLine.Grove)
            {
                // Soft and organic all the way through, including the impact: a bloom opening on
                // the target on top of the dart's own ring.
                SpawnExpandingRing(impact, shotColor, 0.12f, damage >= 5 ? 0.92f : 0.7f, style.Duration);
                SpawnEffect(hitPosition, shotColor, damage >= 5 ? 0.42f : 0.3f, style.Duration * 0.6f);
                return;
            }

            SpawnEffect(hitPosition, shotColor, damage >= 5 ? 0.3f : 0.22f, 0.1f);
        }

        /// <summary>
        /// Whether the SpawnTowerAttackCue call just made launched a dart toward its hit. Read and
        /// cleared by <see cref="SpawnCreepHitCue"/>, which RenderEvents calls straight after it
        /// for the same CreepDamagedEvent. Also read (without clearing — see its own remarks) by
        /// RenderEvents itself, to decide whether <see cref="CreepDamagedHitFlashColor"/> fires
        /// immediately or waits for the dart (see <see cref="UpdateProjectiles"/>).
        /// </summary>
        private bool lastAttackCueLaunchedProjectile;

        /// <summary>
        /// Colour/scale/duration of the hit flash <c>RenderEvents</c> raises for a
        /// <c>CreepDamagedEvent</c> — OPEN_ITEMS.md item 53. Shared with <see cref="UpdateProjectiles"/>
        /// so a dart-carried hit gets the exact same flash the direct-hit path always has, just timed
        /// to the dart's actual landing instead of the event's arrival.
        /// </summary>
        private static readonly Color CreepDamagedHitFlashColor = new(1f, 0.88f, 0.44f);
        private const float CreepDamagedHitFlashScale = 0.24f;
        private const float CreepDamagedHitFlashDuration = 0.12f;

        /// <summary><see cref="SpawnProjectile"/>, recording that this attack cue's hit will land with the dart.</summary>
        private void LaunchProjectile(Vector3 from, Vector3 to, Color color, float intensity, ProjectileShape shape, float impactScale, int impactSparks)
        {
            if (SpawnProjectile(from, to, color, intensity, shape, impactScale, impactSparks))
            {
                lastAttackCueLaunchedProjectile = true;
            }
        }

        /// <summary>A brief ring pulse at a tower's base: "this tower just acted", without a bracket.</summary>
        /// <remarks>
        /// 0.15s and a filled soft disc rather than the annulus the impacts use, so it reads as a
        /// glow under the tower rather than a second impact. The recoil kick itself is
        /// UpdateTowerMotion's (TowerPresentation.cs) and is not duplicated here.
        /// </remarks>
        private void SpawnTowerBaseFlash(Vector3 towerPosition, Color color)
        {
            if (!IsOnActiveLane(towerPosition))
            {
                return;
            }

            SpawnExpandingRingCore(towerPosition + Vector3.up * 0.05f, new Color(color.r, color.g, color.b, 0.55f), 0.55f, 1f, 0.15f, BoardRenderResources.ContactShadowMesh);
        }

        /// <summary>A hit that arrived with no dart to carry it: a small radial burst in the hit colour.</summary>
        /// <remarks>
        /// RenderEvents calls this straight after SpawnTowerAttackCue for the same event. When
        /// that cue launched a dart, the dart raises the impact itself when it lands ~0.12s later
        /// (UpdateProjectiles), and a burst here as well would put the impact on the creep before
        /// the shot had reached it. Tesla, the vine lashes and Pulse do not launch darts and land
        /// here. The two beams this used to cross at the creep were the frames' X at every hit.
        /// </remarks>
        private void SpawnCreepHitCue(Vector3 position, Color color, int damage)
        {
            if (lastAttackCueLaunchedProjectile)
            {
                lastAttackCueLaunchedProjectile = false;
                return;
            }

            SpawnImpactBurst(position + Vector3.up * 0.08f, color, damage >= 5 ? 0.5f : 0.36f, damage >= 5 ? 5 : 4, 0.2f);
        }

        /// <summary>Role tells on a hit creep: a shade being revealed, a siege unit taking fire.</summary>
        /// <remarks>
        /// Both used to be two beams meeting at the creep — a diagonal cross for the reveal, a
        /// vertical with a crossbar for siege — and read as the same X the hit cue drew. A reveal
        /// is a shroud coming off, so it is a pale ring opening around the creep with a few sparks
        /// lifting away; siege keeps its red-and-gold pair as a low red ring with gold sparks.
        /// </remarks>
        private void SpawnCreepRoleFeedbackCue(Vector3 position, string creepId, int damage)
        {
            if (ContainsRole(creepId, "shade") || ContainsRole(creepId, "invisible") || ContainsRole(creepId, "stealth"))
            {
                var color = new Color(0.72f, 0.94f, 1f);
                SpawnImpactBurst(position + Vector3.up * 0.2f, color, 0.7f, 4, 0.26f);
                if (damage >= 5)
                {
                    // No "REVEAL" word; the reveal VFX on the creep is the tell.
                    SpawnReducedEffectCue(position + Vector3.right * 0.2f, "REVEAL", color);
                }

                return;
            }

            if (ContainsRole(creepId, "siege") || ContainsRole(creepId, "attacker"))
            {
                SpawnImpactBurst(position + Vector3.up * 0.1f, LeakRed, 0.6f, 0, 0.22f);
                for (var spark = 0; spark < 3; spark++)
                {
                    var heading = Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 360f), 0f) * Vector3.forward;
                    SpawnSpark(position + Vector3.up * 0.2f, (heading + Vector3.up * 0.5f).normalized, SignalGold, 0.06f, 0.3f, 2.2f, 0.2f, 1.4f);
                }

                if (damage >= 5)
                {
                    SpawnReducedEffectCue(position, "SIEGE", LeakRed);
                }
            }
        }

        /// <summary>Which kind of creep got through, marked alongside the gate line.</summary>
        /// <remarks>
        /// Both accents run along X, parallel to the gate line the leak cue draws, so a role tell
        /// reads as a second stroke on that line rather than as a mark stamped on the creep.
        ///
        /// The siege tell used to be a diagonal laid across a vertical, which is two beams crossing
        /// at a point — the same crossed-sticks shape that made the arrival cue read as a missing
        /// asset, for the same reason. Its gold is kept, as a stroke just behind the red one, so
        /// the information survives without the glyph.
        /// </remarks>
        private void SpawnCreepLeakRoleCue(Vector3 position, string creepId)
        {
            if (ContainsRole(creepId, "siege") || ContainsRole(creepId, "attacker"))
            {
                SpawnBeam(position + new Vector3(-0.5f, 0.26f, 0.08f), position + new Vector3(0.5f, 0.26f, 0.08f), SignalGold, 0.24f);
                return;
            }

            if (ContainsRole(creepId, "shade") || ContainsRole(creepId, "invisible") || ContainsRole(creepId, "stealth"))
            {
                SpawnBeam(position + new Vector3(-0.32f, 0.22f, 0f), position + new Vector3(0.32f, 0.22f, 0f), new Color(0.72f, 0.94f, 1f), 0.22f);
            }
        }

        /// <summary>
        /// The flash half of a creep's death: a radial burst in the style's palette, sized and
        /// paced by how the creep breaks.
        /// </summary>
        /// <remarks>
        /// Wave 3 left each style with one directional shard beam beside its particle burst; the
        /// re-judged frames still read the beam as the arm of an X, and the particle burst alone
        /// (fourteen soft dots) had no shape to it at 0.1s. Every style is now a
        /// <see cref="SpawnImpactBurst"/>: a ring opening from the creep plus a fan of tapered
        /// sparks, reading over ~6 frames. HeavyShatter is the widest and warmest, ShardScatter
        /// the quickest and thinnest, SoftDissolve a faint slow ring with no sparks (the shrink in
        /// UpdateDyingCreeps carries that one), SparkBurst — the common case — a plain gold burst.
        ///
        /// The model-level half of the send-off — shrinking, sinking and darkening this creep's
        /// own instance — is started in ReleaseMissingCreeps (Pooling.cs), not here: by the time
        /// this method runs, that sweep has already run for the same frame (see BeginCreepDying's
        /// remark for why a hook here would be one release too late).
        /// </remarks>
        private void SpawnCreepDeathCue(Vector3 position, Color color, string creepId, CreepVisualProfile visualProfile)
        {
            var deathCueStyle = CreepDeathCueStyleFor(creepId, visualProfile);

            if (deathCueStyle == CreepDeathCueStyle.HeavyShatter)
            {
                SpawnImpactBurst(position + Vector3.up * 0.16f, Color.Lerp(color, new Color(1f, 0.55f, 0.25f), 0.5f), 1.15f, 7, 0.32f);
                SpawnEffect(position + Vector3.up * 0.2f, color, 0.64f, 0.32f, BurstShape.Impact);
                return;
            }

            if (deathCueStyle == CreepDeathCueStyle.ShardScatter)
            {
                SpawnImpactBurst(position + Vector3.up * 0.12f, Color.Lerp(color, new Color(0.72f, 1f, 0.86f), 0.5f), 0.8f, 6, 0.24f);
                SpawnEffect(position + Vector3.up * 0.16f, color, 0.5f, 0.26f, BurstShape.Impact);
                return;
            }

            if (deathCueStyle == CreepDeathCueStyle.SoftDissolve)
            {
                var pale = Color.Lerp(color, new Color(0.72f, 0.84f, 0.95f), 0.6f);
                SpawnImpactBurst(position + Vector3.up * 0.14f, new Color(pale.r, pale.g, pale.b, 0.6f), 0.7f, 0, 0.3f);
                SpawnEffect(position + Vector3.up * 0.14f, color, 0.32f, 0.3f);
                return;
            }

            SpawnImpactBurst(position + Vector3.up * 0.14f, color, 0.85f, 6, 0.26f);
            SpawnEffect(position + Vector3.up * 0.18f, color, 0.46f, 0.24f, BurstShape.Impact);
        }

        /// <summary>
        /// A jagged, segmented arc between two points. Tesla's Chain Arc.
        /// </summary>
        /// <remarks>
        /// Each hop of the chain arrives as its own CreepDamagedEvent carrying that hop's already
        /// halved damage, so passing the caller's style through unchanged makes the chain's decay
        /// visible without the renderer having to know anything about the chain at all.
        /// </remarks>
        private void SpawnForkedArc(Vector3 start, Vector3 end, Color color, WeaponStyle style, int segments)
        {
            var previous = start;
            var span = end - start;
            // Perpendicular on the ground plane: the kink has to be across the arc, not along it.
            var lateral = Vector3.Cross(span.normalized, Vector3.up).normalized;
            for (var index = 1; index <= segments; index++)
            {
                var t = (float)index / segments;
                var point = start + span * t;
                if (index < segments)
                {
                    // Zero at both ends so the arc still starts at the coil and lands on the target.
                    var envelope = Mathf.Sin(t * Mathf.PI);
                    point += lateral * (UnityEngine.Random.Range(-0.34f, 0.34f) * envelope);
                    point += Vector3.up * (UnityEngine.Random.Range(-0.06f, 0.14f) * envelope);
                }

                SpawnBeam(previous, point, color, style.Duration, style.Width, style.Intensity);
                previous = point;
            }
        }

        /// <summary>
        /// A round in flight plus a casing thrown clear. Gatling's tracer.
        /// </summary>
        /// <remarks>
        /// The fastest, shortest dart in the roster and the only one with no trail: Gatling fires
        /// every tick, the trail is a second quad per shot, and a tracer round is a dot with a
        /// direction rather than a comet. The mid-gap static beam it replaces was already meant
        /// to read as "a round in flight, not a rod" — at 0.1s sampling it was a rod anyway.
        /// </remarks>
        private void SpawnTracerShot(Vector3 muzzle, Vector3 target, Color color, WeaponStyle style)
        {
            var direction = (target - muzzle).normalized;
            LaunchProjectile(muzzle, target, color, style.Intensity * 1.25f, new ProjectileShape(style.Width * 0.7f, 0.36f, 0f, ProjectileFlightSeconds * 0.75f), 0.34f, 3);
            SpawnEffect(muzzle, color, 0.26f, 0.07f, BurstShape.Muzzle, direction);

            // Ejected sideways and slightly up, brass rather than muzzle-coloured — a spark now,
            // so it tumbles clear instead of standing as a short stick beside the barrel.
            var eject = Vector3.Cross(direction, Vector3.up).normalized + Vector3.up * 0.4f;
            SpawnSpark(muzzle, eject, new Color(0.85f, 0.68f, 0.32f), 0.04f, 0.14f, 1.6f, style.Duration * 1.4f, 0.9f);
        }

        /// <summary>
        /// One heavy short round with a hard muzzle flash behind it. Barricade's slug.
        /// </summary>
        /// <remarks>
        /// The widest dart, short and with a short trail; its impact (ring and five sparks) lands
        /// with it rather than being raised here at fire time.
        /// </remarks>
        private void SpawnSlugShot(Vector3 muzzle, Vector3 target, Color color, WeaponStyle style)
        {
            var direction = (target - muzzle).normalized;
            LaunchProjectile(muzzle, target, color, style.Intensity, new ProjectileShape(style.Width * 1.1f, 0.44f, 0.4f, ProjectileFlightSeconds), 0.6f, 5);
            // Recoil reads as a flash driven back past the barrel, opposite the shot.
            SpawnEffect(muzzle - direction * 0.12f, color, 0.4f, 0.11f, BurstShape.Muzzle, -direction);
        }

        /// <summary>
        /// Several strands reaching from tower to target, bowed apart. Thorn Snare and Elder Canopy.
        /// </summary>
        private void SpawnVineLash(Vector3 start, Vector3 end, Color color, WeaponStyle style, int strands, float bow)
        {
            const int SegmentsPerStrand = 5;
            var span = end - start;
            var lateral = Vector3.Cross(span.normalized, Vector3.up).normalized;
            for (var strand = 0; strand < strands; strand++)
            {
                // Each strand bows to its own side. Curved along a quadratic through an offset
                // control point rather than bent at a single midpoint: two straight segments meeting
                // at a sharp corner drew a hard geometric diamond, which read as anything but
                // organic — worse than the plain beam it replaced.
                var side = strands == 1 ? 0f : (strand / (float)(strands - 1) - 0.5f) * 2f;
                var control = start + span * 0.5f + lateral * (side * bow) + Vector3.up * 0.1f;
                var previous = start;
                for (var segment = 1; segment <= SegmentsPerStrand; segment++)
                {
                    var t = (float)segment / SegmentsPerStrand;
                    var inverse = 1f - t;
                    var point = inverse * inverse * start + 2f * inverse * t * control + t * t * end;
                    if (segment < SegmentsPerStrand)
                    {
                        // Small irregularity so the strands do not read as drafted curves.
                        point += lateral * (UnityEngine.Random.Range(-0.05f, 0.05f));
                    }

                    // Tapers toward the tip: a tendril, not a cable.
                    var taper = Mathf.Lerp(0.85f, 0.45f, t);
                    SpawnBeam(previous, point, color, style.Duration, style.Width * taper, style.Intensity);
                    previous = point;
                }
            }
        }

        /// <summary>A pulse on a cell: a ring opening from the cell centre and fading. Build, sell, arrival.</summary>
        /// <remarks>
        /// Was four beams drawn as a square outline — the square bracket the render review's
        /// frames carried on the build cell, the sell cell, the arrival gate and, until Wave 5,
        /// every hit cell. A square drawn in beams says "selection gizmo" whatever its colour. A
        /// ring opening at the same spot says the same thing — THIS cell — as motion rather than
        /// markup. Keeps its name and signature: RenderEvents (not this pass's file) calls it for
        /// build and sell. Draws under ReducedEffects too: it is one instanced quad, and a build
        /// confirmation is worth more at that setting, not less.
        /// </remarks>
        private void SpawnCellFrameCue(Vector3 center, Color color, float duration)
        {
            if (!IsOnActiveLane(center))
            {
                return;
            }

            SpawnExpandingRingCore(center + Vector3.up * 0.12f, new Color(color.r, color.g, color.b, 0.7f), 0.45f, 1.1f, Mathf.Max(0.15f, duration + 0.08f), BoardRenderResources.MechanicRingMesh);
        }

        /// <summary>The line across the gate a leaking creep just crossed.</summary>
        /// <remarks>
        /// The line is the cue. This used to raise a burst here as well, at
        /// <c>GridToWorld(CenterColumn, LaneLength - 1)</c> — which is the gate cell, and a creep
        /// leaks *at* the gate, so it landed on top of the caller's own burst at the creep's
        /// position. Two clouds, fourteen particles and twenty, in the same place at the same
        /// moment, which is most of why a leak looked like a smear rather than an event.
        ///
        /// One burst and one line reads as a thing crossing a threshold. Two bursts and a line
        /// reads as a mess in the shape of a leak.
        ///
        /// Finding #8 (2026-09-01 render review) described this as "an orange square outline",
        /// which does not match what this method draws (one line, not a square) — most likely the
        /// capture it was written from also caught a nearby tower's SpawnCellFrameCue firing on
        /// the same frame. Left as the bare line: a leaking creep is just another key that drops
        /// out of the snapshot, so it now gets the same BeginCreepDying shrink/sink send-off a
        /// kill gets (see ReleaseMissingCreeps, Pooling.cs) with no leak-specific wiring needed,
        /// and RenderEvents already raises a 0.6-scale BurstShape.Sweep at the creep's own
        /// position for every leak. Between those two, one thin gate line reading as "something
        /// crossed here" is enough; a second burst or ring stacked on top of an
        /// already-Sweep-bursting, now visibly-sinking creep would be the over-build this task
        /// explicitly warned against, not an improvement.
        /// </remarks>
        private void SpawnLeakGateCue(int laneId)
        {
            var offset = LaneOffset(laneId);
            SpawnBeam(
                new Vector3(offset + 0.7f, 0.48f, WorldZ(LaneLength - 1)),
                new Vector3(offset + LaneWidth - 1.7f, 0.48f, WorldZ(LaneLength - 1)),
                LeakRed,
                0.3f);
        }

        private void SpawnIncomeLaneCue(int laneId)
        {
            var offset = LaneOffset(laneId);
            var west = new Vector3(offset + 0.85f, 0.42f, WorldZ(1));
            var east = new Vector3(offset + LaneWidth - 1.85f, 0.42f, WorldZ(1));
            SpawnBeam(west, east, SignalGold, 0.2f);
        }

        /// <summary>How far a lane-level ring pulse grows: past the lane's own width, into its neighbours'.</summary>
        /// <remarks>
        /// A whole lane going down or winning is bigger than any single-cell event, so the ring
        /// this now raises (OPEN_ITEMS.md item 53) needs to read as lane-scaled rather than
        /// cell-scaled — <see cref="SpawnCellFrameCue"/>'s own end scale of 1.1 would shrink to a
        /// dot against a 7-wide lane. Sized off LaneWidth rather than LaneLength: the camera frames
        /// one lane at a time roughly as wide as it is tall on screen, so a ring keyed to the
        /// narrower axis fills the visible lane without the far end still visibly short of it.
        /// </remarks>
        private const float LaneRingEndScale = LaneWidth + 1f;

        private void SpawnLaneShutdownCue(int laneId)
        {
            var center = LaneCenter(laneId);
            if (!IsOnActiveLane(center))
            {
                return;
            }

            // Two crossed beams (an X down the lane) replaced with the same ring-pulse language
            // Wave 5 gave every other cue this shape — a square or a cross reads as "asset failed
            // to load" from directly above; a ring reads as motion. See SpawnCellFrameCue's own
            // remarks for the same call made at cell scale.
            SpawnExpandingRingCore(center + Vector3.up * 0.14f, new Color(LeakRed.r, LeakRed.g, LeakRed.b, 0.7f), 1.2f, LaneRingEndScale, 0.6f, BoardRenderResources.MechanicRingMesh);
        }

        private void SpawnVictoryLaneCue(int laneId)
        {
            var center = LaneCenter(laneId);
            if (!IsOnActiveLane(center))
            {
                return;
            }

            SpawnEffect(center + Vector3.up * 0.38f, SignalGold, 1.05f, 0.45f);
            // The perpendicular beam pair (a + across the lane) replaced the same way as the
            // shutdown cue's X — see its own remarks.
            SpawnExpandingRingCore(center + Vector3.up * 0.14f, new Color(SignalGold.r, SignalGold.g, SignalGold.b, 0.7f), 1.2f, LaneRingEndScale, 0.6f, BoardRenderResources.MechanicRingMesh);
        }

        private void SpawnSendCue(CreepQueuedEvent queued)
        {
            var senderPosition = GridToWorld(new GridPosition(CenterColumn, LaneLength - 1), new LaneId(queued.SenderId.Value)) + Vector3.up * 0.2f;
            var defenderPosition = SpawnPosition(queued.DefenderId.Value);
            var color = CreepRoleColor(queued.CreepId.Value, queued.SenderId.Value);

            // Both ends of this cue live in their own lanes, and the active-lane camera shows one
            // lane. Drawn ungated, a cross-lane send put a full-saturation additive beam from a
            // sender gate that is off-screen, diagonally across the whole visible board and out
            // past the HUD — by a wide margin the largest thing on screen, every time anyone sent
            // anything, for an event in a lane the player cannot see. The burst at the sender's
            // gate had the same problem: it landed in open space beside the lane.
            //
            // `IsOnActiveLane` returns true whenever the camera is framing more than one lane, so
            // in the overview framings the departure, the transit and the arrival all still read
            // as they were designed to. This only stands the off-lane halves down when there is no
            // lane on screen to show them in.
            //
            // Same reasoning that already gates the SEND text below, applied to the geometry that
            // is far louder than the text ever was.
            // Gating on the camera alone is not enough in the all-lanes framings, where
            // `IsOnActiveLane` is true for every lane by design. Every seat sends continuously, so
            // with a full table that permitted a beam per send across the whole board at once —
            // a permanent crossfire of lasers between lanes, which is what a hand-off down the
            // table looks like from the overview and why this reads as transfers firing at things.
            //
            // A send the local player is neither making nor receiving is not theirs to answer, so
            // it gets no transit line. This is the rule the SEND text below already follows, and
            // the beam is the loudest thing on the board rather than 12% of its text. Their own
            // sends still draw, and a send arriving in their lane still draws, which are the two
            // cases they can actually do something about.
            var localIsInvolved = IsLocalSeat(queued.SenderId) || IsLocalSeat(queued.DefenderId);

            var senderVisible = IsOnActiveLane(senderPosition);
            var defenderVisible = IsOnActiveLane(defenderPosition);

            if (senderVisible)
            {
                SpawnEffect(senderPosition, color, 0.44f, 0.24f);
            }

            if (senderVisible && defenderVisible && localIsInvolved)
            {
                SpawnBeam(senderPosition + Vector3.up * 0.18f, defenderPosition + Vector3.up * 0.18f, color, 0.22f);
            }

            if (defenderVisible)
            {
                SpawnEffect(defenderPosition, color, 0.54f, 0.3f);
            }
            // Only the local player's own sends. A banner for an opponent sending into someone
            // else's lane is 12% of all board text and nothing the player can act on.
            if (simulationDriver != null && queued.SenderId.Equals(simulationDriver.LocalPlayerId))
            {
                SpawnFloatingText(senderPosition + Vector3.left * 0.42f, BoardLabelKind.Send, "SEND", color, 0.42f);
            }
            // The large "{qty}x {NAME}" spawn banner over the defender's gate was removed: it
            // dominated the top of the board and duplicated information the send dock already
            // shows. The sender-side SEND cue and the gate effect still mark the event.
            SpawnReducedEffectCue(defenderPosition, "SEND", color);
            audioDirector.Play(LTWAudioCue.CreepSent);
        }

        /// <summary>One animating board label: what it is, where it started, when, and for how long.</summary>
        /// <remarks>
        /// A class rather than the struct it used to be because a merge rewrites it in place —
        /// amount, text, clock — and the list would otherwise have to swap the whole entry out.
        /// </remarks>
        private sealed class FloatingLabel
        {
            public FloatingLabel(GameObject @object, TMPro.TextMeshPro label, BoardLabelKind kind, int amount, Vector3 anchor, float spawnedAt, float duration)
            {
                Object = @object;
                Label = label;
                Kind = kind;
                Amount = amount;
                Anchor = anchor;
                Origin = @object.transform.position;
                SpawnedAt = spawnedAt;
                Duration = duration;
            }

            public GameObject Object { get; }
            public TMPro.TextMeshPro Label { get; }
            public BoardLabelKind Kind { get; }

            /// <summary>Running total for an amount kind; meaningless for free text.</summary>
            public int Amount { get; private set; }

            /// <summary>The event position the label was asked for, before lift and stacking. What neighbour tests measure against.</summary>
            public Vector3 Anchor { get; }

            /// <summary>Where the rise starts: the anchor plus lift plus any stacking offset.</summary>
            public Vector3 Origin { get; }

            public float SpawnedAt { get; private set; }
            public float Duration { get; private set; }

            /// <summary>Folds a same-kind label into this one: new total, new wording, clock restarted.</summary>
            /// <remarks>
            /// The clock restarts from now rather than extending, so the rise and the scale punch
            /// replay from the origin — on a label at most 0.2s old that is a jump of under 0.15
            /// units, and it reads as the number ticking up. The longer of the two durations is
            /// kept so a merge never shortens a label.
            /// </remarks>
            public void Absorb(int amount, string text, float now, float duration)
            {
                Amount += amount;
                SpawnedAt = now;
                Duration = Mathf.Max(Duration, duration);
                if (Label != null)
                {
                    Label.text = text;
                }
            }
        }
    }
}
