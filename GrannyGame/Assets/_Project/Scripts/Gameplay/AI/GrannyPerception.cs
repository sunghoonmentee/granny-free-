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

        /// <summary>The last noise she actually acted on. For debugging and the HUD.</summary>
        public Noise? LastHeard { get; private set; }

        public Transform Target => target;
        public DifficultyProfile Difficulty => difficulty;

        Noise pendingNoise;
        float pendingNoiseTime;

        /// <summary>
        /// The hiding place she watched the player climb into.
        ///
        /// Hiding does not make the player invisible to a search — it only breaks
        /// line of sight. If she saw it happen she goes straight there and opens
        /// it; if she did not, the spot is just another thing she might check.
        /// That difference is the whole gamble: hiding while she can see you is
        /// not hiding, it is choosing where to be caught.
        /// </summary>
        public Interaction.HidingSpot TargetHidingSpot { get; set; }

        /// <summary>True when she is going to open a specific spot rather than guess.</summary>
        public bool SawThemHide => TargetHidingSpot != null;

        /// <summary>
        /// Watches people get into things. Only what she can see at that moment
        /// counts — climbing into a wardrobe behind her back is free.
        /// </summary>
        void OnSomeoneHid(GameObject occupant)
        {
            if (occupant == null)
            {
                // They came out. Whatever she thought she knew about that spot is
                // spent, or she would keep returning to an empty wardrobe.
                TargetHidingSpot = null;
                return;
            }

            if (!CanSeeTarget) return;
            if (target == null || !occupant.transform.IsChildOf(target.root)) return;

            TargetHidingSpot = FindSpotHolding(occupant);
            if (TargetHidingSpot != null)
                LastKnownPosition = TargetHidingSpot.transform.position;
        }

        static Interaction.HidingSpot FindSpotHolding(GameObject occupant)
        {
            foreach (var spot in Object.FindObjectsByType<Interaction.HidingSpot>(FindObjectsSortMode.None))
                if (spot.Occupant == occupant)
                    return spot;

            return null;
        }

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

        void OnEnable()
        {
            NoiseBus.Heard += OnNoise;

            foreach (var spot in Object.FindObjectsByType<Interaction.HidingSpot>(FindObjectsSortMode.None))
                spot.OccupancyChanged += OnSomeoneHid;
        }

        void OnDisable()
        {
            NoiseBus.Heard -= OnNoise;

            foreach (var spot in Object.FindObjectsByType<Interaction.HidingSpot>(FindObjectsSortMode.None))
                if (spot != null) spot.OccupancyChanged -= OnSomeoneHid;
        }

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

                // Seeing them out in the open makes any wardrobe she was watching
                // stale. Seeing them while they are still inside one does not —
                // for a frame or two after climbing in they are visible through
                // the gap, and clearing here threw away the very thing she had
                // just watched happen.
                if (TargetHidingSpot == null || TargetHidingSpot.Occupant == null)
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
            LastKnownPosition = pendingNoise.Position;
            LastHeard = pendingNoise;
        }

        /// <summary>
        /// Everything the bus lets through, she hears — from anywhere in the house,
        /// on any floor. There is no distance test here on purpose: the original's
        /// rule is that she hears the house, and a radius check is what made her
        /// sit in the basement while the player smashed things two storeys up.
        /// </summary>
        void OnNoise(Noise noise)
        {
            // The doors she slams and the traps she sets must not send her chasing
            // herself around the house.
            if (noise.Source != null && noise.Source.transform.IsChildOf(transform)) return;

            // The freshest noise wins: a bottle thrown across the hall while she is
            // already walking somewhere redirects her, which is what makes throwing
            // one worth doing.
            pendingNoise = noise;
            pendingNoiseTime = Mathf.Max(0.001f, difficulty != null ? difficulty.ReactionDelay : 0.35f);
        }

        /// <summary>Clears what she knows — used when a new day starts.</summary>
        public void Forget()
        {
            CanSeeTarget = false;
            LastKnownPosition = null;
            TimeSinceSeen = float.MaxValue;
            pendingNoiseTime = 0f;
            LastHeard = null;
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
