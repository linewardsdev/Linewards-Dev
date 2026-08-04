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

        /// <summary>Board labels currently animating. See SpawnFloatingText and UpdateFloatingLabels.</summary>
        private readonly List<FloatingLabel> floatingLabels = new List<FloatingLabel>();

        private void SpawnFloatingText(Vector3 position, string text, Color color) => SpawnFloatingText(position, text, color, 0.7f);

        /// <summary>
        /// Orientation that presents flat world-space text square-on to the gameplay camera.
        /// Falls back to the old board-flat orientation only if no camera is resolvable.
        /// </summary>
        private Quaternion FloatingTextRotation()
        {
            var camera = presentationCamera != null ? presentationCamera : Camera.main;
            return camera != null ? camera.transform.rotation : Quaternion.Euler(90f, 0f, 0f);
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
        /// </remarks>
        private void SpawnFloatingText(Vector3 position, string text, Color color, float duration)
        {
            if (!IsOnActiveLane(position))
            {
                return;
            }

            // Dropped rather than queued: a label that cannot be shown now is worthless a second
            // later, and queueing would keep the cost while losing the timing.
            if (floatingLabels.Count >= MaxLiveFloatingLabels)
            {
                return;
            }

            var textObject = GetTextObject();
            textObject.transform.position = position + Vector3.up * 0.55f;
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
                label.fontSize = 3.4f;
                label.raycastTarget = false;
                // The outline is the whole point of moving to SDF: board text sits over lane
                // plating, range halos and creep bodies, and an unoutlined glyph at this size loses
                // its edges against all three.
                //
                // ONE shared outlined material for every label, built once (BoardTextMaterial).
                // Two other routes were tried and neither produced an outline: setting _OutlineWidth
                // and the OUTLINE_ON keyword on fontMaterial, and TMP's per-component outlineWidth
                // and outlineColor. Both compiled, ran, and rendered flat glyphs. Enabling the
                // keyword on a real material and handing it to the component is what TMP actually
                // honours, and it batches rather than instancing a material per label.
                label.fontSharedMaterial = BoardTextMaterial(label.font);
            }

            label.text = text;
            label.color = color;
            label.fontSize = 3.4f * PresentationPreferences.TextScale;

            // Board furniture such as the endpoint gate plates draws through SpriteRenderers with
            // sorting orders up to 3, so board text sorts above all board decoration.
            var textRenderer = textObject.GetComponent<MeshRenderer>();
            if (textRenderer != null)
            {
                textRenderer.sortingOrder = FloatingTextSortingOrder;
            }

            floatingLabels.Add(new FloatingLabel(textObject, label, Time.time, duration));
            timedPresentations.Add(new TimedPresentation(textObject, Time.time + duration, textPool));
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

        private Material boardTextMaterial;

        /// <summary>
        /// The shared outlined material every board label draws with.
        /// </summary>
        /// <remarks>
        /// A dark outline is what keeps small text legible over lane plating, range halos and creep
        /// bodies. It is a material variant rather than a per-label property because that is the
        /// form TMP honours, and because one shared material lets all board text batch.
        /// </remarks>
        private Material BoardTextMaterial(TMPro.TMP_FontAsset font)
        {
            if (boardTextMaterial != null)
            {
                return boardTextMaterial;
            }

            boardTextMaterial = new Material(font.material) { name = "LTW Board Text" };
            boardTextMaterial.EnableKeyword("OUTLINE_ON");
            boardTextMaterial.SetFloat("_OutlineWidth", 0.25f);
            boardTextMaterial.SetColor("_OutlineColor", new Color(0.02f, 0.03f, 0.05f, 1f));
            return boardTextMaterial;
        }

        /// <summary>Rises, fades and punches in scale over its life. See SpawnFloatingText.</summary>
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
        /// The frame is the whole cue. It used to also draw two beams across the diagonals, and a
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
        private void SpawnTowerAttackCue(Vector3 towerPosition, Vector3 hitPosition, string towerId, int damage, Transform bodyTransform, int tier = 1)
        {
            Vector3 At(Vector3 localOffset) =>
                bodyTransform != null ? bodyTransform.TransformPoint(localOffset) : towerPosition + localOffset;

            var shotColor = TowerShotColor(towerId, damage);
            var muzzle = At(Vector3.up * 0.62f);
            // The five bespoke branches below are all Arcane, and take the same thin, bright, quick
            // grammar as every other Arcane tower — they keep their own choreography and colours,
            // which are already tuned, but no longer their own arbitrary beam widths.
            var line = LineFor(towerId);
            var style = StyleFor(line, damage, tier);
            if (IsArrowTower(towerId))
            {
                SpawnBeam(At(new Vector3(-0.5f, 0.62f, -0.18f)), At(new Vector3(0.5f, 0.62f, -0.18f)), shotColor, 0.08f);
                SpawnBeam(At(new Vector3(0f, 0.58f, -0.32f)), At(new Vector3(0f, 0.66f, 0.26f)), SignalGold, 0.08f);
                SpawnBeam(At(new Vector3(0f, 0.62f, 0.08f)), hitPosition + Vector3.up * 0.12f, shotColor, style.Duration, style.Width, style.Intensity);
                SpawnCellFrameCue(hitPosition, shotColor, damage >= 5 ? 0.15f : 0.1f);
                SpawnEffect(At(new Vector3(0f, 0.62f, 0.12f)), shotColor, damage >= 5 ? 0.28f : 0.2f, 0.08f, BurstShape.Muzzle, hitPosition - At(new Vector3(0f, 0.62f, 0.12f)));
                return;
            }

            if (IsControlTower(towerId))
            {
                // The old twin symmetric beams (fixed local offsets either side of centre) were
                // tuned for a Control tower that never turned to aim — with its core+ring assembly
                // now genuinely tracking the target (HeadPivot), a single beam from the core reads
                // as an actual aimed shot rather than a fixed decorative gate. Expanding rings lean
                // into Control's own ring/portal shape, replacing a static glow at the tower and
                // the same blocky SpawnCellFrameCue square other towers' VFX had.
                SpawnBeam(At(Vector3.up * 0.62f), hitPosition + Vector3.up * 0.12f, shotColor, style.Duration * 1.4f, style.Width, style.Intensity);
                SpawnExpandingRing(At(Vector3.up * 0.28f), shotColor, 0.15f, 1.4f, 0.35f);
                SpawnExpandingRing(hitPosition + Vector3.up * 0.18f, shotColor, 0.1f, 0.85f, 0.22f);
                SpawnEffect(hitPosition, shotColor, damage >= 5 ? 0.42f : 0.32f, 0.16f);
                return;
            }

            if (IsRelayTower(towerId))
            {
                SpawnBeam(muzzle, hitPosition + Vector3.up * 0.2f, shotColor, style.Duration * 1.6f, style.Width, style.Intensity);
                SpawnBeam(At(new Vector3(-0.34f, 0.34f, 0f)), At(new Vector3(0.34f, 0.34f, 0f)), shotColor, 0.14f);
                SpawnBeam(At(new Vector3(0f, 0.58f, -0.34f)), At(new Vector3(0f, 0.58f, 0.34f)), shotColor, 0.14f);
                SpawnCellFrameCue(towerPosition, shotColor, 0.16f);
                SpawnEffect(muzzle, shotColor, 0.26f, 0.12f, BurstShape.Muzzle, hitPosition - muzzle);
                return;
            }

            if (IsPulseTower(towerId))
            {
                // Previously drew 4 beams connecting the tower's own corners plus 2 more crossing
                // diagonally — a literal square outline that read as dynamic while the whole body
                // still rotated with each shot, but now that Pulse stays fixed (see locksYaw in
                // UpdateTowerMotion), it flashed as an obvious static geometric square every time
                // it fired. Replaced with an actual expanding shockwave — Pulse's whole identity is
                // an energy pulse, and a ring that visibly grows outward from the spinning Ring
                // part reads as that far better than a static glow ever could. A quick gold core
                // pop underneath gives it a starting flash to expand from.
                // Four short radial spokes, thrown outward the instant it fires. Measured at the
                // board camera, this tower's cue put 146 lit pixels on screen at the moment of
                // firing — the lowest in the roster by two orders of magnitude — because everything
                // it drew was an expanding ring or a particle burst, and BOTH of those develop over
                // later frames rather than existing when the shot happens. Spokes are geometry, so
                // they are there immediately, and radiating outward is the one direction language
                // that does not contradict an omnidirectional splash emitter.
                for (var spoke = 0; spoke < 4; spoke++)
                {
                    var heading = Quaternion.Euler(0f, 45f + spoke * 90f, 0f) * Vector3.forward;
                    SpawnBeam(At(Vector3.up * 0.5f), At(Vector3.up * 0.5f) + heading * 0.72f, shotColor, style.Duration, style.Width * 1.3f, style.Intensity);
                }

                SpawnExpandingRing(At(Vector3.up * 0.5f), shotColor, 0.15f, 1.6f, 0.4f);
                SpawnEffect(At(Vector3.up * 0.5f), SignalGold, 0.16f, 0.1f);
                // SpawnCellFrameCue drew the same kind of static square-outline box this VFX used
                // to draw around the tower itself — same problem, same fix: an expanding ring
                // reads as the splash actually spreading from the impact, not a blocky marker.
                SpawnExpandingRing(hitPosition + Vector3.up * 0.18f, shotColor, 0.1f, 0.85f, 0.22f);
                SpawnEffect(hitPosition, shotColor, damage >= 5 ? 0.5f : 0.36f, 0.16f);
                return;
            }

            if (IsPrismTower(towerId))
            {
                SpawnBeam(At(new Vector3(-0.16f, 0.7f, 0f)), At(new Vector3(0.16f, 0.7f, 0f)), SignalGold, 0.12f);
                SpawnBeam(At(new Vector3(0f, 0.7f, -0.16f)), At(new Vector3(0f, 0.7f, 0.16f)), SignalGold, 0.12f);
                SpawnEffect(At(Vector3.up * 0.7f), SignalGold, 0.22f, 0.12f);
                SpawnBeam(At(Vector3.up * 0.7f), hitPosition + Vector3.up * 0.16f, shotColor, style.Duration * 1.6f, style.Width, style.Intensity);
                SpawnEffect(hitPosition + Vector3.up * 0.08f, shotColor, damage >= 5 ? 0.46f : 0.32f, 0.18f);
                return;
            }

            var impact = hitPosition + Vector3.up * 0.12f;

            // Per-tower tells. Each of these towers has a mechanic that was invisible while it drew
            // the shared fallback below. Several read their own mechanic straight off `damage`,
            // which is the honest source: Grovebond, Crowd Bloom and Tesla's halving chain all
            // express themselves as damage the simulation already computed.
            if (ContainsRole(towerId, "tesla"))
            {
                // Chain Arc hops backward down the queue, halving each time, and each hop arrives
                // as its own event — so a thinner, dimmer arc for a weaker hop shows the decay.
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
                // servicing tether to its neighbours is drawn continuously elsewhere.
                // A thin service beam first, for the same reason as Pulse above: rings and bursts
                // both arrive late, and measured at the instant of firing this tower put 424 lit
                // pixels on screen. Kept deliberately thin and short-lived — this is a support
                // tower and the beam is there to say WHEN it acted, not to look like a weapon.
                SpawnBeam(muzzle, impact, shotColor, style.Duration * 0.8f, style.Width * 0.6f, style.Intensity);
                SpawnExpandingRing(muzzle, shotColor, 0.3f, 0.95f, style.Duration * 1.6f);
                SpawnExpandingRing(impact, shotColor, 0.2f, 0.6f, style.Duration);
                SpawnEffect(impact, shotColor, 0.24f, 0.12f);
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
                // target or a big stack being punished specifically.
                var bloom = Mathf.Lerp(0.75f, 1.8f, Mathf.Clamp01(damage / 10f));
                SpawnBeam(muzzle, impact, shotColor, style.Duration, style.Width, style.Intensity);
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
                // Grovebond adds damage per bonded neighbour, so a shot that visibly thickens with
                // damage is the bond paying off.
                var bonded = style.Scaled(Mathf.Lerp(0.7f, 1.6f, Mathf.Clamp01(damage / 8f)), 1f);
                SpawnBeam(muzzle, impact, shotColor, bonded.Duration, bonded.Width, bonded.Intensity);
                SpawnEffect(impact, shotColor, damage >= 5 ? 0.32f : 0.22f, style.Duration * 0.5f);
                return;
            }

            // The line grammar. Whatever is left reaches this tail — it is where a GROVE spore
            // bloom and a FOUNDRY gatling used to fire the exact same blue box.

            // Replaces two beams that crossed at the tower body: they were fixed to local axes, so
            // they read as a static X unrelated to where the tower was shooting. A burst thrown
            // along the firing direction reads as the weapon actually discharging.
            SpawnEffect(muzzle, shotColor, damage >= 5 ? 0.3f : 0.22f, 0.1f, BurstShape.Muzzle, impact - muzzle);
            SpawnBeam(muzzle, impact, shotColor, style.Duration, style.Width, style.Intensity);

            if (line == TowerLine.Grove)
            {
                // Soft and organic all the way through, including the impact: a bloom opening on
                // the target instead of the hard square SpawnCellFrameCue snaps around its cell.
                SpawnExpandingRing(impact, shotColor, 0.12f, damage >= 5 ? 0.92f : 0.7f, style.Duration);
                SpawnEffect(hitPosition, shotColor, damage >= 5 ? 0.42f : 0.3f, style.Duration * 0.6f);
                return;
            }

            SpawnCellFrameCue(hitPosition, shotColor, damage >= 5 ? 0.16f : 0.12f);
            SpawnEffect(hitPosition, shotColor, damage >= 5 ? 0.3f : 0.22f, 0.1f);
        }

        private void SpawnCreepHitCue(Vector3 position, Color color, int damage)
        {
            var scale = damage >= 5 ? 0.44f : 0.3f;
            SpawnBeam(position + new Vector3(-scale, 0.2f, 0f), position + new Vector3(scale, 0.2f, 0f), color, 0.1f);
            SpawnBeam(position + new Vector3(0f, 0.2f, -scale), position + new Vector3(0f, 0.2f, scale), color, 0.1f);
        }

        private void SpawnCreepRoleFeedbackCue(Vector3 position, string creepId, int damage)
        {
            if (ContainsRole(creepId, "shade") || ContainsRole(creepId, "invisible") || ContainsRole(creepId, "stealth"))
            {
                var color = new Color(0.72f, 0.94f, 1f);
                SpawnBeam(position + new Vector3(-0.28f, 0.3f, -0.34f), position + new Vector3(0.28f, 0.3f, 0.34f), color, 0.18f);
                SpawnBeam(position + new Vector3(-0.28f, 0.2f, 0.34f), position + new Vector3(0.28f, 0.2f, -0.34f), new Color(0.36f, 0.5f, 0.58f), 0.18f);
                if (damage >= 5)
                {
                    // No "REVEAL" word; the reveal VFX on the creep is the tell.
                    SpawnReducedEffectCue(position + Vector3.right * 0.2f, "REVEAL", color);
                }

                return;
            }

            if (ContainsRole(creepId, "siege") || ContainsRole(creepId, "attacker"))
            {
                SpawnBeam(position + new Vector3(0f, 0.24f, -0.5f), position + new Vector3(0f, 0.24f, 0.58f), LeakRed, 0.16f);
                SpawnBeam(position + new Vector3(-0.34f, 0.18f, 0.32f), position + new Vector3(0.34f, 0.18f, 0.32f), SignalGold, 0.14f);
                if (damage >= 5)
                {
                    SpawnReducedEffectCue(position, "SIEGE", LeakRed);
                }
            }
        }

        private void SpawnCreepLeakRoleCue(Vector3 position, string creepId)
        {
            if (ContainsRole(creepId, "siege") || ContainsRole(creepId, "attacker"))
            {
                SpawnBeam(position + new Vector3(-0.46f, 0.22f, -0.48f), position + new Vector3(0.46f, 0.22f, 0.48f), LeakRed, 0.24f);
                SpawnBeam(position + new Vector3(0f, 0.28f, -0.62f), position + new Vector3(0f, 0.28f, 0.62f), SignalGold, 0.24f);
                return;
            }

            if (ContainsRole(creepId, "shade") || ContainsRole(creepId, "invisible") || ContainsRole(creepId, "stealth"))
            {
                SpawnBeam(position + new Vector3(-0.32f, 0.22f, 0f), position + new Vector3(0.32f, 0.22f, 0f), new Color(0.72f, 0.94f, 1f), 0.22f);
            }
        }

        private void SpawnCreepDeathCue(Vector3 position, Color color, string creepId, CreepVisualProfile visualProfile)
        {
            var deathCueStyle = CreepDeathCueStyleFor(creepId, visualProfile);
            if (deathCueStyle == CreepDeathCueStyle.HeavyShatter)
            {
                SpawnBeam(position + new Vector3(-0.48f, 0.14f, -0.12f), position + new Vector3(0.48f, 0.14f, 0.12f), color, 0.18f);
                SpawnBeam(position + new Vector3(-0.2f, 0.3f, -0.44f), position + new Vector3(0.2f, 0.3f, 0.44f), color, 0.18f);
                SpawnBeam(position + new Vector3(-0.34f, 0.34f, 0.34f), position + new Vector3(0.34f, 0.08f, -0.34f), color, 0.18f);
                return;
            }

            if (deathCueStyle == CreepDeathCueStyle.ShardScatter)
            {
                SpawnBeam(position + new Vector3(-0.46f, 0.12f, 0f), position + new Vector3(-0.12f, 0.24f, 0.36f), color, 0.12f);
                SpawnBeam(position + new Vector3(0.42f, 0.12f, 0.04f), position + new Vector3(0.1f, 0.24f, -0.38f), color, 0.12f);
                SpawnBeam(position + new Vector3(0f, 0.12f, -0.48f), position + new Vector3(0.34f, 0.24f, -0.12f), color, 0.12f);
                SpawnBeam(position + new Vector3(0f, 0.12f, 0.48f), position + new Vector3(-0.34f, 0.24f, 0.12f), color, 0.12f);
                return;
            }

            if (deathCueStyle == CreepDeathCueStyle.SoftDissolve)
            {
                SpawnBeam(position + new Vector3(-0.3f, 0.2f, -0.3f), position + new Vector3(0.3f, 0.2f, 0.3f), color, 0.2f);
                SpawnBeam(position + new Vector3(-0.3f, 0.2f, 0.3f), position + new Vector3(0.3f, 0.2f, -0.3f), color, 0.2f);
                return;
            }

            SpawnBeam(position + new Vector3(-0.38f, 0.16f, 0f), position + new Vector3(0.38f, 0.16f, 0f), color, 0.14f);
            SpawnBeam(position + new Vector3(0f, 0.16f, -0.38f), position + new Vector3(0f, 0.16f, 0.38f), color, 0.14f);
            SpawnBeam(position + new Vector3(-0.24f, 0.22f, -0.24f), position + new Vector3(0.24f, 0.22f, 0.24f), color, 0.14f);
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
        /// A short bright streak that does not span the whole distance, plus a casing thrown clear.
        /// Gatling's tracer.
        /// </summary>
        /// <remarks>
        /// Deliberately fewer objects than the generic tail it replaces (two beams, two bursts and a
        /// cell frame). Gatling fires every tick, which makes it the one tower here where the effect
        /// could plausibly cost real frame time on a phone.
        /// </remarks>
        private void SpawnTracerShot(Vector3 muzzle, Vector3 target, Color color, WeaponStyle style)
        {
            var direction = (target - muzzle).normalized;
            var distance = Vector3.Distance(muzzle, target);
            // A round in flight, not a rod connecting the barrel to the target: the tracer covers
            // the middle of the gap and leaves both ends open.
            var from = muzzle + direction * (distance * 0.28f);
            var to = muzzle + direction * (distance * 0.78f);
            SpawnBeam(from, to, color, style.Duration * 0.7f, style.Width * 0.8f, style.Intensity * 1.25f);
            SpawnEffect(muzzle, color, 0.26f, 0.07f, BurstShape.Muzzle, direction);

            // Ejected sideways and slightly up, brass rather than muzzle-coloured.
            var eject = Vector3.Cross(direction, Vector3.up).normalized * 0.3f + Vector3.up * 0.12f;
            SpawnBeam(muzzle, muzzle + eject, new Color(0.85f, 0.68f, 0.32f), style.Duration * 0.55f, 0.05f, 0.8f);
        }

        /// <summary>
        /// One heavy short round with a hard muzzle flash behind it. Barricade's slug.
        /// </summary>
        private void SpawnSlugShot(Vector3 muzzle, Vector3 target, Color color, WeaponStyle style)
        {
            var direction = (target - muzzle).normalized;
            SpawnBeam(muzzle, target, color, style.Duration, style.Width * 1.5f, style.Intensity);
            // Recoil reads as a flash driven back past the barrel, opposite the shot.
            SpawnEffect(muzzle - direction * 0.12f, color, 0.4f, 0.11f, BurstShape.Muzzle, -direction);
            SpawnEffect(target, color, 0.36f, 0.12f);
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

        private void SpawnCellFrameCue(Vector3 center, Color color, float duration)
        {
            var northWest = center + new Vector3(-0.48f, 0.18f, 0.48f);
            var northEast = center + new Vector3(0.48f, 0.18f, 0.48f);
            var southWest = center + new Vector3(-0.48f, 0.18f, -0.48f);
            var southEast = center + new Vector3(0.48f, 0.18f, -0.48f);
            SpawnBeam(northWest, northEast, color, duration);
            SpawnBeam(southWest, southEast, color, duration);
            SpawnBeam(northWest, southWest, color, duration);
            SpawnBeam(northEast, southEast, color, duration);
        }

        private void SpawnLeakGateCue(int laneId)
        {
            var offset = LaneOffset(laneId);
            var gateCenter = GridToWorld(new GridPosition(CenterColumn, LaneLength - 1), new LaneId(laneId)) + Vector3.up * 0.24f;
            SpawnEffect(gateCenter, LeakRed, 0.72f, 0.34f);
            SpawnBeam(new Vector3(offset + 0.7f, 0.48f, WorldZ(LaneLength - 1)), new Vector3(offset + LaneWidth - 1.7f, 0.48f, WorldZ(LaneLength - 1)), LeakRed, 0.3f);
        }

        private void SpawnIncomeLaneCue(int laneId)
        {
            var offset = LaneOffset(laneId);
            var west = new Vector3(offset + 0.85f, 0.42f, WorldZ(1));
            var east = new Vector3(offset + LaneWidth - 1.85f, 0.42f, WorldZ(1));
            SpawnBeam(west, east, SignalGold, 0.2f);
        }

        private void SpawnLaneShutdownCue(int laneId)
        {
            var offset = LaneOffset(laneId);
            var southwest = new Vector3(offset + 0.55f, 0.52f, 0.45f);
            var northeast = new Vector3(offset + LaneWidth - 1.55f, 0.52f, LaneLength - 0.45f);
            var northwest = new Vector3(offset + 0.55f, 0.52f, LaneLength - 0.45f);
            var southeast = new Vector3(offset + LaneWidth - 1.55f, 0.52f, 0.45f);
            SpawnBeam(southwest, northeast, LeakRed, 0.48f);
            SpawnBeam(northwest, southeast, LeakRed, 0.48f);
        }

        private void SpawnVictoryLaneCue(int laneId)
        {
            var center = LaneCenter(laneId);
            var offset = LaneOffset(laneId);
            SpawnEffect(center + Vector3.up * 0.38f, SignalGold, 1.05f, 0.45f);
            SpawnBeam(new Vector3(offset + 0.65f, 0.5f, BoardCenterZ), new Vector3(offset + LaneWidth - 1.65f, 0.5f, BoardCenterZ), SignalGold, 0.42f);
            SpawnBeam(new Vector3(offset + BoardCenterX, 0.5f, 0.65f), new Vector3(offset + BoardCenterX, 0.5f, LaneLength - 0.65f), SignalGold, 0.42f);
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
                SpawnFloatingText(senderPosition + Vector3.left * 0.42f, "SEND", color, 0.42f);
            }
            // The large "{qty}x {NAME}" spawn banner over the defender's gate was removed: it
            // dominated the top of the board and duplicated information the send dock already
            // shows. The sender-side SEND cue and the gate effect still mark the event.
            SpawnReducedEffectCue(defenderPosition, "SEND", color);
            PlaySound(sendClip);
        }

        /// <summary>One animating board label: where it started, when, and for how long.</summary>
        private readonly struct FloatingLabel
        {
            public FloatingLabel(GameObject @object, TMPro.TextMeshPro label, float spawnedAt, float duration)
            {
                Object = @object;
                Label = label;
                Origin = @object.transform.position;
                SpawnedAt = spawnedAt;
                Duration = duration;
            }

            public GameObject Object { get; }
            public TMPro.TextMeshPro Label { get; }
            public Vector3 Origin { get; }
            public float SpawnedAt { get; }
            public float Duration { get; }
        }
    }
}
