using UnityEngine;

namespace LTW.UnityClient.Simulation
{
    /// <summary>
    /// The shapes an effect burst can take. Chosen by what the event IS, not by who raises it.
    /// </summary>
    public enum BurstShape
    {
        /// <summary>Omnidirectional spark burst. Hits, kills, generic impacts.</summary>
        Impact,

        /// <summary>Upward drift with slow fade. Spawns, income, build confirmation.</summary>
        Rise,

        /// <summary>Flat outward sweep across the ground plane. Leaks, eliminations.</summary>
        Sweep,

        /// <summary>Tight, short, forward-biased. Muzzle flash.</summary>
        Muzzle,
    }

    /// <summary>
    /// A long-lived world-space ParticleSystem that every effect of one shape emits into.
    /// </summary>
    /// <remarks>
    /// The project had zero ParticleSystem instances before this, and the reason turned out to be
    /// structural rather than neglect: `com.unity.modules.particlesystem` was not in the package
    /// manifest, so the type did not exist to be used. Every effect was a pooled primitive with a
    /// flat colour that appeared, held and vanished — a shape that reads as a debug gizmo no matter
    /// what colour it is, because nothing about it moves.
    ///
    /// ONE SYSTEM PER SHAPE, NOT ONE PER BURST. This is the whole design and it is worth stating,
    /// because the obvious approach — pool an emitter per effect, the way this renderer pools
    /// everything else — was tried first and measured: it took peak active presentation objects
    /// from 2,769 to 6,082 on the same seed, because each emitter has to be held past its own
    /// particles' lifetime before it can be recycled, and most effects here are very short (a
    /// muzzle flash is 0.08s). Padding the residency of a 0.08s effect by even a frame or two
    /// multiplies its cost.
    ///
    /// Emitting into a shared world-space system removes the problem rather than tuning it: four
    /// GameObjects exist for the whole match no matter how many effects fire, and an effect costs
    /// particles instead of objects. Particles are what the GPU is good at; GameObjects are what
    /// it is not.
    ///
    /// It works because tint travels per particle, in <c>startColor</c>, and the colour-over-lifetime
    /// gradient is a white alpha envelope that multiplies it. So one system serves every colour in
    /// the game without a per-effect material or a per-effect curve.
    /// </remarks>
    [RequireComponent(typeof(ParticleSystem))]
    public sealed class LTWParticleBurst : MonoBehaviour
    {
        /// <summary>
        /// Ceiling on live particles in one shared system.
        /// </summary>
        /// <remarks>
        /// Shared across every effect of this shape, so it is a whole-screen budget rather than a
        /// per-effect one. Sized against the harness's measured worst case: heavy-pressure frames
        /// reach a few hundred simultaneous events, and at ~14 particles each that is a few
        /// thousand. Beyond the cap Unity drops new particles rather than growing, which degrades
        /// by thinning the densest moment instead of by stuttering.
        /// </remarks>
        public const int MaxParticles = 3000;

        private ParticleSystem system;
        private ParticleSystemRenderer systemRenderer;
        private BurstShape shape;
        private static Material sharedMaterial;

        /// <summary>The additive material every burst shares.</summary>
        private static Material SharedMaterial
        {
            get
            {
                if (sharedMaterial != null)
                {
                    return sharedMaterial;
                }

                var shader = Shader.Find("LTW/Particle Additive");
                if (shader == null)
                {
                    // Falling back rather than rendering magenta. The unlit fallback loses the soft
                    // round falloff and reads as hard squares, which is worse but still legible; a
                    // null shader is not.
                    Debug.LogWarning("LTW/Particle Additive shader not found; effects fall back to flat unlit quads.");
                    shader = RenderCompat.Unlit;
                }

                sharedMaterial = new Material(shader) { name = "LTW Particle Additive (runtime)" };
                return sharedMaterial;
            }
        }

        /// <summary>Creates the shared emitter for one shape, parented under the match object.</summary>
        public static LTWParticleBurst Create(Transform parent, BurstShape shape)
        {
            var host = new GameObject($"LTW VFX {shape}");
            host.transform.SetParent(parent, false);
            var burst = host.AddComponent<LTWParticleBurst>();
            burst.shape = shape;
            return burst;
        }

        private void Awake()
        {
            system = GetComponent<ParticleSystem>();
            systemRenderer = GetComponent<ParticleSystemRenderer>();

            var main = system.main;
            main.playOnAwake = true;
            main.loop = true;
            main.duration = 1f;
            main.maxParticles = MaxParticles;
            // World space is what makes a shared emitter possible: particles stay where they were
            // emitted rather than following this object, so one system at the origin can serve
            // events anywhere on the board.
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = 0f;

            // No rate module. Every particle comes from an explicit Emit, so a rate of anything
            // would trickle particles into the scene forever.
            var emission = system.emission;
            emission.enabled = false;

            var shapeModule = system.shape;
            shapeModule.enabled = false;

            ConfigureLifetimeCurves();

            systemRenderer.sharedMaterial = SharedMaterial;
            systemRenderer.renderMode = ParticleSystemRenderMode.Billboard;
            systemRenderer.alignment = ParticleSystemRenderSpace.View;
            systemRenderer.sortMode = ParticleSystemSortMode.None;
            systemRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            systemRenderer.receiveShadows = false;
            systemRenderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            systemRenderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;

            system.Play();
        }

        /// <summary>Live particle count, for diagnostics and tests.</summary>
        public int LiveParticles => system == null ? 0 : system.particleCount;

        /// <summary>Removes every live particle. Used on match reset.</summary>
        public void ClearAll()
        {
            if (system != null)
            {
                system.Clear();
            }
        }

        /// <summary>
        /// The fade and shrink every particle follows, set once because it is shape-independent.
        /// </summary>
        /// <remarks>
        /// The gradient's RGB is white throughout, so it multiplies each particle's own
        /// <c>startColor</c> rather than replacing it — that is what lets one shared system carry
        /// every role colour in the game. Only alpha is shaped, as a rise-and-fall envelope, so a
        /// burst decays instead of cutting out.
        /// </remarks>
        private void ConfigureLifetimeCurves()
        {
            var colorOverLifetime = system.colorOverLifetime;
            colorOverLifetime.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[]
                {
                    new GradientAlphaKey(0f, 0f),
                    // Peak below 1 on purpose. Blending is additive, so a dozen overlapping
                    // particles at full alpha sum well past 1.0 and clip to white — which throws
                    // away the role colour that is the entire point of tinting them, and then bloom
                    // amplifies the clipped white further. Capturing at 1.0 showed exactly that:
                    // impacts read as white blobs rather than as coloured sparks.
                    new GradientAlphaKey(0.62f, 0.12f),
                    new GradientAlphaKey(0f, 1f),
                });
            colorOverLifetime.color = new ParticleSystem.MinMaxGradient(gradient);

            var sizeOverLifetime = system.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                new Keyframe(0f, 0.35f),
                new Keyframe(0.25f, 1f),
                new Keyframe(1f, 0.1f)));

            // Drag, so bursts decelerate instead of travelling at constant speed until they expire.
            // Without it every effect reads as a starburst diagram.
            var limitVelocity = system.limitVelocityOverLifetime;
            limitVelocity.enabled = true;
            limitVelocity.dampen = 0.35f;
            limitVelocity.limit = new ParticleSystem.MinMaxCurve(0.6f);
        }

        /// <summary>
        /// Emits one burst at a world position. Costs particles, not GameObjects.
        /// </summary>
        /// <param name="position">World position of the burst centre.</param>
        /// <param name="color">Tint; multiplied by the shared alpha envelope over each particle's life.</param>
        /// <param name="scale">Overall size multiplier, matching the old primitive effect's scale.</param>
        /// <param name="lifetime">Seconds until the last particle expires.</param>
        /// <param name="direction">Forward bias for <see cref="BurstShape.Muzzle"/>; ignored otherwise.</param>
        public void Emit(Vector3 position, Color color, float scale, float lifetime, Vector3 direction = default)
        {
            if (system == null)
            {
                return;
            }

            var count = ParticleCountFor(shape);
            for (var index = 0; index < count; index++)
            {
                var particle = new ParticleSystem.EmitParams
                {
                    position = position + StartOffset(shape, scale),
                    velocity = Velocity(shape, scale, direction),
                    startColor = color,
                    startSize = StartSize(shape, scale),
                    // Staggered so a burst does not blink out all at once, which is the specific
                    // thing that made the old primitive effects read as a toggle rather than an
                    // event. The last particle still expires at `lifetime`.
                    startLifetime = lifetime * Random.Range(0.55f, 1f),
                    angularVelocity = Random.Range(-180f, 180f),
                };

                system.Emit(particle, 1);
            }
        }

        private static int ParticleCountFor(BurstShape shape) => shape switch
        {
            BurstShape.Muzzle => 6,
            BurstShape.Sweep => 20,
            BurstShape.Rise => 12,
            _ => 14,
        };

        private static Vector3 StartOffset(BurstShape shape, float scale) => shape switch
        {
            // Sweep starts on a ring rather than a point so it reads as a wave leaving the centre.
            BurstShape.Sweep => Random.insideUnitCircle.normalized.ToXZ() * (scale * 0.25f),
            BurstShape.Muzzle => Vector3.zero,
            // Spread wider than the particles are large, so a burst starts as separable sparks.
            // At a tighter radius all fourteen begin coincident and additively sum into one blob
            // for the first third of their life, which is when they are brightest.
            _ => Random.insideUnitSphere * (scale * 0.45f),
        };

        private static Vector3 Velocity(BurstShape shape, float scale, Vector3 direction)
        {
            var speed = scale * 2.4f;
            switch (shape)
            {
                case BurstShape.Rise:
                    // Mostly up, with enough lateral spread that the column is not a straight line.
                    return new Vector3(Random.Range(-0.35f, 0.35f), Random.Range(0.8f, 1.6f), Random.Range(-0.35f, 0.35f)) * speed;

                case BurstShape.Sweep:
                    // Flat on the board plane: a leak or elimination is a ground event, and vertical
                    // spray at this camera angle reads as an explosion above the lane instead.
                    return Random.insideUnitCircle.normalized.ToXZ() * (speed * Random.Range(0.7f, 1.3f));

                case BurstShape.Muzzle:
                    var forward = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.up;
                    return (forward + Random.insideUnitSphere * 0.35f).normalized * (speed * Random.Range(0.9f, 1.5f));

                default:
                    return Random.insideUnitSphere.normalized * (speed * Random.Range(0.5f, 1.2f));
            }
        }

        /// <summary>
        /// Particle size, kept well under the burst's spread radius.
        /// </summary>
        /// <remarks>
        /// Halved from the first pass. Sparks larger than the radius they are scattered across
        /// cannot read as separate objects at any alpha — they are one shape with a lumpy edge.
        /// </remarks>
        private static float StartSize(BurstShape shape, float scale) => shape switch
        {
            BurstShape.Muzzle => scale * Random.Range(0.28f, 0.46f),
            BurstShape.Sweep => scale * Random.Range(0.14f, 0.24f),
            _ => scale * Random.Range(0.16f, 0.32f),
        };
    }

    internal static class BurstVectorExtensions
    {
        /// <summary>Lifts a 2D direction onto the board's XZ plane.</summary>
        public static Vector3 ToXZ(this Vector2 value) => new Vector3(value.x, 0f, value.y);
    }
}
