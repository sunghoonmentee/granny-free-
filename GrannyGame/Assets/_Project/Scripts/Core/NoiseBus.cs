using System;
using UnityEngine;

namespace Granny.Core
{
    /// <summary>
    /// What made the sound. Kinds are actions, not loudness levels: whether a kind
    /// is heard at all is decided once, in <see cref="NoiseRules"/>.
    /// </summary>
    public enum NoiseKind
    {
        /// <summary>Walking, running, crouching. Never heard — the original rule.</summary>
        Footstep,

        /// <summary>A door eased open or shut.</summary>
        DoorMove,

        /// <summary>A door thrown open or slammed.</summary>
        DoorSlam,

        /// <summary>A dropped or thrown item landing hard.</summary>
        ItemImpact,

        /// <summary>Glass, a jar, a vase — anything that shatters.</summary>
        Breakage,

        /// <summary>Hammering, prying, a lock clattering to the floor.</summary>
        ToolWork,

        /// <summary>A bear trap snapping shut.</summary>
        Trap,

        /// <summary>A drawer or cabinet sliding.</summary>
        Container,

        /// <summary>Climbing into or out of a wardrobe or under a bed.</summary>
        Hiding,

        /// <summary>Trying a locked door.</summary>
        LockedRattle,

        /// <summary>A floorboard giving under someone's weight.</summary>
        Creak,

        /// <summary>A bell on a tripwire.</summary>
        Bell,
    }

    /// <summary>One sound, somewhere in the house.</summary>
    public readonly struct Noise
    {
        public readonly Vector3 Position;

        /// <summary>Floor index from <see cref="HouseLayout.FloorOf(Vector3)"/>.</summary>
        public readonly int Floor;

        public readonly NoiseKind Kind;

        /// <summary>The object that made it, so a listener can ignore its own noise.</summary>
        public readonly GameObject Source;

        /// <summary>Time.time when it was made.</summary>
        public readonly float Time;

        public Noise(Vector3 position, NoiseKind kind, GameObject source = null)
        {
            Position = position;
            Floor = HouseLayout.FloorOf(position);
            Kind = kind;
            Source = source;
            Time = UnityEngine.Time.time;
        }

        public override string ToString() =>
            $"{Kind} @ {HouseLayout.FloorName(Floor)} ({Position.x:F1}, {Position.y:F1}, {Position.z:F1})";
    }

    /// <summary>
    /// Which sounds she hears. This is the one table to change to retune it.
    ///
    /// The rule from the original: she hears the house, not the player. Moving —
    /// at any speed — makes no sound she reacts to; doing something does. And an
    /// action she hears, she hears from anywhere in the house, on any floor.
    /// </summary>
    public static class NoiseRules
    {
        /// <summary>
        /// Easing a door open is silent in the original, and in the design doc.
        /// Slamming one is not. Flip this to make ordinary door use audible.
        /// </summary>
        public const bool DoorMovementAudible = false;

        public static bool IsAudible(NoiseKind kind) => kind switch
        {
            NoiseKind.DoorSlam => true,
            NoiseKind.ItemImpact => true,
            NoiseKind.Breakage => true,
            NoiseKind.ToolWork => true,
            NoiseKind.Trap => true,
            NoiseKind.Creak => true,
            NoiseKind.Bell => true,
            NoiseKind.DoorMove => DoorMovementAudible,
            _ => false,
        };
    }

    /// <summary>
    /// The house's one channel for sound. Everything that makes a noise publishes
    /// here; everything that listens subscribes here. Nothing on either side knows
    /// about the other.
    ///
    /// Filtering happens at the bus, not at the listener, so a sound that should
    /// never be heard cannot leak into a new listener that forgets to check.
    /// </summary>
    public static class NoiseBus
    {
        /// <summary>Raised for every audible noise, wherever it happened.</summary>
        public static event Action<Noise> Heard;

        /// <summary>Raised for noises dropped by <see cref="NoiseRules"/>. For debugging and tests.</summary>
        public static event Action<Noise> Filtered;

        /// <summary>Broadcasts a noise. Returns whether it was audible.</summary>
        public static bool Emit(Vector3 position, NoiseKind kind, GameObject source = null)
        {
            var noise = new Noise(position, kind, source);

            if (!NoiseRules.IsAudible(kind))
            {
                Filtered?.Invoke(noise);
                return false;
            }

            Heard?.Invoke(noise);
            return true;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetOnLoad()
        {
            Heard = null;
            Filtered = null;
        }
    }
}
