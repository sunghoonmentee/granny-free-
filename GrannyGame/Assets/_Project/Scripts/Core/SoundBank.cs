using System;
using UnityEngine;

namespace Granny.Core
{
    /// <summary>
    /// The one place a noise becomes something you can hear.
    ///
    /// <see cref="NoiseBus"/> deals in what happened; this deals in what it
    /// sounds like. Keeping them apart means the rule about which sounds she
    /// reacts to can never drift from the rule about which sounds the player
    /// gets to hear — the player hears everything, she does not — and it means
    /// swapping the placeholder tones for real recordings is an edit to one
    /// asset rather than a hunt through the gameplay code.
    /// </summary>
    [CreateAssetMenu(menuName = "Granny/Sound Bank", fileName = "SoundBank")]
    public sealed class SoundBank : ScriptableObject
    {
        [Serializable]
        public struct Entry
        {
            public NoiseKind kind;
            public AudioClip clip;

            [Tooltip("How loud, relative to the bank's own volume.")]
            [Range(0f, 1f)] public float volume;

            [Tooltip("Pitch is nudged by this much either way, so repeats do not sound identical.")]
            [Range(0f, 0.5f)] public float pitchJitter;
        }

        [SerializeField] Entry[] entries = Array.Empty<Entry>();

        [Header("Loose sounds")]
        [Tooltip("Her stick on the boards — the sound the player navigates by.")]
        [SerializeField] AudioClip cane;

        [Tooltip("Played while she is close and you are hidden.")]
        [SerializeField] AudioClip heartbeat;

        [Tooltip("The hall clock, looped.")]
        [SerializeField] AudioClip clock;

        [Tooltip("The sting when she reaches you.")]
        [SerializeField] AudioClip scare;

        public AudioClip Cane => cane;
        public AudioClip Heartbeat => heartbeat;
        public AudioClip Clock => clock;
        public AudioClip Scare => scare;

        /// <summary>
        /// What <paramref name="kind"/> sounds like, or a null clip when nothing
        /// has been recorded for it yet. Callers check the clip, not a bool, so a
        /// half-filled bank degrades to silence rather than an exception.
        /// </summary>
        public Entry Find(NoiseKind kind)
        {
            foreach (var entry in entries)
                if (entry.kind == kind)
                    return entry;

            return default;
        }

        public bool Has(NoiseKind kind) => Find(kind).clip != null;
    }
}
