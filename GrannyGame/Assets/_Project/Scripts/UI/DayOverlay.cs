using System.Collections;
using Granny.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace Granny.UI
{
    /// <summary>
    /// The black screen between days, and the two lines that end a run.
    ///
    /// Being caught deliberately cuts to black rather than showing a death
    /// animation: the player is told they lost a day, not shown how, which keeps
    /// the encounter itself the frightening part.
    /// </summary>
    public sealed class DayOverlay : MonoBehaviour
    {
        [Header("Sources")]
        [SerializeField] GameDirector director;

        [Header("Widgets")]
        [SerializeField] Image blackout;
        [SerializeField] Text dayLabel;
        [SerializeField] Text verdictLabel;
        [SerializeField] CanvasGroup dayGroup;

        [Header("Timing")]
        [SerializeField, Min(0f)] float dayCardSeconds = 2.4f;

        Coroutine dayCard;

        void Awake()
        {
            if (director == null) director = FindAnyObjectByType<GameDirector>();

            SetAlpha(blackout, 0f);
            if (dayGroup != null) dayGroup.alpha = 0f;
            if (verdictLabel != null) verdictLabel.text = string.Empty;
        }

        void OnEnable()
        {
            if (director == null) return;

            director.BlackoutChanged += OnBlackout;
            director.DayStarted += OnDayStarted;
            director.GameOver += OnGameOver;
            director.Escaped += OnEscaped;
        }

        void OnDisable()
        {
            if (director == null) return;

            director.BlackoutChanged -= OnBlackout;
            director.DayStarted -= OnDayStarted;
            director.GameOver -= OnGameOver;
            director.Escaped -= OnEscaped;
        }

        void OnBlackout(float amount) => SetAlpha(blackout, amount);

        void OnDayStarted(int day)
        {
            if (dayLabel == null) return;

            var allowed = director != null && director.Days != null ? director.Days.DaysAllowed : 5;
            dayLabel.text = $"DAY {day} / {allowed}";

            if (dayCard != null) StopCoroutine(dayCard);
            dayCard = StartCoroutine(ShowDayCard());
        }

        IEnumerator ShowDayCard()
        {
            if (dayGroup == null) yield break;

            yield return Fade(dayGroup, 1f, 0.35f);

            var held = 0f;
            while (held < dayCardSeconds)
            {
                held += Time.deltaTime;
                yield return null;
            }

            yield return Fade(dayGroup, 0f, 0.6f);
            dayCard = null;
        }

        static IEnumerator Fade(CanvasGroup group, float target, float seconds)
        {
            var start = group.alpha;
            var elapsed = 0f;

            while (elapsed < seconds)
            {
                elapsed += Time.deltaTime;
                group.alpha = Mathf.Lerp(start, target, elapsed / seconds);
                yield return null;
            }

            group.alpha = target;
        }

        void OnGameOver()
        {
            SetAlpha(blackout, 1f);
            if (verdictLabel != null) verdictLabel.text = "She kept you.";
            Player.SetCursorVisible();
        }

        void OnEscaped()
        {
            if (verdictLabel != null) verdictLabel.text = "You got out.";
            Player.SetCursorVisible();
        }

        static void SetAlpha(Graphic graphic, float alpha)
        {
            if (graphic == null) return;

            var colour = graphic.color;
            colour.a = alpha;
            graphic.color = colour;
            graphic.enabled = alpha > 0.001f;
        }

        /// <summary>Small shim so the UI does not need a reference to the player rig.</summary>
        static class Player
        {
            public static void SetCursorVisible()
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }
    }
}
