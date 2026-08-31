using System;
using UnityEngine;

namespace Granny.Core
{
    /// <summary>What kind of sound was made. Used for tuning and for debug display.</summary>
    public enum NoiseKind
    {
        Footstep,
        Door,
        Impact,
        Breakage,
        Voice,
        Machine,
    }

    /// <summary>One thing that was heard, somewhere in the house.</summary>
    public readonly struct Noise
    {
        public readonly Vector3 Position;

        /// <summary>Metres at which this is still audible, before any obstruction.</summary>
        public readonly float Radius;

        public readonly NoiseKind Kind;

        /// <summary>The object that made it, so a listener can ignore its own noise.</summary>
        public readonly GameObject Source;

        public Noise(Vector3 position, float radius, NoiseKind kind, GameObject source = null)
        {
            Position = position;
            Radius = Mathf.Max(0f, radius);
            Kind = kind;
            Source = source;
        }

        public bool Reaches(Vector3 listener) =>
            (listener - Position).sqrMagnitude <= Radius * Radius;
    }

    /// <summary>
    /// A dropped bottle and a slammed door have nothing to do with each other, and
    /// neither should know that Granny exists. Everything that makes a sound
    /// publishes here; everything that listens subscribes here.
    ///
    /// Static because there is exactly one house and one set of ears. The scene
    /// hook below clears subscribers between play sessions so a stale listener
    /// from a previous run cannot keep receiving.
    /// </summary>
    public static class NoiseBus
    {
        /// <summary>Raised for every noise made anywhere, regardless of distance.</summary>
        public static event Action<Noise> Heard;

        public static void Emit(Noise noise) => Heard?.Invoke(noise);

        public static void Emit(Vector3 position, float radius, NoiseKind kind, GameObject source = null) =>
            Emit(new Noise(position, radius, kind, source));

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetOnLoad() => Heard = null;
    }
}
