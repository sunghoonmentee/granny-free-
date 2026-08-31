using Granny.Core;
using NUnit.Framework;
using UnityEngine;

namespace Granny.Tests
{
    public class NoiseBusTests
    {
        // The bus is static, so every test here unsubscribes its own listener;
        // one left behind would leak into whatever runs next.

        [Test]
        public void EmittedNoiseReachesSubscribers()
        {
            Noise? received = null;
            void Listener(Noise n) => received = n;

            NoiseBus.Heard += Listener;
            try
            {
                NoiseBus.Emit(new Vector3(1f, 0f, 2f), 8f, NoiseKind.Door);
            }
            finally
            {
                NoiseBus.Heard -= Listener;
            }

            Assert.IsTrue(received.HasValue);
            Assert.AreEqual(new Vector3(1f, 0f, 2f), received.Value.Position);
            Assert.AreEqual(8f, received.Value.Radius, 1e-4f);
            Assert.AreEqual(NoiseKind.Door, received.Value.Kind);
        }

        [Test]
        public void UnsubscribedListenersStopReceiving()
        {
            var count = 0;
            void Listener(Noise _) => count++;

            NoiseBus.Heard += Listener;
            NoiseBus.Emit(Vector3.zero, 1f, NoiseKind.Impact);
            NoiseBus.Heard -= Listener;
            NoiseBus.Emit(Vector3.zero, 1f, NoiseKind.Impact);

            Assert.AreEqual(1, count);
        }

        [Test]
        public void EmittingWithNoListenersIsHarmless()
        {
            Assert.DoesNotThrow(() => NoiseBus.Emit(Vector3.zero, 5f, NoiseKind.Footstep));
        }

        [Test]
        public void ReachesComparesAgainstTheRadius()
        {
            var noise = new Noise(Vector3.zero, 5f, NoiseKind.Footstep);

            Assert.IsTrue(noise.Reaches(new Vector3(3f, 0f, 4f)), "Exactly on the radius should count.");
            Assert.IsTrue(noise.Reaches(new Vector3(1f, 0f, 1f)));
            Assert.IsFalse(noise.Reaches(new Vector3(0f, 0f, 5.01f)));
        }

        [Test]
        public void NegativeRadiusIsClampedToZero()
        {
            var noise = new Noise(Vector3.zero, -4f, NoiseKind.Voice);

            Assert.AreEqual(0f, noise.Radius);
            Assert.IsFalse(noise.Reaches(new Vector3(0.1f, 0f, 0f)));
        }
    }
}
