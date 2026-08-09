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
    /// Tower presentation: transform, aim and recoil motion, spin parts, role colours, tier
    /// markers, and the weapon styles the shot effects are drawn from.
    /// </summary>
    public sealed partial class UnityVerticalSliceRenderer
    {
        private readonly Dictionary<long, float> towerLastFiredAt = new Dictionary<long, float>();
        private readonly Dictionary<long, Vector3> towerAimTarget = new Dictionary<long, Vector3>();
        private readonly Dictionary<long, float> towerAimYaw = new Dictionary<long, float>();
        private readonly Dictionary<long, SpinPartState> towerSpinPartState = new Dictionary<long, SpinPartState>();

        // Real 3D tower prefabs have their own mesh pivot sitting exactly at the model's visual
        // base, but GridToWorld's fixed Y (0.35, shared with every other entity type) sits 0.47
        // units above BoardTopY (-0.12), the board mesh's actual baked floor surface — the same
        // surface each tower's own contact-shadow decal is correctly pinned to (UpdateContactShadow).
        // That gap is invisible from directly overhead, but the tilted match camera projects it into
        // a real, visible vertical disconnect between a tower and its own shadow, reading as
        // "floating"/"off-center". Anchoring real tower prefabs to BoardTopY instead removes the gap
        // without touching GridToWorld itself, which creeps and the (now-unused) primitive tower
        // fallback still rely on. Clearance must clear RangeHalo, the lowest root-level accessory
        // shape at local Y -0.06 (Tower3DImportPipeline.CreateRangeHalo) — those accessories sit
        // below root by design and would otherwise dip beneath BoardTopY and clip into the floor.
        private const float TowerBaseClearance = 0.08f;

        private static void SetTowerTransform(GameObject instance, GridPosition position, LaneId laneId, string towerId, TowerVisualProfile visualProfile)
        {
            var hasRealPrefab = visualProfile != null && visualProfile.Prefab != null;
            var lift = hasRealPrefab ? visualProfile.Lift : TowerRoleLift(towerId);
            var basePosition = GridToWorld(position, laneId);
            var baseY = hasRealPrefab ? BoardTopY + TowerBaseClearance : basePosition.y;
            instance.transform.position = new Vector3(basePosition.x, baseY + lift, basePosition.z);
            instance.transform.localScale = visualProfile != null && visualProfile.HasScale ? visualProfile.Scale : TowerRoleScale(towerId);
            instance.transform.rotation = Quaternion.identity;
        }

        private const float TowerRecoilDuration = 0.35f;
        private const float TowerAimTurnDegreesPerSecond = 260f;

        /// <summary>
        /// Idle motion, aim rotation and fire recoil all apply to the tower's Body child, not the
        /// root — RoleMarker/OwnerTrim/RangeHalo are siblings of Body (see
        /// Tower3DImportPipeline.GenerateWrapperIfRawExists) and are rotationally symmetric shapes
        /// that should never visibly kick or spin with an attack reaction.
        /// </summary>
        private void UpdateTowerMotion(GameObject towerObject, long key, Vector3 towerPosition, TowerVisualProfile visualProfile)
        {
            var parts = ResolveTowerMotionParts(key, towerObject);
            var body = parts.Body;
            var idle = TowerRoleMotion(visualProfile.Role);

            // Pulse has no clean seam anywhere on its mesh (its 4 spikes run the tower's full
            // height — every split attempt visibly detached their tips, see the Blender renders
            // behind the Pulse ring split), so it has no isolated part that could carry aim
            // rotation without swinging the whole stationary-looking bastion around with it. It
            // stays fixed entirely; only its Ring spins, and only its idle pulse carries the
            // "Pulse" identity — it's a splash/AOE emitter, not a turret that needs to point at a
            // specific target.
            // Was hardcoded to Pulse. Now a per-role trait, because Foundry Core fires upward out
            // of its stacks and Barricade Bastion is a fixed emplacement that fires one direction
            // only — both would contradict their own mechanic if they turned to track a target.
            var locksYaw = TowerMotionProfileFor(visualProfile.Role).LocksYaw;
            var yaw = locksYaw ? 0f : towerAimYaw.TryGetValue(key, out var currentYaw) ? currentYaw : 0f;
            if (!locksYaw && towerAimTarget.TryGetValue(key, out var aimTarget))
            {
                var direction = aimTarget - towerPosition;
                direction.y = 0f;
                if (direction.sqrMagnitude > 0.0001f)
                {
                    // atan2(x, z) assumes the model's own unrotated mesh faces +Z at yaw 0 — true
                    // for most of these towers, but not universal, and TowerHeadRestHeadingDegrees
                    // corrects for whichever roles it doesn't hold for (measured directly from the
                    // mesh, not assumed).
                    var desiredYaw = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg
                        - TowerHeadRestHeadingDegrees(visualProfile.Role);
                    yaw = Mathf.MoveTowardsAngle(yaw, desiredYaw, TowerAimTurnDegreesPerSecond * Time.deltaTime);
                }
            }

            if (!locksYaw)
            {
                towerAimYaw[key] = yaw;
            }

            var timeSinceFired = towerLastFiredAt.TryGetValue(key, out var firedAt) ? Time.time - firedAt : float.MaxValue;
            var recoilDuration = Mathf.Max(0.01f, TowerMotionProfileFor(visualProfile.Role).RecoilDuration);
            var recoil = timeSinceFired < recoilDuration ? 1f - timeSinceFired / recoilDuration : 0f;

            // Recoil is a rigid kick along the tower's current firing axis (position + a small
            // backward pitch), not a squash/stretch scale distortion — these towers are stone and
            // metal, and jelly-deformation on a rigid body reads as wrong regardless of how well
            // it's tuned. Idle keeps a small UNIFORM scale pulse (a "breathing" energy effect,
            // not an axis-skewed squash), which is a different thing from a recoil punch.
            var idleScale = 1f + idle.ScalePulse;
            // Pulse is a stationary splash/AOE emitter, not a mechanical weapon with a kickback —
            // it should show zero recoil-driven position/pitch motion when it fires, only its own
            // VFX flash and ring spin. locksYaw (Pulse-only, see above) doubles as that flag here.
            var recoilProfile = TowerMotionProfileFor(visualProfile.Role);
            var recoilKickScale = recoilProfile.SuppressRecoil ? 0f : recoilProfile.RecoilScale;
            var recoilKick = recoil * 0.16f * recoilKickScale;
            var kickDirection = Quaternion.Euler(0f, yaw, 0f) * Vector3.back;
            var recoilPosition = kickDirection * recoilKick + Vector3.down * (recoil * 0.04f * recoilKickScale);

            // Turret-style towers (currently just Arrow, split via split_tower_rigid_part.py) have
            // a HeadPivot separate from Base: aim yaw and recoil apply to HeadPivot alone, so only
            // the cannon swivels/kicks while the foundation underneath stays put, like a real
            // turret. HeadPivot is a purpose-built empty with an identity rest transform (see
            // Tower3DImportPipeline — the actual Head mesh is reparented under it), exactly like
            // Body, so it's just as safe to overwrite outright. Towers without one (everything
            // else so far) fall back to turning the whole Body, exactly as before.
            var headPivot = parts.HeadPivot;
            if (headPivot != null)
            {
                body.localPosition = idle.PositionOffset;
                body.localRotation = Quaternion.Euler(idle.PitchDegrees, 0f, 0f);
                body.localScale = Vector3.one * idleScale;

                // A HeadPivot kick moves only the isolated barrel relative to a stationary Base —
                // the same 0.16-unit magnitude that read as a subtle whole-model punch before
                // (Base and Head always moved together, so no gap could ever show) instead reads
                // as the barrel flying off its mount, since nothing hides the separation anymore.
                // Scaled down substantially so recoil stays a tight, visibly-connected kick.
                var headRecoilPosition = kickDirection * (recoil * 0.05f * recoilKickScale)
                    + Vector3.down * (recoil * 0.015f * recoilKickScale);
                headPivot.localPosition = headRecoilPosition;
                headPivot.localRotation = Quaternion.Euler(-recoil * 6f * recoilKickScale, yaw, 0f);
            }
            else
            {
                body.localPosition = idle.PositionOffset + recoilPosition;
                body.localRotation = Quaternion.Euler(idle.PitchDegrees - recoil * 10f * recoilKickScale, yaw, 0f);
                body.localScale = Vector3.one * idleScale;
            }

            // Rigid sub-parts (Control's floating ring, Relay's dish, Prism's spire) spin
            // independently of Body's own idle/aim/recoil motion — a continuous spin about the
            // WORLD-vertical axis, not something driven by firing state. Searched by name rather
            // than a fixed path since a spin part sits under whatever depth the imported raw mesh
            // hierarchy happens to nest it at (e.g. Body/Imported3DVisual/LTW_Unity_ExportRoot/Ring),
            // which is an import-pipeline detail this call site shouldn't need to know. Each tower
            // has at most one spin part today, so the first name found wins.
            UpdateBarrelSpin(key, parts.Barrel);

            var spinPart = parts.SpinPart;
            if (spinPart != null)
            {
                // A plain Quaternion.Euler(0, angle, 0) assumes the part's own local Y axis IS
                // world-up, which isn't guaranteed once an FBX export/import round-trip has done
                // its own Z-up/Y-up axis conversion partway down the hierarchy — it produced an
                // end-over-end tumble instead of a flat Saturn's-rings spin on Control's ring.
                // Instead, convert world-up into whatever axis it actually corresponds to in the
                // part's own rest space (cached once), the same fix pattern used for the Brute
                // rig's degenerate straight-down bone case in rig_quadruped_creep.py.
                if (!towerSpinPartState.TryGetValue(key, out var spinState))
                {
                    var localSpinAxis = spinPart.parent.InverseTransformDirection(Vector3.up).normalized;
                    spinState = new SpinPartState(localSpinAxis, spinPart.localRotation);
                    towerSpinPartState[key] = spinState;
                }

                // Rest tilt is applied BETWEEN the rest pose and the sweep: tip the part off
                // horizontal first, then sweep that tipped part about world-up. The other order
                // would tilt the whole swept result and give a wobble rather than a radar sweep.
                var restTilt = SpinPartRestTiltDegrees(visualProfile.Role);
                var tilted = restTilt == 0f
                    ? spinState.RestLocalRotation
                    : Quaternion.AngleAxis(restTilt, spinPart.parent.InverseTransformDirection(Vector3.forward).normalized)
                      * spinState.RestLocalRotation;

                spinPart.localRotation = Quaternion.AngleAxis(Time.time * TowerRingSpinDegreesPerSecond, spinState.LocalSpinAxis) * tilted;
            }
        }

        private const float TowerRingSpinDegreesPerSecond = 32f;

        /// <summary>
        /// How far off horizontal a tower's spin part rests, in degrees. Zero for everything the
        /// board reads correctly.
        /// </summary>
        /// <remarks>
        /// This exists for the Relay, reported from play as "tilting away from the POV, almost as
        /// if it's not quite 3D" (docs/screenshot-reviews/tower-perspective-relay).
        ///
        /// MEASURED CAUSE. The board camera is orthographic and looks down 56.5 degrees from
        /// horizontal, so its view axis is 33.5 degrees off vertical. The Relay's dish is the only
        /// genuinely flat hero feature on the roster whose face points straight UP, which puts its
        /// normal 33.5 degrees off the view axis — it renders at 0.83 of its true width, near enough
        /// to a perfect circle to carry no foreshortening at all. Every other tower's hero feature
        /// is oriented horizontally and renders at 0.55 or less: Prism's spire, Tesla's coil and
        /// Gatling's head all measure 0.55, the Repair Drone's 0.00. A circle has no orientation
        /// cue, so the tower's dominant element gives the eye nothing to read depth from, and the
        /// whole tower reads as a sprite.
        ///
        /// It also made the Relay's only idle animation invisible. The dish already sweeps about
        /// world-up at <see cref="TowerRingSpinDegreesPerSecond"/>, and world-up was exactly its own
        /// axis of symmetry — rendered at four sweep phases 90 degrees apart, all four frames came
        /// out identical. Tipping the dish is what turns that existing sweep into a radar sweep.
        ///
        /// INTENT IS RECORDED, NOT INFERRED, which is what the review doc said would unblock this.
        /// The hand-authored Relay that predates the Meshy model — TowerShape.SignalMast in
        /// TowerVisualPrefabGenerator — mounts its two dishes at Euler X of -58 and +68 degrees.
        /// A steeply pitched dish is the design; the imported replacement lost it.
        ///
        /// 40 rather than the recorded 58, and the difference is the part count. The original splays
        /// TWO dishes in opposite directions, so one always presents a face to the camera. There is
        /// one dish here, and rendered across the sweep at 58 it spends roughly a quarter of every
        /// revolution edge-on and effectively disappears. At 40 it is dimensional at every phase and
        /// legible at all of them.
        ///
        /// The number itself lives on <see cref="TowerVisualTuning"/> because the codex's preview
        /// stage has to apply the same one — a correction made here alone would leave the screen
        /// that exists to showcase a tower disagreeing with the game about what it looks like.
        /// </remarks>
        private static float SpinPartRestTiltDegrees(TowerVisualRole role) =>
            TowerVisualTuning.SpinPartRestTiltDegrees(role);

        private static readonly string[] TowerSpinPartNames = TowerVisualTuning.SpinPartNames;

        /// <summary>
        /// The four transforms <see cref="UpdateTowerMotion"/> drives, found once per pooled tower
        /// instance instead of once per tower per frame.
        /// </summary>
        /// <remarks>
        /// This is the same defect the rest of this file's frame/snapshot split addresses, in its
        /// most expensive form: finding these is not per-snapshot work but per-INSTANCE work — a
        /// tower's Body, HeadPivot, Barrel and spin part cannot move within its hierarchy — and it
        /// was being redone sixty times a second for every tower on the board. It also allocates,
        /// which the rest of the search-by-name code in this file does not: <see cref="FindDeepChild"/>
        /// walks with <c>foreach (Transform child in parent)</c>, and Transform's enumerator is a
        /// class, so one object is allocated for every node visited on every walk. With five walks
        /// per tower per frame (HeadPivot, Barrel, then Ring/Dish/Spire until one hits) over a full
        /// eight-lane board, that was measured as the single largest source of per-frame garbage in
        /// the renderer, larger than the string keys and the per-snapshot re-application combined.
        ///
        /// Keyed by entity id but validated against the INSTANCE, because pooling can hand the same
        /// entity id a different GameObject (see GetOrCreateTower, which swaps pools when a tower's
        /// visual profile changes) — an entry that did not check would then drive the wrong object's
        /// transforms.
        /// </remarks>
        private readonly struct TowerMotionParts
        {
            public TowerMotionParts(GameObject instance, Transform body, Transform headPivot, Transform barrel, Transform spinPart)
            {
                Instance = instance;
                Body = body;
                HeadPivot = headPivot;
                Barrel = barrel;
                SpinPart = spinPart;
            }

            public GameObject Instance { get; }
            public Transform Body { get; }
            public Transform HeadPivot { get; }
            public Transform Barrel { get; }
            public Transform SpinPart { get; }
        }

        private readonly Dictionary<long, TowerMotionParts> towerMotionParts = new Dictionary<long, TowerMotionParts>();

        private TowerMotionParts ResolveTowerMotionParts(long key, GameObject towerObject)
        {
            if (towerMotionParts.TryGetValue(key, out var cached) && cached.Instance == towerObject && cached.Body != null)
            {
                return cached;
            }

            var body = ResolveTowerMotionTarget(towerObject);
            var parts = new TowerMotionParts(
                towerObject,
                body,
                FindDeepChild(body, "HeadPivot"),
                FindDeepChild(body, "Barrel"),
                FindSpinPart(body));
            towerMotionParts[key] = parts;
            return parts;
        }

        /// <summary>Barrel spin, in degrees/second, while the gun is actively firing.</summary>
        private const float BarrelFiringSpinDegreesPerSecond = 900f;

        /// <summary>Barrel spin while idle. Not zero — a gatling that stops dead reads as broken.</summary>
        private const float BarrelIdleSpinDegreesPerSecond = 40f;

        /// <summary>How long the barrel takes to coast down from firing speed to idle.</summary>
        private const float BarrelSpindownSeconds = 0.9f;

        private readonly Dictionary<long, float> towerBarrelAngle = new Dictionary<long, float>();
        private readonly Dictionary<long, SpinPartState> towerBarrelState = new Dictionary<long, SpinPartState>();

        /// <summary>
        /// Spins a gatling-style barrel about its own long axis, faster while it is firing.
        /// </summary>
        /// <remarks>
        /// Deliberately NOT folded into the Ring/Dish/Spire spin above, for two reasons. That spin is
        /// about world UP, which is right for a horizontal ring and meaningless for a barrel; and it
        /// is a constant rate, whereas the whole read of a gatling is that it winds up when it starts
        /// working and coasts down when it stops.
        ///
        /// The axis is taken from the barrel's own mesh bounds — its longest extent IS the bore — and
        /// cached in the barrel's LOCAL space. Local matters: the barrel hangs under HeadPivot, which
        /// yaws to aim, so a world-space axis would only be correct at the rotation it happened to be
        /// sampled at. Deriving it from geometry also means it cannot drift out of step with the
        /// measured rest heading the way a second hand-entered constant would.
        ///
        /// The angle is ACCUMULATED rather than computed from Time.time * rate, so that changing the
        /// rate speeds the barrel up instead of teleporting it to a new phase.
        /// </remarks>
        private void UpdateBarrelSpin(long key, Transform barrel)
        {
            if (barrel == null)
            {
                return;
            }

            if (!towerBarrelState.TryGetValue(key, out var state))
            {
                state = new SpinPartState(LongestLocalAxis(barrel), barrel.localRotation);
                towerBarrelState[key] = state;
            }

            var sinceFired = towerLastFiredAt.TryGetValue(key, out var firedAt) ? Time.time - firedAt : float.MaxValue;
            var firing = Mathf.Clamp01(1f - sinceFired / BarrelSpindownSeconds);
            var rate = Mathf.Lerp(BarrelIdleSpinDegreesPerSecond, BarrelFiringSpinDegreesPerSecond, firing);

            var angle = (towerBarrelAngle.TryGetValue(key, out var previous) ? previous : 0f) + rate * Time.deltaTime;
            if (angle > 360f)
            {
                angle -= 360f;
            }

            towerBarrelAngle[key] = angle;
            barrel.localRotation = Quaternion.AngleAxis(angle, state.LocalSpinAxis) * state.RestLocalRotation;
        }

        /// <summary>The part's longest mesh-bounds extent, as a direction in its own local space.</summary>
        private static Vector3 LongestLocalAxis(Transform part)
        {
            var filter = part.GetComponentInChildren<MeshFilter>();
            if (filter == null || filter.sharedMesh == null)
            {
                return Vector3.forward;
            }

            var extents = filter.sharedMesh.bounds.extents;
            var meshAxis = extents.x >= extents.y && extents.x >= extents.z
                ? Vector3.right
                : extents.y >= extents.z ? Vector3.up : Vector3.forward;
            var world = filter.transform.TransformDirection(meshAxis);
            var local = part.InverseTransformDirection(world);
            return local.sqrMagnitude < 1e-6f ? Vector3.forward : local.normalized;
        }

        private static Transform FindSpinPart(Transform body)
        {
            for (var index = 0; index < TowerSpinPartNames.Length; index++)
            {
                var found = FindDeepChild(body, TowerSpinPartNames[index]);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        private readonly struct SpinPartState
        {
            public SpinPartState(Vector3 localSpinAxis, Quaternion restLocalRotation)
            {
                LocalSpinAxis = localSpinAxis;
                RestLocalRotation = restLocalRotation;
            }

            public Vector3 LocalSpinAxis { get; }
            public Quaternion RestLocalRotation { get; }
        }

        private static Transform FindDeepChild(Transform parent, string name)
        {
            foreach (Transform child in parent)
            {
                if (child.name == name)
                {
                    return child;
                }

                var found = FindDeepChild(child, name);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        /// <summary>
        /// Resolves the tower's actual mesh-bearing Body transform, NOT
        /// <see cref="TowerVisualProfile.BodyRendererPath"/> — that field is "BodyTintAnchor" for
        /// every tower profile (see TowerVisualLibrary.asset), a separate mesh-less empty used only
        /// for the SetProfileColor tint lookup. It is a sibling of Body, not an alias for it (see
        /// Tower3DImportPipeline.GenerateWrapperIfRawExists, which creates both as distinct children
        /// of root). Applying motion to BodyTintAnchor silently animates nothing, since it has no
        /// renderer anywhere under it — this was the actual reason tower idle/aim/recoil motion
        /// (and now ring-spin) never appeared on screen.
        /// </summary>
        private static Transform ResolveTowerMotionTarget(GameObject towerObject)
        {
            return towerObject.transform.Find("Body") ?? towerObject.transform;
        }

        /// <summary>
        /// The tower's Body transform, if the tower is currently active, else null. Attack VFX uses
        /// this to place beams/effects relative to the tower's actual current animated state
        /// (idle drift, aim rotation, recoil) instead of a fixed world-space offset from the grid
        /// position — a tower that has turned to face its target was firing its beam from where it
        /// used to point, not where it currently does, before this was threaded through.
        /// </summary>
        private Transform ResolveTowerBodyTransform(long towerKey)
        {
            if (!activeTowers.TryGetValue(towerKey, out var towerObject) || towerObject == null)
            {
                return null;
            }

            // Turret-style towers carry aim/recoil on a HeadPivot instead of Body (see
            // UpdateTowerMotion) — attack VFX must follow whichever transform actually turns, or a
            // muzzle flash fires from where the cannon used to point before it swiveled. Towers
            // without one keep using Body, unchanged.
            var parts = ResolveTowerMotionParts(towerKey, towerObject);
            return parts.HeadPivot != null ? parts.HeadPivot : parts.Body;
        }

        private static Vector3 TowerRoleScale(string towerId)
        {
            if (ContainsRole(towerId, "slow") || ContainsRole(towerId, "splash") || ContainsRole(towerId, "control") || ContainsRole(towerId, "pulse"))
            {
                return new Vector3(0.92f, 0.34f, 0.92f);
            }

            if (IsRelayTower(towerId))
            {
                return new Vector3(0.46f, 0.92f, 0.46f);
            }

            if (ContainsRole(towerId, "prism"))
            {
                return new Vector3(0.42f, 1.22f, 0.42f);
            }

            return new Vector3(0.48f, 1.08f, 0.48f);
        }

        private static float TowerRoleLift(string towerId)
        {
            if (IsRelayTower(towerId))
            {
                return 0.18f;
            }

            return 0.12f;
        }

        /// <summary>
        /// Per-role idle motion for a tower's Body child. Yaw is deliberately left untouched here
        /// (stays 0): aim rotation in <see cref="UpdateTowerMotion"/> owns yaw exclusively (applied
        /// to the split Head for turret-style towers, to Body itself otherwise), so idle and aim
        /// never fight over the same axis.
        /// </summary>
        /// <summary>
        /// The match camera is orthographic and tilted (ConfigureDefaultCamera, default 30 degrees
        /// off vertical), so Y-axis position and X-axis pitch reach the screen at only sin(tilt) of
        /// their true magnitude — real, but still partial. Idle motion leans on XZ position and
        /// uniform scale, which lose nothing to that projection, and layers pitch/position on top
        /// rather than relying on them alone.
        /// </summary>
        /// <summary>
        /// How far a tower's mesh, at yaw 0 (HeadPivot/Body's identity rest rotation), actually
        /// faces from world +Z, in the same atan2(x, z) convention the aim-yaw math above uses —
        /// measured directly IN UNITY (a Blender-side measurement of the same mesh gave the wrong
        /// sign, since Blender's FBX exporter mirrors X during its right-handed-to-left-handed
        /// conversion), not asserted. Without this correction the turret still tracks (relative
        /// motion as a target moves is correct either way), just aimed a constant angle away from
        /// the actual target — invisible while the whole Body carried the rotation, obvious once
        /// Arrow's barrel became an isolated, independently-aimed Head. Other roles default to 0
        /// pending the same per-mesh measurement; none have shown the same symptom yet, but none
        /// have an isolated barrel-like part to reveal it either.
        /// </summary>
        /// <summary>
        /// Everything the presentation layer needs to know about a tower role, in one row.
        /// </summary>
        /// <remarks>
        /// This replaces three separate role-keyed switches (rest heading, idle motion, yaw lock).
        /// With five towers that was tolerable; at fifteen it meant three places to edit per tower
        /// and three places to forget. Adding a tower is now one row.
        ///
        /// BreatheAmp is a uniform scale pulse, not a deformation — these are hard-surface
        /// structures and should not squash. DriftAmp is a lateral sway in local units. Sharpness
        /// above 1 makes the pulse peaked rather than sinusoidal, which is what reads as a
        /// heartbeat instead of a sway.
        /// </remarks>
        private readonly struct TowerMotionProfile
        {
            public TowerMotionProfile(
                float breatheHz,
                float breatheAmp,
                float driftHz = 0f,
                float driftAmp = 0f,
                float sharpness = 1f,
                bool locksYaw = false,
                float restHeadingDegrees = 0f,
                bool? suppressRecoil = null,
                float recoilScale = 1f,
                float recoilDuration = TowerRecoilDuration)
            {
                RecoilDuration = recoilDuration;
                SuppressRecoil = suppressRecoil ?? locksYaw;
                BreatheHz = breatheHz;
                BreatheAmp = breatheAmp;
                DriftHz = driftHz;
                DriftAmp = driftAmp;
                Sharpness = sharpness;
                LocksYaw = locksYaw;
                RestHeadingDegrees = restHeadingDegrees;
                RecoilScale = recoilScale;
            }

            public float BreatheHz { get; }
            public float BreatheAmp { get; }
            public float DriftHz { get; }
            public float DriftAmp { get; }
            public float Sharpness { get; }

            /// <summary>Tower never rotates to face its target.</summary>
            public bool LocksYaw { get; }

            /// <summary>
            /// Tower shows no recoil kick when it fires. Defaults to <see cref="LocksYaw"/>, which is
            /// how this behaved when the two were the same flag.
            /// </summary>
            /// <remarks>
            /// They had to come apart for the Barricade Bastion. It locks yaw because it never turns,
            /// but a fixed emplacement's whole read is the kick straight back along its one axis, and
            /// tying the two together left it firing with no reaction at all.
            /// </remarks>
            public bool SuppressRecoil { get; }

            /// <summary>
            /// Multiplier on the firing kick, where 1 is the original uniform magnitude.
            /// </summary>
            /// <remarks>
            /// Recoil used to be one hardcoded magnitude shared by every tower that showed any, so a
            /// 10-gold Sapling Sentinel kicked exactly as hard as a 52-gold Foundry Core lobbing a
            /// mortar shell. Weight is most of what separates these towers visually, and firing was
            /// the one moment that said nothing about it.
            ///
            /// Scales the whole kick — backward travel, downward drop and pitch — so a value stays a
            /// statement about the tower's weight rather than about one axis.
            /// </remarks>
            public float RecoilScale { get; }

            /// <summary>
            /// How long one kick takes to decay. MUST stay under the tower's own firing interval.
            /// </summary>
            /// <remarks>
            /// The shared 0.35s default silently breaks for anything fast. The Gatling Turret's
            /// cooldown is 1 tick — 0.25s at 4 ticks/second — so each kick was re-triggered before
            /// the previous one had decayed, and the gun sat pinned near full recoil instead of
            /// pulsing. That reads as a tower shaking itself apart rather than as rate of fire, and
            /// reducing the MAGNITUDE cannot fix it: the problem is that the animation never
            /// finishes. Fast guns need a short kick, not only a small one.
            /// </remarks>
            public float RecoilDuration { get; }

            /// <summary>
            /// Heading the head's mesh already points at in its rest pose, subtracted from the aim
            /// heading. Must be MEASURED IN UNITY, not Blender: Blender's FBX export mirrors X
            /// during the right-handed to left-handed conversion, which silently flips the sign.
            /// </summary>
            public float RestHeadingDegrees { get; }
        }

        private static TowerMotionProfile TowerMotionProfileFor(TowerVisualRole role)
        {
            switch (role)
            {
                // Aim and recoil live on the split Head turret (UpdateTowerMotion), so Body only
                // needs a faint idle presence — an alert, mostly-still gun emplacement.
                // Rest heading measured in Unity: barrel tip at local (x=0.725, z=0.001) = +90.
                case TowerVisualRole.Arrow:
                    return new TowerMotionProfile(1.4f, 0.048f, driftHz: 0.9f, driftAmp: 0.028f, restHeadingDegrees: 90f, recoilScale: 1.2f);

                // The arms+core+ring assembly turns to aim and the ring spins independently, both
                // real visible motion, so Body-level sway on top was pure excess. Reads as a
                // mostly-still ancient structure with a faint pulse of life.
                case TowerVisualRole.Control:
                    return new TowerMotionProfile(1.6f, 0.026f, driftHz: 0.7f, driftAmp: 0.028f, recoilScale: 0.5f);

                // The split Dish spins continuously; Body adds a slow mast sway underneath rather
                // than competing with the dish for attention.
                case TowerVisualRole.Relay:
                    return new TowerMotionProfile(1.1f, 0.02f, driftHz: 0.6f, driftAmp: 0.025f, recoilScale: 0.4f);

                // No cleanly separable emitter part on this mesh, so the name is carried by a
                // heartbeat-shaped pulse on the whole Body: peaked, not sinusoidal. Yaw locked —
                // a dome has no facing, and rotating it read as the whole tower spinning.
                // Amplitude raised from 0.022 after measuring it on screen: the throb that names
                // this tower moved its silhouette 0.62px peak-to-peak at 1080x1920, which is not a
                // pulse anyone can see. Sharpness 3 keeps the shape — most of the cycle sits near
                // rest and it spikes — so a larger amplitude reads as a harder beat rather than as a
                // wobble. Still the weakest px-per-amplitude in the roster: a low flat dome changes
                // very little on screen when it scales, which is why it needs the most: 0.09 only
                // reached 2.55px, so this is the measured amount rather than a guessed one.
                case TowerVisualRole.Pulse:
                    return new TowerMotionProfile(1.1f, 0.112f, sharpness: 3f, locksYaw: true);

                // The split Spire spins continuously; Body adds a faint glow-breathe underneath.
                case TowerVisualRole.Prism:
                    return new TowerMotionProfile(1.8f, 0.025f, recoilScale: 0.8f);

                // --- Foundry line -------------------------------------------------------------
                // Machines: tight, fast, mechanical. Small amplitudes, no lazy drift.
                // Light recoil because it fires every other tick — a full-weight kick repeated that
                // often stops reading as a reaction and turns into a permanent shake.
                // The gun is now split from its pedestal (Head/Base, split_tower_rigid_part.py at
                // z=0.556 where the barrel housing's radius jumps clear of the dome), so aim and
                // recoil drive HeadPivot alone and the base stays planted — previously the whole
                // tower swung and kicked as one piece.
                //
                // Rest heading measured IN UNITY off the generated prefab (98.7 degrees), never in
                // Blender: the FBX export mirrors X, which flips the sign.
                //
                // The kick is small AND short. Short is the load-bearing half: this fires every
                // 0.25s, so anything at the 0.35s default never returns to rest between shots.
                case TowerVisualRole.Gatling:
                    return new TowerMotionProfile(2.4f, 0.036f, restHeadingDegrees: 98.7f, recoilScale: 0.3f, recoilDuration: 0.12f);

                // A coil under load. Fast shallow pulse reads as electrical rather than breathing.
                // The lightest kick of any tower that has one: an arc discharge has no projectile
                // mass behind it, so what little movement there is comes from the coil, not a barrel.
                // Yaw locked: a tiered masonry pagoda cannot swivel on its foundations, and Chain Arc
                // leaps between creeps rather than firing along a line, so it has nothing to point.
                // Amplitude raised from 0.014, which measured 0.89px on screen — invisible. The
                // fast rate is what makes it read as electrical rather than as breathing, so the
                // rate is untouched and only the depth changes.
                case TowerVisualRole.Tesla:
                    return new TowerMotionProfile(3.2f, 0.05f, sharpness: 2f, locksYaw: true, suppressRecoil: false, recoilScale: 0.35f);

                // A furnace. Slow heavy peaked pulse, like a bellows. Yaw locked: it fires upward
                // out of its stacks, so it has no facing to turn toward a target.
                //
                // Recoil explicitly un-suppressed, and the heaviest in the game. It had none at all
                // before, purely because SuppressRecoil defaults to LocksYaw and this tower locks yaw
                // — the same conflation the Barricade Bastion already had to be rescued from. Having
                // no facing is a reason not to TURN; it is not a reason to lob the heaviest shell on
                // the board (14 damage on a 6-tick cooldown) with no reaction whatsoever.
                // Amplitude raised from 0.02, measured at 1.18px — a bellows nobody could see
                // working. The slow rate and the peaked shape are the bellows; only the depth moves.
                case TowerVisualRole.Foundry:
                    return new TowerMotionProfile(0.8f, 0.054f, sharpness: 2.5f, locksYaw: true, suppressRecoil: false, recoilScale: 1.8f);

                // Yaw locked and nearly inert by design — a fixed emplacement that fires along one
                // direction only. Any turn or sway would contradict the mechanic. The kick is
                // oversized to match: with the idle almost dead, firing is the only motion it has,
                // so it has to carry the whole read on its own.
                case TowerVisualRole.Barricade:
                    return new TowerMotionProfile(0.7f, 0.03f, locksYaw: true, suppressRecoil: false, recoilScale: 1.5f);

                // A bolted-down spire, not an aircraft. It previously carried the widest drift in the
                // roster (0.04) to read as "hovering rather than planted" — but the mesh is a pillar
                // on a plinth with a dish on top, so drifting it sideways read as the whole structure
                // sliding around inside its cell rather than as flight. Drift removed and the pulse
                // cut to a faint idle, near Barricade's deliberately-inert level. The thing that
                // should look airborne is the servicing tether it projects, not the building.
                // Yaw locked: a pillar bolted to a plinth cannot rotate, and what it actually projects is
                // a servicing tether to a neighbour, not a shot at a creep.
                case TowerVisualRole.RepairDrone:
                    return new TowerMotionProfile(1.2f, 0.026f, locksYaw: true, suppressRecoil: false, recoilScale: 0.5f);

                // --- Grove line ---------------------------------------------------------------
                // Living things: slower and larger than the machines, with real sway.
                // A huge canopy. Slow, wide sway — the only tower whose drift is meant to read
                // from across the board.
                // Yaw locked: a rooted tree does not pivot to face anything. The widest sway in the roster
                // now carries it alone instead of competing with a rotation.
                case TowerVisualRole.ElderCanopy:
                    return new TowerMotionProfile(0.6f, 0.03f, driftHz: 0.4f, driftAmp: 0.055f, locksYaw: true, suppressRecoil: false, recoilScale: 0.9f);

                // Small and eager. Quicker and springier than its elders.
                // Yaw locked, same reason as its elder. The quick springy sway is the whole read.
                case TowerVisualRole.Sapling:
                    return new TowerMotionProfile(2.0f, 0.045f, driftHz: 1.2f, driftAmp: 0.048f, locksYaw: true, suppressRecoil: false, recoilScale: 0.5f);

                // A flower. Slow open-and-close bloom, peaked so it reads as breathing.
                // Yaw locked: a flower on a stalk. The peaked open-and-close pulse already names the tower.
                // Amplitude raised from 0.035, measured at 1.66px. This is the tower whose NAME is
                // the motion, and after the yaw lock the bloom is the only thing it does; at under
                // two pixels it did not do it.
                case TowerVisualRole.Bloomheart:
                    return new TowerMotionProfile(0.9f, 0.068f, sharpness: 2f, driftHz: 0.5f, driftAmp: 0.02f, locksYaw: true, suppressRecoil: false, recoilScale: 0.6f);

                // Coiled and tense. Very little motion until it strikes, so almost static — which is
                // exactly why the strike itself is one of the hardest kicks here. A snare whose whole
                // character is stored tension needs the release to land.
                // Yaw locked: a snare waits. One that turns to watch a creep approach is not a trap.
                // suppressRecoil is explicitly false so locking yaw does not also remove the snap —
                // stillness THEN a hard snap is the entire characterisation.
                case TowerVisualRole.ThornSnare:
                    return new TowerMotionProfile(0.5f, 0.042f, driftHz: 0.45f, driftAmp: 0.035f, locksYaw: true, suppressRecoil: false, recoilScale: 1.4f);

                // A fungal bloom venting spores. Slow swell with a lazy drift.
                // Yaw locked: a cloud has no facing, which its own name says.
                case TowerVisualRole.SporeCloud:
                    return new TowerMotionProfile(0.7f, 0.032f, sharpness: 1.6f, driftHz: 0.35f, driftAmp: 0.035f, locksYaw: true, suppressRecoil: false, recoilScale: 0.4f);

                // Arrow's twin-shot cousin, kitbashed from the same base with the same +90 barrel
                // heading. Slightly slower and shallower than Arrow at idle so the pair read as
                // kin rather than copies; recoil at 1.0 because Twin Volley emits two
                // TowerFiredEvents per cooldown and the double kick is the mechanic's read.
                case TowerVisualRole.TwinCrescent:
                    return new TowerMotionProfile(1.2f, 0.044f, driftHz: 0.8f, driftAmp: 0.026f, restHeadingDegrees: 90f, recoilScale: 1.0f);

                default:
                    return new TowerMotionProfile(1.3f, 0.02f);
            }
        }

        private static float TowerHeadRestHeadingDegrees(TowerVisualRole role) =>
            TowerMotionProfileFor(role).RestHeadingDegrees;

        private static TowerMotion TowerRoleMotion(TowerVisualRole role)
        {
            var profile = TowerMotionProfileFor(role);
            var time = Time.time;

            var wave = Mathf.Sin(time * profile.BreatheHz);
            var breathe = profile.Sharpness > 1f
                ? Mathf.Pow(Mathf.Abs(wave), profile.Sharpness) * profile.BreatheAmp
                : wave * profile.BreatheAmp;

            var drift = Vector3.zero;
            if (profile.DriftAmp > 0f)
            {
                drift = new Vector3(
                    Mathf.Sin(time * profile.DriftHz) * profile.DriftAmp,
                    0f,
                    Mathf.Cos(time * profile.DriftHz * 0.85f) * profile.DriftAmp * 0.8f);
            }

            return new TowerMotion(drift, 0f, breathe);
        }

        private static Color TowerRoleColor(string towerId, int ownerId)
        {
            if (ContainsRole(towerId, "slow") || ContainsRole(towerId, "ice") || ContainsRole(towerId, "control"))
            {
                return new Color(0.56f, 0.86f, 1f);
            }

            if (ContainsRole(towerId, "splash") || ContainsRole(towerId, "fire") || ContainsRole(towerId, "area"))
            {
                return new Color(1f, 0.58f, 0.22f);
            }

            if (IsRelayTower(towerId))
            {
                return SignalGold;
            }

            return OwnerAccent(ownerId);
        }

        /// <summary>
        /// Brightness applied to a tower's role marker for each tier it has been upgraded to.
        /// </summary>
        /// <remarks>
        /// An upgraded tower was otherwise pixel-identical to a fresh one, so a player who had
        /// spent gold levelling eight towers had no way to see which eight without tapping each in
        /// turn. Same defect the Repair Drone's buff had before it got a tether: a mechanic you
        /// cannot see is a spreadsheet.
        ///
        /// The role marker carries it rather than the body, because the marker is a small accent
        /// whose colour is already role-coded — shifting it reads as "this one is hotter" without
        /// changing silhouette, footprint or the owner trim that identifies whose it is.
        ///
        /// KNOWN WEAK, and measured rather than assumed: the marker colour is already saturated at
        /// tier 1 (HSV value 1.0), so this does not brighten so much as wash toward white, and the
        /// steps are uneven — RGB delta 0.392 from tier 1 to 2 but only 0.136 from 2 to 3. It is
        /// therefore a hint, not a readout, and telling tier 2 from tier 3 across a busy board is
        /// not something it can be relied on for. The authoritative answer is the TIER line on the
        /// selected-tower panel. A capture at the real game camera could not settle legibility
        /// either way because the towers sit under their own range halos and role labels, so this
        /// wants a human look before anything depends on it.
        /// </remarks>
        private static float TierMarkerBoost(int tier) => tier switch
        {
            >= 3 => 2.05f,
            2 => 1.6f,
            _ => 1.16f
        };

        private static void ApplyTowerColor(GameObject towerObject, string towerId, int ownerId, TowerVisualProfile visualProfile, int tier)
        {
            var roleColor = BoostValue(TowerMarkerColor(towerId), TierMarkerBoost(tier));
            var baseColor = BoostValue(TowerBaseColor(towerId), 1.08f);
            var ownerColor = OwnerAccent(ownerId);
            var rangeColor = DimValue(roleColor, 0.7f);

            if (visualProfile == null || visualProfile.Prefab == null)
            {
                SetColor(towerObject, TowerRoleColor(towerId, ownerId));
                return;
            }

            SetProfileColor(towerObject, visualProfile.BodyRendererPath, baseColor);
            SetProfileColor(towerObject, visualProfile.RoleMarkerRendererPath, roleColor);
            SetProfileColor(towerObject, visualProfile.OwnerTrimRendererPath, AccentPoolColor(ownerColor));
            SetProfileColor(towerObject, visualProfile.RangeHaloRendererPath, rangeColor);
        }

        private static void ConfigureTowerRoleMarker(GameObject towerObject, string towerId, int ownerId)
        {
            var roleColor = TowerMarkerColor(towerId);
            var baseColor = TowerBaseColor(towerId);
            var ownerColor = OwnerAccent(ownerId);
            var isControl = IsControlTower(towerId);
            var isRelay = IsRelayTower(towerId);
            var isPulse = IsPulseTower(towerId);
            var isPrism = IsPrismTower(towerId);
            var isFocused = !isControl && !isRelay && !isPulse && !isPrism;

            var basePlate = EnsureChild(towerObject, "RoleBasePlate", PrimitiveType.Cylinder);
            var ownerTrim = EnsureChild(towerObject, "OwnerTrim", PrimitiveType.Cylinder);
            var ownerPylon = EnsureChild(towerObject, "OwnerPylon", PrimitiveType.Cube);
            var ownerPennant = EnsureChild(towerObject, "OwnerPennant", PrimitiveType.Cube);
            var rangeHalo = EnsureChild(towerObject, "RangeReadHalo", PrimitiveType.Cylinder);

            ConfigureChild(basePlate, true, new Vector3(0f, -0.28f, 0f), TowerBaseScale(towerId), baseColor);
            ConfigureChild(ownerTrim, true, new Vector3(0f, -0.19f, 0f), TowerOwnerTrimScale(towerId), ownerColor);
            ConfigureChild(ownerPylon, true, new Vector3(-0.42f, 0.18f, -0.42f), new Vector3(0.08f, 0.52f, 0.08f), ownerColor);
            ConfigureChild(ownerPennant, true, new Vector3(-0.24f, 0.44f, -0.42f), new Vector3(0.34f, 0.12f, 0.06f), ownerColor);
            ConfigureChild(rangeHalo, true, new Vector3(0f, -0.22f, 0f), TowerRangeHaloScale(towerId), roleColor);

            var focusedBowLeft = EnsureChild(towerObject, "FocusedBowLeft", PrimitiveType.Cube);
            var focusedBowRight = EnsureChild(towerObject, "FocusedBowRight", PrimitiveType.Cube);
            var focusedSpire = EnsureChild(towerObject, "FocusedSpire", PrimitiveType.Cube);
            var focusedString = EnsureChild(towerObject, "FocusedString", PrimitiveType.Cube);
            var focusedLens = EnsureChild(towerObject, "FocusedLens", PrimitiveType.Sphere);
            var focusedArrowHead = EnsureChild(towerObject, "FocusedArrowHead", PrimitiveType.Cube);

            ConfigureChild(focusedBowLeft, isFocused, new Vector3(-0.28f, 0.58f, 0f), new Vector3(0.1f, 0.82f, 0.14f), baseColor);
            ConfigureChild(focusedBowRight, isFocused, new Vector3(0.28f, 0.58f, 0f), new Vector3(0.1f, 0.82f, 0.14f), baseColor);
            ConfigureChild(focusedSpire, isFocused, new Vector3(0f, 0.66f, -0.02f), new Vector3(0.14f, 1.08f, 0.14f), roleColor);
            ConfigureChild(focusedString, isFocused, new Vector3(0f, 0.82f, 0.34f), new Vector3(0.08f, 0.72f, 0.08f), MintSignal);
            ConfigureChild(focusedLens, isFocused, new Vector3(0f, 1.18f, 0.08f), new Vector3(0.24f, 0.24f, 0.34f), MintSignal);
            ConfigureChild(focusedArrowHead, isFocused, new Vector3(0f, 1.34f, 0.08f), new Vector3(0.34f, 0.18f, 0.18f), roleColor);

            var controlRing = EnsureChild(towerObject, "ControlRing", PrimitiveType.Cylinder);
            var controlDish = EnsureChild(towerObject, "ControlDish", PrimitiveType.Cylinder);
            var controlCore = EnsureChild(towerObject, "ControlCore", PrimitiveType.Sphere);
            var controlNorthArc = EnsureChild(towerObject, "ControlNorthArc", PrimitiveType.Cube);
            var controlSouthArc = EnsureChild(towerObject, "ControlSouthArc", PrimitiveType.Cube);
            var controlNodeA = EnsureChild(towerObject, "ControlNodeA", PrimitiveType.Sphere);
            var controlNodeB = EnsureChild(towerObject, "ControlNodeB", PrimitiveType.Sphere);
            var controlNodeC = EnsureChild(towerObject, "ControlNodeC", PrimitiveType.Sphere);

            ConfigureChild(controlRing, isControl, new Vector3(0f, 0.24f, 0f), new Vector3(1.42f, 0.04f, 1.42f), roleColor);
            ConfigureChild(controlDish, isControl, new Vector3(0f, 0.58f, 0f), new Vector3(1.1f, 0.05f, 1.1f), baseColor);
            ConfigureChild(controlCore, isControl, new Vector3(0f, 0.78f, 0f), new Vector3(0.34f, 0.34f, 0.34f), roleColor);
            ConfigureChild(controlNorthArc, isControl, new Vector3(0f, 0.78f, 0.54f), new Vector3(0.82f, 0.08f, 0.12f), roleColor);
            ConfigureChild(controlSouthArc, isControl, new Vector3(0f, 0.78f, -0.54f), new Vector3(0.82f, 0.08f, 0.12f), roleColor);
            ConfigureChild(controlNodeA, isControl, new Vector3(0f, 0.88f, 0.52f), new Vector3(0.18f, 0.18f, 0.18f), MintSignal);
            ConfigureChild(controlNodeB, isControl, new Vector3(-0.46f, 0.82f, -0.28f), new Vector3(0.16f, 0.16f, 0.16f), MintSignal);
            ConfigureChild(controlNodeC, isControl, new Vector3(0.46f, 0.82f, -0.28f), new Vector3(0.16f, 0.16f, 0.16f), MintSignal);

            var relayMast = EnsureChild(towerObject, "RelayMast", PrimitiveType.Cube);
            var relayCore = EnsureChild(towerObject, "RelayCore", PrimitiveType.Sphere);
            var relayCapacitorLeft = EnsureChild(towerObject, "RelayCapacitorLeft", PrimitiveType.Cube);
            var relayCapacitorRight = EnsureChild(towerObject, "RelayCapacitorRight", PrimitiveType.Cube);
            var relaySignalTop = EnsureChild(towerObject, "RelaySignalTop", PrimitiveType.Cylinder);
            var relayLowerSignal = EnsureChild(towerObject, "RelayLowerSignal", PrimitiveType.Cylinder);
            var relaySignalBeam = EnsureChild(towerObject, "RelaySignalBeam", PrimitiveType.Cube);

            ConfigureChild(relayMast, isRelay, new Vector3(0f, 0.76f, 0f), new Vector3(0.1f, 1.12f, 0.1f), SignalGold);
            ConfigureChild(relayCore, isRelay, new Vector3(0f, 1.18f, 0f), new Vector3(0.3f, 0.3f, 0.3f), SignalGold);
            ConfigureChild(relayCapacitorLeft, isRelay, new Vector3(-0.28f, 0.5f, 0f), new Vector3(0.12f, 0.56f, 0.12f), baseColor);
            ConfigureChild(relayCapacitorRight, isRelay, new Vector3(0.28f, 0.5f, 0f), new Vector3(0.12f, 0.56f, 0.12f), baseColor);
            ConfigureChild(relaySignalTop, isRelay, new Vector3(0f, 1.54f, 0f), new Vector3(0.56f, 0.04f, 0.56f), MintSignal);
            ConfigureChild(relayLowerSignal, isRelay, new Vector3(0f, 1.34f, 0f), new Vector3(0.38f, 0.035f, 0.38f), SignalGold);
            ConfigureChild(relaySignalBeam, isRelay, new Vector3(0f, 1.44f, 0f), new Vector3(0.06f, 0.4f, 0.06f), MintSignal);

            var pulseCore = EnsureChild(towerObject, "PulseCore", PrimitiveType.Sphere);
            var pulseRingA = EnsureChild(towerObject, "PulseRingA", PrimitiveType.Cylinder);
            var pulseRingB = EnsureChild(towerObject, "PulseRingB", PrimitiveType.Cylinder);
            var pulseArcNorth = EnsureChild(towerObject, "PulseArcNorth", PrimitiveType.Cube);
            var pulseArcSouth = EnsureChild(towerObject, "PulseArcSouth", PrimitiveType.Cube);

            ConfigureChild(pulseCore, isPulse, new Vector3(0f, 0.58f, 0f), new Vector3(0.44f, 0.44f, 0.44f), roleColor);
            ConfigureChild(pulseRingA, isPulse, new Vector3(0f, 0.34f, 0f), new Vector3(1.26f, 0.04f, 1.26f), roleColor);
            ConfigureChild(pulseRingB, isPulse, new Vector3(0f, 0.86f, 0f), new Vector3(0.92f, 0.035f, 0.92f), MintSignal);
            ConfigureChild(pulseArcNorth, isPulse, new Vector3(0f, 0.72f, 0.48f), new Vector3(0.78f, 0.08f, 0.12f), roleColor);
            ConfigureChild(pulseArcSouth, isPulse, new Vector3(0f, 0.72f, -0.48f), new Vector3(0.78f, 0.08f, 0.12f), roleColor);

            var prismSpire = EnsureChild(towerObject, "PrismSpire", PrimitiveType.Cube);
            var prismLens = EnsureChild(towerObject, "PrismLens", PrimitiveType.Sphere);
            var prismBeam = EnsureChild(towerObject, "PrismBeamRead", PrimitiveType.Cube);
            var prismLeftFacet = EnsureChild(towerObject, "PrismLeftFacet", PrimitiveType.Cube);
            var prismRightFacet = EnsureChild(towerObject, "PrismRightFacet", PrimitiveType.Cube);

            ConfigureChild(prismSpire, isPrism, new Vector3(0f, 0.82f, 0f), new Vector3(0.2f, 1.34f, 0.2f), roleColor);
            ConfigureChild(prismLens, isPrism, new Vector3(0f, 1.52f, 0.02f), new Vector3(0.36f, 0.24f, 0.36f), MintSignal);
            ConfigureChild(prismBeam, isPrism, new Vector3(0f, 1.08f, 0.42f), new Vector3(0.06f, 0.82f, 0.06f), MintSignal);
            ConfigureChild(prismLeftFacet, isPrism, new Vector3(-0.22f, 0.66f, 0f), new Vector3(0.08f, 0.82f, 0.12f), baseColor);
            ConfigureChild(prismRightFacet, isPrism, new Vector3(0.22f, 0.66f, 0f), new Vector3(0.08f, 0.82f, 0.12f), baseColor);
        }

        /// <summary>The three buildable tower lines, as the weapon effects see them.</summary>
        private enum TowerLine
        {
            Arcane,
            Foundry,
            Grove
        }

        /// <summary>How one line's shots are drawn. See <see cref="StyleFor"/>.</summary>
        private readonly struct WeaponStyle
        {
            public WeaponStyle(float width, float intensity, float duration)
            {
                Width = width;
                Intensity = intensity;
                Duration = duration;
            }

            public float Width { get; }

            public float Intensity { get; }

            public float Duration { get; }

            /// <summary>Same shot, drawn heavier. Duration deliberately does not scale — a shot
            /// that lingers longer at higher tier would drift out of step with the fire rate.</summary>
            public WeaponStyle Scaled(float width, float intensity) =>
                new WeaponStyle(Width * width, Intensity * intensity, Duration);
        }

        private static bool IsControlTower(string towerId) => ContainsRole(towerId, "slow") || ContainsRole(towerId, "splash") || ContainsRole(towerId, "control") || ContainsRole(towerId, "area");

        private static bool IsArrowTower(string towerId) => ContainsRole(towerId, "arrow") || ContainsRole(towerId, "basic");

        private static bool IsRelayTower(string towerId) => ContainsRole(towerId, "economy") || ContainsRole(towerId, "utility") || ContainsRole(towerId, "relay");

        private static bool IsPulseTower(string towerId) => ContainsRole(towerId, "pulse");

        private static bool IsPrismTower(string towerId) => ContainsRole(towerId, "prism");

        private static Vector3 TowerOwnerTrimScale(string towerId)
        {
            if (IsControlTower(towerId) || IsPulseTower(towerId))
            {
                return new Vector3(1.28f, 0.035f, 1.28f);
            }

            if (IsPrismTower(towerId))
            {
                return new Vector3(0.78f, 0.035f, 0.78f);
            }

            if (IsRelayTower(towerId))
            {
                return new Vector3(0.92f, 0.035f, 0.92f);
            }

            return new Vector3(0.84f, 0.035f, 0.84f);
        }

        private static Vector3 TowerBaseScale(string towerId)
        {
            if (IsControlTower(towerId) || IsPulseTower(towerId))
            {
                return new Vector3(1.18f, 0.055f, 1.18f);
            }

            if (IsPrismTower(towerId))
            {
                return new Vector3(0.62f, 0.06f, 0.62f);
            }

            if (IsRelayTower(towerId))
            {
                return new Vector3(0.78f, 0.06f, 0.78f);
            }

            return new Vector3(0.72f, 0.06f, 0.72f);
        }

        private static Vector3 TowerRangeHaloScale(string towerId)
        {
            if (IsRelayTower(towerId))
            {
                return new Vector3(1.45f, 0.018f, 1.45f);
            }

            if (IsPulseTower(towerId))
            {
                return new Vector3(1.72f, 0.018f, 1.72f);
            }

            if (IsPrismTower(towerId))
            {
                return new Vector3(2.25f, 0.018f, 2.25f);
            }

            return new Vector3(1.88f, 0.018f, 1.88f);
        }

        private static Color TowerBaseColor(string towerId)
        {
            var marker = TowerMarkerColor(towerId);
            return new Color(marker.r * 0.45f, marker.g * 0.45f, marker.b * 0.45f);
        }

        private static Vector3 TowerMarkerScale(string towerId)
        {
            if (IsControlTower(towerId) || IsPulseTower(towerId))
            {
                return new Vector3(0.8f, 0.08f, 0.8f);
            }

            if (IsRelayTower(towerId))
            {
                return new Vector3(0.34f, 0.34f, 0.34f);
            }

            if (IsPrismTower(towerId))
            {
                return new Vector3(0.18f, 0.36f, 0.18f);
            }

            return new Vector3(0.22f, 0.22f, 0.22f);
        }

        /// <summary>
        /// The identity colour for a tower role. Everything else derives from it:
        /// <see cref="TowerBaseColor"/> dims it, and the 3D body materials carry the matching
        /// emission tint, so marker, base and glow all agree.
        /// </summary>
        /// <remarks>
        /// Hues are deliberately spread. The previous mapping returned the same pale blue for
        /// control and for prism, and placed relay within roughly fifteen degrees of pulse, so two
        /// pairs of roles were effectively indistinguishable by colour. Roles are matched through
        /// the explicit predicates rather than loose substring tests, which is what let prism fall
        /// through to control's branch.
        /// </remarks>
        private static Color TowerMarkerColor(string towerId) => TowerCatalog.ForContentId(towerId).Accent;

        /// <summary>
        /// Which of the three tower lines a content id belongs to, for weapon styling.
        /// </summary>
        /// <remarks>
        /// Reads <see cref="TowerCatalog"/> rather than restating the grouping, so a tower moved
        /// between lines cannot end up firing one line's weapon from another line's card. Inherits
        /// that catalog's Arrow fallback, which lands on Arcane — the same arm the shot styling used
        /// to take for every unrecognised tower anyway.
        /// </remarks>
        private static TowerLine LineFor(string towerId) => TowerCatalog.ForContentId(towerId).Category switch
        {
            TowerCatalog.CategoryFoundry => TowerLine.Foundry,
            TowerCatalog.CategoryGrove => TowerLine.Grove,
            _ => TowerLine.Arcane
        };

        /// <summary>
        /// The weapon grammar for a line: how wide, how bright and how long its shots draw.
        /// </summary>
        /// <remarks>
        /// Before this, every tower outside the original five fired the same beam at the same width
        /// for the same duration, in one of two colours picked purely by whether damage reached 5 —
        /// so a GROVE spore bloom and a FOUNDRY gatling fired identical blue shots. The three lines
        /// have distinct names, models and card accents, and the weapons ignored all of it.
        ///
        /// Durations stay at or under 0.3s. GROVE is the slow, lingering one by design, but the
        /// board already carries range halos, role-marker labels and health bars, and effects that
        /// outstay that clutter have twice proved unreadable here.
        /// </remarks>
        private static WeaponStyle StyleFor(TowerLine line, int damage, int tier = 1)
        {
            var baseStyle = line switch
            {
                // Thin, cold and quick: precision energy.
                TowerLine.Arcane => new WeaponStyle(0.11f, 1.75f, damage >= 5 ? 0.13f : 0.1f),
                // Heavier and hotter, and it hangs a moment longer: machinery throwing ordnance.
                TowerLine.Foundry => new WeaponStyle(0.17f, 1.35f, damage >= 5 ? 0.16f : 0.13f),
                // Thick, soft and slow: something living reaching out.
                _ => new WeaponStyle(0.26f, 1.0f, damage >= 5 ? 0.3f : 0.24f)
            };

            return baseStyle.Scaled(TierWidthBoost(tier), TierIntensityBoost(tier));
        }

        /// <summary>
        /// How much wider a shot draws at each tier.
        /// </summary>
        /// <remarks>
        /// An upgrade should be something you can see happen, not something you read off a panel.
        /// The tower's own marker colour was carrying that job badly — a measured RGB delta of only
        /// 0.136 between tiers 2 and 3, against 0.392 from 1 to 2 — so the weapon carries it too.
        /// Width is the stronger of the two cues here; intensity alone saturates and stops reading.
        /// </remarks>
        private static float TierWidthBoost(int tier) => tier switch
        {
            >= 3 => 1.5f,
            2 => 1.22f,
            _ => 1f
        };

        private static float TierIntensityBoost(int tier) => tier switch
        {
            >= 3 => 1.35f,
            2 => 1.16f,
            _ => 1f
        };

        private static Color LineShotColor(TowerLine line, int damage) => line switch
        {
            TowerLine.Foundry => damage >= 5 ? new Color(1f, 0.5f, 0.16f) : new Color(1f, 0.62f, 0.26f),
            TowerLine.Grove => damage >= 5 ? new Color(0.44f, 0.9f, 0.34f) : new Color(0.58f, 0.88f, 0.46f),
            _ => damage >= 5 ? SignalGold : ArcaneBlue
        };

        private static Color TowerShotColor(string towerId, int damage)
        {
            if (IsControlTower(towerId) || IsPrismTower(towerId))
            {
                return new Color(0.72f, 0.94f, 1f);
            }

            if (IsPulseTower(towerId))
            {
                return new Color(1f, 0.7f, 0.28f);
            }

            if (IsRelayTower(towerId))
            {
                return damage >= 5 ? SignalGold : MintSignal;
            }

            // Everything else is keyed to its line. The five above keep their own colours, which are
            // already tuned and are all Arcane anyway.
            return LineShotColor(LineFor(towerId), damage);
        }

        private readonly struct TowerMotion
        {
            public TowerMotion(Vector3 positionOffset, float pitchDegrees, float scalePulse)
            {
                PositionOffset = positionOffset;
                PitchDegrees = pitchDegrees;
                ScalePulse = scalePulse;
            }

            public Vector3 PositionOffset { get; }
            public float PitchDegrees { get; }
            public float ScalePulse { get; }
        }
    }
}
