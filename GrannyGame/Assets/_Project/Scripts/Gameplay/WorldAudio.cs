using System.Collections.Generic;
using Granny.Core;
using UnityEngine;

namespace Granny.Gameplay
{
    /// <summary>
    /// Gives the house a voice.
    ///
    /// Every noise anything publishes gets played where it happened, in 3D, so
    /// the player can tell a jar breaking upstairs from one breaking in the
    /// cellar. That includes the quiet ones: a drawer is inaudible *to her* and
    /// very audible to the person pulling it. Those are two different questions
    /// and this answers the player's one.
    ///
    /// Sources are pooled, because a shelf of jars hitting the floor at once
    /// would otherwise make a GameObject per jar and leave them behind.
    /// </summary>
    public sealed class WorldAudio : MonoBehaviour
    {
        [SerializeField] SoundBank bank;

        [Header("Mix")]
        [SerializeField, Range(0f, 1f)] float volume = 0.9f;

        [Tooltip("Past this, a sound is inaudible. Generous: the house is big and she is meant to be locatable.")]
        [SerializeField, Min(1f)] float maxDistance = 45f;

        [Tooltip("Within this, volume does not fall off at all.")]
        [SerializeField, Min(0.5f)] float minDistance = 2.5f;

        [Header("Pool")]
        [SerializeField, Min(1)] int voices = 16;

        readonly List<AudioSource> pool = new();
        int next;

        public SoundBank Bank => bank;

        void Awake()
        {
            for (var i = 0; i < voices; i++)
                pool.Add(MakeVoice($"Voice_{i}"));
        }

        void OnEnable()
        {
            // Both: the player hears everything that happens, including the
            // sounds the rules keep from reaching her.
            NoiseBus.Heard += Play;
            NoiseBus.Filtered += Play;
        }

        void OnDisable()
        {
            NoiseBus.Heard -= Play;
            NoiseBus.Filtered -= Play;
        }

        AudioSource MakeVoice(string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);

            var source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 1f;               // fully positional
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = minDistance;
            source.maxDistance = maxDistance;
            source.dopplerLevel = 0f;               // nothing here moves fast enough to warrant it

            return source;
        }

        void Play(Noise noise)
        {
            if (bank == null) return;

            var entry = bank.Find(noise.Kind);
            if (entry.clip == null) return;

            PlayAt(entry.clip, noise.Position,
                entry.volume <= 0f ? 1f : entry.volume, entry.pitchJitter);
        }

        /// <summary>
        /// Plays a clip at a point in the world. Public so anything with a sound
        /// of its own — a cane, a bell — can borrow a voice rather than carrying
        /// an AudioSource around.
        /// </summary>
        public void PlayAt(AudioClip clip, Vector3 position, float clipVolume = 1f, float pitchJitter = 0f)
        {
            if (clip == null || pool.Count == 0) return;

            var source = pool[next];
            next = (next + 1) % pool.Count;

            source.transform.position = position;
            source.clip = clip;
            source.volume = volume * clipVolume;
            source.pitch = 1f + Random.Range(-pitchJitter, pitchJitter);
            source.Play();
        }
    }
}
