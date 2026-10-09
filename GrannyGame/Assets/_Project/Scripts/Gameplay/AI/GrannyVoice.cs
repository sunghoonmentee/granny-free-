using Granny.Core;
using UnityEngine;
using UnityEngine.AI;

namespace Granny.Gameplay.AI
{
    /// <summary>
    /// Her stick on the floorboards.
    ///
    /// This is the only way the player can tell where she is without looking,
    /// and it is deliberately generous: the tap is loud, it carries, and it
    /// speeds up when she does. Knowing she is one floor below and walking away
    /// is most of what makes the house playable rather than a slot machine.
    ///
    /// It does not go through <see cref="NoiseBus"/>, because the bus is about
    /// what *she* can hear and she already knows where she is.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public sealed class GrannyVoice : MonoBehaviour
    {
        [Header("The stick")]
        [Tooltip("Seconds between taps when she is barely moving.")]
        [SerializeField, Min(0.1f)] float slowestInterval = 1.15f;

        [Tooltip("Seconds between taps at a run.")]
        [SerializeField, Min(0.05f)] float fastestInterval = 0.38f;

        [Tooltip("Speed at which the taps are as quick as they get.")]
        [SerializeField, Min(0.5f)] float fastSpeed = 4f;

        [SerializeField, Range(0f, 1f)] float volume = 0.8f;
        [SerializeField, Range(0f, 0.5f)] float pitchJitter = 0.12f;

        NavMeshAgent agent;
        GrannyBrain brain;
        WorldAudio speaker;
        SoundBank bank;

        float nextTap;

        void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            brain = GetComponent<GrannyBrain>();
        }

        void Update()
        {
            if (!Ready()) return;

            // Flat on the floor, she makes no sound at all. That silence is the
            // player's cue that the dart landed.
            if (brain != null && brain.IsDown) return;

            var speed = agent.velocity.magnitude;
            if (speed < 0.15f) return;

            nextTap -= Time.deltaTime;
            if (nextTap > 0f) return;

            var pace = Mathf.InverseLerp(0.15f, fastSpeed, speed);
            nextTap = Mathf.Lerp(slowestInterval, fastestInterval, pace);

            speaker.PlayAt(bank.Cane, transform.position, volume, pitchJitter);
        }

        /// <summary>
        /// Finds the world's audio lazily. She is built before the scene's audio
        /// exists in some orders, and a missing speaker should cost silence
        /// rather than a null every frame.
        /// </summary>
        bool Ready()
        {
            if (speaker == null) speaker = FindAnyObjectByType<WorldAudio>();
            if (speaker == null) return false;

            if (bank == null) bank = speaker.Bank;
            return bank != null && bank.Cane != null;
        }
    }
}
