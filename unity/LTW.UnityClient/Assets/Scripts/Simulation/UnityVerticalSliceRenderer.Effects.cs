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
    /// World VFX: particle bursts, weapon beams, expanding rings, mortar shells in flight,
    /// spore fog, and the per-tower mechanic markers and servicing tethers.
    /// </summary>
    public sealed partial class UnityVerticalSliceRenderer
    {
        /// <summary>
        /// Height for board decals that span MORE than their own cell — currently Grovebond's bond
        /// ring and Thorn Snare's bramble zone.
        /// </summary>
        /// <remarks>
        /// Both were drawn at floor level (BoardTopY + ~0.012), which is correct for a decal that
        /// stays inside one cell, and wrong for these two. Grovebond's ring is 1.25-1.9 cells across
        /// so that it visibly reaches the neighbours it is bonded to, and Thorn's is sized to the
        /// braked span. Reaching onto a neighbouring cell means reaching under that cell's raised
        /// build plate, and the ring disappears beneath it — reported from play as "the sapling
        /// underglow is below some of the game board".
        ///
        /// Anchored just under <see cref="TowerBaseClearance"/> rather than to a measured plate
        /// height: towers stand ON the plates, so every plate is necessarily below the height a
        /// tower's own base sits at, and staying below that keeps these decals reading as painted on
        /// the board rather than floating across the towers they belong to.
        /// </remarks>
        private const float SpanningDecalLift = 0.07f;

        // Sphere-shaped impact effects and cube-shaped beams keep separate pools; sharing one made
        // them hand each other the wrong primitive shape (see GetPooled).
        private readonly Queue<GameObject> effectPool = new Queue<GameObject>();
        private readonly Queue<GameObject> beamPool = new Queue<GameObject>();
        private readonly Queue<GameObject> textPool = new Queue<GameObject>();

        private Material shockwaveRingMaterial;
        private readonly Queue<GameObject> shockwaveRingPool = new Queue<GameObject>();
        private readonly List<ExpandingRingEffect> activeShockwaveRings = new List<ExpandingRingEffect>();
        private readonly List<MortarShellEffect> activeMortarShells = new List<MortarShellEffect>();
        private readonly Dictionary<long, GameObject> towerMechanicMarkers = new Dictionary<long, GameObject>();

        /// <summary>One decal per braked cell, keyed by lane and cell rather than by tower.</summary>
        /// <remarks>
        /// Keyed by CELL because that is what the mechanic is: two thorn towers whose coverage
        /// overlaps brake the shared cell once, and keying by tower would stack two decals on it and
        /// draw it twice as dense for no mechanical reason. The simulation already de-duplicates by
        /// route index for the same reason; this keeps the drawing honest to that.
        /// </remarks>
        private readonly Dictionary<long, GameObject> brambleCellDecals = new Dictionary<long, GameObject>();

        private readonly HashSet<long> liveBrambleCells = new HashSet<long>();

        private readonly List<long> staleBrambleCells = new List<long>();
        // Keyed by the SERVICED tower's id, not the drone's — a tower can have at most one tether
        // regardless of how many drones are adjacent to it (see UpdateTowerServicingTether), so this
        // stays a strict one-per-tower dictionary just like towerMechanicMarkers above. Kept separate
        // from that dictionary rather than merged into it because the two hold different pooled
        // shapes (a flat ring vs. a stretched cube) drawn from different pools; see GetPooled's own
        // comment on why ring and beam pools must not mix.
        private readonly Dictionary<long, GameObject> towerServicingTethers = new Dictionary<long, GameObject>();

        /// <summary>One drifting fog disc per Spore Cloud Bloom, sized to that tower's attack range.</summary>
        /// <remarks>
        /// Its own dictionary and its own pool rather than sharing towerMechanicMarkers, for the same
        /// reason the servicing tethers have theirs: a pooled object carries the material it was
        /// built with, so mixing a fog quad into the shockwave-ring pool would hand a ring the fog
        /// shader (or the reverse) the first time one was recycled.
        /// </remarks>
        private readonly Dictionary<long, GameObject> towerSporeFog = new Dictionary<long, GameObject>();
        private readonly Queue<GameObject> sporeFogPool = new Queue<GameObject>();
        private Material sporeFogMaterial;

        private void SpawnEffect(Vector3 position, Color color) => SpawnEffect(position, color, 0.62f, 0.3f);

        private void SpawnReducedEffectCue(Vector3 position, string label, Color color)
        {
            if (PresentationPreferences.ReducedEffects)
            {
                // BoardLabelKind.Text: a cue word never merges, so this path only gains the
                // stacking every label gets; two "LEAK"s in one tick are still two "LEAK"s.
                SpawnFloatingText(position + Vector3.up * 0.18f, BoardLabelKind.Text, label, color, 0.5f);
            }
        }

        private void SpawnEffect(Vector3 position, Color color, float scale, float duration) =>
            SpawnEffect(position, color, scale, duration, BurstShape.Impact);

        /// <summary>
        /// Emits a particle burst for a game event.
        /// </summary>
        /// <remarks>
        /// Every effect in the game routes through here, which is why upgrading this one method from
        /// a primitive to a particle system upgrades roughly twenty call sites at once — build, sell,
        /// spawn, hit, kill, leak, income, elimination, muzzle flash and mortar impact.
        ///
        /// It used to spawn a pooled sphere, set its colour, and release it after `duration`. That is
        /// a shape which appears, holds and vanishes: nothing about it moves, so it read as a debug
        /// gizmo regardless of colour.
        ///
        /// No pooling and no TimedPresentation registration, unlike every other presentation object
        /// here. A burst emits into a shared long-lived system (see LTWParticleBurst), so it costs
        /// particles rather than GameObjects and there is nothing to release. Pooling an emitter per
        /// burst was tried first and measured: peak active presentation objects went from 2,769 to
        /// 6,082 on the same seed, because each emitter must outlive its own particles and most
        /// effects here are shorter than the pad that requires.
        ///
        /// The signature is unchanged so the existing call sites keep their tuned scales and
        /// durations. Those values were chosen against the old flash and still mean the same things —
        /// how big the event is and how long it lasts.
        /// </remarks>
        private void SpawnEffect(Vector3 position, Color color, float scale, float duration, BurstShape shape, Vector3 direction = default)
        {
            if (PresentationPreferences.ReducedEffects)
            {
                return;
            }

            BurstEmitter(shape)?.Emit(position, color, scale, duration, direction);
        }

        /// <summary>The shared emitter for one burst shape, created on first use.</summary>
        /// <remarks>
        /// Parented to this renderer so the emitters are torn down with the match rather than
        /// leaking across resets, and so they never appear in the pooled-object accounting the
        /// batch harness asserts on — they are fixtures, not pooled instances.
        /// </remarks>
        private LTWParticleBurst BurstEmitter(BurstShape shape)
        {
            if (burstEmitters.TryGetValue(shape, out var emitter) && emitter != null)
            {
                return emitter;
            }

            emitter = LTWParticleBurst.Create(transform, shape);
            burstEmitters[shape] = emitter;
            return emitter;
        }

        private readonly Dictionary<BurstShape, LTWParticleBurst> burstEmitters =
            new Dictionary<BurstShape, LTWParticleBurst>();

        /// <summary>Kills every live particle. Called on reset so effects do not survive a match.</summary>
        private void ClearBurstEmitters()
        {
            foreach (var pair in burstEmitters)
            {
                if (pair.Value != null)
                {
                    pair.Value.ClearAll();
                }
            }
        }

        /// <summary>Default beam thickness. Wider than the old 0.06 box, which was a wire.</summary>
        private const float DefaultBeamWidth = 0.16f;

        /// <summary>
        /// How much wider the beam's mesh is than the beam it draws. The shader treats the inner
        /// 1/<see cref="BeamHaloWidthScale"/> of the tube as the bright core and fades a halo across
        /// the rest, so <c>width</c> stays the width of the visible shot at every call site.
        /// </summary>
        private const float BeamHaloWidthScale = 3f;

        private Material weaponBeamMaterial;

        private Material WeaponBeamMaterial()
        {
            if (weaponBeamMaterial == null)
            {
                weaponBeamMaterial = BoardRenderResources.CreateWeaponBeamMaterial("LTW Weapon Beam");
            }

            return weaponBeamMaterial;
        }

        private void SpawnBeam(Vector3 start, Vector3 end, Color color, float duration) =>
            SpawnBeam(start, end, color, duration, DefaultBeamWidth, 1f);

        /// <summary>
        /// A tower's shot: a hot core in a soft glow, tapering toward the target.
        /// </summary>
        /// <remarks>
        /// The mesh is still a cube, but it is no longer drawn as one. LTWWeaponBeam fades alpha
        /// radially from the cube's axis, so the square cross-section never shows and the box reads
        /// as a round tube — which is why this does not need to billboard a quad toward an
        /// orthographic camera, the usual way beam rendering goes wrong.
        ///
        /// <paramref name="width"/> and <paramref name="intensity"/> exist for the per-line and
        /// per-tier work: a GROVE vine is thicker and dimmer than an ARCANE lance, and a tier-3 shot
        /// is heavier than a tier-1 one. Callers that do not care get the defaults.
        ///
        /// Still early-outs under ReducedEffects, so any mechanic whose ONLY tell is a beam is
        /// invisible at that setting. That is a known gap, not a new one.
        /// </remarks>
        /// <summary>Most beams alive at once before new ones are dropped.</summary>
        /// <remarks>
        /// Beams were the only pooled presentation with no ceiling on them. Floating labels have had
        /// both a cap and a lane filter for a while, and bursts were moved onto shared particle
        /// emitters, which left beams as the one thing that still allocated a GameObject per shot
        /// with nothing bounding the total. Measured on an eight-lane match: 26,071 live presentation
        /// objects against about 1,100 creeps and towers, so roughly 25,000 of them were beams.
        ///
        /// 96 was the first value here and it was wrong. It rested on an estimate of about 24 alive;
        /// measured, one lane peaked near 1,190, so the cap was dropping 17,491 on-camera beams
        /// against 11,573 drawn — the majority of what a player could see, and worst exactly when
        /// their own lane was busiest.
        ///
        /// 1,500 is a safety net rather than a clamp. Halving every tower's fire rate took the
        /// measured peak to about 700, and at that level the cap and no cap produce the same wall
        /// time (63.5s either way), so nothing is being bought by clipping. It exists only so a
        /// pathological case cannot allocate without bound.
        /// </remarks>
        private const int MaxLiveBeams = 1500;

        private void SpawnBeam(Vector3 start, Vector3 end, Color color, float duration, float width, float intensity)
        {
            if (PresentationPreferences.ReducedEffects)
            {
                return;
            }

            // Off-camera lanes cost exactly as much to draw and can never be seen. The board holds
            // eight lanes and the camera frames one, so this is most of the work — the same reason
            // SpawnFloatingText has filtered on it for a while, applied to the one cue that did not.
            if (!IsOnActiveLane(Vector3.Lerp(start, end, 0.5f)))
            {
                return;
            }

            // Dropped rather than queued, matching the floating labels: a shot that cannot be drawn
            // now is worthless a second later, and queueing keeps the cost while losing the timing.
            if (liveBeams >= MaxLiveBeams)
            {
                return;
            }

            var beam = GetPooled(beamPool, "TowerBeam", PrimitiveType.Cube);
            var midpoint = Vector3.Lerp(start, end, 0.5f);
            var distance = Vector3.Distance(start, end);
            beam.transform.position = midpoint;
            beam.transform.LookAt(end);
            // Wider than the beam being drawn: LTWWeaponBeam fades a soft halo out across the mesh
            // and keeps the requested width as its bright core, so the geometry has to extend past
            // the visible shot or there is no room for the falloff.
            var meshWidth = width * BeamHaloWidthScale;
            beam.transform.localScale = new Vector3(meshWidth, meshWidth, Mathf.Max(0.1f, distance));

            if (beam.TryGetComponent<Renderer>(out var renderer))
            {
                // sharedMaterial would recolour every live beam at once; each shot needs its own.
                renderer.material = WeaponBeamMaterial();
                var instance = renderer.material;
                instance.color = color;
                if (instance.HasProperty("_Color")) instance.SetColor("_Color", color);
                if (instance.HasProperty("_CoreColor"))
                {
                    // The core is the shot's colour pushed toward white, so every beam has a hotter
                    // inside than its edge without needing a second colour authored per caller.
                    instance.SetColor("_CoreColor", Color.Lerp(color, Color.white, 0.72f));
                }

                if (instance.HasProperty("_Intensity")) instance.SetFloat("_Intensity", intensity);
                // Driven from the same constant that widened the mesh, so the core stays exactly the
                // requested width however BeamHaloWidthScale is retuned.
                if (instance.HasProperty("_CoreRadius")) instance.SetFloat("_CoreRadius", 1f / BeamHaloWidthScale);
                // Casting shadows from a glow is wrong and costs a pass per shot.
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }

            liveBeams++;
            timedPresentations.Add(new TimedPresentation(beam, Time.time + duration, beamPool));
        }

        /// <summary>
        /// A flat ring that grows from startScale to endScale and fades to transparent over
        /// duration — a real shockwave, unlike <see cref="SpawnEffect"/>'s static spawn-hold-vanish
        /// flash. Built from the same quad+soft-falloff-shader combo as the ground contact shadow
        /// decals (<see cref="BoardRenderResources.ContactShadowMesh"/>/CreateContactShadowMaterial),
        /// since that shader already gives a soft radially-fading edge for free.
        /// </summary>
        private void SpawnExpandingRing(Vector3 position, Color color, float startScale, float endScale, float duration)
        {
            if (PresentationPreferences.ReducedEffects)
            {
                return;
            }

            var ring = GetPooledShockwaveRing();
            ring.transform.position = position;
            ring.transform.localScale = new Vector3(startScale, 1f, startScale);
            SetColor(ring, color);
            activeShockwaveRings.Add(new ExpandingRingEffect(ring, Time.time, duration, startScale, endScale, color));
        }

        /// <summary>
        /// Launches a mortar shell: an arcing projectile plus a ground telegraph at the cell it will
        /// land on.
        /// </summary>
        /// <remarks>
        /// The telegraph is the important half. A Foundry Core deals no damage when it fires and its
        /// shell lands half a second later, so without a marker on the ground the player has no way to
        /// read where or when — the tower becomes hidden dice and reads as broken. With it, the delay
        /// is fair information: you can see the shell in the air and the cell it is committed to.
        ///
        /// The whole flight is animated client-side from the launch event, which is why
        /// TowerFiredEvent carries ImpactTick and ImpactPosition. Keying the landing off the damage
        /// event instead would make a shell that hits nothing visually evaporate in mid-air — and
        /// while the simulation now refuses to fire shells it cannot land, a shell can still lose its
        /// target to another tower during the flight.
        /// </remarks>
        private void SpawnMortarShell(Vector3 from, Vector3 to, float flightSeconds)
        {
            if (PresentationPreferences.ReducedEffects || flightSeconds <= 0f)
            {
                return;
            }

            var shell = GetPooled(effectPool, "MortarShell", PrimitiveType.Sphere);
            shell.transform.localScale = Vector3.one * 0.22f;
            shell.transform.position = from + Vector3.up * MortarLaunchHeight;
            SetColor(shell, MortarShellColor);

            // A ring that contracts onto the impact cell, so its size reads as a countdown.
            var telegraph = GetPooledShockwaveRing();
            telegraph.transform.position = to + Vector3.up * 0.02f;
            telegraph.transform.localScale = new Vector3(MortarTelegraphStartScale, 1f, MortarTelegraphStartScale);
            SetColor(telegraph, MortarTelegraphColor);

            activeMortarShells.Add(new MortarShellEffect(shell, telegraph, from, to, Time.time, flightSeconds));
        }

        private void UpdateMortarShells()
        {
            for (var index = activeMortarShells.Count - 1; index >= 0; index--)
            {
                var shell = activeMortarShells[index];
                var t = Mathf.Clamp01((Time.time - shell.StartTime) / shell.Duration);

                // Straight line across the board, parabola in height: 4t(1-t) peaks at t=0.5 and is
                // zero at both ends, so the shell leaves the stacks and meets the ground exactly on
                // the telegraph.
                var ground = Vector3.Lerp(shell.From, shell.To, t);
                var lift = MortarLaunchHeight + MortarArcHeight * 4f * t * (1f - t);
                shell.Shell.transform.position = new Vector3(ground.x, shell.From.y + lift, ground.z);

                // Telegraph contracts and brightens as impact approaches.
                var telegraphScale = Mathf.Lerp(MortarTelegraphStartScale, MortarTelegraphEndScale, t);
                shell.Telegraph.transform.localScale = new Vector3(telegraphScale, 1f, telegraphScale);
                SetColor(shell.Telegraph, new Color(
                    MortarTelegraphColor.r,
                    MortarTelegraphColor.g,
                    MortarTelegraphColor.b,
                    Mathf.Lerp(MortarTelegraphColor.a * 0.55f, MortarTelegraphColor.a, t)));

                if (t < 1f)
                {
                    continue;
                }

                // Impact. The crater fires whether or not anything was standing there, so a shell that
                // loses its target still visibly lands rather than vanishing.
                SpawnExpandingRing(shell.To + Vector3.up * 0.05f, MortarImpactColor, 0.2f, 1.9f, 0.34f);
                SpawnEffect(shell.To + Vector3.up * 0.12f, MortarImpactColor, 0.6f, 0.24f);
                // The boom lands WITH the crater, not when the shell was fired — sound and
                // ring are one event to the eye, and the flight delay is the whole point of
                // a mortar. Panned by the impact point, which is a world position here.
                audioDirector.Play(LTWAudioCue.SplashImpact, PanForWorld(shell.To));

                ReleaseToPool(shell.Shell, effectPool);
                ReleaseToPool(shell.Telegraph, shockwaveRingPool);
                activeMortarShells.RemoveAt(index);
            }
        }

        /// <summary>
        /// Draws the standing ground marker a tower's mechanic needs, if it has one.
        /// </summary>
        /// <remarks>
        /// Two mechanics are invisible without this, and an invisible mechanic is a spreadsheet:
        ///
        /// Thorn Snare brakes creeps that stand in its zone. The creep does visibly crawl, but nothing
        /// says WHERE the zone is, so the player cannot place a second tower to exploit it. A decal
        /// over the braked cells makes the zone a thing you can build around.
        ///
        /// Grovebond gives a Sapling +1 damage per adjacent Grove tower. Three of its four states are
        /// otherwise pixel-identical — the only evidence is a damage number that has to be compared
        /// against a different sapling. The marker's brightness tracks the bonus.
        ///
        /// One pooled quad per tower, updated in place, released with the tower.
        /// </remarks>
        private void UpdateTowerMechanicMarker(
            long key,
            string towerId,
            GridPosition position,
            LaneId laneId,
            LTW.Simulation.Bridge.VerticalSliceSnapshot snapshot)
        {
            var isThorn = towerId.Contains("thorn");
            var isSapling = towerId.Contains("sapling");
            if ((!isThorn && !isSapling) || PresentationPreferences.ReducedEffects)
            {
                ReleaseTowerMechanicMarker(key);
                return;
            }

            if (!towerMechanicMarkers.TryGetValue(key, out var marker) || marker == null)
            {
                // GetPooledMechanicDecal, not GetPooledShockwaveRing: this is a standing zone
                // marker, not a transient burst, and finding #5 (2026-09-01 render review) asked
                // for standing mechanic decals to read as a crisp inset ring rather than a soft
                // glow. See MechanicDecalMaterial's remark for why that means a separate pool too.
                marker = GetPooledMechanicDecal();
                marker.name = $"TowerMechanicMarker_{key}";
                towerMechanicMarkers[key] = marker;
            }

            var centre = GridToWorld(position, laneId);
            if (isThorn)
            {
                // Thorn's zone is drawn per braked CELL by UpdateBrambleCells, from the cells the
                // simulation reports. This marker is released rather than drawn, so the tower does
                // not carry a second, differently-shaped claim about the same mechanic.
                ReleaseTowerMechanicMarker(key);
                return;
            }

            // Grovebond: brightness and size track the bonus, so a bonded cluster reads at a glance.
            var bonus = CountAdjacentGroveTowers(position, laneId, snapshot);
            if (bonus == 0)
            {
                // An unbonded sapling gets no ring at all. That is the clearest possible read of the
                // mechanic: the ring's presence means "this one is bonded".
                ReleaseTowerMechanicMarker(key);
                return;
            }

            marker.transform.position = new Vector3(centre.x, BoardTopY + SpanningDecalLift, centre.z);
            // Floors were originally 0.55 scale / 0.16 alpha, which at the common bonus of 1 was
            // invisible under the tower mesh — verified in a capture. The ring now starts wide enough
            // to clear the silhouette, and still grows with the bonus.
            //
            // Width is what fixed that invisibility, not alpha, which is why the alpha floor could
            // come back down to 0.15 without reopening it: the ring is over twice as wide as the one
            // that vanished, and it now sits above the build plates rather than under them.
            //
            // Finding #5 (2026-09-01 render review): 0.15 read as part of the same soft-decal mush
            // as every other ground mark, at exactly the bonus-of-1 case that comment calls the
            // common one. Raised to 0.32 so a freshly-bonded sapling is legible without a capture to
            // find it, still ramping up to the (now brighter) GrovebondMarkerColor ceiling as the
            // bonus grows — the ramp itself is unchanged, only its ends moved.
            var scale = Mathf.Lerp(1.25f, 1.9f, (bonus - 1) / 2f);
            marker.transform.localScale = new Vector3(scale, 1f, scale);
            SetColor(marker, new Color(
                GrovebondMarkerColor.r,
                GrovebondMarkerColor.g,
                GrovebondMarkerColor.b,
                Mathf.Lerp(0.32f, GrovebondMarkerColor.a, (bonus - 1) / 2f)));
        }

        /// <summary>
        /// Mirrors CombatService.GrovebondBonus: orthogonal only, same lane, same owner, capped at 3.
        /// </summary>
        /// <remarks>
        /// Duplicating the rule in presentation is a real risk of drift, but the alternative is a new
        /// snapshot field carrying a number that only exists to be drawn. Kept honest by the marker
        /// being the only consumer — if it disagrees with the damage numbers, the marker is wrong.
        /// </remarks>
        private static int CountAdjacentGroveTowers(
            GridPosition position,
            LaneId laneId,
            LTW.Simulation.Bridge.VerticalSliceSnapshot snapshot)
        {
            var adjacent = 0;
            for (var index = 0; index < snapshot.Towers.Count; index++)
            {
                var other = snapshot.Towers[index];
                if (other.LaneId.Value != laneId.Value)
                {
                    continue;
                }

                if (Mathf.Abs(other.Position.X - position.X) + Mathf.Abs(other.Position.Y - position.Y) != 1)
                {
                    continue;
                }

                var id = other.TowerId.Value;
                if (id.Contains("sapling") || id.Contains("bloomheart") || id.Contains("thorn")
                    || id.Contains("spore") || id.Contains("canopy"))
                {
                    adjacent++;
                }
            }

            return Mathf.Min(3, adjacent);
        }

        /// <summary>
        /// Draws a persistent tether from a tower to the Repair Drone Spire servicing it, if any.
        /// </summary>
        /// <remarks>
        /// CombatService.EffectiveCooldown reduces a serviced tower's cooldown by one tick and does
        /// nothing else visible, so without this the only evidence was a tower firing slightly
        /// faster than its stated cooldown — not something a player can see, only measure
        /// (GAMEPLAY_REVIEW_FINDINGS.md's open "Repair Drone's [buff] is invisible" item, written
        /// against the mechanic's earlier +1 range shape and stale since Servicing replaced it — a
        /// range halo would now show the wrong thing, since range no longer changes).
        ///
        /// Mirrors CombatService.IsServicedByDrone client-side, the same tradeoff already accepted
        /// for CountAdjacentGroveTowers above: duplicating the adjacency rule here risks drift from
        /// the simulation, but the alternative is a snapshot field that exists only to be drawn, and
        /// the tether being the only consumer keeps it honest — if it disagrees with a tower's
        /// actual fire rate, the tether is wrong, not the mechanic.
        ///
        /// A tower gets at most one tether even if multiple drones are adjacent, since Servicing
        /// does not stack (see EffectiveCooldown's own comment on why). The lowest EntityId among
        /// adjacent drones is picked so the choice is stable and independent of snapshot ordering,
        /// not because the specific choice of drone matters.
        /// </remarks>
        private void UpdateTowerServicingTether(long key, LTW.Simulation.Combat.TowerCombatState tower, LTW.Simulation.Bridge.VerticalSliceSnapshot snapshot)
        {
            if (PresentationPreferences.ReducedEffects || IsRepairDroneTower(tower.TowerId.Value))
            {
                // The drone itself never grows a tether toward whichever neighbour happens to
                // service IT in turn (two adjacent drones is a legal, if unusual, placement) — a
                // tether reads as "this tower is being helped", which is not the drone's story.
                ReleaseTowerServicingTether(key);
                return;
            }

            LTW.Simulation.Combat.TowerCombatState drone = null;
            for (var index = 0; index < snapshot.Towers.Count; index++)
            {
                var other = snapshot.Towers[index];
                if (other.EntityId.Equals(tower.EntityId))
                {
                    continue;
                }

                if (!other.LaneId.Equals(tower.LaneId) || !other.OwnerId.Equals(tower.OwnerId))
                {
                    continue;
                }

                if (!IsRepairDroneTower(other.TowerId.Value))
                {
                    continue;
                }

                if (Mathf.Abs(other.Position.X - tower.Position.X) + Mathf.Abs(other.Position.Y - tower.Position.Y) != 1)
                {
                    continue;
                }

                if (drone == null || other.EntityId.Value < drone.EntityId.Value)
                {
                    drone = other;
                }
            }

            if (drone == null)
            {
                ReleaseTowerServicingTether(key);
                return;
            }

            if (!towerServicingTethers.TryGetValue(key, out var tether) || tether == null)
            {
                tether = GetPooled(beamPool, "ServicingTether", PrimitiveType.Cube);
                towerServicingTethers[key] = tether;
            }

            var from = GridToWorld(tower.Position, tower.LaneId) + Vector3.up * ServicingTetherHeight;
            var to = GridToWorld(drone.Position, drone.LaneId) + Vector3.up * ServicingTetherHeight;
            var midpoint = Vector3.Lerp(from, to, 0.5f);
            var distance = Vector3.Distance(from, to);
            tether.transform.position = midpoint;
            tether.transform.LookAt(to);
            // Mesh is wider than the tether it draws, for the same reason SpawnBeam's is: the shader
            // keeps the inner 1/BeamHaloWidthScale as the bright core and fades a halo across the
            // rest, so the geometry has to extend past the visible line or the falloff has no room.
            var tetherMeshWidth = ServicingTetherThickness * BeamHaloWidthScale;
            tether.transform.localScale = new Vector3(tetherMeshWidth, tetherMeshWidth, Mathf.Max(0.1f, distance));
            // Assert the material, don't just tint it. This shares beamPool with SpawnBeam, so a
            // recycled object arrives carrying whatever the last user put on it. Exactly the failure
            // GetPooled's own comment describes for meshes, one dimension over.
            if (tether.TryGetComponent<Renderer>(out var tetherRenderer))
            {
                tetherRenderer.sharedMaterial = ServicingTetherMaterial();
            }
        }

        private Material servicingTetherMaterial;

        /// <summary>
        /// The Repair Drone's service link: the same beam shader as a shot, tuned to read as a
        /// standing connection rather than a fired one.
        /// </summary>
        /// <remarks>
        /// This was a solid opaque cube, and at board scale a 0.11 box in the drone's gold read as
        /// a bar of UI debris lying across the lane rather than an effect — which is what it was
        /// mistaken for. The information it carries is worth keeping and is not carried anywhere
        /// else: a serviced tower fires one tick faster (CombatService.IsServicedByDrone), and
        /// nothing on screen otherwise says which neighbour is getting it. Range has a halo;
        /// "this one is cooling down faster" has only this.
        ///
        /// So the fix is how it draws, not whether. LTWWeaponBeam already fades radially from the
        /// cube's axis, which turns the box into a soft tube and removes the hard slab edges.
        ///
        /// Three deliberate differences from a weapon shot:
        ///
        /// - `_Taper` 0. A shot narrows toward its target because it has a direction; a link between
        ///   two towers is symmetric and tapering it would imply a flow that does not exist.
        /// - `_Intensity` well below a shot's. This is on screen continuously for as long as the two
        ///   towers stand, where a shot flashes for a fraction of a second, so it has to sit under
        ///   the combat it shares a board with rather than compete.
        /// - Shared, not per-instance. Every tether is the same colour, so one material serves all of
        ///   them and keeps the SRP Batcher's path — unlike SpawnBeam, which needs a colour per shot.
        ///
        /// The colour is deliberately unchanged: (0.95, 0.82, 0.45) is the Repair Drone's own entry
        /// in TowerCatalog, so the link reads as belonging to the tower that casts it.
        /// </remarks>
        private Material ServicingTetherMaterial()
        {
            if (servicingTetherMaterial == null)
            {
                servicingTetherMaterial = BoardRenderResources.CreateWeaponBeamMaterial("LTW Servicing Tether");
                var material = servicingTetherMaterial;
                material.color = ServicingTetherColor;
                if (material.HasProperty("_Color")) material.SetColor("_Color", ServicingTetherColor);
                if (material.HasProperty("_CoreColor"))
                {
                    material.SetColor("_CoreColor", Color.Lerp(ServicingTetherColor, Color.white, 0.55f));
                }

                if (material.HasProperty("_CoreRadius")) material.SetFloat("_CoreRadius", 1f / BeamHaloWidthScale);
                if (material.HasProperty("_Taper")) material.SetFloat("_Taper", 0f);
                if (material.HasProperty("_Intensity")) material.SetFloat("_Intensity", ServicingTetherIntensity);
            }

            return servicingTetherMaterial;
        }

        /// <summary>Dimmer than a shot, because it is on screen continuously rather than for a frame.</summary>
        private const float ServicingTetherIntensity = 0.55f;

        private static bool IsRepairDroneTower(string towerId) => towerId.IndexOf("repair_drone", StringComparison.OrdinalIgnoreCase) >= 0;

        private void ReleaseTowerServicingTether(long key)
        {
            if (!towerServicingTethers.TryGetValue(key, out var tether))
            {
                return;
            }

            if (tether != null)
            {
                ReleaseToPool(tether, beamPool);
            }

            towerServicingTethers.Remove(key);
        }

        /// <summary>
        /// Draws the braked ground, one decal per cell the simulation reports as under bramble.
        /// </summary>
        /// <remarks>
        /// Driven entirely by <c>snapshot.BrambleCells</c>, which is resolved through the same
        /// <c>BuildBrambleZones</c> that halves creep pace — so the ground drawn and the ground that
        /// brakes are the same set by construction, and a test asserts they agree every tick. The
        /// previous per-tower disc computed its own footprint and had already drifted from the rule.
        ///
        /// Cells no longer braked are released each frame, so selling a Thorn Snare or re-mazing a
        /// lane clears its ground immediately rather than leaving decals over cells that no longer
        /// slow anything.
        /// </remarks>
        private void UpdateBrambleCells(LTW.Simulation.Bridge.VerticalSliceSnapshot snapshot)
        {
            liveBrambleCells.Clear();

            if (!PresentationPreferences.ReducedEffects)
            {
                foreach (var lane in snapshot.BrambleCells)
                {
                    foreach (var cell in lane.Value)
                    {
                        var key = BrambleCellKey(lane.Key, cell);
                        liveBrambleCells.Add(key);

                        if (!brambleCellDecals.TryGetValue(key, out var decal) || decal == null)
                        {
                            // GetPooledMechanicDecal, not GetPooledShockwaveRing — see that pool's
                            // remark: a standing braked-cell marker is meant to read as a crisp
                            // hatched cell (finding #5, 2026-09-01), not the soft transient glow
                            // shockwave bursts use.
                            decal = GetPooledMechanicDecal();
                            decal.name = $"BrambleCell_{lane.Key.Value}_{cell.X}_{cell.Y}";
                            brambleCellDecals[key] = decal;
                        }

                        var centre = GridToWorld(cell, lane.Key);
                        decal.transform.position = new Vector3(centre.x, BoardTopY + SpanningDecalLift, centre.z);
                        decal.transform.localScale = new Vector3(BrambleCellScale, 1f, BrambleCellScale);
                        SetColor(decal, BrambleMarkerColor);
                    }
                }
            }

            if (brambleCellDecals.Count == liveBrambleCells.Count)
            {
                return;
            }

            // Collected into a reusable scratch list rather than a LINQ projection, because a
            // dictionary cannot be modified while it is being enumerated and this file follows the
            // renderer's no-LINQ-in-render-paths rule. The list is a field so a frame that releases
            // nothing allocates nothing.
            staleBrambleCells.Clear();
            foreach (var existing in brambleCellDecals)
            {
                if (!liveBrambleCells.Contains(existing.Key))
                {
                    staleBrambleCells.Add(existing.Key);
                }
            }

            foreach (var key in staleBrambleCells)
            {
                if (brambleCellDecals.TryGetValue(key, out var stale) && stale != null)
                {
                    ReleaseToPool(stale, mechanicDecalPool);
                }

                brambleCellDecals.Remove(key);
            }
        }

        /// <summary>Lane and cell packed into one key. The board is far smaller than the 16 bits each gets.</summary>
        private static long BrambleCellKey(LaneId laneId, GridPosition cell) =>
            ((long)laneId.Value << 32) | ((long)(ushort)cell.X << 16) | (ushort)cell.Y;

        private void ReleaseTowerMechanicMarker(long key)
        {
            if (!towerMechanicMarkers.TryGetValue(key, out var marker))
            {
                return;
            }

            if (marker != null)
            {
                ReleaseToPool(marker, mechanicDecalPool);
            }

            towerMechanicMarkers.Remove(key);
        }

        private void UpdateExpandingRings()
        {
            // UpdateDyingCreeps (Pooling.cs) piggybacks on this method rather than getting its own
            // Update() hook: Update() lives in UnityVerticalSliceRenderer.cs, out of scope for
            // this pass, but it already calls UpdateExpandingRings every frame — before the
            // presentationDetail early return — which is exactly the unconditional per-frame
            // timing a dying creep's shrink/sink needs. See UpdateDyingCreeps' own remark
            // (finding #8, 2026-09-01 render review).
            UpdateDyingCreeps();

            for (var index = activeShockwaveRings.Count - 1; index >= 0; index--)
            {
                var ring = activeShockwaveRings[index];
                var t = Mathf.Clamp01((Time.time - ring.StartTime) / ring.Duration);
                var scale = Mathf.Lerp(ring.StartScale, ring.EndScale, t);
                ring.Object.transform.localScale = new Vector3(scale, 1f, scale);
                SetColor(ring.Object, new Color(ring.BaseColor.r, ring.BaseColor.g, ring.BaseColor.b, ring.BaseColor.a * (1f - t)));

                if (t >= 1f)
                {
                    ReleaseToPool(ring.Object, shockwaveRingPool);
                    activeShockwaveRings.RemoveAt(index);
                }
            }
        }

        private Material ShockwaveRingMaterial()
        {
            if (shockwaveRingMaterial == null)
            {
                shockwaveRingMaterial = BoardRenderResources.CreateContactShadowMaterial(
                    "LTW Shockwave Ring",
                    Color.white,
                    0.55f);
            }

            return shockwaveRingMaterial;
        }

        /// <summary>Sharp enough to read as a stencilled edge rather than a glow — the "2-px edge"
        /// half of finding #5's "0.5+ alpha with a 2-px edge" ask for mechanic decals.</summary>
        private const float MechanicDecalSoftness = 0.16f;

        private Material mechanicDecalMaterial;
        private readonly Queue<GameObject> mechanicDecalPool = new Queue<GameObject>();

        /// <summary>
        /// A sharper-edged sibling of <see cref="ShockwaveRingMaterial"/>, for the STANDING mechanic
        /// markers — Thorn's braked cells, Grovebond's bond ring — rather than transient bursts.
        /// </summary>
        /// <remarks>
        /// Finding #5 (2026-09-01 render review): every soft-edged decal on the board — cast shadow,
        /// contact shadow, owner glow, mechanic marker — blurred into the same mush, and nothing read
        /// as a zone a player could plan around. A transient burst (<see cref="SpawnExpandingRing"/>,
        /// the mortar telegraph) is still meant to read as a flash of light dissipating, so those keep
        /// <see cref="ShockwaveRingMaterial"/> unchanged at 0.55 softness; a standing zone marker is
        /// meant to read as a stencilled ring or hatched cell, so this variant uses
        /// <see cref="MechanicDecalSoftness"/> instead. <see cref="BoardRenderResources.CreateContactShadowMaterial"/>
        /// already takes softness as a parameter — this is that same call with a different number,
        /// not a new shader.
        ///
        /// A separate material AND a separate pool (<see cref="mechanicDecalPool"/> /
        /// <see cref="GetPooledMechanicDecal"/>), not this material swapped onto shockwaveRingPool's
        /// objects — the same reason towerSporeFog and towerServicingTethers each keep their own pool
        /// rather than sharing shockwaveRingPool/beamPool (see those fields' own remarks): a pooled
        /// object here is only ever given its material once, at creation, and colour changes after
        /// that go through <see cref="SetColor"/> (which clones whatever material the object already
        /// has, not the caller's). Recycling a mechanic decal through the shockwave-ring pool would
        /// hand some future burst this shader's crisp edge, or hand a future mechanic marker a soft
        /// one, the first time the two pools' objects changed hands.
        /// </remarks>
        private Material MechanicDecalMaterial()
        {
            if (mechanicDecalMaterial == null)
            {
                mechanicDecalMaterial = BoardRenderResources.CreateContactShadowMaterial(
                    "LTW Mechanic Decal",
                    Color.white,
                    MechanicDecalSoftness);
            }

            return mechanicDecalMaterial;
        }

        private GameObject GetPooledMechanicDecal()
        {
            if (mechanicDecalPool.Count > 0)
            {
                var pooled = mechanicDecalPool.Dequeue();
                pooled.SetActive(true);
                return pooled;
            }

            var decal = new GameObject("MechanicDecal");
            decal.AddComponent<MeshFilter>().sharedMesh = BoardRenderResources.ContactShadowMesh;
            var renderer = decal.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = MechanicDecalMaterial();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            return decal;
        }

        /// <summary>
        /// Green spore fog spreading from a Spore Cloud Bloom out to the edge of its range.
        /// </summary>
        /// <remarks>
        /// The fog IS the range indicator: it is densest over the tower and fades to nothing exactly
        /// where the tower stops reaching, so a player can read how far it covers without a range
        /// ring drawn on top.
        ///
        /// One honest imprecision. Range in the simulation is MANHATTAN — CombatService.IsInRange
        /// sums |dx| + |dy| — so the true footprint is a diamond, while this is a circle. A circle
        /// inscribed to touch the diamond's points therefore overstates the diagonals, and one
        /// shrunk to fit understates the axes. It is drawn at the full range because fog with a
        /// visible diamond edge would look authored rather than atmospheric, and because the whole
        /// point of the gradient is that the boundary is not locatable anyway. If the fog is ever
        /// promoted from atmosphere to a precise range READOUT, this has to become a diamond.
        ///
        /// Scale is diameter, hence 2x the range. Sat just above the spanning board decals so the
        /// fog layers over the lane rather than fighting the bramble and bond rings for the same
        /// millimetre.
        /// </remarks>
        private void UpdateSporeFog(long key, string towerId, Vector3 towerPosition, float rangeCells)
        {
            if (!ContainsRole(towerId, "spore") || PresentationPreferences.ReducedEffects)
            {
                ReleaseSporeFog(key);
                return;
            }

            if (!towerSporeFog.TryGetValue(key, out var fog) || fog == null)
            {
                fog = GetPooledSporeFog();
                fog.name = $"SporeFog_{key}";
                towerSporeFog[key] = fog;
            }

            var diameter = Mathf.Max(1f, rangeCells * 2f);
            fog.transform.position = new Vector3(towerPosition.x, BoardTopY + SporeFogLift, towerPosition.z);
            fog.transform.localScale = new Vector3(diameter, 1f, diameter);
        }

        /// <summary>Just above SpanningDecalLift, so fog reads as sitting over the ground markings.</summary>
        private const float SporeFogLift = 0.075f;

        /// <summary>
        /// A tower's attack range in cells, read from the simulation and cached per tower id.
        /// </summary>
        /// <remarks>
        /// Read rather than copied: a client-side table of ranges is exactly the drift that put
        /// three different Arrow costs in the codebase before UnityCommandAdapter started reading
        /// cost from ContentCatalog. Cached because this runs per Spore Cloud per frame and the
        /// lookup is a linear scan of the tower list.
        ///
        /// Falls back to 3 — Spore Cloud's authored range — only if the driver is not wired yet, so
        /// a fog that appears before the simulation is up is the right size rather than a dot.
        /// </remarks>
        private readonly Dictionary<string, float> towerRangeCells = new Dictionary<string, float>();

        private float TowerRangeCells(string towerId)
        {
            if (towerRangeCells.TryGetValue(towerId, out var cached))
            {
                return cached;
            }

            var range = 3f;
            var catalog = simulationDriver != null ? simulationDriver.Content : null;
            if (catalog != null)
            {
                foreach (var tower in catalog.Towers)
                {
                    if (tower.Id.Value == towerId)
                    {
                        range = tower.RangeCells;
                        break;
                    }
                }

                towerRangeCells[towerId] = range;
            }

            return range;
        }

        /// <remarks>
        /// Finding #5 (2026-09-01 render review) asked mechanic decals generally to move toward a
        /// crisper, higher-alpha read, and named spore fog among the things stacking on the board.
        /// Alpha alone moved here, 0.26 -> 0.36: softness stays at 0.95 deliberately, because both
        /// this file's own remark on <see cref="UpdateSporeFog"/> and the shader's header
        /// (LTWSporeFog.shader) document that a spread-across-the-whole-radius gradient with no
        /// locatable edge IS the mechanic's read — sharpening it the way <see cref="MechanicDecalMaterial"/>
        /// does for Bramble/Grovebond would turn the range indicator into something a player could
        /// (wrongly) read as a precise diamond boundary, which those two remarks specifically call
        /// out as a future-me trap. Raising alpha, which only affects how STRONG the same gradient
        /// reads, was judged to satisfy the finding's "stacking is illegible" complaint without
        /// fighting that design.
        /// </remarks>
        private Material SporeFogMaterial()
        {
            if (sporeFogMaterial == null)
            {
                sporeFogMaterial = BoardRenderResources.CreateSporeFogMaterial(
                    "LTW Spore Fog",
                    new Color(0.42f, 0.86f, 0.34f, 0.36f),
                    softness: 0.95f,
                    churn: 0.55f,
                    speed: 0.45f);
            }

            return sporeFogMaterial;
        }

        private GameObject GetPooledSporeFog()
        {
            if (sporeFogPool.Count > 0)
            {
                var pooled = sporeFogPool.Dequeue();
                pooled.SetActive(true);
                return pooled;
            }

            var fog = new GameObject("SporeFog");
            fog.AddComponent<MeshFilter>().sharedMesh = BoardRenderResources.ContactShadowMesh;
            var renderer = fog.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = SporeFogMaterial();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            return fog;
        }

        private void ReleaseSporeFog(long key)
        {
            if (!towerSporeFog.TryGetValue(key, out var fog))
            {
                return;
            }

            if (fog != null)
            {
                fog.SetActive(false);
                sporeFogPool.Enqueue(fog);
            }

            towerSporeFog.Remove(key);
        }

        private GameObject GetPooledShockwaveRing()
        {
            if (shockwaveRingPool.Count > 0)
            {
                var pooled = shockwaveRingPool.Dequeue();
                pooled.SetActive(true);
                return pooled;
            }

            var ring = new GameObject("ShockwaveRing");
            ring.AddComponent<MeshFilter>().sharedMesh = BoardRenderResources.ContactShadowMesh;
            var renderer = ring.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = ShockwaveRingMaterial();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            return ring;
        }

        /// <summary>
        /// One decal per braked cell, from the cells the simulation says are braked.
        /// </summary>
        /// <remarks>
        /// Replaces a fixed 2.6-cell disc centred on the tower, which was wrong in two ways that
        /// compounded into the mechanic being invisible — the owner did not know the game HAD a
        /// slowing tower.
        ///
        /// It was the wrong SIZE, and drifting. Its comment read "CombatService.BrambleZoneCells is
        /// 3, and the zone starts at the first route cell in range", which stopped being true: the
        /// cap was lifted when the maze showed 3 cells out of 52 contributed 0%, so the zone now
        /// follows the tower's real coverage with 3 as a MINIMUM. A disc sized to the old cap
        /// under-draws every zone bigger than it, and on a serpentine route that passes one tower
        /// several times the real zone is several separate runs the disc cannot express at all.
        /// That is a client-side copy of a simulation rule going stale — the exact failure this
        /// codebase keeps recording.
        ///
        /// It was the wrong SHAPE, which is what forced it faint. Alpha went 0.34 -> 0.15 because a
        /// solid disc that size read as "a purple slab over the lane". The slab was the problem, not
        /// the opacity: a shape that covers cells the brake does not is bound to look wrong at any
        /// alpha loud enough to notice. Per-cell decals cover only braked ground, so they can be
        /// legible without lying — hence the higher alpha here.
        ///
        /// Raised again, 0.34 -> 0.52, for finding #5 (2026-09-01 render review): "Mechanic decals
        /// become crisp inset rings or hatched cells at 0.5+ alpha with a 2-px edge." Paired with
        /// MechanicDecalMaterial's sharper falloff (see GetPooledMechanicDecal, now used here instead
        /// of GetPooledShockwaveRing) rather than fought against it — a soft edge at low alpha and a
        /// hard edge at low alpha both under-read; this decal only needed one of the two fixed to look
        /// "too much" again, so both moved together deliberately rather than the shape fix alone
        /// being asked to also cover the brightness gap on its own.
        /// </remarks>
        private static readonly Color BrambleMarkerColor = new Color(0.46f, 0.26f, 0.62f, 0.52f);

        /// <summary>A shade under a full cell, so adjacent braked cells read as a patch with texture
        /// rather than one flat rectangle.</summary>
        private const float BrambleCellScale = 0.92f;
        /// <summary>
        /// Alpha dropped from 0.7, for exactly the reason recorded on <see cref="BrambleMarkerColor"/>
        /// above.
        /// </summary>
        /// <remarks>
        /// Both markers were tuned while most of their footprint was hidden under the raised build
        /// plates. Lifting them to <see cref="SpanningDecalLift"/> revealed the whole area and made
        /// both far stronger than intended — Thorn's was corrected then ("thorn ground bloom is too
        /// much"), Grovebond's was not, and it drew the same report from play. This applies the same
        /// correction: Thorn went 0.34 to 0.15, so this takes the pair down by the same proportion.
        ///
        /// The SCALE is deliberately left alone. 1.25-1.9 cells is not decoration — it is how the
        /// ring visibly reaches the neighbours it is bonded to, which is the whole read of the
        /// mechanic, and it is also what made the ring legible when a narrower one was invisible
        /// under the tower mesh. Brightness is the dial that was wrong; width was not.
        ///
        /// Raised again, 0.30 -> 0.52, for finding #5 (2026-09-01 render review) alongside Bramble's
        /// matching move above and the ring's move from GetPooledShockwaveRing to the sharper-edged
        /// GetPooledMechanicDecal (see UpdateTowerMechanicMarker) — "0.5+ alpha with a 2-px edge".
        /// UpdateTowerMechanicMarker's alpha floor for a fresh bond (bonus of 1) moved with it, 0.15
        /// -> 0.32, so the common case this file's own comment flags is not left at the old faint
        /// value while only the rare bonus-of-3 ceiling gets brighter.
        /// </remarks>
        private static readonly Color GrovebondMarkerColor = new Color(0.55f, 0.95f, 0.38f, 0.52f);

        // Repair Drone Spire's own catalog accent (TowerCatalog.cs, id 9, label "DRONE"), reused here
        // rather than an invented color so the tether reads as belonging to the drone at a glance.
        // Thickness/height/alpha were raised past a first guess (0.05/0.18/0.62) after a capture at
        // the actual in-game ActiveLane camera distance showed it lost against both towers' own
        // range-halo spheres — the same failure Grovebond's ring hit originally ("invisible under
        // the tower mesh... starts wide enough to clear the silhouette"). A further attempt at 0.48
        // rose into the always-on-top role-marker text layer and read WORSE, not better, so this
        // stayed at the value that measurably improved on the first guess without competing with
        // that text.
        private static readonly Color ServicingTetherColor = new Color(0.95f, 0.82f, 0.45f, 0.85f);
        private const float ServicingTetherThickness = 0.11f;
        private const float ServicingTetherHeight = 0.34f;

        private const float MortarLaunchHeight = 0.62f;
        private const float MortarArcHeight = 1.15f;
        private const float MortarTelegraphStartScale = 1.6f;
        private const float MortarTelegraphEndScale = 0.62f;
        private static readonly Color MortarShellColor = new Color(1f, 0.62f, 0.24f, 1f);
        private static readonly Color MortarTelegraphColor = new Color(1f, 0.45f, 0.18f, 0.72f);
        private static readonly Color MortarImpactColor = new Color(1f, 0.55f, 0.2f, 1f);

        /// <summary>A shell in flight, with the ground telegraph marking where it will land.</summary>
        private readonly struct MortarShellEffect
        {
            public MortarShellEffect(GameObject shell, GameObject telegraph, Vector3 from, Vector3 to, float startTime, float duration)
            {
                Shell = shell;
                Telegraph = telegraph;
                From = from;
                To = to;
                StartTime = startTime;
                Duration = duration;
            }

            public GameObject Shell { get; }
            public GameObject Telegraph { get; }
            public Vector3 From { get; }
            public Vector3 To { get; }
            public float StartTime { get; }
            public float Duration { get; }
        }

        private readonly struct ExpandingRingEffect
        {
            public ExpandingRingEffect(GameObject @object, float startTime, float duration, float startScale, float endScale, Color baseColor)
            {
                Object = @object;
                StartTime = startTime;
                Duration = duration;
                StartScale = startScale;
                EndScale = endScale;
                BaseColor = baseColor;
            }

            public GameObject Object { get; }
            public float StartTime { get; }
            public float Duration { get; }
            public float StartScale { get; }
            public float EndScale { get; }
            public Color BaseColor { get; }
        }
    }
}
