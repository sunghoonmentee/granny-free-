using Granny.Core;
using UnityEngine;

namespace Granny.Gameplay.AI
{
    /// <summary>
    /// Granny's ears and eyes. Nothing here decides what to do — it only reports
    /// what she currently knows, so the state machine has one source of truth and
    /// the two can be reasoned about separately.
    /// </summary>
    public sealed class GrannyPerception : MonoBehaviour
    {
        [Header("Configuration")]
        [SerializeField] DifficultyProfile difficulty;

        [Tooltip("Where her eyes are. Sight is traced from here, not from her feet.")]
        [SerializeField] Transform eye;

        [Header("Target")]
        [Tooltip("Left empty, the player is found at startup.")]
        [SerializeField] Transform target;

        /// <summary>True while the player is visible right now.</summary>
        public bool CanSeeTarget { get; private set; }

        /// <summary>Where the player was last seen or last heard. Null if she has nothing.</summary>
        public Vector3? LastKnownPosition { get; private set; }

        /// <summary>Seconds since the player was last actually seen.</summary>
        public float TimeSinceSeen { get; private set; } = float.MaxValue;

        /// <summary>A noise she has registered but not yet reacted to.</summary>
        public bool HasPendingNoise => pendingNoiseTime > 0f;

        public Transform Target => target;
        public DifficultyProfile Difficulty => difficulty;

        Vector3 pendingNoisePosition;
        float pendingNoiseTime;

        /// <summary>
        /// Hiding does not make the player invisible to a search — it only breaks
        /// line of sight. The brain asks this before deciding to open a wardrobe.
        /// </summary>
        public Interaction.HidingSpot TargetHidingSpot { get; set; }

        void Awake()
        {
            if (eye == null) eye = transform;

            if (target == null)
            {
                var player = Object.FindAnyObjectByType<Player.PlayerMotor>();
                if (player != null) target = player.transform;
            }

            if (difficulty == null)
                Debug.LogError($"[{nameof(GrannyPerception)}] No difficulty profile assigned.", this);
        }

        void OnEnable() => NoiseBus.Heard += OnNoise;

        void OnDisable() => NoiseBus.Heard -= OnNoise;

        void Update()
        {
            UpdateSight();
            UpdatePendingNoise();
        }

        void UpdateSight()
        {
            CanSeeTarget = false;

            if (target == null || difficulty == null)
            {
                TimeSinceSeen += Time.deltaTime;
                return;
            }

            var toTarget = target.position + Vector3.up * 1.2f - eye.position;
            var distance = toTarget.magnitude;

            if (distance <= difficulty.SightRange)
            {
                // Standing right behind her still counts: creeping past at arm's
                // length should not be free just because she is facing away.
                var withinCone = distance <= difficulty.AwarenessRange ||
                                 Vector3.Angle(eye.forward, toTarget) <= difficulty.SightAngle * 0.5f;

                if (withinCone && !Physics.Raycast(
                        eye.position, toTarget.normalized, distance - 0.1f,
                        GameLayers.SightBlockers, QueryTriggerInteraction.Ignore))
                {
                    CanSeeTarget = true;
                }
            }

            if (CanSeeTarget)
            {
                TimeSinceSeen = 0f;
                LastKnownPosition = target.position;
                TargetHidingSpot = null;
            }
            else
            {
                TimeSinceSeen += Time.deltaTime;
            }
        }

        void UpdatePendingNoise()
        {
            if (pendingNoiseTime <= 0f) return;

            pendingNoiseTime -= Time.deltaTime;
            if (pendingNoiseTime > 0f) return;

            pendingNoiseTime = 0f;
            LastKnownPosition = pendingNoisePosition;
        }

        void OnNoise(Noise noise)
        {
            if (difficulty == null) return;

            // Her own footsteps and the doors she slams must not send her chasing
            // herself around the house.
            if (noise.Source != null && noise.Source.transform.IsChildOf(transform)) return;

            var audible = noise.Radius * difficulty.HearingScale;
            if ((transform.position - noise.Position).sqrMagnitude > audible * audible) return;

            // A louder, closer noise supersedes one she has not acted on yet.
            pendingNoisePosition = noise.Position;
            pendingNoiseTime = Mathf.Max(0.001f, difficulty.ReactionDelay);
        }

        /// <summary>Clears what she knows — used when a new day starts.</summary>
        public void Forget()
        {
            CanSeeTarget = false;
            LastKnownPosition = null;
            TimeSinceSeen = float.MaxValue;
            pendingNoiseTime = 0f;
            TargetHidingSpot = null;
        }

        /// <summary>Plants a destination directly, e.g. when scripted or for tests.</summary>
        public void Suspect(Vector3 position)
        {
            LastKnownPosition = position;
            pendingNoiseTime = 0f;
        }

        public void SetDifficulty(DifficultyProfile profile) => difficulty = profile;
    }
}
