using Granny.Core;
using UnityEngine;

namespace Granny.Gameplay
{
    /// <summary>
    /// Turns the lights down to where they were when the house was unplayable.
    ///
    /// The first build of this game was too dark to see in, which was a bug: the
    /// ambient was at 0.035, the fog swallowed anything past fifteen metres, and
    /// every surface was painted dark on top of that. Fixing it was most of R1.
    ///
    /// This puts it back on purpose, for the people who want it. It is applied
    /// at runtime rather than baked into the scene so there is exactly one lit
    /// house to maintain, and the mode is a filter over it rather than a second
    /// copy that will drift.
    /// </summary>
    public sealed class DarkMode : MonoBehaviour
    {
        [Header("How dark is dark")]
        [SerializeField] Color ambient = new(0.035f, 0.034f, 0.040f);
        [SerializeField, Range(0f, 0.1f)] float fogDensity = 0.055f;

        [Tooltip("Every bulb in the house is dimmed to this much of itself.")]
        [SerializeField, Range(0f, 1f)] float bulbScale = 0.35f;

        Color litAmbient;
        float litFog;
        Light[] bulbs;
        float[] litIntensity;

        /// <summary>Whether the lights are currently down.</summary>
        public bool IsDark { get; private set; }

        void Start()
        {
            litAmbient = RenderSettings.ambientLight;
            litFog = RenderSettings.fogDensity;

            bulbs = FindObjectsByType<Light>(FindObjectsSortMode.None);
            litIntensity = new float[bulbs.Length];

            for (var i = 0; i < bulbs.Length; i++)
                litIntensity[i] = bulbs[i] != null ? bulbs[i].intensity : 0f;

            Apply(GameModes.Dark);
        }

        /// <summary>Public so the pause menu can show the difference immediately.</summary>
        public void Apply(bool dark)
        {
            IsDark = dark;

            RenderSettings.ambientLight = dark ? ambient : litAmbient;
            RenderSettings.fogDensity = dark ? fogDensity : litFog;

            if (bulbs == null) return;

            for (var i = 0; i < bulbs.Length; i++)
            {
                if (bulbs[i] == null) continue;

                // The player's torch is not a house light and must not be dimmed
                // with them — in this mode it is the only thing keeping the game
                // playable at all.
                if (bulbs[i].type == LightType.Spot) continue;

                bulbs[i].intensity = dark ? litIntensity[i] * bulbScale : litIntensity[i];
            }
        }
    }
}
