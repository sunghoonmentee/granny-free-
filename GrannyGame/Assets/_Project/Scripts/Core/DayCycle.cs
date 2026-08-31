using System;
using UnityEngine;

namespace Granny.Core
{
    /// <summary>
    /// The five-day clock, as plain C# so the rules can be tested without a scene.
    ///
    /// Being caught is not death — it costs a day. That is what makes the whole
    /// game playable: every mistake teaches you the house, and the countdown is
    /// what stops that from being free.
    /// </summary>
    public sealed class DayCycle
    {
        readonly int daysAllowed;

        public DayCycle(int daysAllowed = 5)
        {
            this.daysAllowed = Mathf.Max(1, daysAllowed);
            Day = 1;
        }

        /// <summary>Current day, starting at 1.</summary>
        public int Day { get; private set; }

        public int DaysAllowed => daysAllowed;

        /// <summary>Days left after this one. Zero on the final day.</summary>
        public int DaysRemaining => Mathf.Max(0, daysAllowed - Day);

        public bool IsFinalDay => Day >= daysAllowed;

        public bool IsOver { get; private set; }

        public bool HasEscaped { get; private set; }

        /// <summary>Raised with the new day number after being caught.</summary>
        public event Action<int> DayStarted;

        /// <summary>Raised when the last day is used up.</summary>
        public event Action GameOver;

        /// <summary>Raised when the player gets out.</summary>
        public event Action Escaped;

        /// <summary>
        /// Called when Granny catches the player. Advances the day, or ends the
        /// run if there are none left.
        /// </summary>
        public void Caught()
        {
            if (IsOver) return;

            if (IsFinalDay)
            {
                IsOver = true;
                GameOver?.Invoke();
                return;
            }

            Day++;
            DayStarted?.Invoke(Day);
        }

        /// <summary>Called when the player makes it out of the house.</summary>
        public void Escape()
        {
            if (IsOver) return;

            IsOver = true;
            HasEscaped = true;
            Escaped?.Invoke();
        }

        /// <summary>Restores a saved run.</summary>
        public void Restore(int day)
        {
            Day = Mathf.Clamp(day, 1, daysAllowed);
            IsOver = false;
            HasEscaped = false;
        }

        public void Reset()
        {
            Day = 1;
            IsOver = false;
            HasEscaped = false;
        }
    }
}
