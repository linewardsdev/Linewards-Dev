using UnityEngine;

namespace LTW.UnityClient.Simulation
{
    /// <summary>
    /// Static ambient underlayer behind the board: one large ground plate plus one slow-drifting
    /// ambient particle field.
    /// </summary>
    /// <remarks>
    /// 2026-09-01 live render review, finding #13 (docs/screenshot-reviews/render-review-20260901/
    /// README.md; docs/OPEN_ITEMS.md item 50): "eight board slabs floating in a flat navy clear
    /// colour, with nothing behind, beside or beneath them... no horizon, no backdrop layer, no
    /// architecture connecting the lanes, and no depth cue at all." The fix specified there is
    /// exactly what this builds: "a static underlayer at -2 units: a large dark stone plate with a
    /// subtle radial vignette, faint circuit inlays that pick up the lane accents, and a
    /// slow-drifting particle field. Cheap to render (one quad, one particle system) and it fills
    /// every framing."
    ///
    /// Deliberately independent of UnityVerticalSliceRenderer, BoardMeshBuilder and
    /// BoardRenderResources: this is a separate scenery layer, not board or unit rendering, and it
    /// must not depend on or modify anything those own. It reads their approximate world-space
    /// board footprint only as hardcoded numbers (see <see cref="PlateCenterX"/> below) because
    /// those layout constants are private to <c>UnityVerticalSliceRenderer</c>.
    ///
    /// Both the plate and the particle field are created once, here, and then left alone: no
    /// per-frame C# work, no pooling, and no dependency on the presentation camera — a world-space
    /// ground plane and a world-space particle system are both visible from any framing without
    /// needing to face or track it.
    /// </remarks>
    public static class BoardBackdrop
    {
        /// <summary>
        /// World-space center of the plate, matching <c>UnityVerticalSliceRenderer.AllLaneCenter()</c>
        /// / <c>BoardCenterZ</c>: <c>(LaneOffset(1) + LaneOffset(LaneCount)) * 0.5 + BoardCenterX</c>
        /// and <c>(LaneLength - 1) * 0.5</c> with that renderer's <c>LaneWidth</c> = 7,
        /// <c>LaneSpacing</c> = 9, <c>LaneCount</c> = 8, <c>CenterColumn</c> = 3 and
        /// <c>LaneLength</c> = 16. Hardcoded rather than referenced because those constants are
        /// private to that renderer and this layer must not touch it.
        /// </summary>
        private const float PlateCenterX = 34.5f;
        private const float PlateCenterZ = 7.5f;

        /// <summary>Depth the review's fix specified for the underlayer.</summary>
        private const float PlateY = -2f;

        /// <summary>
        /// Half-extents of the plate in world units. The board itself is ~70 units wide (8 lanes at
        /// 9-unit spacing) by ~16 deep, and the widest camera framing (AllLanes, orthographic size
        /// ~31 with tilt compensation) can show substantially more than that depending on the
        /// device aspect ratio. These are sized generously past every framing on purpose — see the
        /// shader's vignette remarks for why going too big costs nothing: the plate fades to
        /// <c>_EdgeColor</c>, which matches the camera's own clear colour, well inside these
        /// extents, so neither the mesh's rectangular edge nor any far-framing depth clipping is
        /// ever visible.
        /// </summary>
        private static readonly Vector2 PlateHalfExtents = new Vector2(100f, 80f);

        /// <summary>Unity's built-in Plane primitive is 10x10 world units in XZ, centered at the origin.</summary>
        private const float PrimitivePlaneSize = 10f;

        /// <summary>
        /// The player's own signal colour — matches UnityVerticalSliceRenderer's MintSignal /
        /// EndpointPortalColor(isPlayerLane: true). Used as the backdrop's dominant accent: a
        /// single static plate cannot show all eight lanes' colours at once, and the player's lane
        /// is the one that is always active/visible, so it is the reasonable one-tone stand-in.
        /// </summary>
        private static readonly Color PlayerAccent = new Color(0.349f, 0.882f, 0.714f);

        /// <summary>
        /// Secondary accent, rhyming with BoardPalette's lane-1 route-guide/route-triangle blue
        /// family, so the circuit inlay reads as two coherent board tones rather than one flat tint.
        /// </summary>
        private static readonly Color SecondaryAccent = new Color(0.3f, 0.55f, 0.82f);

        public static void Create(GameObject parent)
        {
            var root = new GameObject("LTW Board Backdrop");
            root.transform.SetParent(parent.transform, false);

            CreatePlate(root.transform);
            CreateParticleField(root.transform);
        }

        private static void CreatePlate(Transform parent)
        {
            var plate = RenderCompat.CreatePrimitive(PrimitiveType.Plane);
            plate.name = "LTW Backdrop Plate";
            plate.transform.SetParent(parent, false);
            plate.transform.position = new Vector3(PlateCenterX, PlateY, PlateCenterZ);
            plate.transform.localScale = new Vector3(
                PlateHalfExtents.x * 2f / PrimitivePlaneSize,
                1f,
                PlateHalfExtents.y * 2f / PrimitivePlaneSize);

            // Scenery only: no gameplay raycasts test against a collider here (TouchPlacementController
            // uses a math-only Plane.Raycast, not physics), so the primitive's default MeshCollider
            // is just dead weight and a potential surprise for anything added later that does use
            // Physics.Raycast.
            if (plate.TryGetComponent<Collider>(out var collider))
            {
                Object.Destroy(collider);
            }

            var meshRenderer = plate.GetComponent<MeshRenderer>();
            meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
            meshRenderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            meshRenderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            meshRenderer.sharedMaterial = CreatePlateMaterial();
        }

        private static Material CreatePlateMaterial()
        {
            var shader = Shader.Find("LTW/Backdrop");
            if (shader == null)
            {
                // Same fallback policy as LTWParticleBurst: a missing custom shader degrades to a
                // flat unlit tint rather than the magenta error shader.
                Debug.LogWarning("LTW/Backdrop shader not found; backdrop plate falls back to a flat unlit color.");
                shader = RenderCompat.Unlit;
            }

            // Stone base: a touch lighter than the ambient ground/equator blend so the plate reads
            // as a distinct lit surface rather than dropping straight to void, but still dark
            // enough to sit behind the board's own (darker) route and gutter tones. Derived from
            // the same trilight constants CreateLightRig applies, so the whole scene shares one
            // palette instead of the backdrop being a mismatched insert.
            var stoneBase = Color.Lerp(LocalVerticalSliceLauncher.AmbientGround, LocalVerticalSliceLauncher.AmbientEquator, 0.5f) * 1.25f;
            stoneBase.a = 1f;

            var material = new Material(shader) { name = "LTW Backdrop Plate (runtime)" };
            if (shader.name != "LTW/Backdrop")
            {
                // No vignette/circuit support on the fallback shader; still tint it to the stone
                // colour rather than leaving it at the fallback shader's own default white/grey.
                RenderCompat.SetAlbedo(material, stoneBase);
                return material;
            }

            material.SetColor("_BaseColor", stoneBase);

            // Matches the presentation camera's own clear colour exactly, on purpose: wherever the
            // vignette (or, at extreme framings, the plate's own far edge / depth clipping) stops
            // showing detail, it hands off to a colour identical to the void behind it, so there is
            // never a seam to see regardless of aspect ratio or zoom.
            material.SetColor("_EdgeColor", LocalVerticalSliceLauncher.PresentationClearColor);
            material.SetVector("_PlateCenterWS", new Vector4(PlateCenterX, 0f, PlateCenterZ, 0f));
            material.SetVector("_PlateExtentsWS", new Vector4(PlateHalfExtents.x, 0f, PlateHalfExtents.y, 0f));
            material.SetColor("_AccentColorA", PlayerAccent);
            material.SetColor("_AccentColorB", SecondaryAccent);
            return material;
        }

        /// <summary>
        /// The "slow-drifting particle field" from the fix spec: a handful of faint, slow motes
        /// drifting up and sideways over the board. Deliberately its own always-on system rather
        /// than routed through LTWParticleBurst's shared pooled emitters — those exist to let many
        /// short-lived gameplay bursts share four GameObjects, which does not apply to one static
        /// ambient system that is never cleared or reset.
        /// </summary>
        private static void CreateParticleField(Transform parent)
        {
            var host = new GameObject("LTW Backdrop Particles");
            host.transform.SetParent(parent, false);
            host.transform.position = new Vector3(PlateCenterX, 2f, PlateCenterZ);

            var system = host.AddComponent<ParticleSystem>();
            var systemRenderer = host.GetComponent<ParticleSystemRenderer>();

            var main = system.main;
            main.loop = true;
            main.playOnAwake = true;
            main.duration = 20f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(9f, 15f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.32f);
            main.startColor = new ParticleSystem.MinMaxGradient(FaintMote(PlayerAccent), FaintMote(Color.white));
            // World space: the field spans the whole board footprint from one static emitter, so
            // particles must not follow this host transform.
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = 0f;
            main.maxParticles = 60;

            var emission = system.emission;
            emission.enabled = true;
            // ~2.2/s at a 9-15s lifetime keeps roughly two dozen motes alive at once on average —
            // tens, not hundreds, per the fix spec's "ambient atmosphere, not a VFX moment".
            emission.rateOverTime = 2.2f;

            var shape = system.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.position = Vector3.zero;
            shape.scale = new Vector3(90f, 8f, 50f);
            shape.boxThickness = Vector3.one; // emit from the full volume, not just the box's shell.

            var velocityOverLifetime = system.velocityOverLifetime;
            velocityOverLifetime.enabled = true;
            velocityOverLifetime.space = ParticleSystemSimulationSpace.World;
            velocityOverLifetime.x = new ParticleSystem.MinMaxCurve(-0.05f, 0.05f);
            velocityOverLifetime.y = new ParticleSystem.MinMaxCurve(0.03f, 0.09f);
            velocityOverLifetime.z = new ParticleSystem.MinMaxCurve(-0.05f, 0.05f);

            var colorOverLifetime = system.colorOverLifetime;
            colorOverLifetime.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[]
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(1f, 0.25f),
                    new GradientAlphaKey(1f, 0.75f),
                    new GradientAlphaKey(0f, 1f),
                });
            colorOverLifetime.color = new ParticleSystem.MinMaxGradient(gradient);

            systemRenderer.renderMode = ParticleSystemRenderMode.Billboard;
            systemRenderer.alignment = ParticleSystemRenderSpace.View;
            systemRenderer.sortMode = ParticleSystemSortMode.None;
            systemRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            systemRenderer.receiveShadows = false;
            systemRenderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            systemRenderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            systemRenderer.sharedMaterial = CreateParticleMaterial();

            system.Play();
        }

        /// <summary>A faint, low-alpha version of an accent colour for a drifting ambient mote.</summary>
        private static Color FaintMote(Color tint) => new Color(tint.r, tint.g, tint.b, 0.22f);

        private static Material CreateParticleMaterial()
        {
            // Reuses the VFX system's existing additive shader rather than authoring a new one:
            // same soft round falloff, same "no texture asset" reasoning, and it already reads its
            // tint from per-particle vertex colour, which is exactly what this field needs.
            var shader = Shader.Find("LTW/Particle Additive");
            if (shader == null)
            {
                Debug.LogWarning("LTW/Particle Additive shader not found; backdrop motes fall back to a flat unlit material.");
                shader = RenderCompat.Unlit;
            }

            var material = new Material(shader) { name = "LTW Backdrop Motes (runtime)" };
            if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", new Color(0.85f, 0.95f, 1f, 1f));
            }

            if (material.HasProperty("_Softness"))
            {
                material.SetFloat("_Softness", 0.9f);
            }

            return material;
        }
    }
}
