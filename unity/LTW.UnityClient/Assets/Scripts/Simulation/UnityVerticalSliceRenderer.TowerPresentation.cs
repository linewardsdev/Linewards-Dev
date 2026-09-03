using System;
using System.Collections.Generic;
using LTW.Simulation.Combat;
using LTW.Simulation.Events;
using LTW.Simulation.Primitives;
using LTW.UnityClient.UI;
using UnityEngine;
using UnityEngine.Rendering;

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

        /// <summary>
        /// World Y a real tower prefab's root is placed at, before its profile's own lift.
        /// </summary>
        /// <remarks>
        /// Exposed for the placement ghost, which is a copy of the same prefab and has to stand on
        /// the same floor: it used to sit at GridToWorld's 0.6, so the preview floated well above
        /// the spot the tower then landed on.
        /// </remarks>
        internal static float PlacedTowerBaseY => BoardTopY + TowerBaseClearance;

        private static void SetTowerTransform(GameObject instance, GridPosition position, LaneId laneId, string towerId, TowerVisualProfile visualProfile)
        {
            var hasRealPrefab = visualProfile != null && visualProfile.Prefab != null;
            var lift = hasRealPrefab ? visualProfile.Lift : TowerRoleLift(towerId);
            var basePosition = GridToWorld(position, laneId);
            var baseY = hasRealPrefab ? BoardTopY + TowerBaseClearance : basePosition.y;
            instance.transform.position = new Vector3(basePosition.x, baseY + lift + TowerRootSink(towerId), basePosition.z);
            instance.transform.localScale = visualProfile != null && visualProfile.HasScale ? visualProfile.Scale : TowerRoleScale(towerId);
            instance.transform.rotation = Quaternion.identity;

            if (hasRealPrefab)
            {
                ApplyTowerBodyShadowPolicy(instance);
            }
        }

        /// <summary>
        /// Finding #11 (2026-09-01 render review): Elder Canopy and Bloomheart show their roots
        /// hovering above the tile. Both meshes come through the shared Grove import (unsplit,
        /// same as every other non-turret role — see the "Foundry and Grove lines" specs in
        /// Tower3DProofSetGenerator), which normalises every source mesh to the same rest pose;
        /// these two just happen to have been authored/baked with their root slightly above their
        /// own pivot. Nudging every tower's shared placement math to fix two roles would be wrong
        /// for the other thirteen, so this is a tiny per-content-id lookup instead. A real fix
        /// belongs in the mesh's own rest pivot if these prefabs get touched again; this is a
        /// targeted, code-only sink in the meantime.
        /// </summary>
        private static float TowerRootSink(string towerId) => towerId switch
        {
            "tower.elder_canopy" => -0.06f,
            "tower.bloomheart" => -0.07f,
            _ => 0f
        };

        /// <summary>
        /// Finding #11 (2026-09-01 render review): live towers never cast a shadow. Root cause is
        /// Tower3DImportPipeline's NormalizeRendererPolicy/ApplyRuntimeMaterial, which force
        /// shadowCastingMode Off / receiveShadows false on EVERY MeshRenderer under the imported
        /// hierarchy at wrapper-generation time — a blanket policy that is correct for the flat
        /// cosmetic accessories (RangeHalo/OwnerTrim/RoleMarker, which really do look wrong
        /// casting a shadow from a thin ring or plate) but also caught Body's own visible mesh.
        /// Confirmed directly against the shipped prefabs (e.g. Tower_Gatling_3D.prefab): the
        /// renderers actually used at normal camera distance — Body/Imported3DVisual/.../Base and
        /// Body/HeadPivot/Head+Barrel — are baked with both flags off.
        ///
        /// This is fixed here at runtime rather than in the import pipeline: regenerating a
        /// wrapper from its raw FBX is exactly the operation GenerateWrapperIfRawExists' own
        /// remarks warn is destructive post-hoc — these prefabs have since had an LODGroup and
        /// hand-bound LOD1/LOD2 renderers added by a later pass the generator knows nothing about,
        /// and an earlier blanket regeneration already silently stripped that LODGroup from all
        /// fifteen towers once. Re-running it again without Unity available to verify the result
        /// is a worse trade than a two-line runtime override. RoleMarker/OwnerTrim/RangeHalo are
        /// root-level siblings of Body, not descendants of it (see
        /// Tower3DImportPipeline.GenerateWrapperIfRawExists), so restricting this to renderers
        /// under Body cannot reach them — the accessories keep their existing shadowless look.
        /// </summary>
        private static void ApplyTowerBodyShadowPolicy(GameObject towerObject)
        {
            var body = towerObject.transform.Find("Body");
            if (body == null)
            {
                return;
            }

            var renderers = body.GetComponentsInChildren<MeshRenderer>(true);
            for (var index = 0; index < renderers.Length; index++)
            {
                renderers[index].shadowCastingMode = ShadowCastingMode.On;
                renderers[index].receiveShadows = true;
            }
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
            var parts = ResolveTowerMotionParts(key, towerObject, visualProfile);
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

            if (IsTeslaTower(visualProfile.TowerId))
            {
                UpdateTeslaSteam(key, body);
            }
        }

        // Finding #16 (2026-09-01 render review): "two puff sprites fixed to [Tesla's] flanks
        // that never animate". There is no such sprite anywhere in this project — grepped for
        // Steam/Puff/Vent/Smoke across Assets and read Tesla's whole import path
        // (Tower3DProofSetGenerator's "tower.tesla" spec, Tower3DImportPipeline's anchor
        // creation, Tower_Tesla_3D.prefab's own node list) end to end; the prefab carries only
        // Muzzle/Lens anchors, LODs and the body mesh. Wave 3's own tracking
        // (docs/OPEN_ITEMS.md item 51) already names the actual fix instead of a sprite to
        // animate: "route through the Rise burst on a 0.6 s timer" — the same pooled
        // BurstShape.Rise puff SpawnEffect already uses for a tower coming online or a lane
        // banking income (UnityVerticalSliceRenderer.cs). Two Rise puffs, one per flank of the
        // coil, on a steady interval read as venting steam without needing any new sprite,
        // any new pooled mesh, or a change to Effects.cs.
        private const float TeslaSteamIntervalSeconds = 0.6f;
        private const float TeslaSteamFlankOffset = 0.18f;
        private const float TeslaSteamScale = 0.22f;
        private const float TeslaSteamDuration = 0.32f;
        private static readonly Color TeslaSteamColor = new Color(0.83f, 0.9f, 0.96f, 0.42f);
        private readonly Dictionary<long, float> towerNextSteamAt = new Dictionary<long, float>();

        private void UpdateTeslaSteam(long key, Transform body)
        {
            if (!towerNextSteamAt.TryGetValue(key, out var nextAt))
            {
                // Staggers each Tesla's cycle by a small, deterministic-per-instance amount off
                // the raw entity id so several coils on the board don't all vent in lockstep.
                nextAt = Time.time + (key % 7) * (TeslaSteamIntervalSeconds / 7f);
            }

            if (Time.time < nextAt)
            {
                return;
            }

            towerNextSteamAt[key] = Time.time + TeslaSteamIntervalSeconds;

            // "Lens" is the coil's actual glowing tip mesh (see Tower3DImportPipeline's anchor
            // resolution: the Lens/ControlCore/PulseCore family all sit at spec.CorePosition) —
            // falling back to Body itself keeps this harmless if a future Tesla rebuild renames
            // or removes that mesh.
            var coil = FindDeepChild(body, "Lens") ?? body;
            var origin = coil.position + Vector3.up * 0.1f;
            var flank = body.right * TeslaSteamFlankOffset;
            SpawnEffect(origin + flank, TeslaSteamColor, TeslaSteamScale, TeslaSteamDuration, BurstShape.Rise);
            SpawnEffect(origin - flank, TeslaSteamColor, TeslaSteamScale, TeslaSteamDuration, BurstShape.Rise);
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

        private TowerMotionParts ResolveTowerMotionParts(long key, GameObject towerObject, TowerVisualProfile visualProfile = null)
        {
            if (towerMotionParts.TryGetValue(key, out var cached) && cached.Instance == towerObject && cached.Body != null)
            {
                return cached;
            }

            var body = ResolveTowerMotionTarget(towerObject);

            // visualProfile is only null from ResolveTowerBodyTransform's attack-VFX call site,
            // which can only run for a tower RenderSnapshot's UpdateTowerMotion loop already
            // resolved earlier this same frame (RenderSnapshot runs before RenderEvents in
            // Render()) — so the cache-hit branch above always wins there in practice, and this
            // guard exists only so a null profile can never NRE on the pulse-tower check.
            if (visualProfile != null && IsPulseTower(visualProfile.TowerId))
            {
                ApplyPulseRangeRingFix(body);
            }

            var parts = new TowerMotionParts(
                towerObject,
                body,
                FindDeepChild(body, "HeadPivot"),
                FindDeepChild(body, "Barrel"),
                FindSpinPart(body));
            towerMotionParts[key] = parts;
            return parts;
        }

        /// <summary>
        /// Finding #15 (2026-09-01 render review): Pulse's "Ring" mesh — the same accessory the
        /// comment above (in <see cref="UpdateTowerMotion"/>) already calls out as the one part of
        /// Pulse that spins, via the shared Ring/Dish/Spire sweep in
        /// <see cref="TowerVisualLibrary.SpinPartNames"/> — is real modelled geometry with real
        /// thickness (confirmed directly in Tower_Pulse_3D.prefab: a MeshFilter/MeshRenderer
        /// pair, not a code-generated primitive; PulseRingA/PulseRingB are anchor empties with no
        /// renderer at all, see Tower3DImportPipeline.ResolveAnchorPosition). As it sweeps about
        /// world-up against the board's 30-degree camera tilt, it repeatedly passes through
        /// near-edge-on phases where a thin 3D band aliases into a scratchy, hand-drawn-looking
        /// line even with SMAA — the same class of prefab-vs-runtime tradeoff
        /// <see cref="ApplyTowerBodyShadowPolicy"/> above documents: the correct fix is in the
        /// import pipeline (author it as a flat decal instead of a modelled band), but
        /// regenerating Tower_Pulse_3D's wrapper is exactly the destructive, unverifiable
        /// operation GenerateWrapperIfRawExists' own remarks warn against with no interactive
        /// Unity session available this pass.
        ///
        /// So this disables the aliasing mesh and grows a flat decal in its place instead. A
        /// decal lying flat in the XZ plane never has an edge-on phase to alias regardless of
        /// camera tilt or spin, which a thin vertical band fundamentally cannot avoid. Reuses
        /// BoardRenderResources.ContactShadowMesh/CreateContactShadowMaterial — the exact
        /// infrastructure the standing mechanic markers (Thorn's braked cells, Grovebond's bond
        /// ring, via Effects.cs's MechanicDecalMaterial) already use for "a stencilled ring that
        /// must never present an edge" — at MechanicDecalSoftness's sharp ~2px edge (matching
        /// OPEN_ITEMS.md item 51's "pooled shockwave-ring decal at 2 px") rather than the softer,
        /// glowier ShockwaveRingMaterial the transient burst effects use, since this is a
        /// standing accessory, not a flash of light.
        ///
        /// Runs once per pooled instance (from ResolveTowerMotionParts' cache-miss branch, same
        /// as HeadPivot/Barrel/SpinPart above), guarded by the decal's own presence so a re-entry
        /// after a pool swap is a no-op rather than a duplicate.
        /// </summary>
        private void ApplyPulseRangeRingFix(Transform body)
        {
            if (body.Find(PulseRangeRingDecalName) != null)
            {
                return;
            }

            Bounds? ringBounds = null;
            var renderers = body.GetComponentsInChildren<MeshRenderer>(true);
            for (var index = 0; index < renderers.Length; index++)
            {
                var candidate = renderers[index];
                if (candidate.gameObject.name != "Ring")
                {
                    continue;
                }

                ringBounds ??= candidate.bounds;
                candidate.enabled = false;
            }

            var decal = new GameObject(PulseRangeRingDecalName);
            decal.transform.SetParent(body, false);

            if (ringBounds.HasValue)
            {
                var bounds = ringBounds.Value;
                decal.transform.position = bounds.center;
                var bodyScale = Mathf.Max(0.0001f, body.lossyScale.x);
                var diameter = Mathf.Max(bounds.size.x, bounds.size.z) / bodyScale;
                decal.transform.localScale = new Vector3(diameter, 1f, diameter);
            }
            else
            {
                // No "Ring" mesh found (a future rebuild renamed or removed it) — fall back to the
                // procedural fallback's own PulseRingA placement/scale (ConfigureTowerRoleMarker
                // above) so this still reads as a range ring rather than nothing.
                decal.transform.localPosition = new Vector3(0f, 0.34f, 0f);
                decal.transform.localScale = new Vector3(1.26f, 1f, 1.26f);
            }

            var filter = decal.AddComponent<MeshFilter>();
            filter.sharedMesh = BoardRenderResources.ContactShadowMesh;
            var renderer = decal.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = MechanicDecalMaterial();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            SetColor(decal, DimValue(TowerMarkerColor("tower.pulse"), 0.7f));
        }

        private const string PulseRangeRingDecalName = "PulseRangeRingDecal";

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
                state = new SpinPartState(LongestLocalAxis(barrel), barrel.localRotation, barrel.localPosition, LocalBoundsCentre(barrel));
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
            var spin = Quaternion.AngleAxis(angle, state.LocalSpinAxis) * state.RestLocalRotation;
            barrel.localRotation = spin;

            // localRotation turns the mesh about the transform's ORIGIN, which is only the bore if the
            // export put it there — a Meshy part keeps the whole model's origin unless it was re-split
            // with --center-origin, and the barrel orbited a point outside itself when it was not
            // (render review finding #2, 2026-09-01). So the origin is moved each frame by exactly
            // what the spin displaced the bounds centre by, which holds that centre fixed in HeadPivot
            // space whatever the pivot is. A pivot already on the centre makes this a no-op.
            barrel.localPosition = state.RestLocalPosition
                + state.RestLocalRotation * state.LocalCentre
                - spin * state.LocalCentre;
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

        /// <summary>
        /// The part's mesh-bounds centre in its own space, pre-scaled so that
        /// <c>localRotation * centre</c> is the parent-space offset from the part's origin. Found
        /// the same way as <see cref="LongestLocalAxis"/>, so both describe the same mesh.
        /// </summary>
        private static Vector3 LocalBoundsCentre(Transform part)
        {
            var filter = part.GetComponentInChildren<MeshFilter>();
            if (filter == null || filter.sharedMesh == null)
            {
                return Vector3.zero;
            }

            var world = filter.transform.TransformPoint(filter.sharedMesh.bounds.center);
            return Vector3.Scale(part.InverseTransformPoint(world), part.localScale);
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
                : this(localSpinAxis, restLocalRotation, Vector3.zero, Vector3.zero)
            {
            }

            public SpinPartState(Vector3 localSpinAxis, Quaternion restLocalRotation, Vector3 restLocalPosition, Vector3 localCentre)
            {
                LocalSpinAxis = localSpinAxis;
                RestLocalRotation = restLocalRotation;
                RestLocalPosition = restLocalPosition;
                LocalCentre = localCentre;
            }

            public Vector3 LocalSpinAxis { get; }
            public Quaternion RestLocalRotation { get; }

            /// <summary>Where the part sat before any spin, in its parent's space.</summary>
            public Vector3 RestLocalPosition { get; }

            /// <summary>
            /// The part's bounds centre as <see cref="LocalBoundsCentre"/> reads it. Zero for the
            /// Ring/Dish/Spire path, whose pivots already sit on the spin axis.
            /// </summary>
            public Vector3 LocalCentre { get; }
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
                // heading; recoil at 1.0 because Twin Volley emits two TowerFiredEvents per
                // cooldown and the double kick is the mechanic's read. Amplitudes are far below
                // Arrow's because px-per-amplitude is a property of the MESH — the upper crescent
                // arm sits high and off the yaw axis (Pulse's flat-dome trap, inverted). Probed:
                // Arrow-family values read 16.6/8.8px against Arrow's 2.65/3.64; these read
                // 11.8/7.3, and the two runs bracket the split — idle now contributes only a
                // couple of px, the rest is aim yaw and the double recoil swinging the arm's
                // lever through the bounds centre. That share is the mechanic moving, not idle
                // noise, and no idle knob here can lower it; judge it on a device before
                // touching recoilScale.
                case TowerVisualRole.TwinCrescent:
                    return new TowerMotionProfile(1.2f, 0.018f, driftHz: 0.8f, driftAmp: 0.005f, restHeadingDegrees: 90f, recoilScale: 1.0f);

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

            // Breathe is GROVE's alone (2026-08-09, reported as "the breathing effect on all the
            // towers is still too much"). A scale pulse reads as something alive drawing breath,
            // which is the Grove line's whole identity — saplings, canopies, blooms — and reads as
            // wobble on a stone bastion or a machine that should sit dead still between shots.
            // Every line having it is what made it look like an engine artefact rather than a
            // characteristic: when everything breathes, nothing is breathing.
            //
            // Asked of the catalog rather than matched against a hardcoded list of roles, so a
            // tower that changes line takes its breathe with it instead of keeping a property its
            // new line does not have. ForRole falls back to Arrow, which is Arcane, so an unmapped
            // role gets no breathe — the safe direction for a suppression.
            //
            // Amplitude only. BreatheHz, Sharpness and every other authored value is untouched, so
            // restoring a line is one condition rather than a re-tuning pass, and Grove's own
            // measured figures still hold.
            var breathes = TowerCatalog.ForRole((int)role).Category == TowerCatalog.CategoryGrove;
            var breatheAmp = breathes ? profile.BreatheAmp : 0f;
            var wave = Mathf.Sin(time * profile.BreatheHz);
            var breathe = profile.Sharpness > 1f
                ? Mathf.Pow(Mathf.Abs(wave), profile.Sharpness) * breatheAmp
                : wave * breatheAmp;

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

        // Instance rather than static since Wave 5: the tier crown below keeps a per-pooled-instance
        // cache (towerTierCrowns) so a snapshot that changes nothing about a tower costs nothing.
        private void ApplyTowerColor(GameObject towerObject, string towerId, int ownerId, TowerVisualProfile visualProfile, int tier)
        {
            var roleColor = BoostValue(TowerMarkerColor(towerId), TierMarkerBoost(tier));
            var baseColor = BoostValue(TowerBaseColor(towerId), 1.08f);
            var ownerColor = OwnerAccent(ownerId);
            var rangeTint = DimValue(roleColor, 0.7f);
            var rangeColor = new Color(rangeTint.r, rangeTint.g, rangeTint.b, RangeHaloAlpha);

            if (visualProfile == null || visualProfile.Prefab == null)
            {
                SetColor(towerObject, TowerRoleColor(towerId, ownerId));
                return;
            }

            SetProfileColor(towerObject, visualProfile.BodyRendererPath, baseColor);
            SetProfileColor(towerObject, visualProfile.RoleMarkerRendererPath, roleColor);
            SetProfileColor(towerObject, visualProfile.OwnerTrimRendererPath, AccentPoolColor(ownerColor));
            SetProfileColor(towerObject, visualProfile.RangeHaloRendererPath, rangeColor);
            ApplyTierSilhouette(towerObject, visualProfile, tier);
            ApplyTierCrown(towerObject, ownerId, ownerColor, TowerMarkerColor(towerId), tier);
        }

        // Rest local scale of the three accessories CreateOwnerTrim/CreateRoleMarker/
        // CreateRangeHalo bake into every one of the 15 tower wrappers, unconditionally and with
        // the same values regardless of role (verified directly against several shipped prefabs
        // spanning both split-headed and unsplit roles: Gatling, Control, Relay, ElderCanopy,
        // Bloomheart all carry the exact same three scales). ApplyTierSilhouette scales relative
        // to these fixed rest values rather than the accessory's current scale, so re-applying it
        // on every snapshot (tier can only go up, but this stays correct either way) never
        // compounds.
        private static readonly Vector3 OwnerTrimRestScale = new Vector3(1.05f, 1.05f, 1f);
        private static readonly Vector3 RoleMarkerRestScale = new Vector3(0.16f, 0.08f, 0.16f);
        private static readonly Vector3 RangeHaloRestScale = new Vector3(0.92f, 0.008f, 0.92f);

        /// <summary>
        /// Alpha written onto the RangeHalo accessory's material in ApplyTowerColor, and the
        /// widest the halo may be drawn, in world units (one board cell).
        /// </summary>
        /// <remarks>
        /// R8d (re-audit 2026-09-02, OPEN_ITEMS item 53): "the tier-2 Control ward's RangeHalo
        /// reads as an opaque violet pancake under the tower." rangeColor's alpha came from
        /// TowerCatalog's Accent (1.0) through DimValue, which only scales RGB — and the halo
        /// material the wrappers bake in (mat_tower_*_3d_range_halo_v01, URP Lit, transparent,
        /// premultiplied) honours alpha, so at 1.0 it is opaque by construction. 0.25 here.
        ///
        /// Recorded because it changes what this fix can be judged on: in every one of the 16
        /// shipped wrappers the RangeHalo MeshRenderer is m_Enabled: 0
        /// (Tower3DImportPipeline.CreateRangeHalo sets renderer.enabled = false), and no runtime
        /// path enables it — SetProfileColor tints it and ApplyTierSilhouette scales it, but a
        /// disabled renderer draws nothing at either. Both changes below are therefore correct
        /// and currently invisible, and whatever the frame showed under the Control ward is a
        /// different object (the OwnerTrim pool, the Control model's own ring, or the LOD_1
        /// mesh are the candidates under the same footprint). The scale clamp still binds the
        /// moment the halo is ever switched on: rest 0.92 x 1.18 on a profile scale of 0.9
        /// (the largest shipped) is 0.98 of a cell, and any profile at or above 0.92 crosses it.
        /// </remarks>
        private const float RangeHaloAlpha = 0.25f;
        private const float RangeHaloMaxWorldDiameter = 1f;

        /// <summary>
        /// Finding #12 (2026-09-01 render review): TierMarkerBoost's colour ramp is "a hint, not a
        /// readout" per its own remarks — telling tier 2 from tier 3 apart across a busy board is
        /// hard from colour alone. This adds the structural silhouette change the review asked
        /// for, on top of (not instead of) that colour ramp: tier 2 grows the owner trim plate and
        /// the tower's one small emissive accessory (RoleMarker — built from the recipe's
        /// EnergyMaterial in Tower3DImportPipeline.CreateRoleMarker, i.e. exactly the "crystal"
        /// the review meant) by 1.1x; tier 3 grows both again and enlarges RangeHalo (the
        /// halo/base-ring accessory, whose colour already ramps via TierMarkerBoost) beyond its
        /// own tier-2 size as the "second emissive element".
        ///
        /// Driven entirely by the tower's existing TowerVisualProfile renderer-path fields — the
        /// same three names ApplyTowerColor already recolors — rather than any per-role accessory
        /// list, so it needs no per-role authoring and covers all 15 roles uniformly. A role
        /// missing one of these children is skipped, not thrown on; none currently are missing one
        /// (RoleMarker/OwnerTrim/RangeHalo are all created unconditionally per wrapper), but a
        /// future role that skipped one would just keep its tier-1 look for that accessory.
        /// </summary>
        private static void ApplyTierSilhouette(GameObject towerObject, TowerVisualProfile visualProfile, int tier)
        {
            var trimAndCoreScale = tier switch
            {
                >= 3 => 1.21f,
                2 => 1.1f,
                _ => 1f
            };
            var haloScale = tier >= 3 ? 1.18f : 1f;

            // R8d (re-audit 2026-09-02, OPEN_ITEMS item 53): the halo may never exceed the tower's
            // own footprint. Its world diameter is rest scale x tier multiplier x the root's
            // profile scale (SetTowerTransform has already applied the latter by the time
            // ApplyTowerColor calls this — see the snapshot loop), so the multiplier is clamped
            // against that product rather than against the local scale alone.
            var rootScale = Mathf.Max(0.0001f, towerObject.transform.localScale.x);
            var haloWorldDiameter = RangeHaloRestScale.x * haloScale * rootScale;
            if (haloWorldDiameter > RangeHaloMaxWorldDiameter)
            {
                haloScale *= RangeHaloMaxWorldDiameter / haloWorldDiameter;
            }

            ScaleProfileChild(towerObject, visualProfile.OwnerTrimRendererPath, OwnerTrimRestScale, trimAndCoreScale);
            ScaleProfileChild(towerObject, visualProfile.RoleMarkerRendererPath, RoleMarkerRestScale, trimAndCoreScale);
            ScaleProfileChild(towerObject, visualProfile.RangeHaloRendererPath, RangeHaloRestScale, haloScale);
        }

        private static void ScaleProfileChild(GameObject root, string rendererPath, Vector3 restScale, float multiplier)
        {
            var target = string.IsNullOrWhiteSpace(rendererPath) ? null : root.transform.Find(rendererPath);
            if (target == null)
            {
                return;
            }

            target.localScale = restScale * multiplier;
        }

        // ---- Tier crown (Wave 5, finding #12) --------------------------------------------------

        private const string TierCrownName = "TierCrown";
        private const string TierCrownStudNamePrefix = "Stud";

        /// <summary>Studs at tier 3; tier 2 draws half of them. Tier 1 draws none.</summary>
        private const int TierCrownMaxStuds = 8;

        /// <summary>
        /// Stud diameter and height in WORLD units, independent of the tower's profile scale — a
        /// 0.55-scale Barricade and a 0.9-scale Tesla get the same-sized stud, because the stud's
        /// job is to be readable at the shipped 60–90 px tower size, not to be proportional to the
        /// model. Tier 3's is a shade larger so its ring also reads heavier, not only fuller.
        /// </summary>
        // First Wave 5 capture: at 0.14/0.16 the studs were as large and as blue as the selection
        // ring and competed with it; 0.10/0.12 still reads as a tier mark at the shipped framing.
        private const float TierCrownStudWorldSizeTier2 = 0.10f;
        private const float TierCrownStudWorldSizeTier3 = 0.12f;

        /// <summary>
        /// Clamp on the measured footprint half-extent, world units. The floor stops a very narrow
        /// mesh from drawing its studs inside its own plinth; the ceiling plus half a tier-3 stud
        /// stays inside the cell (half a cell is 0.5), so a crown never crosses into a neighbour.
        /// </summary>
        private const float TierCrownMinWorldRadius = 0.18f;
        private const float TierCrownMaxWorldRadius = 0.40f;

        /// <summary>Hair above BoardTopY the stud base may never sink below, to avoid z-fighting the board.</summary>
        private const float TierCrownFloorLift = 0.005f;

        /// <summary>Value boost on the owner accent for the studs; see <see cref="BoardRenderResources.TierCrownMaterial"/>.</summary>
        private const float TierCrownAccentBoost = 1.15f;

        /// <summary>
        /// Fraction of the body material's own emission peak that the role accent is added at,
        /// per tier — the "+25% / +50%" the Wave 5 brief asked for, expressed against each
        /// material's actual HDR range rather than a fixed constant (every shipped body material
        /// peaks at 2.0, so this is accent x 0.5 at tier 2 and accent x 1.0 at tier 3 today).
        /// </summary>
        private const float TierEmissionLiftTier2 = 0.25f;
        private const float TierEmissionLiftTier3 = 0.5f;

        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
        private static MaterialPropertyBlock tierEmissionPropertyBlock;

        /// <summary>
        /// What a pooled tower instance's crown was last built for, plus its once-measured
        /// footprint. Keyed by the instance's id, not the entity's: pooling hands the same
        /// GameObject to different entities over a match, and the footprint is a property of the
        /// mesh, not the entity.
        /// </summary>
        private readonly struct TierCrownState
        {
            public TierCrownState(int tier, int ownerId, float localRadius, float localFloorY)
            {
                Tier = tier;
                OwnerId = ownerId;
                LocalRadius = localRadius;
                LocalFloorY = localFloorY;
            }

            public int Tier { get; }
            public int OwnerId { get; }

            /// <summary>Footprint half-extent in the tower root's local units (world / profile scale).</summary>
            public float LocalRadius { get; }

            /// <summary>Stud base height in the root's local units.</summary>
            public float LocalFloorY { get; }
        }

        // Keyed by the pooled instance itself rather than GetInstanceID(): Unity 6000.5 marks
        // GetInstanceID obsolete-as-error, and a reference key is exactly what "per pooled
        // instance" means anyway.
        private readonly Dictionary<GameObject, TierCrownState> towerTierCrowns = new Dictionary<GameObject, TierCrownState>();

        private static int TierCrownStudCount(int tier) => tier switch
        {
            >= 3 => TierCrownMaxStuds,
            2 => TierCrownMaxStuds / 2,
            _ => 0
        };

        /// <summary>
        /// Finding #12 (render review 2026-09-01), judged open again by Wave 4: the new
        /// `36-tier-pair-closeup` step places a tier-3 Arrow at lane 1 (2,14) beside a tier-1 Arrow
        /// at (4,14) at the same framing, and they are indistinguishable. Wave 2's
        /// <see cref="ApplyTierSilhouette"/> scales OwnerTrim/RoleMarker/RangeHalo, but RangeHalo's
        /// renderer is disabled in all 16 shipped wrappers (see <see cref="RangeHaloAlpha"/>'s
        /// remarks) and the other two are too small to read at the shipped 60–90 px tower size,
        /// so a 10–21% scale on them moves nothing a viewer can see. That scaling is kept —
        /// harmless — but the read now comes from here.
        ///
        /// THE SHAPE: a ring of raised studs on the board around the tower's base — four on the
        /// diagonals at tier 2, eight (every 45 degrees, a superset of tier 2's positions) at tier
        /// 3, in the owner accent (see <see cref="BoardRenderResources.TierCrownStudMesh"/> for
        /// why a countable ring rather than another colour ramp). Parented to the tower ROOT, not
        /// Body: Body carries idle drift, aim yaw and recoil (<see cref="UpdateTowerMotion"/>), and
        /// a crown that swung with the barrel or kicked on every shot would read as part of the
        /// weapon rather than as a rank marker; the root is only ever positioned by
        /// <see cref="SetTowerTransform"/>. It is the same "root-level accessory, sibling of Body"
        /// contract OwnerTrim/RoleMarker/RangeHalo already follow.
        ///
        /// THE SIZING RULE: ring radius = the tower's measured footprint + half a stud, so the studs
        /// sit just outside the widest point of the body mesh — measured from the renderer bounds
        /// under Body at first sight of each pooled instance, exactly as
        /// <see cref="ApplyPulseRangeRingFix"/> measures the Pulse ring, and converted into the
        /// root's local units so the ring lands at the right world radius on the 0.55-scale
        /// Barricade and the 0.9-scale Tesla alike. Stud size is fixed in WORLD units for the same
        /// reason (see <see cref="TierCrownStudWorldSizeTier2"/>). For a tower whose widest point is
        /// its canopy rather than its plinth (Elder Canopy) the ring lands under the canopy's rim,
        /// which still reads as "a ring around this tower".
        ///
        /// POOLED AND IDEMPOTENT: the crown is a fixed set of at most eight stud children under one
        /// "TierCrown" child, created once per pooled instance and then only enabled/placed, so
        /// re-applying the same tier does nothing (the per-instance cache short-circuits before
        /// any transform is touched), a tier change re-places the same studs, and a tier-1 tower
        /// hides them. Because the studs are children of the pooled instance they leave and return
        /// with it — <c>ReleaseTowerToPool</c> deactivates the whole hierarchy — so Pooling.cs
        /// needed no hook; the next <see cref="ApplyTowerColor"/> on the instance rebuilds for
        /// whatever tier and owner it now has.
        /// </summary>
        private void ApplyTierCrown(GameObject towerObject, int ownerId, Color ownerColor, Color roleAccent, int tier)
        {
            var instanceId = towerObject;
            var hasState = towerTierCrowns.TryGetValue(instanceId, out var state);
            if (hasState && state.Tier == tier && state.OwnerId == ownerId)
            {
                return;
            }

            var root = towerObject.transform;
            var body = ResolveTowerMotionTarget(towerObject);
            if (!hasState)
            {
                state = MeasureTierCrownFootprint(root, body);
            }

            towerTierCrowns[instanceId] = new TierCrownState(tier, ownerId, state.LocalRadius, state.LocalFloorY);

            var studCount = TierCrownStudCount(tier);
            var crown = root.Find(TierCrownName);
            if (crown == null)
            {
                if (studCount > 0)
                {
                    crown = new GameObject(TierCrownName).transform;
                    crown.SetParent(root, false);
                }
            }

            if (crown != null)
            {
                crown.localPosition = Vector3.zero;
                crown.localRotation = Quaternion.identity;
                crown.localScale = Vector3.one;
                crown.gameObject.SetActive(studCount > 0);

                if (studCount > 0)
                {
                    // Root scale is the profile scale SetTowerTransform applied (same read as
                    // ApplyTierSilhouette's halo clamp); world sizes divide by it to become local.
                    var rootScale = Mathf.Max(0.0001f, root.localScale.x);
                    var studLocalSize = (tier >= 3 ? TierCrownStudWorldSizeTier3 : TierCrownStudWorldSizeTier2) / rootScale;
                    var ringRadius = state.LocalRadius + studLocalSize * 0.5f;
                    var material = BoardRenderResources.TierCrownMaterial(BoostValue(ownerColor, TierCrownAccentBoost));
                    var angleStep = 360f / studCount;
                    // Tier 2 on the diagonals: two studs face the tilted camera instead of one, and
                    // the four are exactly the odd positions of tier 3's eight, so an upgrade adds
                    // studs between the existing ones rather than moving them.
                    var angleOffset = studCount == TierCrownMaxStuds ? 0f : 45f;

                    for (var index = 0; index < TierCrownMaxStuds; index++)
                    {
                        var stud = EnsureTierCrownStud(crown, index);
                        var active = index < studCount;
                        stud.gameObject.SetActive(active);
                        if (!active)
                        {
                            continue;
                        }

                        var angle = (angleOffset + index * angleStep) * Mathf.Deg2Rad;
                        stud.localPosition = new Vector3(Mathf.Cos(angle) * ringRadius, state.LocalFloorY, Mathf.Sin(angle) * ringRadius);
                        stud.localRotation = Quaternion.identity;
                        stud.localScale = Vector3.one * studLocalSize;
                        if (stud.TryGetComponent<MeshRenderer>(out var studRenderer))
                        {
                            studRenderer.sharedMaterial = material;
                        }
                    }
                }
            }

            ApplyTierEmission(body, roleAccent, tier);
        }

        /// <summary>
        /// Footprint half-extent and floor height of a tower's Body, in the root's local units.
        /// Measured against the ROOT's position, not the bounds centre, so an asymmetric mesh
        /// (Arrow's barrel side) still gets a ring centred on the tower.
        /// </summary>
        private static TierCrownState MeasureTierCrownFootprint(Transform root, Transform body)
        {
            var rootScale = Mathf.Max(0.0001f, root.localScale.x);
            var rootPosition = root.position;
            var extent = 0f;
            var lowestY = rootPosition.y;
            var measured = false;

            var renderers = body.GetComponentsInChildren<MeshRenderer>(true);
            for (var index = 0; index < renderers.Length; index++)
            {
                var candidate = renderers[index];
                // The Pulse decal is a runtime child sized to the Ring it replaces; the Ring
                // itself is still under Body (disabled, bounds intact) and is the measure.
                if (candidate.gameObject.name == PulseRangeRingDecalName)
                {
                    continue;
                }

                var bounds = candidate.bounds;
                extent = Mathf.Max(extent, Mathf.Max(
                    Mathf.Max(Mathf.Abs(bounds.min.x - rootPosition.x), Mathf.Abs(bounds.max.x - rootPosition.x)),
                    Mathf.Max(Mathf.Abs(bounds.min.z - rootPosition.z), Mathf.Abs(bounds.max.z - rootPosition.z))));
                lowestY = measured ? Mathf.Min(lowestY, bounds.min.y) : bounds.min.y;
                measured = true;
            }

            if (!measured)
            {
                // A wrapper with no mesh under Body (nothing shipped is) still gets a legible ring
                // at the narrow end of the clamp rather than no tier read at all.
                extent = TierCrownMinWorldRadius;
            }

            extent = Mathf.Clamp(extent, TierCrownMinWorldRadius, TierCrownMaxWorldRadius);

            // Studs stand on the model's own base plane (root y, where the wrapper's pivot sits)
            // or lower if the mesh extends below its pivot, but never under the board surface.
            var floorWorldY = Mathf.Max(BoardTopY + TierCrownFloorLift, Mathf.Min(rootPosition.y, lowestY));
            return new TierCrownState(0, -1, extent / rootScale, (floorWorldY - rootPosition.y) / rootScale);
        }

        private static Transform EnsureTierCrownStud(Transform crown, int index)
        {
            // Studs are created in index order and never reordered, so the child index is the id.
            if (index < crown.childCount)
            {
                return crown.GetChild(index);
            }

            var stud = new GameObject(TierCrownStudNamePrefix + index);
            stud.transform.SetParent(crown, false);
            stud.AddComponent<MeshFilter>().sharedMesh = BoardRenderResources.TierCrownStudMesh;
            var renderer = stud.AddComponent<MeshRenderer>();
            // Same renderer policy as the Pulse decal and the wrapper accessories: a thumb-sized
            // boss casting a shadow is noise, and probes are wasted on an unlit material.
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            return stud.transform;
        }

        /// <summary>
        /// The second half of the Wave 5 tier read: a tier-keyed lift on the body's own emission.
        /// </summary>
        /// <remarks>
        /// Assets/Resources/Shaders/LTWStylizedUnit.shader does expose one — <c>[HDR]
        /// _EmissionColor</c>, applied as <c>_EmissionMap * _EmissionColor</c> — and all 16
        /// shipped body materials (mat_tower_*_body_runtime_v01) bind an emission map, enable
        /// <c>_EMISSION</c> and carry an HDR colour peaking at 2.0, so lifting the colour lifts the
        /// glow the model already has (its lens, coil, core: the emission map masks it to those
        /// regions) rather than flooding the whole body. Additive on the material's OWN value,
        /// read back from <c>sharedMaterial</c> at apply time, so the tint the art pass chose is
        /// preserved and only brightened; the ROLE accent is added rather than the owner's
        /// because the body materials' emission tints already agree with that accent (see
        /// <see cref="TowerMarkerColor"/>), so the lift brightens the existing hue instead of
        /// shifting it — the owner is carried by the crown.
        ///
        /// Through a MaterialPropertyBlock, never <c>renderer.material</c>: the per-renderer
        /// override leaves every tower on the shared material. It does take that renderer out of
        /// the SRP batcher, which is why it is only written when tier or owner changes and only
        /// for tier 2/3 bodies; a tier-1 body gets an empty block, which clears any override a
        /// pooled instance inherited from a previous life at a higher tier. Renderers whose
        /// material has no emission property (the Pulse decal's contact-shadow recipe) are left
        /// alone.
        /// </remarks>
        private static void ApplyTierEmission(Transform body, Color accent, int tier)
        {
            var lift = tier switch
            {
                >= 3 => TierEmissionLiftTier3,
                2 => TierEmissionLiftTier2,
                _ => 0f
            };

            tierEmissionPropertyBlock ??= new MaterialPropertyBlock();
            var renderers = body.GetComponentsInChildren<MeshRenderer>(true);
            for (var index = 0; index < renderers.Length; index++)
            {
                var renderer = renderers[index];
                var material = renderer.sharedMaterial;
                if (material == null || !material.HasProperty(EmissionColorId))
                {
                    continue;
                }

                tierEmissionPropertyBlock.Clear();
                if (lift > 0f)
                {
                    var baseEmission = material.GetColor(EmissionColorId);
                    var peak = Mathf.Max(baseEmission.r, Mathf.Max(baseEmission.g, baseEmission.b));
                    if (peak <= 0f)
                    {
                        peak = 1f;
                    }

                    var lifted = baseEmission + accent * (lift * peak);
                    lifted.a = baseEmission.a;
                    tierEmissionPropertyBlock.SetColor(EmissionColorId, lifted);
                }

                renderer.SetPropertyBlock(tierEmissionPropertyBlock);
            }
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

        private static bool IsTeslaTower(string towerId) => ContainsRole(towerId, "tesla");

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
