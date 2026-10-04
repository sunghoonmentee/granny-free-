using Granny.Core;
using NUnit.Framework;
using UnityEngine;

namespace Granny.Tests
{
    /// <summary>
    /// The noise rules are the game's difficulty. Getting them wrong is not a
    /// crash — it is a hunter who sits in the basement all run, or one who comes
    /// running every time you take a step.
    /// </summary>
    public class NoiseBusTests
    {
        // The bus is static, so every test here unsubscribes its own listener;
        // one left behind would leak into whatever runs next.

        static (Noise? heard, Noise? filtered) Capture(Vector3 position, NoiseKind kind)
        {
            Noise? heard = null;
            Noise? filtered = null;

            void OnHeard(Noise n) => heard = n;
            void OnFiltered(Noise n) => filtered = n;

            NoiseBus.Heard += OnHeard;
            NoiseBus.Filtered += OnFiltered;
            try
            {
                NoiseBus.Emit(position, kind);
            }
            finally
            {
                NoiseBus.Heard -= OnHeard;
                NoiseBus.Filtered -= OnFiltered;
            }

            return (heard, filtered);
        }

        [Test]
        public void AnAudibleNoiseCarriesItsPlaceAndFloor()
        {
            var (heard, _) = Capture(new Vector3(1f, 3.2f, 2f), NoiseKind.Breakage);

            Assert.IsTrue(heard.HasValue);
            Assert.AreEqual(new Vector3(1f, 3.2f, 2f), heard.Value.Position);
            Assert.AreEqual(HouseLayout.Upper, heard.Value.Floor);
            Assert.AreEqual(NoiseKind.Breakage, heard.Value.Kind);
        }

        [Test]
        public void EveryActionSheShouldHearIsHeard()
        {
            foreach (var kind in new[]
                     {
                         NoiseKind.DoorSlam, NoiseKind.ItemImpact, NoiseKind.Breakage,
                         NoiseKind.ToolWork, NoiseKind.Trap,
                     })
            {
                var (heard, _) = Capture(Vector3.zero, kind);
                Assert.IsTrue(heard.HasValue, $"{kind} should reach her.");
            }
        }

        [Test]
        public void MovingIsNeverHeard()
        {
            // The rule the whole stealth loop rests on: she hears the house, not
            // the player. Walking and running make no sound she reacts to.
            var (heard, filtered) = Capture(Vector3.zero, NoiseKind.Footstep);

            Assert.IsFalse(heard.HasValue, "Footsteps must never reach her.");
            Assert.IsTrue(filtered.HasValue, "...but they should still be reported for debugging.");
        }

        [Test]
        public void QuietActionsAreFilteredOut()
        {
            foreach (var kind in new[]
                     {
                         NoiseKind.Footstep, NoiseKind.DoorMove, NoiseKind.Container,
                         NoiseKind.Hiding, NoiseKind.LockedRattle,
                     })
            {
                var (heard, _) = Capture(Vector3.zero, kind);
                Assert.IsFalse(heard.HasValue, $"{kind} should not reach her.");
            }
        }

        [Test]
        public void EasingADoorIsQuietButSlammingItIsNot()
        {
            Assert.IsFalse(NoiseRules.IsAudible(NoiseKind.DoorMove));
            Assert.IsTrue(NoiseRules.IsAudible(NoiseKind.DoorSlam));
        }

        [Test]
        public void DistanceAndFloorDoNotMatter()
        {
            // There is no hearing range. Anywhere in the house, any floor.
            foreach (var position in new[]
                     {
                         new Vector3(0f, -3.2f, 0f), new Vector3(200f, 6.4f, -200f),
                     })
            {
                var (heard, _) = Capture(position, NoiseKind.Breakage);
                Assert.IsTrue(heard.HasValue, $"A noise at {position} must still be heard.");
            }
        }

        [Test]
        public void EmitReportsWhetherItWasAudible()
        {
            Assert.IsTrue(NoiseBus.Emit(Vector3.zero, NoiseKind.Breakage));
            Assert.IsFalse(NoiseBus.Emit(Vector3.zero, NoiseKind.Footstep));
        }

        [Test]
        public void UnsubscribedListenersStopReceiving()
        {
            var count = 0;
            void Listener(Noise _) => count++;

            NoiseBus.Heard += Listener;
            NoiseBus.Emit(Vector3.zero, NoiseKind.ItemImpact);
            NoiseBus.Heard -= Listener;
            NoiseBus.Emit(Vector3.zero, NoiseKind.ItemImpact);

            Assert.AreEqual(1, count);
        }

        [Test]
        public void EmittingWithNoListenersIsHarmless()
        {
            Assert.DoesNotThrow(() => NoiseBus.Emit(Vector3.zero, NoiseKind.Breakage));
        }

        [Test]
        public void FloorsAreReadOffTheHeight()
        {
            Assert.AreEqual(HouseLayout.Basement, HouseLayout.FloorOf(-3.2f));
            Assert.AreEqual(HouseLayout.Ground, HouseLayout.FloorOf(0f));
            Assert.AreEqual(HouseLayout.Ground, HouseLayout.FloorOf(-0.05f), "A foot below the floor is still that floor.");
            Assert.AreEqual(HouseLayout.Upper, HouseLayout.FloorOf(3.2f));
            Assert.AreEqual(HouseLayout.Attic, HouseLayout.FloorOf(6.4f));
        }
    }
}
