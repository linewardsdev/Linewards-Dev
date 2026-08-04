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
    /// Creep presentation: travel interpolation, per-role motion profiles, rig playback speed,
    /// body colour, health bars and the readability overlays.
    /// </summary>
    public sealed partial class UnityVerticalSliceRenderer
    {
        private readonly Dictionary<long, Animator> creepAnimators = new Dictionary<long, Animator>();

        /// <summary>Measured local-space body top per creep role, used to place health bars.</summary>
        private static readonly Dictionary<string, float> CreepBodyTopByRole = new Dictionary<string, float>();

        /// <summary>Local-space gap between a creep's body top and its health bar.</summary>
        private const float HealthBarGap = 0.12f;

        /// <summary>
        /// Reference creep speed, in cells per tick, that a walk clip is treated as authored for.
        /// A creep at this speed plays its clip at 1.0x.
        /// </summary>
        /// <remarks>
        /// 1 because five of the eight rigged creeps ship at speed 1 (Brute, Obsidian Brute,
        /// Fracture Burrower, Aegis Warden, Siege Colossus) — normalising on the majority means the
        /// common case keeps exactly the playback its rig was tuned against.
        /// </remarks>
        private const int CreepWalkReferenceSpeed = 1;

        /// <summary>
        /// How much of the speed difference the playback rate actually takes up, as an exponent.
        /// </summary>
        /// <remarks>
        /// 0.5, not 1.0, and the under-correction is deliberate. Matching stride to ground speed
        /// exactly would want a linear exponent, but two things make linear the wrong target here:
        ///
        /// - Full correction is unreachable anyway. SpeedPerSecond is cells per TICK at 4 ticks/sec,
        ///   so a speed-2 creep crosses 8 world units/sec on a board where a cell is one unit. The
        ///   Spire Turret Walker's rig was measured at 8.3x residual foot skate even after its stride
        ///   and leg cycle were tuned specifically for this (docs/GD_TUNING_LOG.md, 2026-07-28), and
        ///   that entry also records that pushing the leg cycle further "starts to read as a blur at
        ///   24 footfalls/sec, which trades one artefact for another". Playing a clip at 8x would be
        ///   squarely in that territory.
        /// - The fast creeps are already partly compensated. The five Meshy-rigged bipeds got their
        ///   clip chosen BY speed when they landed — running for the fast ones, walking for the slow
        ///   ones (commit a88dae1) — precisely because a run cycle's longer stride and faster cadence
        ///   cut skate. Layering full linear scaling on top would double-count that.
        ///
        /// So the goal is not to eliminate skate, which the board's scale forbids. It is to stop the
        /// roster sharing ONE playback rate across a 3x speed spread, so a Siege Colossus lumbers and
        /// a Zephyr Wraith scurries instead of both cycling their limbs identically. Yields 1.00x /
        /// 1.41x / 1.73x at speeds 1 / 2 / 3.
        ///
        /// These are reasoned values, not observed ones — nobody has watched them yet. Tracked as its
        /// own visual-tuning item on the gameplay checklist.
        /// </remarks>
        private const float CreepWalkSpeedExponent = 0.5f;

        /// <summary>Playback clamp, so a future speed value cannot drive the clip to a blur or a stall.</summary>
        private const float CreepWalkPlaybackMin = 0.6f;
        private const float CreepWalkPlaybackMax = 2.2f;

        /// <summary>
        /// Matches a rigged creep's clip playback to how fast it actually crosses the board.
        /// </summary>
        /// <remarks>
        /// Without this every rigged creep played its clip at the authored rate no matter how fast it
        /// travelled, which is the runtime half of the foot-skate finding in GD_TUNING_LOG's
        /// 2026-07-28 entry: "Nothing syncs animator playback to movement speed; the clip loops at its
        /// authored rate while the simulation translates the creep independently." That entry attacked
        /// the problem from inside the rig, which was the only lever a Blender script has. This is the
        /// lever it could not reach.
        ///
        /// Speed comes from the presentation snapshot rather than a client-side table keyed on the
        /// creep id — the same correction item 12 made for max health, and for the same reason.
        /// </remarks>
        private void UpdateCreepAnimationSpeed(long key, GameObject instance, string creepId, int speedPerSecond)
        {
            if (!IsRiggedCreep(instance, creepId))
            {
                return;
            }

            if (!creepAnimators.TryGetValue(key, out var animator) || animator == null)
            {
                // Cached per active creep: GetComponentInChildren walks the hierarchy, which is far
                // too expensive to repeat every frame per creep (OPEN_ITEMS.md's retired 2026-07-29 review, grouped smaller items already flags
                // per-frame work in this Update path).
                animator = instance.GetComponentInChildren<Animator>(true);
                creepAnimators[key] = animator;
            }

            if (animator == null)
            {
                return;
            }

            var ratio = Mathf.Max(1, speedPerSecond) / (float)CreepWalkReferenceSpeed;
            animator.speed = Mathf.Clamp(
                Mathf.Pow(ratio, CreepWalkSpeedExponent),
                CreepWalkPlaybackMin,
                CreepWalkPlaybackMax);
        }

        /// <summary>
        /// Whether a creep role's prefab carries a skeletal rig, cached per role. Rigged creeps let
        /// their animation clip own idle motion instead of the procedural bob, which would
        /// otherwise double up with the clip's own body movement.
        /// </summary>
        private static readonly Dictionary<string, bool> RiggedByRole = new Dictionary<string, bool>();

        private static bool IsRiggedCreep(GameObject instance, string creepId)
        {
            if (RiggedByRole.TryGetValue(creepId, out var cached))
            {
                return cached;
            }

            var rigged = instance.GetComponentInChildren<Animator>(true) != null;
            RiggedByRole[creepId] = rigged;
            return rigged;
        }

        /// <summary>
        /// How far a creep may move in one update and still be interpolated rather than snapped.
        /// Anything beyond this is treated as a teleport (a lane transfer) rather than travel.
        /// </summary>
        /// <remarks>
        /// This has to sit above the fastest creep's per-tick travel and below <see cref="LaneSpacing"/>.
        /// CombatService.MoveCreeps advances a creep by SpeedPerSecond WHOLE CELLS per tick, and one
        /// cell is one world unit, so a creep's per-tick travel in world units equals its speed
        /// value. The old 2.5 hardcode predated any creep faster than 2: Crystal Wisp ships at
        /// speed 3, cleared the threshold on every single tick, and so snapped continuously instead
        /// of ever interpolating — reading as teleporting across the lane. Half of LaneSpacing
        /// leaves headroom for future speed tuning while still snapping a lane transfer, which
        /// moves a creep a full LaneSpacing sideways and genuinely is a teleport.
        /// CreepSpeedTests guards the lower bound so a future speed bump fails a test rather than
        /// silently reintroducing the teleport.
        /// </remarks>
        private const float CreepTeleportSnapDistance = LaneSpacing * 0.5f;

        /// <summary>
        /// Where a creep actually sits between its current cell and the one it is walking toward.
        /// </summary>
        /// <remarks>
        /// The simulation moves a creep in whole cells: it banks MovementProgress each tick and steps
        /// exactly one cell when that reaches MovementCost. At the original pace that was one cell per
        /// tick, so "current cell" was never more than 0.25s stale and the lerp below hid it. Slowing
        /// creeps to a third of that (CombatService.BaseMovementCost) makes it up to 0.75s stale, at
        /// which point a creep visibly holds still and then hops.
        ///
        /// So the cell pair and the progress between them are read from the snapshot and resolved
        /// here, on the presentation side of the boundary — the simulation stays all-integer and the
        /// float division happens in the only layer that wants a float. Clamped because a braked
        /// creep's real cost is double what MovementCost reports (see the snapshot's own remark), so
        /// its progress legitimately runs past one cell's worth.
        /// </remarks>
        private static Vector3 CreepTravelPosition(CreepPresentationSnapshot creep)
        {
            var from = GridToWorld(creep.Position, creep.LaneId);
            if (creep.MovementCost <= 0 || creep.NextPosition.Equals(creep.Position))
            {
                return from;
            }

            var fraction = Mathf.Clamp01(creep.MovementProgress / (float)creep.MovementCost);
            return Vector3.Lerp(from, GridToWorld(creep.NextPosition, creep.LaneId), fraction);
        }

        private static void SetCreepTransform(GameObject instance, Vector3 lanePosition, LaneId laneId, string creepId, CreepVisualProfile visualProfile, bool snapToTarget, float hitFlashUntil, long key, float facingYaw)
        {
            var roleMotion = CreepRoleMotion(creepId, visualProfile, hitFlashUntil, IsRiggedCreep(instance, creepId), key);
            var targetPosition = lanePosition + CreepRoleOffset(creepId) + roleMotion.PositionOffset;
            instance.transform.position = snapToTarget || Vector3.Distance(instance.transform.position, targetPosition) > CreepTeleportSnapDistance
                ? targetPosition
                : Vector3.Lerp(instance.transform.position, targetPosition, Mathf.Clamp01(Time.deltaTime * 8f));
            instance.transform.localScale = CreepRoleScale(creepId, visualProfile) * roleMotion.ScaleMultiplier;
            // Facing FIRST, then the idle motion, so sway and spin read as happening to a creep that
            // is pointing somewhere rather than replacing where it points. The old line applied only
            // the idle rotation, which is why a creep never turned: it faced its import orientation
            // forever and strafed through every corner.
            instance.transform.rotation = Quaternion.Euler(0f, facingYaw, 0f) * roleMotion.Rotation;
        }

        private static void ApplyCreepColor(GameObject creepObject, string creepId, int senderId, CreepVisualProfile visualProfile, float healthFraction, bool isHitFlashing)
        {
            var bodyColor = CreepBodyColor(creepId, senderId, healthFraction, isHitFlashing);
            var senderColor = SenderColor(senderId);
            var damageColor = healthFraction < 0.35f
                ? new Color(0.68f, 0.22f, 0.16f)
                : bodyColor;

            if (visualProfile == null || visualProfile.Prefab == null)
            {
                SetColor(creepObject, bodyColor);
                return;
            }

            SetProfileColor(creepObject, visualProfile.BodyRendererPath, bodyColor);
            SetProfileColors(creepObject, visualProfile.SenderAccentRendererPaths, AccentPoolColor(senderColor));
            SetProfileColors(creepObject, visualProfile.DamageRendererPaths, damageColor);
        }

        /// <summary>
        /// Local-space top of a creep's body, measured once per role and cached.
        /// </summary>
        /// <remarks>
        /// The health bar heights in <see cref="CreepHealthBarMetrics"/> were hand-tuned against
        /// the flat 2D plate profiles, whose Y scale was around 0.54. The 3D wrappers scale
        /// uniformly, which pushed the same constants anywhere from 0.91x to 2.03x of a creep's
        /// height: the swarm bar sat inside its model while the brute's floated well clear.
        /// Measuring the body instead keeps every bar the same short distance above its creep and
        /// survives the next scale change, which retuning the constants would not.
        /// </remarks>
        private static float CreepBodyTop(GameObject creepObject, string creepId)
        {
            if (CreepBodyTopByRole.TryGetValue(creepId, out var cached))
            {
                return cached;
            }

            var top = 0f;
            var renderers = creepObject.GetComponentsInChildren<Renderer>(true);
            for (var index = 0; index < renderers.Length; index++)
            {
                var rendererObject = renderers[index].gameObject;
                if (IsHealthBarPart(rendererObject.name) || !rendererObject.activeInHierarchy)
                {
                    continue;
                }

                var localTop = creepObject.transform.InverseTransformPoint(renderers[index].bounds.max).y;
                if (localTop > top)
                {
                    top = localTop;
                }
            }

            if (top <= 0.01f)
            {
                // Renderers are not ready yet; leave the cache empty so a later frame measures it.
                return 0f;
            }

            CreepBodyTopByRole[creepId] = top;
            return top;
        }

        private static bool IsHealthBarPart(string name) =>
            name == "HealthBarBack" || name == "HealthBarFill" || name == "HealthBarMidTick" || name == "HealthWoundPip";

        /// <summary>
        /// Two-piece health bar: a dark backing and a coloured fill, shown only once a creep has
        /// actually taken damage.
        /// </summary>
        /// <remarks>
        /// This used to stack four separate cubes per creep — backing, fill, a dark mid-tick
        /// splitting the fill in half, and a red "wound pip" hanging off the end — and drew all of
        /// them on every creep at all times, including at full health. On screen that read as a
        /// cluster of unrelated coloured lines floating above each unit rather than as one bar, and
        /// at the spawn gate it appeared as a stray green/red streak before its creep was even
        /// visible. The mid-tick and wound pip carried no information the fill width did not
        /// already convey, so both are gone; hiding the bar at full health removes it entirely for
        /// most units most of the time.
        /// </remarks>
        private static void ConfigureCreepHealthBar(GameObject creepObject, string creepId, float healthFraction)
        {
            var metrics = CreepHealthBarMetrics.For(creepId);

            // Measure before the bar parts exist, so they cannot inflate the body's top.
            var bodyTop = CreepBodyTop(creepObject, creepId);
            var barY = bodyTop > 0f ? bodyTop + HealthBarGap : metrics.Y;

            var back = EnsureChild(creepObject, "HealthBarBack", PrimitiveType.Cube);
            var fill = EnsureChild(creepObject, "HealthBarFill", PrimitiveType.Cube);

            var damaged = healthFraction < 0.999f;
            back.SetActive(damaged);
            fill.SetActive(damaged);
            if (!damaged)
            {
                DeactivateChild(creepObject, "HealthBarMidTick");
                DeactivateChild(creepObject, "HealthWoundPip");
                return;
            }

            ConfigureHealthBarChild(back, new Vector3(0f, barY, metrics.Z), new Vector3(metrics.Width, metrics.Height, metrics.Depth), new Color(0.015f, 0.022f, 0.035f));
            var fillWidth = Mathf.Max(metrics.MinFillWidth, metrics.Width * Mathf.Clamp01(healthFraction));
            var fillX = (fillWidth - metrics.Width) * 0.5f;
            ConfigureHealthBarChild(fill, new Vector3(fillX, barY + metrics.FillLift, metrics.Z), new Vector3(fillWidth, metrics.Height * 1.12f, metrics.Depth * 1.08f), CreepHealthColor(healthFraction));

            // Retired parts: pooled creeps can carry them over from a previous life, so they are
            // explicitly switched off rather than merely no longer created.
            DeactivateChild(creepObject, "HealthBarMidTick");
            DeactivateChild(creepObject, "HealthWoundPip");
        }

        private static void DeactivateChild(GameObject root, string childName)
        {
            var child = root.transform.Find(childName);
            if (child != null && child.gameObject.activeSelf)
            {
                child.gameObject.SetActive(false);
            }
        }

        private static void ConfigureHealthBarChild(GameObject child, Vector3 localPosition, Vector3 localScale, Color color)
        {
            ConfigureChild(child, true, localPosition, localScale, color);
            child.transform.rotation = Quaternion.identity;
        }

        private static Color CreepHealthColor(float healthFraction)
        {
            if (healthFraction <= 0.34f)
            {
                return LeakRed;
            }

            if (healthFraction <= 0.66f)
            {
                return SignalGold;
            }

            return MintSignal;
        }

        private static void ConfigureCreepReadabilityOverlay(GameObject creepObject, string creepId, int senderId, float healthFraction, bool isHitFlashing)
        {
            DeactivateRoleReadabilityOverlay(creepObject);

            var roleColor = isHitFlashing ? new Color(1f, 0.94f, 0.62f) : BoostValue(CreepRoleColor(creepId, senderId), 1.14f);
            var senderColor = SenderColor(senderId);
            var damageColor = healthFraction < 0.35f ? LeakRed : roleColor;

            if (ContainsRole(creepId, "swarm"))
            {
                var jitter = Mathf.Sin(Time.time * 19f) * 0.06f;
                ConfigureChild(EnsureChild(creepObject, "RoleSwarmValueRing", PrimitiveType.Cylinder), true, new Vector3(0f, -0.33f, 0f), new Vector3(1.34f, 0.02f, 1.18f), DimValue(roleColor, 0.72f));
                ConfigureChild(EnsureChild(creepObject, "RoleSwarmLeadSpark", PrimitiveType.Sphere), true, new Vector3(0.38f + jitter, 0.18f, 0.52f), new Vector3(0.18f, 0.18f, 0.18f), roleColor);
                return;
            }

            if (ContainsRole(creepId, "brute") || ContainsRole(creepId, "tank") || ContainsRole(creepId, "boss"))
            {
                ConfigureChild(EnsureChild(creepObject, "RoleBruteLeftShoulder", PrimitiveType.Cube), true, new Vector3(-0.44f, 0.36f, 0.18f), new Vector3(0.22f, 0.18f, 0.46f), damageColor);
                ConfigureChild(EnsureChild(creepObject, "RoleBruteRightShoulder", PrimitiveType.Cube), true, new Vector3(0.44f, 0.36f, 0.18f), new Vector3(0.22f, 0.18f, 0.46f), damageColor);
                ConfigureChild(EnsureChild(creepObject, "RoleBruteCenterPlate", PrimitiveType.Cube), true, new Vector3(0f, 0.5f, 0.36f), new Vector3(0.42f, 0.08f, 0.2f), SignalGold);
                return;
            }

            if (ContainsRole(creepId, "shade") || ContainsRole(creepId, "invisible") || ContainsRole(creepId, "stealth"))
            {
                var shimmer = Mathf.Sin(Time.time * 8f) * 0.08f;
                ConfigureChild(EnsureChild(creepObject, "RoleShadeLeftEcho", PrimitiveType.Cube), true, new Vector3(-0.34f + shimmer, 0.2f, 0.02f), new Vector3(0.06f, 0.5f, 0.66f), new Color(0.72f, 0.94f, 1f));
                ConfigureChild(EnsureChild(creepObject, "RoleShadeRightEcho", PrimitiveType.Cube), true, new Vector3(0.34f - shimmer, 0.2f, -0.04f), new Vector3(0.06f, 0.42f, 0.58f), new Color(0.36f, 0.5f, 0.58f));
                ConfigureChild(EnsureChild(creepObject, "RoleShadeCoreLine", PrimitiveType.Cube), true, new Vector3(0f, 0.42f, 0.22f), new Vector3(0.1f, 0.08f, 0.46f), roleColor);
                return;
            }

            if (ContainsRole(creepId, "siege") || ContainsRole(creepId, "attacker"))
            {
                var windup = Mathf.Abs(Mathf.Sin(Time.time * 5f));
                ConfigureChild(EnsureChild(creepObject, "RoleSiegeRamHead", PrimitiveType.Cube), true, new Vector3(0f, 0.18f, 0.62f), new Vector3(0.48f, 0.22f, 0.2f), roleColor);
                ConfigureChild(EnsureChild(creepObject, "RoleSiegeWarningLeft", PrimitiveType.Cube), true, new Vector3(-0.38f, 0.24f, 0.14f), new Vector3(0.08f, 0.24f + windup * 0.08f, 0.58f), LeakRed);
                ConfigureChild(EnsureChild(creepObject, "RoleSiegeWarningRight", PrimitiveType.Cube), true, new Vector3(0.38f, 0.24f, 0.14f), new Vector3(0.08f, 0.24f + windup * 0.08f, 0.58f), LeakRed);
                return;
            }

            ConfigureChild(EnsureChild(creepObject, "RoleRunnerChevron", PrimitiveType.Cube), true, new Vector3(0f, 0.28f, 0.62f), new Vector3(0.24f, 0.08f, 0.28f), roleColor);
            ConfigureChild(EnsureChild(creepObject, "RoleRunnerWake", PrimitiveType.Cube), true, new Vector3(0f, -0.2f, -0.72f), new Vector3(0.055f, 0.035f, 0.58f), senderColor);
        }

        private static void DeactivateRoleReadabilityOverlay(GameObject creepObject)
        {
            for (var index = 0; index < CreepReadabilityOverlayNames.Length; index++)
            {
                var marker = creepObject.transform.Find(CreepReadabilityOverlayNames[index]);
                if (marker != null)
                {
                    marker.gameObject.SetActive(false);
                }
            }
        }

        private static bool UsesAiPlateVisual(GameObject instance) =>
            instance != null && instance.transform.Find("AIPlateVisual") != null;

        /// <summary>
        /// True when a creep is one of the generated 3D wrappers built by Creep3DImportPipeline.
        /// </summary>
        /// <remarks>
        /// The role readability overlay exists to tell flat 2D plates apart: it sticks coloured
        /// primitives onto the creep at offsets tuned for the plate profiles. A generated mesh
        /// carries its own silhouette and material identity, and those offsets land wrong at the
        /// wrapper's uniform scale - the swarm value ring sits at -0.33, below the board - so the
        /// overlay is suppressed for meshes. Ownership still reads from the SenderAccent decal.
        /// </remarks>
        private static bool UsesMeshVisual(GameObject instance) =>
            instance != null && instance.transform.Find("Body/Imported3DVisual") != null;

        private static void DeactivateCreepGameplayOverlays(GameObject creepObject)
        {
            DeactivateRoleReadabilityOverlay(creepObject);
            DeactivateKnownCreepMarkers(creepObject);
            for (var index = 0; index < CreepHealthOverlayNames.Length; index++)
            {
                var marker = creepObject.transform.Find(CreepHealthOverlayNames[index]);
                if (marker != null)
                {
                    marker.gameObject.SetActive(false);
                }
            }
        }

        private static Vector3 CreepRoleScale(string creepId, CreepVisualProfile visualProfile)
        {
            if (visualProfile != null && visualProfile.HasScale)
            {
                return new Vector3(
                    visualProfile.Scale.x * 1.18f,
                    visualProfile.Scale.y * 1.12f,
                    visualProfile.Scale.z * 1.18f);
            }

            if (ContainsRole(creepId, "swarm"))
            {
                return new Vector3(0.24f, 0.12f, 0.24f);
            }

            if (ContainsRole(creepId, "brute") || ContainsRole(creepId, "tank"))
            {
                return new Vector3(0.9f, 0.5f, 1.02f);
            }

            if (ContainsRole(creepId, "boss"))
            {
                return new Vector3(0.82f, 0.72f, 0.82f);
            }

            if (ContainsRole(creepId, "shade") || ContainsRole(creepId, "invisible") || ContainsRole(creepId, "stealth"))
            {
                return new Vector3(0.36f, 0.2f, 0.58f);
            }

            if (ContainsRole(creepId, "flying") || ContainsRole(creepId, "air"))
            {
                return new Vector3(0.42f, 0.18f, 0.42f);
            }

            if (ContainsRole(creepId, "attacker") || ContainsRole(creepId, "siege"))
            {
                return new Vector3(0.46f, 0.3f, 0.34f);
            }

            if (ContainsRole(creepId, "aura") || ContainsRole(creepId, "support"))
            {
                return new Vector3(0.52f, 0.34f, 0.52f);
            }

            return new Vector3(0.42f, 0.24f, 0.72f);
        }

        private static Vector3 CreepRoleOffset(string creepId)
        {
            // Spire Turret Walker walks the direct route, which runs straight through cells that
            // towers are standing on — so without real clearance it renders INSIDE them. 0.72 is
            // chosen against tower height rather than as a bigger version of the 0.32 used for
            // flying creeps: at 0.32 it still clipped the taller towers, which reads as a bug
            // rather than as the one unit that goes over the maze.
            if (ContainsRole(creepId, "turret_walker"))
            {
                return Vector3.up * 0.72f;
            }

            if (ContainsRole(creepId, "flying") || ContainsRole(creepId, "air"))
            {
                return Vector3.up * 0.32f;
            }

            if (ContainsRole(creepId, "boss"))
            {
                return Vector3.up * 0.16f;
            }

            return Vector3.up * 0.02f;
        }

        /// <summary>
        /// Per-role idle motion applied on top of the creep's lane position.
        /// </summary>
        /// <remarks>
        /// Position offsets here must stay centred on the lane. The swarm and brute styles used to
        /// carry a constant lateral shift of 0.16, which read acceptably when creeps were flat 2D
        /// plates at roughly half the current scale but sits them visibly off-centre now they are
        /// 3D meshes. Only oscillating components belong in the offset; a constant one is a
        /// misalignment.
        /// </remarks>
        private const float CreepHitFlashDuration = 0.16f;

        /// <summary>
        /// Everything that makes one creep move like itself and not like its neighbour, in one row.
        /// </summary>
        /// <remarks>
        /// This replaces a dispatch on <see cref="CreepVisualMotionStyle"/>, which was a set of
        /// SHARED curves: Shade and Ash Revenant executed byte-identical code, and five rigged
        /// creeps all named HeavyBob. Sharing a curve is fine when a style is a family of two, and
        /// stops being fine at fifteen creeps that are supposed to be individually recognisable.
        /// Each creep now owns a row, exactly as each tower owns one in TowerMotionProfileFor.
        ///
        /// The style enum survives as the FALLBACK for a creep id with no row (see
        /// <see cref="CreepMotionProfileForStyle"/>), so authoring a new creep in the visual library
        /// without touching this file still yields sensible motion rather than nothing.
        ///
        /// What reaches the screen: the match camera is orthographic and tilted ~30 degrees off
        /// vertical, so yaw, XZ drift and uniform scale arrive intact while vertical bob arrives at
        /// roughly sin(tilt). Bob and roll are therefore flavour; sway, spin, drift and pulse carry
        /// the read.
        /// </remarks>
        private readonly struct CreepMotionProfile
        {
            public CreepMotionProfile(
                float bobHz = 0f,
                float bobAmp = 0f,
                bool bobRectified = false,
                float swayHz = 0f,
                float swayDegrees = 0f,
                float spinDegreesPerSecond = 0f,
                float driftHz = 0f,
                float driftAmp = 0f,
                float pulseHz = 0f,
                float pulseAmp = 0f,
                float rollDegrees = 0f,
                float pitchDegrees = 0f,
                float forwardOffset = 0f,
                float flinchScale = 0.28f,
                float flinchTilt = 0f)
            {
                BobHz = bobHz;
                BobAmp = bobAmp;
                BobRectified = bobRectified;
                SwayHz = swayHz;
                SwayDegrees = swayDegrees;
                SpinDegreesPerSecond = spinDegreesPerSecond;
                DriftHz = driftHz;
                DriftAmp = driftAmp;
                PulseHz = pulseHz;
                PulseAmp = pulseAmp;
                RollDegrees = rollDegrees;
                PitchDegrees = pitchDegrees;
                ForwardOffset = forwardOffset;
                FlinchScale = flinchScale;
                FlinchTilt = flinchTilt;
            }

            public float BobHz { get; }
            public float BobAmp { get; }

            /// <summary>Use |sin| so the body settles and rises rather than oscillating evenly, which reads as weight bearing rather than floating.</summary>
            public bool BobRectified { get; }

            public float SwayHz { get; }

            /// <summary>Yaw weave AROUND the direction of travel. Distinct from a spin, which never settles.</summary>
            public float SwayDegrees { get; }

            public float SpinDegreesPerSecond { get; }
            public float DriftHz { get; }
            public float DriftAmp { get; }
            public float PulseHz { get; }

            /// <summary>Uniform scale breathe. Never an axis squash — several of these are crystal or stone.</summary>
            public float PulseAmp { get; }

            public float RollDegrees { get; }
            public float PitchDegrees { get; }
            public float ForwardOffset { get; }

            /// <summary>
            /// Scale punch on taking a hit. Inversely tracks weight across the roster — Crystal Wisp
            /// 0.42, Siege Colossus 0.15 — so how hard something rocks when struck says what it
            /// weighs, before its health bar is read.
            /// </summary>
            public float FlinchScale { get; }

            public float FlinchTilt { get; }
        }

        /// <summary>
        /// The per-creep motion row. One per creep in the roster; unknown ids fall through to the
        /// style-derived profile.
        /// </summary>
        private static CreepMotionProfile CreepMotionProfileFor(string creepId, CreepVisualProfile visualProfile, bool isRigged)
        {
            switch (creepId)
            {
                // --- CORE ---------------------------------------------------------------------
                // Light and quick. The fast lateral dart is the whole silhouette read; the forward
                // offset leans it into its own travel.
                case "creep.runner":
                    return new CreepMotionProfile(
                        bobHz: 13f, bobAmp: 0.018f, driftHz: 13f, driftAmp: 0.055f,
                        rollDegrees: 4f, pitchDegrees: 7f, forwardOffset: 0.06f, flinchScale: 0.34f);

                // Rock golem, rigged. Secondary only: a slow load-bearing breathe under the walk.
                case "creep.brute":
                    return new CreepMotionProfile(swayHz: 1.1f, swayDegrees: 2.5f, pulseHz: 1.4f, pulseAmp: 0.020f, flinchScale: 0.24f);

                // A cluster, not a body. Fast erratic jitter plus a continuous turn so the shards
                // never present the same face twice.
                case "creep.swarm":
                    return new CreepMotionProfile(
                        driftHz: 15f, driftAmp: 0.045f, spinDegreesPerSecond: 60f,
                        pulseHz: 9f, pulseAmp: 0.030f, flinchScale: 0.38f);

                // Cloaked drifter. Weaves around its facing rather than pirouetting, and the slow
                // scale pulse does the ghostly fade.
                case "creep.shade":
                    return new CreepMotionProfile(
                        bobHz: 1.9f, bobAmp: 0.050f, swayHz: 1.5f, swayDegrees: 14f,
                        driftHz: 1.1f, driftAmp: 0.060f, pulseHz: 2.3f, pulseAmp: 0.045f, flinchScale: 0.30f);

                // Beast hybrid. A slow shoulder roll winding up under its own mass.
                case "creep.siege":
                    return new CreepMotionProfile(
                        bobHz: 6f, bobAmp: 0.020f, bobRectified: true, rollDegrees: 4f,
                        pulseHz: 1.6f, pulseAmp: 0.030f, flinchScale: 0.22f);

                // --- RAPID --------------------------------------------------------------------
                // Crystal suspended in a cage. Slow turn so the facets read from above; the wide
                // drift is what sells floating rather than hovering in place.
                case "creep.wisp":
                    return new CreepMotionProfile(
                        bobHz: 2.6f, bobAmp: 0.075f, spinDegreesPerSecond: 52f,
                        driftHz: 0.9f, driftAmp: 0.070f, pulseHz: 3.4f, pulseAmp: 0.035f, flinchScale: 0.42f);

                // Ash Revenant. Deliberately NOT Shade's curve, which it used to share outright:
                // where Shade is a slow wide weave, this is quicker, tighter and more agitated, with
                // a strong fade pulse and a slight unresolved turn — ash coming apart and reforming
                // rather than a hood gliding.
                case "creep.revenant":
                    return new CreepMotionProfile(
                        bobHz: 3.1f, bobAmp: 0.070f, swayHz: 2.4f, swayDegrees: 9f,
                        spinDegreesPerSecond: 18f, driftHz: 1.7f, driftAmp: 0.040f,
                        pulseHz: 4.2f, pulseAmp: 0.070f, flinchScale: 0.44f);

                // Bigger, slower golem. Reads as Brute's heavier cousin: lower frequency, wider sway.
                case "creep.obsidian_brute":
                    return new CreepMotionProfile(swayHz: 0.7f, swayDegrees: 3.2f, pulseHz: 0.9f, pulseAmp: 0.025f, flinchScale: 0.20f);

                // No limbs, so the whole body carries the writhe: a coil turn plus a tightening and
                // loosening pulse. Rectified bob so it settles and rises, reading muscular.
                case "creep.serpent":
                    return new CreepMotionProfile(
                        bobHz: 2.2f, bobAmp: 0.035f, bobRectified: true, spinDegreesPerSecond: 34f,
                        driftHz: 1.7f, driftAmp: 0.050f, pulseHz: 2.2f, pulseAmp: 0.055f, flinchScale: 0.26f);

                // Machine, rigged, and its rig already scans its turret. Almost nothing added: a
                // fast shallow pulse that reads as a servo holding load, and no sway at all, because
                // a mechanical walker leaning would fight the gait it was rigged with.
                case "creep.turret_walker":
                    return new CreepMotionProfile(pulseHz: 2.8f, pulseAmp: 0.010f, flinchScale: 0.26f);

                // --- ELITE (all rigged; secondary layer only) ---------------------------------
                // Fast flyer. Banks into its own travel — the widest sway of the rigged set.
                case "creep.zephyr":
                    return new CreepMotionProfile(swayHz: 2.2f, swayDegrees: 6f, pulseHz: 3.0f, pulseAmp: 0.030f, flinchScale: 0.40f);

                // Stealth. The strongest fade pulse of the rigged set, so it reads as phasing rather
                // than merely walking.
                case "creep.stalker":
                    return new CreepMotionProfile(swayHz: 1.6f, swayDegrees: 4f, pulseHz: 2.0f, pulseAmp: 0.050f, flinchScale: 0.34f);

                // Burrower. A slow deep swell, like something surfacing and sinking as it advances.
                case "creep.burrower":
                    return new CreepMotionProfile(swayHz: 0.9f, swayDegrees: 3f, pulseHz: 1.2f, pulseAmp: 0.045f, bobRectified: true, flinchScale: 0.22f);

                // Shield tank. Deliberately the stillest thing in the roster — a guarded advance that
                // gives away nothing. Stillness is the characterisation here, not an absence of one.
                case "creep.warden":
                    return new CreepMotionProfile(swayHz: 0.5f, swayDegrees: 1.5f, pulseHz: 0.8f, pulseAmp: 0.015f, flinchScale: 0.18f);

                // Heaviest thing on the board at 90 hp. Ponderous: the lowest frequencies anywhere in
                // the roster, and the smallest flinch, so hits visibly fail to move it.
                case "creep.colossus":
                    return new CreepMotionProfile(swayHz: 0.4f, swayDegrees: 3.5f, pulseHz: 0.55f, pulseAmp: 0.030f, flinchScale: 0.15f);

                default:
                    return CreepMotionProfileForStyle(creepId, visualProfile, isRigged);
            }
        }

        /// <summary>
        /// Fallback row for a creep with no entry of its own, derived from its authored
        /// <see cref="CreepVisualMotionStyle"/>.
        /// </summary>
        /// <remarks>
        /// Keeps the visual library meaningful: a creep added to CreepVisualLibrary without a row
        /// above still moves like its declared family instead of defaulting to Runner's twitch.
        /// </remarks>
        private static CreepMotionProfile CreepMotionProfileForStyle(string creepId, CreepVisualProfile visualProfile, bool isRigged)
        {
            var style = visualProfile != null ? visualProfile.MotionStyle : CreepVisualMotionStyle.Auto;
            if (style == CreepVisualMotionStyle.Auto)
            {
                if (ContainsRole(creepId, "swarm")) style = CreepVisualMotionStyle.ClusterJitter;
                else if (ContainsRole(creepId, "brute") || ContainsRole(creepId, "tank") || ContainsRole(creepId, "boss")) style = CreepVisualMotionStyle.HeavyBob;
                else if (ContainsRole(creepId, "flying") || ContainsRole(creepId, "air")) style = CreepVisualMotionStyle.Hover;
                else if (ContainsRole(creepId, "shade") || ContainsRole(creepId, "invisible") || ContainsRole(creepId, "stealth")) style = CreepVisualMotionStyle.Shimmer;
                else if (ContainsRole(creepId, "attacker") || ContainsRole(creepId, "siege")) style = CreepVisualMotionStyle.SiegeWindup;
                else style = CreepVisualMotionStyle.RunnerDart;
            }

            switch (style)
            {
                case CreepVisualMotionStyle.ClusterJitter:
                    return new CreepMotionProfile(driftHz: 15f, driftAmp: 0.045f, spinDegreesPerSecond: 60f, flinchScale: 0.36f);
                case CreepVisualMotionStyle.HeavyBob:
                    return new CreepMotionProfile(bobHz: 3.4f, bobAmp: 0.055f, bobRectified: true, swayHz: 3.4f, swayDegrees: 1.5f, flinchScale: 0.24f, flinchTilt: 26f);
                case CreepVisualMotionStyle.Hover:
                    return new CreepMotionProfile(bobHz: 2.6f, bobAmp: 0.075f, spinDegreesPerSecond: 52f, driftHz: 0.9f, driftAmp: 0.070f, pulseHz: 3.4f, pulseAmp: 0.035f, flinchScale: 0.40f);
                case CreepVisualMotionStyle.Shimmer:
                    return new CreepMotionProfile(bobHz: 1.9f, bobAmp: 0.050f, swayHz: 1.5f, swayDegrees: 14f, driftHz: 1.1f, driftAmp: 0.060f, pulseHz: 2.3f, pulseAmp: 0.045f, flinchScale: 0.32f);
                case CreepVisualMotionStyle.Coil:
                    return new CreepMotionProfile(bobHz: 2.2f, bobAmp: 0.035f, bobRectified: true, spinDegreesPerSecond: 34f, driftHz: 1.7f, driftAmp: 0.050f, pulseHz: 2.2f, pulseAmp: 0.055f, flinchScale: 0.26f);
                case CreepVisualMotionStyle.SiegeWindup:
                    return new CreepMotionProfile(bobHz: 6f, bobAmp: 0.020f, bobRectified: true, rollDegrees: 4f, flinchScale: 0.24f);
                case CreepVisualMotionStyle.AuraPulse:
                    return new CreepMotionProfile(bobHz: 5f, bobAmp: 0.035f, bobRectified: true, spinDegreesPerSecond: 35f, flinchScale: 0.30f);
                default:
                    return new CreepMotionProfile(bobHz: 13f, bobAmp: 0.018f, driftHz: 13f, driftAmp: 0.055f, rollDegrees: 4f, pitchDegrees: 7f, forwardOffset: 0.06f, flinchScale: 0.34f);
            }
        }

        /// <summary>
        /// Spreads instances of the same creep out of lockstep, in radians.
        /// </summary>
        /// <remarks>
        /// Every creep drove its motion straight off Time.time, so every instance of a given creep
        /// was in perfect phase with every other one. A Swarm send puts several on the board at once
        /// and they jittered as one rigid body; the effect got worse the more of something you sent,
        /// which is exactly backwards. Offsetting by a hash of the entity key makes a group read as
        /// individuals, and is stable for the life of the entity so nothing jumps between frames.
        ///
        /// The key became a long when the presentation dictionaries stopped being keyed by strings,
        /// and this deliberately still hashes its DECIMAL DIGITS rather than the number, so every
        /// creep keeps the exact phase it had before. Hashing the long directly would have been
        /// shorter and would have re-scattered every creep on the board — a change to how the game
        /// looks, smuggled in by a change to a dictionary key type.
        /// </remarks>
        private static float CreepMotionPhase(long key)
        {
            if (key == 0L)
            {
                return 0f;
            }

            unchecked
            {
                // Digits, most significant first, without materialising the string that used to
                // carry them. Only the low 16 bits of the hash survive below, so a negative key's
                // '-' sign is the one character this cannot reproduce; entity ids are never
                // negative, and 0 keeps the old empty-key answer above.
                var digits = 1;
                for (var scale = key; scale >= 10L; scale /= 10L)
                {
                    digits++;
                }

                var divisor = 1L;
                for (var index = 1; index < digits; index++)
                {
                    divisor *= 10L;
                }

                var hash = 17;
                while (divisor > 0L)
                {
                    hash = hash * 31 + (char)('0' + (int)(key / divisor % 10L));
                    divisor /= 10L;
                }

                return (hash & 0xFFFF) / 65535f * (Mathf.PI * 2f);
            }
        }

        private static CreepMotion CreepRoleMotion(string creepId, CreepVisualProfile visualProfile, float hitFlashUntil, bool isRigged = false, long key = 0L)
        {
            var profile = CreepMotionProfileFor(creepId, visualProfile, isRigged);
            var time = Time.time + CreepMotionPhase(key);
            var flinch = Mathf.Clamp01((hitFlashUntil - Time.time) / CreepHitFlashDuration);
            var scale = 1f + flinch * profile.FlinchScale;

            // A rigged creep's clip already animates its body, so bob, roll, pitch and drift would
            // fight it — those all describe things a walk cycle is already doing. Sway and the scale
            // pulse are applied to the ROOT transform, which the clip never touches (root motion is
            // off on all eight rigs), so they layer cleanly and are what makes one rigged creep
            // distinguishable from another without re-authoring anyone's skeleton.
            if (isRigged)
            {
                var riggedSway = profile.SwayDegrees == 0f ? 0f : Mathf.Sin(time * profile.SwayHz) * profile.SwayDegrees;
                var riggedPulse = profile.PulseAmp == 0f ? 0f : Mathf.Sin(time * profile.PulseHz) * profile.PulseAmp;
                return new CreepMotion(
                    Vector3.zero,
                    Quaternion.Euler(0f, riggedSway, 0f),
                    scale * (1f + riggedPulse));
            }

            var bobWave = profile.BobHz == 0f
                ? 0f
                : profile.BobRectified
                    ? Mathf.Abs(Mathf.Sin(time * profile.BobHz))
                    : Mathf.Sin(time * profile.BobHz);
            var bob = bobWave * profile.BobAmp;

            var drift = Vector3.zero;
            if (profile.DriftAmp != 0f)
            {
                drift = new Vector3(
                    Mathf.Sin(time * profile.DriftHz) * profile.DriftAmp,
                    0f,
                    Mathf.Cos(time * profile.DriftHz * 0.85f) * profile.DriftAmp * 0.8f);
            }

            var yaw = profile.SpinDegreesPerSecond * time
                + (profile.SwayDegrees == 0f ? 0f : Mathf.Sin(time * profile.SwayHz) * profile.SwayDegrees);
            var roll = profile.RollDegrees == 0f ? 0f : Mathf.Sin(time * profile.BobHz) * profile.RollDegrees;
            var pulse = profile.PulseAmp == 0f ? 0f : Mathf.Sin(time * profile.PulseHz) * profile.PulseAmp;

            // The flinch tilt kicks AGAINST the current roll so a hit visibly interrupts the idle
            // rather than blending into it. The scale punch is what actually carries the reaction:
            // this camera is tilted only ~30 degrees off vertical, so tilt and vertical displacement
            // mostly project away, while uniform scale survives intact.
            var flinchTilt = profile.FlinchTilt == 0f
                ? 0f
                : -Mathf.Sign(roll == 0f ? 1f : roll) * flinch * profile.FlinchTilt;

            return new CreepMotion(
                new Vector3(drift.x, bob - flinch * 0.09f, drift.z + profile.ForwardOffset),
                Quaternion.Euler(profile.PitchDegrees + flinch * 20f, yaw, roll + flinchTilt),
                scale * (1f + pulse));
        }

        private static CreepDeathCueStyle CreepDeathCueStyleFor(string creepId, CreepVisualProfile visualProfile)
        {
            var deathCueStyle = visualProfile != null ? visualProfile.DeathCueStyle : CreepDeathCueStyle.Auto;
            if (deathCueStyle != CreepDeathCueStyle.Auto)
            {
                return deathCueStyle;
            }

            if (ContainsRole(creepId, "brute") || ContainsRole(creepId, "tank") || ContainsRole(creepId, "boss"))
            {
                return CreepDeathCueStyle.HeavyShatter;
            }

            if (ContainsRole(creepId, "swarm"))
            {
                return CreepDeathCueStyle.ShardScatter;
            }

            if (ContainsRole(creepId, "invisible") || ContainsRole(creepId, "stealth") || ContainsRole(creepId, "aura") || ContainsRole(creepId, "support"))
            {
                return CreepDeathCueStyle.SoftDissolve;
            }

            return CreepDeathCueStyle.SparkBurst;
        }

        private static Color CreepRoleColor(string creepId, int senderId)
        {
            if (ContainsRole(creepId, "brute") || ContainsRole(creepId, "tank") || ContainsRole(creepId, "boss"))
            {
                return new Color(1f, 0.62f, 0.26f);
            }

            if (ContainsRole(creepId, "swarm"))
            {
                return new Color(0.72f, 1f, 0.86f);
            }

            if (ContainsRole(creepId, "flying") || ContainsRole(creepId, "air"))
            {
                return new Color(0.82f, 0.72f, 1f);
            }

            if (ContainsRole(creepId, "shade") || ContainsRole(creepId, "invisible") || ContainsRole(creepId, "stealth"))
            {
                return new Color(0.72f, 0.84f, 0.9f, 0.62f);
            }

            if (ContainsRole(creepId, "attacker") || ContainsRole(creepId, "siege"))
            {
                return new Color(1f, 0.38f, 0.44f);
            }

            if (ContainsRole(creepId, "aura") || ContainsRole(creepId, "support"))
            {
                return new Color(0.42f, 1f, 0.72f);
            }

            return SenderColor(senderId);
        }

        private static Color CreepBodyColor(string creepId, int senderId, float healthFraction, bool isHitFlashing)
        {
            if (isHitFlashing)
            {
                return new Color(1f, 0.94f, 0.62f);
            }

            var baseColor = CreepRoleColor(creepId, senderId);
            if (healthFraction >= 0.45f)
            {
                return baseColor;
            }

            var damageTint = ContainsRole(creepId, "brute") || ContainsRole(creepId, "tank")
                ? new Color(0.68f, 0.22f, 0.16f)
                : new Color(0.42f, 0.48f, 0.58f);
            var amount = Mathf.InverseLerp(0.45f, 0.05f, healthFraction) * 0.55f;
            return Color.Lerp(baseColor, damageTint, amount);
        }

        private static float CreepHealthFraction(int health, int maxHealth)
        {
            return Mathf.Clamp01(health / (float)Mathf.Max(1, maxHealth));
        }

        private static void ConfigureCreepRoleMarker(GameObject creepObject, string creepId, int senderId, float healthFraction, bool isHitFlashing)
        {
            DeactivateKnownCreepMarkers(creepObject);

            // The old per-creep GroundShadow cylinder is gone. Its -0.42 local offset was tuned
            // against the flat 2D plate profiles, where the shallow scale left it just above the
            // board; at the uniform scale the 3D wrappers use it sinks below the surface and is
            // never seen. Grounding now comes from the pooled contact shadow decals, which are
            // instanced and sit on the board rather than inside it.

            var isSwarm = ContainsRole(creepId, "swarm");
            var isBoss = ContainsRole(creepId, "boss");
            var isBrute = isBoss || ContainsRole(creepId, "brute") || ContainsRole(creepId, "tank");
            var isAir = ContainsRole(creepId, "flying") || ContainsRole(creepId, "air");
            var isStealth = ContainsRole(creepId, "shade") || ContainsRole(creepId, "invisible") || ContainsRole(creepId, "stealth");
            var isSiege = ContainsRole(creepId, "attacker") || ContainsRole(creepId, "siege");
            var isAura = ContainsRole(creepId, "aura") || ContainsRole(creepId, "support");
            var isRunner = !isSwarm && !isBrute && !isAir && !isStealth && !isSiege && !isAura;

            if (isRunner)
            {
                ConfigureRunnerMarker(creepObject, senderId, healthFraction, isHitFlashing);
                return;
            }

            if (isBrute)
            {
                ConfigureBruteMarker(creepObject, isBoss, healthFraction, isHitFlashing);
                return;
            }

            if (isSwarm)
            {
                ConfigureSwarmMarker(creepObject, senderId, healthFraction, isHitFlashing);
                return;
            }

            if (isAir)
            {
                ConfigureAirMarker(creepObject);
                return;
            }

            if (isStealth)
            {
                ConfigureStealthMarker(creepObject);
                return;
            }

            if (isSiege)
            {
                ConfigureSiegeMarker(creepObject);
                return;
            }

            if (isAura)
            {
                ConfigureAuraMarker(creepObject);
            }
        }

        private static void ConfigureRunnerMarker(GameObject creepObject, int senderId, float healthFraction, bool isHitFlashing)
        {
            var senderColor = SenderColor(senderId);
            var accent = isHitFlashing ? new Color(1f, 0.94f, 0.62f) : MintSignal;
            var damageScale = Mathf.Lerp(0.72f, 1f, Mathf.Clamp01(healthFraction));

            ConfigureChild(EnsureChild(creepObject, "RunnerNose", PrimitiveType.Cube), true, new Vector3(0f, 0.04f, 0.56f), new Vector3(0.14f, 0.1f, 0.56f), accent);
            ConfigureChild(EnsureChild(creepObject, "RunnerTail", PrimitiveType.Cube), true, new Vector3(0f, -0.02f, -0.44f), new Vector3(0.08f, 0.08f, 0.38f * damageScale), senderColor);
            ConfigureChild(EnsureChild(creepObject, "RunnerLeftFin", PrimitiveType.Cube), true, new Vector3(-0.3f, 0f, -0.06f), new Vector3(0.07f, 0.08f, 0.34f), senderColor);
            ConfigureChild(EnsureChild(creepObject, "RunnerRightFin", PrimitiveType.Cube), true, new Vector3(0.3f, 0f, -0.06f), new Vector3(0.07f, 0.08f, 0.34f), senderColor);
            ConfigureChild(EnsureChild(creepObject, "RunnerSpeedLine", PrimitiveType.Cube), true, new Vector3(0f, -0.18f, -0.82f), new Vector3(0.045f, 0.035f, 0.62f * damageScale), senderColor);
        }

        private static void ConfigureBruteMarker(GameObject creepObject, bool isBoss, float healthFraction, bool isHitFlashing)
        {
            var armorColor = isHitFlashing ? new Color(1f, 0.94f, 0.62f) : new Color(1f, 0.72f, 0.38f);
            var plateColor = healthFraction < 0.35f ? new Color(0.68f, 0.22f, 0.16f) : new Color(0.74f, 0.38f, 0.22f);

            ConfigureChild(EnsureChild(creepObject, "BruteArmor", PrimitiveType.Cube), true, new Vector3(0f, 0.26f, 0f), isBoss ? new Vector3(0.96f, 0.22f, 1.08f) : new Vector3(0.92f, 0.18f, 1.02f), armorColor);
            ConfigureChild(EnsureChild(creepObject, "BruteLeftPlate", PrimitiveType.Cube), true, new Vector3(-0.5f, 0.12f, 0.04f), new Vector3(0.24f, 0.34f, 0.74f), plateColor);
            ConfigureChild(EnsureChild(creepObject, "BruteRightPlate", PrimitiveType.Cube), true, new Vector3(0.5f, 0.12f, 0.04f), new Vector3(0.24f, 0.34f, 0.74f), plateColor);
            ConfigureChild(EnsureChild(creepObject, "BruteCore", PrimitiveType.Sphere), !isBoss, new Vector3(0f, 0.48f, 0.2f), Vector3.one * Mathf.Lerp(0.18f, 0.28f, Mathf.Clamp01(healthFraction)), SignalGold);

            if (!isBoss)
            {
                return;
            }

            ConfigureChild(EnsureChild(creepObject, "BossCrown", PrimitiveType.Cylinder), true, new Vector3(0f, 0.72f, 0f), new Vector3(0.92f, 0.055f, 0.92f), LeakRed);
            ConfigureChild(EnsureChild(creepObject, "BossCore", PrimitiveType.Sphere), true, new Vector3(0f, 0.54f, 0.16f), new Vector3(0.34f, 0.34f, 0.34f), SignalGold);
            ConfigureChild(EnsureChild(creepObject, "BossLeftHorn", PrimitiveType.Cube), true, new Vector3(-0.44f, 0.62f, 0.16f), new Vector3(0.16f, 0.16f, 0.42f), LeakRed);
            ConfigureChild(EnsureChild(creepObject, "BossRightHorn", PrimitiveType.Cube), true, new Vector3(0.44f, 0.62f, 0.16f), new Vector3(0.16f, 0.16f, 0.42f), LeakRed);
        }

        private static void ConfigureSwarmMarker(GameObject creepObject, int senderId, float healthFraction, bool isHitFlashing)
        {
            var senderColor = isHitFlashing ? new Color(1f, 0.94f, 0.62f) : SenderColor(senderId);
            var time = Time.time;
            var jitter = Mathf.Sin(time * 18f) * 0.08f;
            var livingDots = healthFraction > 0.66f ? 5 : healthFraction > 0.33f ? 4 : 3;

            ConfigureChild(EnsureChild(creepObject, "SwarmDotA", PrimitiveType.Sphere), true, new Vector3(-0.58f + jitter, 0.05f, -0.3f), new Vector3(0.58f, 0.58f, 0.58f), senderColor);
            ConfigureChild(EnsureChild(creepObject, "SwarmDotB", PrimitiveType.Sphere), true, new Vector3(0.56f - jitter, 0.05f, 0.28f), new Vector3(0.5f, 0.5f, 0.5f), MintSignal);
            ConfigureChild(EnsureChild(creepObject, "SwarmDotC", PrimitiveType.Sphere), true, new Vector3(0.04f, 0.08f, -0.62f - jitter), new Vector3(0.42f, 0.42f, 0.42f), new Color(0.72f, 1f, 0.86f));
            ConfigureChild(EnsureChild(creepObject, "SwarmDotD", PrimitiveType.Sphere), livingDots >= 4, new Vector3(-0.18f - jitter, 0.06f, 0.6f), new Vector3(0.36f, 0.36f, 0.36f), senderColor);
            ConfigureChild(EnsureChild(creepObject, "SwarmDotE", PrimitiveType.Sphere), livingDots >= 5, new Vector3(0.66f, 0.05f, -0.18f + jitter), new Vector3(0.32f, 0.32f, 0.32f), MintSignal);
            ConfigureChild(EnsureChild(creepObject, "SwarmTrail", PrimitiveType.Cylinder), true, new Vector3(0f, -0.18f, 0f), new Vector3(1.18f, 0.025f, 1.02f), senderColor);
        }

        /// <summary>
        /// Local slot positions for the mesh-backed swarm cluster, roughly matching the layout of
        /// the original 2D sprite's 7-bot cluster (one lead body, others fanned around it) rather
        /// than an evenly spaced ring.
        /// </summary>
        // Slot spread scales with SwarmShardScale — bigger shards packed at the original ±0.15
        // spacing merge back into one mass, which is the "blob" read this cluster exists to avoid.
        private static readonly Vector3[] SwarmClusterSlots =
        {
            new Vector3(0f, 0f, 0.22f),
            new Vector3(-0.22f, 0f, -0.06f),
            new Vector3(0.22f, 0f, -0.06f),
            new Vector3(-0.12f, 0f, -0.22f),
            new Vector3(0.12f, 0f, -0.22f),
        };

        /// <summary>Child names for the slots above, one per slot and in the same order.</summary>
        private static readonly string[] SwarmShardNames =
        {
            "SwarmShard0",
            "SwarmShard1",
            "SwarmShard2",
            "SwarmShard3",
            "SwarmShard4",
        };

        private const float SwarmShardScale = 0.5f;

        /// <summary>
        /// Replaces the single mesh-backed swarm body with a small cluster of scaled-down copies
        /// of the same mesh, each wandering within its own small "bubble" around a slot position.
        /// </summary>
        /// <remarks>
        /// The 2D sprite this creep's role was designed against showed a cluster of small shard
        /// bots, not one large one; the Meshy 3D pass generated (and the pipeline kept) a single
        /// enlarged body instead. This restores the multi-body read using the existing mesh/
        /// material — no new art asset — and reuses the same health-fraction "living count" idea
        /// the old pre-mesh <see cref="ConfigureSwarmMarker"/> primitive overlay used, so the
        /// cluster visibly thins as this creep takes damage instead of just changing color.
        /// </remarks>
        private static void ConfigureSwarmCluster(GameObject creepObject, string creepId, int senderId, float healthFraction, bool isHitFlashing)
        {
            var body = creepObject.transform.Find("Body");
            var imported = body != null ? body.Find("Imported3DVisual") : null;
            if (imported == null)
            {
                return;
            }

            // The pipeline's single big body is hidden, not destroyed — SwarmShard0..4 below are
            // copies of its own mesh/material, so there is nothing else for this creep to show.
            if (imported.gameObject.activeSelf)
            {
                imported.gameObject.SetActive(false);
            }

            var sourceFilter = imported.GetComponentInChildren<MeshFilter>(true);
            var sourceRenderer = imported.GetComponentInChildren<MeshRenderer>(true);
            if (sourceFilter == null || sourceFilter.sharedMesh == null || sourceRenderer == null)
            {
                return;
            }

            var mesh = sourceFilter.sharedMesh;
            var material = sourceRenderer.sharedMaterial;
            var color = CreepBodyColor(creepId, senderId, healthFraction, isHitFlashing);
            var livingCount = Mathf.Clamp(Mathf.CeilToInt(healthFraction * SwarmClusterSlots.Length), 1, SwarmClusterSlots.Length);
            var time = Time.time;

            // The raw FBX mesh is authored tiny (~0.02 units); it only renders at a sane size
            // because the import hierarchy under Imported3DVisual applies a large scale to it
            // (~39x for this asset). Shards are bare children of the CREEP ROOT, so they inherit
            // none of that — cloning sharedMesh at a plain 0.44 localScale produced ~0.01-unit
            // specks, roughly 90x too small, which is why tuning the shard scale and slot spread
            // alone never made any visible difference. Derive the correction from the source
            // transform itself rather than hardcoding it, so it self-corrects if the import
            // pipeline's scaling ever changes. Rotation is carried across the same way, since the
            // import chain may also hold an orientation correction the raw mesh depends on.
            var creepScale = creepObject.transform.lossyScale;
            var meshScale = sourceFilter.transform.lossyScale;
            var importScale = new Vector3(
                meshScale.x / Mathf.Max(0.0001f, creepScale.x),
                meshScale.y / Mathf.Max(0.0001f, creepScale.y),
                meshScale.z / Mathf.Max(0.0001f, creepScale.z));
            var importRotation = Quaternion.Inverse(creepObject.transform.rotation) * sourceFilter.transform.rotation;

            for (var index = 0; index < SwarmClusterSlots.Length; index++)
            {
                // Named from a table rather than interpolated: this runs per shard per swarm creep
                // per FRAME (the wobble below is what makes it per-frame), so the interpolation was
                // seven string allocations per creep per frame for seven names that never change.
                var shard = EnsureMeshChild(creepObject, SwarmShardNames[index], mesh, material);
                var active = index < livingCount;
                shard.SetActive(active);
                if (!active)
                {
                    continue;
                }

                // Each shard's own phase keeps the cluster from moving as one rigid block — the
                // "bubble" is this small per-shard wander around its slot, not a shared pose.
                var phase = index * 1.7f;
                var wobble = new Vector3(
                    Mathf.Sin(time * 2.1f + phase) * 0.045f,
                    Mathf.Sin(time * 3.3f + phase * 1.3f) * 0.03f + 0.03f,
                    Mathf.Cos(time * 2.4f + phase) * 0.045f);
                shard.transform.localPosition = SwarmClusterSlots[index] + wobble;
                shard.transform.localRotation = Quaternion.Euler(0f, (time * 26f + phase * 40f) % 360f, 0f) * importRotation;
                shard.transform.localScale = importScale * SwarmShardScale;
                SetColor(shard, color);
            }

            // Swarm drops the SenderAccent ownership pool entirely: against a spread cluster of
            // small shards (rather than the single solid body every other creep has) it reads as a
            // dominant glow rather than a subtle ground tint. Note this costs Swarm its sender
            // identity read — CreepRoleColor returns a fixed mint for swarm and ignores senderId,
            // so unlike roles that fall through to SenderColor, the shards themselves carry no
            // sender tint to fall back on.
            var senderAccent = creepObject.transform.Find("SenderAccent");
            if (senderAccent != null && senderAccent.gameObject.activeSelf)
            {
                senderAccent.gameObject.SetActive(false);
            }
        }

        private static void ConfigureAirMarker(GameObject creepObject)
        {
            ConfigureChild(EnsureChild(creepObject, "AirHoverRing", PrimitiveType.Cylinder), true, new Vector3(0f, -0.52f, 0f), new Vector3(0.88f, 0.04f, 0.88f), new Color(0.82f, 0.72f, 1f));
            ConfigureChild(EnsureChild(creepObject, "AirLeftWing", PrimitiveType.Cube), true, new Vector3(-0.5f, 0.02f, 0f), new Vector3(0.42f, 0.08f, 0.18f), new Color(0.82f, 0.72f, 1f));
            ConfigureChild(EnsureChild(creepObject, "AirRightWing", PrimitiveType.Cube), true, new Vector3(0.5f, 0.02f, 0f), new Vector3(0.42f, 0.08f, 0.18f), new Color(0.82f, 0.72f, 1f));
            ConfigureChild(EnsureChild(creepObject, "AirBeacon", PrimitiveType.Sphere), true, new Vector3(0f, 0.28f, 0f), new Vector3(0.2f, 0.2f, 0.2f), MintSignal);
        }

        private static void ConfigureStealthMarker(GameObject creepObject)
        {
            ConfigureChild(EnsureChild(creepObject, "StealthShimmer", PrimitiveType.Cylinder), true, new Vector3(0f, 0f, 0f), new Vector3(1.1f, 0.05f, 1.1f), new Color(0.86f, 0.96f, 1f));
            ConfigureChild(EnsureChild(creepObject, "StealthEchoA", PrimitiveType.Cylinder), true, new Vector3(0f, -0.18f, 0f), new Vector3(1.34f, 0.03f, 1.34f), new Color(0.36f, 0.5f, 0.58f));
            ConfigureChild(EnsureChild(creepObject, "StealthEchoB", PrimitiveType.Cylinder), true, new Vector3(0f, 0.2f, 0f), new Vector3(0.78f, 0.03f, 0.78f), new Color(0.72f, 0.84f, 0.9f));
        }

        private static void ConfigureSiegeMarker(GameObject creepObject)
        {
            ConfigureChild(EnsureChild(creepObject, "SiegeBase", PrimitiveType.Cube), true, new Vector3(0f, -0.02f, -0.06f), new Vector3(0.58f, 0.22f, 0.5f), new Color(0.56f, 0.12f, 0.16f));
            ConfigureChild(EnsureChild(creepObject, "SiegeBarrel", PrimitiveType.Cube), true, new Vector3(0f, 0.08f, 0.42f), new Vector3(0.18f, 0.16f, 0.62f), new Color(1f, 0.38f, 0.44f));
            ConfigureChild(EnsureChild(creepObject, "SiegeSpike", PrimitiveType.Cube), true, new Vector3(0.28f, 0.08f, 0f), new Vector3(0.38f, 0.16f, 0.2f), new Color(1f, 0.3f, 0.36f));
        }

        private static void ConfigureAuraMarker(GameObject creepObject)
        {
            ConfigureChild(EnsureChild(creepObject, "AuraField", PrimitiveType.Cylinder), true, new Vector3(0f, -0.34f, 0f), new Vector3(1.42f, 0.035f, 1.42f), new Color(0.42f, 1f, 0.72f));
            ConfigureChild(EnsureChild(creepObject, "AuraCore", PrimitiveType.Sphere), true, new Vector3(0f, 0.24f, 0f), new Vector3(0.28f, 0.28f, 0.28f), MintSignal);
            ConfigureChild(EnsureChild(creepObject, "AuraNorthNode", PrimitiveType.Sphere), true, new Vector3(0f, 0.02f, 0.46f), new Vector3(0.18f, 0.18f, 0.18f), SignalGold);
            ConfigureChild(EnsureChild(creepObject, "AuraSouthNode", PrimitiveType.Sphere), true, new Vector3(0f, 0.02f, -0.46f), new Vector3(0.18f, 0.18f, 0.18f), SignalGold);
        }

        private static void DeactivateKnownCreepMarkers(GameObject creepObject)
        {
            for (var index = 0; index < CreepMarkerNames.Length; index++)
            {
                var marker = creepObject.transform.Find(CreepMarkerNames[index]);
                if (marker != null)
                {
                    marker.gameObject.SetActive(false);
                }
            }
        }

        private static readonly string[] CreepMarkerNames =
        {
            "GroundShadow",
            "RunnerNose",
            "RunnerTail",
            "RunnerLeftFin",
            "RunnerRightFin",
            "RunnerSpeedLine",
            "BruteArmor",
            "BruteLeftPlate",
            "BruteRightPlate",
            "BruteCore",
            "BossCrown",
            "BossCore",
            "BossLeftHorn",
            "BossRightHorn",
            "SwarmDotA",
            "SwarmDotB",
            "SwarmDotC",
            "SwarmDotD",
            "SwarmDotE",
            "SwarmTrail",
            "AirHoverRing",
            "AirLeftWing",
            "AirRightWing",
            "AirBeacon",
            "StealthShimmer",
            "StealthEchoA",
            "StealthEchoB",
            "SiegeBase",
            "SiegeBarrel",
            "SiegeSpike",
            "AuraField",
            "AuraCore",
            "AuraNorthNode",
            "AuraSouthNode"
        };

        private static readonly string[] CreepReadabilityOverlayNames =
        {
            "RoleRunnerChevron",
            "RoleRunnerWake",
            "RoleBruteLeftShoulder",
            "RoleBruteRightShoulder",
            "RoleBruteCenterPlate",
            "RoleSwarmValueRing",
            "RoleSwarmLeadSpark",
            "RoleShadeLeftEcho",
            "RoleShadeRightEcho",
            "RoleShadeCoreLine",
            "RoleSiegeRamHead",
            "RoleSiegeWarningLeft",
            "RoleSiegeWarningRight"
        };

        private static readonly string[] CreepHealthOverlayNames =
        {
            "HealthBarBack",
            "HealthBarFill",
            "HealthBarMidTick",
            "HealthWoundPip"
        };

        private readonly struct CreepMotion
        {
            public CreepMotion(Vector3 positionOffset, Quaternion rotation, float scaleMultiplier = 1f)
            {
                PositionOffset = positionOffset;
                Rotation = rotation;
                ScaleMultiplier = scaleMultiplier;
            }

            public Vector3 PositionOffset { get; }
            public Quaternion Rotation { get; }
            public float ScaleMultiplier { get; }
        }

        private readonly struct CreepHealthBarMetrics
        {
            private CreepHealthBarMetrics(float width, float height, float depth, float y, float z, float minFillWidth, float woundOffset, float woundSize)
            {
                Width = width;
                Height = height;
                Depth = depth;
                Y = y;
                Z = z;
                MinFillWidth = minFillWidth;
                WoundOffset = woundOffset;
                WoundSize = woundSize;
            }

            public float Width { get; }
            public float Height { get; }
            public float Depth { get; }
            public float Y { get; }
            public float Z { get; }
            public float FillLift => Height * 0.18f;
            public float MinFillWidth { get; }
            public float WoundOffset { get; }
            public float WoundSize { get; }

            public static CreepHealthBarMetrics For(string creepId)
            {
                if (ContainsRole(creepId, "swarm"))
                {
                    return new CreepHealthBarMetrics(1.08f, 0.07f, 0.16f, 0.68f, 0.62f, 0.07f, 0.08f, 0.1f);
                }

                if (ContainsRole(creepId, "boss"))
                {
                    return new CreepHealthBarMetrics(1.58f, 0.095f, 0.22f, 1.26f, 0.78f, 0.1f, 0.12f, 0.14f);
                }

                if (ContainsRole(creepId, "brute") || ContainsRole(creepId, "tank"))
                {
                    return new CreepHealthBarMetrics(1.38f, 0.085f, 0.2f, 1.08f, 0.76f, 0.09f, 0.11f, 0.13f);
                }

                if (ContainsRole(creepId, "siege") || ContainsRole(creepId, "attacker"))
                {
                    return new CreepHealthBarMetrics(1.42f, 0.08f, 0.2f, 0.96f, 0.82f, 0.09f, 0.11f, 0.13f);
                }

                if (ContainsRole(creepId, "shade") || ContainsRole(creepId, "invisible") || ContainsRole(creepId, "stealth"))
                {
                    return new CreepHealthBarMetrics(1.06f, 0.07f, 0.16f, 0.84f, 0.66f, 0.07f, 0.08f, 0.1f);
                }

                return new CreepHealthBarMetrics(0.98f, 0.07f, 0.16f, 0.78f, 0.72f, 0.07f, 0.08f, 0.1f);
            }
        }
    }
}
