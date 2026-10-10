using Granny.Core;
using Granny.Gameplay.AI;
using Granny.Gameplay.Interaction;
using UnityEngine;

namespace Granny.Gameplay.Player
{
    /// <summary>
    /// Your own heart, while you are in the wardrobe and she is in the room.
    ///
    /// Hiding takes the player's agency away: the motor is off, the view is a
    /// slot, and there is nothing to do but wait. That is the right design and it
    /// leaves a hole — with no controls and no information, waiting is just dead
    /// time. The heartbeat is the information. It tells the player how close she
    /// is without telling them where, so the wait has a shape: it gets worse, it
    /// peaks, and then either it fades or the door opens.
    ///
    /// It is the player's own body, so it is flat rather than positional. A
    /// heartbeat coming from the far side of the room would be somebody else's.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public sealed class HidingHeartbeat : MonoBehaviour
    {
        [Header("Range")]
        [Tooltip("Beyond this she is not close enough to be frightening.")]
        [SerializeField, Min(1f)] float noticeDistance = 14f;

        [Tooltip("Inside this it is as loud and as fast as it gets.")]
        [SerializeField, Min(0.5f)] float panicDistance = 2.5f;

        [Header("Mix")]
        [SerializeField, Range(0f, 1f)] float loudest = 0.75f;

        [Tooltip("Pitch at the worst of it. One is a resting heart.")]
        [SerializeField, Range(1f, 2f)] float fastestPitch = 1.45f;

        [Tooltip("How quickly it follows her. Slow, so it swells rather than snaps.")]
        [SerializeField, Min(0.1f)] float follow = 1.8f;

        AudioSource source;
        GrannyBrain hunter;
        HidingSpot spot;

        float level;

        /// <summary>Where the player is hidden, or null.</summary>
        public HidingSpot Spot => spot;

        /// <summary>0 when calm, 1 when she is as close as she gets.</summary>
        public float Level => level;

        void Awake()
        {
            source = GetComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = true;
            source.spatialBlend = 0f;        // the player's own body, not a place
            source.volume = 0f;
        }

        void OnEnable()
        {
            foreach (var place in FindObjectsByType<HidingSpot>(FindObjectsSortMode.None))
                place.OccupancyChanged += OnOccupancyChanged;
        }

        void OnDisable()
        {
            foreach (var place in FindObjectsByType<HidingSpot>(FindObjectsSortMode.None))
                if (place != null) place.OccupancyChanged -= OnOccupancyChanged;
        }

        void OnOccupancyChanged(GameObject occupant)
        {
            // Someone climbed in or out of something. Work out whether it was us.
            spot = null;

            foreach (var place in FindObjectsByType<HidingSpot>(FindObjectsSortMode.None))
                if (place.Occupant == gameObject)
                    spot = place;
        }

        void Update()
        {
            var wanted = WantedLevel();
            level = Mathf.MoveTowards(level, wanted, Time.deltaTime * follow);

            if (level <= 0.001f)
            {
                if (source.isPlaying) source.Stop();
                return;
            }

            if (source.clip == null) source.clip = FindClip();
            if (source.clip == null) return;

            if (!source.isPlaying) source.Play();

            source.volume = level * loudest;
            source.pitch = Mathf.Lerp(1f, fastestPitch, level);
        }

        /// <summary>
        /// Only while hidden, and only for how close she is. Standing in the open
        /// with her across the room is frightening on its own and does not need
        /// scoring; the wardrobe is the place where nothing else is happening.
        /// </summary>
        float WantedLevel()
        {
            if (spot == null || spot.Occupant != gameObject) return 0f;

            if (hunter == null) hunter = FindAnyObjectByType<GrannyBrain>();
            if (hunter == null || hunter.IsDown) return 0f;

            var distance = Vector3.Distance(transform.position, hunter.transform.position);
            return 1f - Mathf.Clamp01(
                (distance - panicDistance) / Mathf.Max(0.1f, noticeDistance - panicDistance));
        }

        AudioClip FindClip()
        {
            var world = FindAnyObjectByType<WorldAudio>();
            return world != null && world.Bank != null ? world.Bank.Heartbeat : null;
        }
    }
}
