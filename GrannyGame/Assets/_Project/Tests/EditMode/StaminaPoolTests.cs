using Granny.Core;
using NUnit.Framework;

namespace Granny.Tests
{
    public class StaminaPoolTests
    {
        static StaminaPool MakePool() => new(
            max: 4f,
            drainPerSecond: 1f,
            regenPerSecond: 1f,
            regenDelay: 1f,
            recoveryFraction: 0.5f);

        static void Run(StaminaPool pool, float seconds, bool sprinting, float step = 0.05f)
        {
            for (var t = 0f; t < seconds; t += step)
                pool.Tick(step, sprinting);
        }

        [Test]
        public void StartsFull()
        {
            Assert.AreEqual(1f, MakePool().Normalized, 1e-4f);
            Assert.IsFalse(MakePool().IsExhausted);
        }

        [Test]
        public void SprintingDrains()
        {
            var pool = MakePool();
            Assert.IsTrue(pool.Tick(1f, wantsToSprint: true));
            Assert.AreEqual(3f, pool.Current, 1e-4f);
        }

        [Test]
        public void EmptyingThePoolLocksOutSprinting()
        {
            var pool = MakePool();
            Run(pool, seconds: 5f, sprinting: true);

            Assert.IsTrue(pool.IsExhausted);
            Assert.AreEqual(0f, pool.Current, 1e-4f);
            Assert.IsFalse(pool.Tick(0.05f, wantsToSprint: true), "Sprint must stay denied while exhausted.");
        }

        [Test]
        public void ExhaustionPersistsUntilTheRecoveryFractionIsReached()
        {
            var pool = MakePool();
            Run(pool, seconds: 5f, sprinting: true);

            // Past the regen delay, but still below 50% of a 4s pool.
            Run(pool, seconds: 1.5f, sprinting: false);
            Assert.IsTrue(pool.IsExhausted, $"Still exhausted at {pool.Normalized:P0}.");

            Run(pool, seconds: 2f, sprinting: false);
            Assert.IsFalse(pool.IsExhausted, $"Should have recovered by {pool.Normalized:P0}.");
            Assert.IsTrue(pool.Tick(0.05f, wantsToSprint: true));
        }

        [Test]
        public void RegenWaitsForTheDelay()
        {
            var pool = MakePool();
            pool.Tick(1f, wantsToSprint: true);
            var afterDrain = pool.Current;

            Run(pool, seconds: 0.5f, sprinting: false);
            Assert.AreEqual(afterDrain, pool.Current, 1e-3f, "Regen started before the delay elapsed.");
        }

        [Test]
        public void RegenStopsAtMax()
        {
            var pool = MakePool();
            Run(pool, seconds: 30f, sprinting: false);
            Assert.AreEqual(pool.Max, pool.Current, 1e-4f);
        }

        [Test]
        public void NotSprintingNeverReportsSprinting()
        {
            var pool = MakePool();
            Assert.IsFalse(pool.Tick(0.1f, wantsToSprint: false));
        }

        [Test]
        public void RefillClearsExhaustion()
        {
            var pool = MakePool();
            Run(pool, seconds: 5f, sprinting: true);
            Assert.IsTrue(pool.IsExhausted);

            pool.Refill();

            Assert.IsFalse(pool.IsExhausted);
            Assert.AreEqual(1f, pool.Normalized, 1e-4f);
        }

        [Test]
        public void NegativeDeltaTimeIsIgnored()
        {
            var pool = MakePool();
            pool.Tick(-1f, wantsToSprint: true);
            Assert.AreEqual(pool.Max, pool.Current, 1e-4f);
        }
    }
}
