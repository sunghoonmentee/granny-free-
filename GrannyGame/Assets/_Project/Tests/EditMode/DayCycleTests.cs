using Granny.Core;
using NUnit.Framework;

namespace Granny.Tests
{
    /// <summary>
    /// Being caught costs a day rather than ending the run, and running out of
    /// days is the only real failure. These pin both edges of that rule.
    /// </summary>
    public class DayCycleTests
    {
        [Test]
        public void StartsOnDayOne()
        {
            var days = new DayCycle(5);

            Assert.AreEqual(1, days.Day);
            Assert.AreEqual(4, days.DaysRemaining);
            Assert.IsFalse(days.IsOver);
            Assert.IsFalse(days.IsFinalDay);
        }

        [Test]
        public void BeingCaughtAdvancesTheDay()
        {
            var days = new DayCycle(5);
            var started = 0;
            days.DayStarted += _ => started++;

            days.Caught();

            Assert.AreEqual(2, days.Day);
            Assert.AreEqual(1, started);
            Assert.IsFalse(days.IsOver);
        }

        [Test]
        public void BeingCaughtOnTheLastDayEndsTheRun()
        {
            var days = new DayCycle(3);
            var over = 0;
            days.GameOver += () => over++;

            days.Caught();   // day 2
            days.Caught();   // day 3, the last
            Assert.IsTrue(days.IsFinalDay);
            Assert.IsFalse(days.IsOver);

            days.Caught();   // no day 4

            Assert.IsTrue(days.IsOver);
            Assert.AreEqual(3, days.Day, "The counter must not run past the allowance.");
            Assert.AreEqual(1, over);
        }

        [Test]
        public void NothingHappensAfterTheRunIsOver()
        {
            var days = new DayCycle(1);
            var over = 0;
            days.GameOver += () => over++;

            days.Caught();
            days.Caught();
            days.Caught();

            Assert.AreEqual(1, over, "Game over must be announced exactly once.");
        }

        [Test]
        public void EscapingEndsTheRunAsAWin()
        {
            var days = new DayCycle(5);
            var escaped = 0;
            days.Escaped += () => escaped++;

            days.Escape();

            Assert.IsTrue(days.IsOver);
            Assert.IsTrue(days.HasEscaped);
            Assert.AreEqual(1, escaped);
        }

        [Test]
        public void BeingCaughtAfterEscapingChangesNothing()
        {
            var days = new DayCycle(5);
            days.Escape();

            days.Caught();

            Assert.AreEqual(1, days.Day);
            Assert.IsTrue(days.HasEscaped);
        }

        [Test]
        public void RestoreClampsToTheAllowance()
        {
            var days = new DayCycle(5);

            days.Restore(4);
            Assert.AreEqual(4, days.Day);

            days.Restore(99);
            Assert.AreEqual(5, days.Day);

            days.Restore(0);
            Assert.AreEqual(1, days.Day);
        }

        [Test]
        public void ResetReturnsToDayOne()
        {
            var days = new DayCycle(2);
            days.Caught();
            days.Caught();
            Assert.IsTrue(days.IsOver);

            days.Reset();

            Assert.AreEqual(1, days.Day);
            Assert.IsFalse(days.IsOver);
            Assert.IsFalse(days.HasEscaped);
        }

        [Test]
        public void AZeroDayAllowanceIsTreatedAsOne()
        {
            var days = new DayCycle(0);

            Assert.AreEqual(1, days.DaysAllowed);
            Assert.IsTrue(days.IsFinalDay);
        }
    }
}
