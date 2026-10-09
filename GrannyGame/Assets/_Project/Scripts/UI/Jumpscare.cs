using System.Collections;
using Granny.Core;
using Granny.Gameplay;
using Granny.Gameplay.AI;
using UnityEngine;
using UnityEngine.UI;

namespace Granny.UI
{
    /// <summary>
    /// The half second between her reaching you and waking up in bed.
    ///
    /// Short on purpose. A scare that outstays its welcome is a cutscene, and a
    /// cutscene is something a player learns to sit through — the whole point is
    /// that being caught should feel like a mistake you made rather than a video
    /// you are shown. So: one hard sting, the camera thrown off its mount, a red
    /// wash, and then the blackout the day cycle was going to do anyway.
    /// </summary>
    public sealed class Jumpscare : MonoBehaviour
    {
        [Header("Wiring")]
        [SerializeField] GameDirector director;
        [SerializeField] Image flash;

        [Header("The moment")]
        [SerializeField, Min(0.05f)] float seconds = 0.55f;
        [SerializeField, Min(0f)] float shakeDegrees = 9f;
        [SerializeField] Color wash = new(0.55f, 0.03f, 0.03f, 1f);

        Transform cameraPivot;
        bool running;

        /// <summary>True while the scare is on screen.</summary>
        public bool IsPlaying => running;

        void Awake()
        {
            if (director == null) director = FindAnyObjectByType<GameDirector>();
            if (flash != null) SetAlpha(0f);
        }

        void OnEnable()
        {
            var granny = FindAnyObjectByType<GrannyBrain>();
            if (granny != null) granny.Caught += OnCaught;
        }

        void OnDisable()
        {
            var granny = FindAnyObjectByType<GrannyBrain>();
            if (granny != null) granny.Caught -= OnCaught;
        }

        void OnCaught(GameObject who)
        {
            if (running || who == null) return;

            var cam = who.GetComponentInChildren<Camera>();
            cameraPivot = cam != null ? cam.transform : null;

            StartCoroutine(Play());
        }

        /// <summary>Runs the scare. Public so it can be triggered without being caught.</summary>
        public IEnumerator Play()
        {
            if (running) yield break;
            running = true;

            // A sting, at the player rather than in the world: this one is not a
            // thing that happened in the house, it is the inside of their head.
            var speaker = FindAnyObjectByType<WorldAudio>();
            if (speaker != null && speaker.Bank != null && cameraPivot != null)
                speaker.PlayAt(speaker.Bank.Scare, cameraPivot.position, 1f, 0.05f);

            var startRotation = cameraPivot != null ? cameraPivot.localRotation : Quaternion.identity;
            var elapsed = 0f;

            while (elapsed < seconds)
            {
                elapsed += Time.unscaledDeltaTime;
                var life = Mathf.Clamp01(elapsed / seconds);

                // Violent at first and gone by the end, so the shake reads as an
                // impact rather than a rumble.
                var violence = (1f - life) * shakeDegrees;

                if (cameraPivot != null)
                    cameraPivot.localRotation = startRotation * Quaternion.Euler(
                        Random.Range(-violence, violence),
                        Random.Range(-violence, violence),
                        Random.Range(-violence, violence));

                SetAlpha(Mathf.Lerp(0.85f, 0f, life));
                yield return null;
            }

            if (cameraPivot != null) cameraPivot.localRotation = startRotation;
            SetAlpha(0f);

            running = false;
        }

        void SetAlpha(float alpha)
        {
            if (flash == null) return;

            var colour = wash;
            colour.a = alpha;
            flash.color = colour;
        }
    }
}
