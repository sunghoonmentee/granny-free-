using Granny.Core;
using Granny.Gameplay.Player;
using UnityEngine;

namespace Granny.Gameplay
{
    /// <summary>
    /// The house making noises of its own.
    ///
    /// Everything audible in here so far has meant something. A creak is a
    /// floorboard somebody stood on, a bell is a wire somebody walked through,
    /// a tap is her stick — the player is taught, correctly, that a sound is
    /// information. Which leaves the house silent whenever nothing is happening,
    /// and a silent house is a safe one: you can stand still and know.
    ///
    /// This takes that away. Every half minute or so a beam groans somewhere
    /// else in the building, and the only honest thing the player can conclude
    /// is that they do not know what it was. Standing still stops being an
    /// answer.
    ///
    /// Deliberately NOT a creak, and deliberately never published to the noise
    /// bus. A floorboard creak is a signal the player is meant to learn to read;
    /// faking those would make a real one worthless, and the game would have
    /// traded a mechanic for a cheap fright. This is a different sound, it means
    /// nothing, and it never reaches her — it is the house, not a person.
    /// </summary>
    public sealed class HouseSettling : MonoBehaviour
    {
        [SerializeField] WorldAudio world;
        [SerializeField] AudioClip settle;

        [Header("How often")]
        [SerializeField, Min(1f)] float shortestGap = 19f;
        [SerializeField, Min(1f)] float longestGap = 58f;

        [Header("Where")]
        [Tooltip("Never closer than this: a groan at your elbow reads as a thing, not as the building.")]
        [SerializeField, Min(0f)] float nearest = 6f;

        [Tooltip("Never further than this, or nobody hears it at all.")]
        [SerializeField, Min(1f)] float furthest = 26f;

        [SerializeField, Min(0f)] float loudness = 0.4f;

        float nextGroan;
        PlayerMotor player;

        /// <summary>Seconds until the next one, for tests.</summary>
        public float Waiting => Mathf.Max(0f, nextGroan - Time.time);

        void OnEnable() => nextGroan = Time.time + Random.Range(shortestGap, longestGap);

        void Update()
        {
            if (Time.time < nextGroan) return;

            nextGroan = Time.time + Random.Range(shortestGap, longestGap);
            Groan();
        }

        /// <summary>
        /// One groan, somewhere the player is not.
        /// </summary>
        /// <returns>False when there was nowhere sensible to put it.</returns>
        public bool Groan()
        {
            if (world == null || settle == null) return false;

            if (player == null)
            {
                player = FindAnyObjectByType<PlayerMotor>();
                if (player == null) return false;
            }

            if (!SomewhereElse(player.transform.position, out var where)) return false;

            world.PlayAt(settle, where, loudness, 0.16f);
            return true;
        }

        /// <summary>
        /// A point in the house far enough off to be somewhere else and near
        /// enough to be heard.
        ///
        /// Tries a handful of times rather than solving it: the house is a box
        /// twenty-four metres by twenty and the player is one point in it, so
        /// almost any guess lands in range, and the few that do not cost nothing
        /// to throw away.
        /// </summary>
        bool SomewhereElse(Vector3 from, out Vector3 point)
        {
            for (var attempt = 0; attempt < 8; attempt++)
            {
                var storey = Random.Range(-1, 3);   // cellar up to the attic

                point = new Vector3(
                    Random.Range(-11f, 11f),
                    HouseLayout.FloorY(storey) + Random.Range(0.3f, 2.4f),
                    Random.Range(-9f, 9f));

                var gap = Vector3.Distance(point, from);
                if (gap >= nearest && gap <= furthest) return true;
            }

            point = default;
            return false;
        }
    }
}
