using System.IO;
using Granny.Core;
using UnityEditor;
using UnityEngine;

namespace Granny.EditorTools
{
    /// <summary>
    /// Writes the game's placeholder sounds as .wav files, from arithmetic.
    ///
    /// The game is about sound and had none, and fetching recordings needs the
    /// player's say-so every time. Synthesising them keeps the project buildable
    /// from source with nothing downloaded: the same bargain the greybox house
    /// makes with geometry. These are stand-ins with the right shape and length,
    /// meant to be swapped for real recordings later — the sound bank is the seam
    /// where that swap happens, and nothing else needs to change.
    ///
    ///     ./tools/unity.ps1 run -Method Granny.EditorTools.AudioBuilder.Run
    /// </summary>
    public static class AudioBuilder
    {
        const string AudioDir = "Assets/_Project/Audio";
        const int SampleRate = 44100;

        [MenuItem("Granny/Build Audio", priority = 25)]
        public static void Run()
        {
            Debug.Log("[Audio] start");

            if (!AssetDatabase.IsValidFolder(AudioDir))
                AssetDatabase.CreateFolder("Assets/_Project", "Audio");

            Write("creak", Creak());
            Write("bell", Bell());
            Write("trap", TrapSnap());
            Write("breakage", Breakage());
            Write("item_impact", ItemImpact());
            Write("tool_work", ToolWork());
            Write("door_move", DoorMove());
            Write("door_slam", DoorSlam());
            Write("container", Container());
            Write("hiding", Hiding());
            Write("locked", LockedRattle());
            Write("cane", CaneTap());
            Write("heartbeat", Heartbeat());
            Write("clock", ClockTick());

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            BuildBank();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Audio] done");
        }

        /// <summary>
        /// Which clip answers which kind of noise, and how loud.
        ///
        /// Quiet entries are not an oversight: a drawer and a hinge are things
        /// only the person touching them hears, so they sit low in the mix while
        /// the sounds she comes running for sit on top of it.
        /// </summary>
        static readonly (NoiseKind kind, string clip, float volume, float jitter)[] BankTable =
        {
            (NoiseKind.Creak, "creak", 0.85f, 0.14f),
            (NoiseKind.Bell, "bell", 1.00f, 0.03f),
            (NoiseKind.Trap, "trap", 1.00f, 0.06f),
            (NoiseKind.Breakage, "breakage", 1.00f, 0.10f),
            (NoiseKind.ItemImpact, "item_impact", 0.80f, 0.16f),
            (NoiseKind.ToolWork, "tool_work", 0.90f, 0.12f),
            (NoiseKind.DoorSlam, "door_slam", 1.00f, 0.08f),
            (NoiseKind.DoorMove, "door_move", 0.35f, 0.18f),
            (NoiseKind.Container, "container", 0.40f, 0.15f),
            (NoiseKind.Hiding, "hiding", 0.35f, 0.10f),
            (NoiseKind.LockedRattle, "locked", 0.55f, 0.12f),
            // Footsteps stay silent here on purpose. Walking makes no sound she
            // reacts to, and giving it one would teach the player the opposite.
        };

        static void BuildBank()
        {
            const string path = AudioDir + "/SoundBank.asset";

            var bank = AssetDatabase.LoadAssetAtPath<SoundBank>(path);
            if (bank == null)
            {
                bank = ScriptableObject.CreateInstance<SoundBank>();
                AssetDatabase.CreateAsset(bank, path);
            }

            var so = new SerializedObject(bank);
            var entries = so.FindProperty("entries");
            entries.arraySize = BankTable.Length;

            for (var i = 0; i < BankTable.Length; i++)
            {
                var row = BankTable[i];
                var entry = entries.GetArrayElementAtIndex(i);

                entry.FindPropertyRelative("kind").enumValueIndex = (int)row.kind;
                entry.FindPropertyRelative("clip").objectReferenceValue = Clip(row.clip);
                entry.FindPropertyRelative("volume").floatValue = row.volume;
                entry.FindPropertyRelative("pitchJitter").floatValue = row.jitter;
            }

            so.FindProperty("cane").objectReferenceValue = Clip("cane");
            so.FindProperty("heartbeat").objectReferenceValue = Clip("heartbeat");
            so.FindProperty("clock").objectReferenceValue = Clip("clock");
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(bank);
            Debug.Log($"[Audio] bank written with {BankTable.Length} kinds");
        }

        static AudioClip Clip(string name) =>
            AssetDatabase.LoadAssetAtPath<AudioClip>($"{AudioDir}/{name}.wav");

        // ------------------------------------------------------------------
        // The sounds
        // ------------------------------------------------------------------

        /// <summary>
        /// A floorboard: a short groan that bends in pitch as the weight settles.
        /// The bend is what makes it read as wood rather than a beep.
        /// </summary>
        static float[] Creak()
        {
            var samples = Buffer(0.45f);
            var rng = new System.Random(11);

            for (var i = 0; i < samples.Length; i++)
            {
                var t = (float)i / SampleRate;
                var life = (float)i / samples.Length;

                var pitch = Mathf.Lerp(150f, 260f, Mathf.Sqrt(life));
                var tone = Mathf.Sin(2f * Mathf.PI * pitch * t);
                var rasp = (float)(rng.NextDouble() * 2.0 - 1.0) * 0.35f;

                samples[i] = (tone * 0.6f + rasp) * Envelope(life, 0.08f, 0.5f) * 0.5f;
            }

            return samples;
        }

        /// <summary>A bell: two close partials ringing out over a second and a half.</summary>
        static float[] Bell()
        {
            var samples = Buffer(1.6f);

            for (var i = 0; i < samples.Length; i++)
            {
                var t = (float)i / SampleRate;
                var life = (float)i / samples.Length;

                var body = Mathf.Sin(2f * Mathf.PI * 880f * t);
                var shimmer = Mathf.Sin(2f * Mathf.PI * 1319f * t) * 0.5f;

                samples[i] = (body + shimmer) * Mathf.Exp(-4f * life) * 0.45f;
            }

            return samples;
        }

        /// <summary>Iron closing: an instant crack with a metallic ring behind it.</summary>
        static float[] TrapSnap()
        {
            var samples = Buffer(0.6f);
            var rng = new System.Random(27);

            for (var i = 0; i < samples.Length; i++)
            {
                var t = (float)i / SampleRate;
                var life = (float)i / samples.Length;

                var crack = (float)(rng.NextDouble() * 2.0 - 1.0) * Mathf.Exp(-60f * life);
                var ring = Mathf.Sin(2f * Mathf.PI * 2100f * t) * Mathf.Exp(-9f * life) * 0.4f;

                samples[i] = (crack + ring) * 0.8f;
            }

            return samples;
        }

        /// <summary>Glass: a bright burst that falls apart into shards.</summary>
        static float[] Breakage()
        {
            var samples = Buffer(0.9f);
            var rng = new System.Random(41);

            for (var i = 0; i < samples.Length; i++)
            {
                var t = (float)i / SampleRate;
                var life = (float)i / samples.Length;

                var burst = (float)(rng.NextDouble() * 2.0 - 1.0) * Mathf.Exp(-14f * life);

                // A few shards still tumbling after the break.
                var shards = 0f;
                for (var h = 1; h <= 4; h++)
                    shards += Mathf.Sin(2f * Mathf.PI * (2400f + h * 900f) * t) *
                              Mathf.Exp(-6f * life) * 0.12f;

                samples[i] = (burst + shards) * 0.75f;
            }

            return samples;
        }

        /// <summary>Something heavy landing on boards.</summary>
        static float[] ItemImpact()
        {
            var samples = Buffer(0.35f);
            var rng = new System.Random(53);

            for (var i = 0; i < samples.Length; i++)
            {
                var t = (float)i / SampleRate;
                var life = (float)i / samples.Length;

                var thud = Mathf.Sin(2f * Mathf.PI * 90f * t) * Mathf.Exp(-18f * life);
                var knock = (float)(rng.NextDouble() * 2.0 - 1.0) * Mathf.Exp(-45f * life) * 0.5f;

                samples[i] = (thud + knock) * 0.8f;
            }

            return samples;
        }

        /// <summary>A hammer blow, or a prybar working a nail.</summary>
        static float[] ToolWork()
        {
            var samples = Buffer(0.3f);
            var rng = new System.Random(67);

            for (var i = 0; i < samples.Length; i++)
            {
                var t = (float)i / SampleRate;
                var life = (float)i / samples.Length;

                var strike = (float)(rng.NextDouble() * 2.0 - 1.0) * Mathf.Exp(-50f * life);
                var metal = Mathf.Sin(2f * Mathf.PI * 1500f * t) * Mathf.Exp(-22f * life) * 0.45f;

                samples[i] = (strike + metal) * 0.85f;
            }

            return samples;
        }

        /// <summary>A hinge turning slowly. Quiet, and she never hears it.</summary>
        static float[] DoorMove()
        {
            var samples = Buffer(0.7f);

            for (var i = 0; i < samples.Length; i++)
            {
                var t = (float)i / SampleRate;
                var life = (float)i / samples.Length;

                var pitch = Mathf.Lerp(320f, 210f, life);
                samples[i] = Mathf.Sin(2f * Mathf.PI * pitch * t) *
                             Envelope(life, 0.25f, 0.4f) * 0.18f;
            }

            return samples;
        }

        /// <summary>A door thrown shut: the one door sound she does hear.</summary>
        static float[] DoorSlam()
        {
            var samples = Buffer(0.5f);
            var rng = new System.Random(79);

            for (var i = 0; i < samples.Length; i++)
            {
                var t = (float)i / SampleRate;
                var life = (float)i / samples.Length;

                var boom = Mathf.Sin(2f * Mathf.PI * 65f * t) * Mathf.Exp(-12f * life);
                var clap = (float)(rng.NextDouble() * 2.0 - 1.0) * Mathf.Exp(-40f * life) * 0.6f;

                samples[i] = (boom + clap) * 0.9f;
            }

            return samples;
        }

        /// <summary>A drawer sliding on its runners.</summary>
        static float[] Container()
        {
            var samples = Buffer(0.55f);
            var rng = new System.Random(83);
            var rumble = 0f;

            for (var i = 0; i < samples.Length; i++)
            {
                var life = (float)i / samples.Length;

                // A lazy low-pass over noise, which is roughly what a drawer is.
                rumble = Mathf.Lerp(rumble, (float)(rng.NextDouble() * 2.0 - 1.0), 0.06f);
                samples[i] = rumble * Envelope(life, 0.15f, 0.45f) * 0.5f;
            }

            return samples;
        }

        /// <summary>Cloth and a held breath: climbing into somewhere to wait.</summary>
        static float[] Hiding()
        {
            var samples = Buffer(0.6f);
            var rng = new System.Random(97);
            var soft = 0f;

            for (var i = 0; i < samples.Length; i++)
            {
                var life = (float)i / samples.Length;

                soft = Mathf.Lerp(soft, (float)(rng.NextDouble() * 2.0 - 1.0), 0.02f);
                samples[i] = soft * Envelope(life, 0.3f, 0.5f) * 0.4f;
            }

            return samples;
        }

        /// <summary>A handle turning against a lock that will not give.</summary>
        static float[] LockedRattle()
        {
            var samples = Buffer(0.4f);
            var rng = new System.Random(101);

            for (var i = 0; i < samples.Length; i++)
            {
                var t = (float)i / SampleRate;
                var life = (float)i / samples.Length;

                // Three quick knocks rather than one.
                var knock = Mathf.Exp(-60f * Mathf.Repeat(life * 3f, 1f));
                var body = Mathf.Sin(2f * Mathf.PI * 240f * t) * 0.5f +
                           (float)(rng.NextDouble() * 2.0 - 1.0) * 0.5f;

                samples[i] = body * knock * 0.45f;
            }

            return samples;
        }

        /// <summary>
        /// Her stick on the boards. This is the one sound the player is meant to
        /// navigate by, so it is short, hard and easy to place.
        /// </summary>
        static float[] CaneTap()
        {
            var samples = Buffer(0.22f);
            var rng = new System.Random(113);

            for (var i = 0; i < samples.Length; i++)
            {
                var t = (float)i / SampleRate;
                var life = (float)i / samples.Length;

                var tap = Mathf.Sin(2f * Mathf.PI * 420f * t) * Mathf.Exp(-30f * life);
                var tip = (float)(rng.NextDouble() * 2.0 - 1.0) * Mathf.Exp(-70f * life) * 0.4f;

                samples[i] = (tap + tip) * 0.7f;
            }

            return samples;
        }

        /// <summary>Two beats, for when she is close and you are holding still.</summary>
        static float[] Heartbeat()
        {
            var samples = Buffer(1.0f);

            for (var i = 0; i < samples.Length; i++)
            {
                var t = (float)i / SampleRate;
                var life = (float)i / samples.Length;

                var first = Mathf.Exp(-26f * life);
                var second = life > 0.18f ? Mathf.Exp(-26f * (life - 0.18f)) * 0.75f : 0f;

                samples[i] = Mathf.Sin(2f * Mathf.PI * 52f * t) * (first + second) * 0.6f;
            }

            return samples;
        }

        /// <summary>The hall clock: the only thing in the house that never stops.</summary>
        static float[] ClockTick()
        {
            var samples = Buffer(1.0f);
            var rng = new System.Random(127);

            for (var i = 0; i < samples.Length; i++)
            {
                var life = (float)i / samples.Length;

                // Tick at the top of the second, tock halfway through.
                var tick = Mathf.Exp(-120f * life);
                var tock = life > 0.5f ? Mathf.Exp(-120f * (life - 0.5f)) * 0.7f : 0f;

                samples[i] = (float)(rng.NextDouble() * 2.0 - 1.0) * (tick + tock) * 0.35f;
            }

            return samples;
        }

        // ------------------------------------------------------------------
        // Plumbing
        // ------------------------------------------------------------------

        static float[] Buffer(float seconds) => new float[Mathf.CeilToInt(SampleRate * seconds)];

        /// <summary>Attack, hold, release — as fractions of the clip's life.</summary>
        static float Envelope(float life, float attack, float release)
        {
            if (life < attack) return life / attack;
            if (life > 1f - release) return (1f - life) / release;
            return 1f;
        }

        static void Write(string name, float[] samples)
        {
            var path = $"{AudioDir}/{name}.wav";
            File.WriteAllBytes(path, Wav(samples));
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        }

        /// <summary>A mono 16-bit PCM wav, which is the least a wav can be.</summary>
        static byte[] Wav(float[] samples)
        {
            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream);

            var dataBytes = samples.Length * 2;

            writer.Write(new[] { 'R', 'I', 'F', 'F' });
            writer.Write(36 + dataBytes);
            writer.Write(new[] { 'W', 'A', 'V', 'E' });

            writer.Write(new[] { 'f', 'm', 't', ' ' });
            writer.Write(16);                       // chunk size
            writer.Write((short)1);                 // PCM
            writer.Write((short)1);                 // mono
            writer.Write(SampleRate);
            writer.Write(SampleRate * 2);           // bytes per second
            writer.Write((short)2);                 // block align
            writer.Write((short)16);                // bits per sample

            writer.Write(new[] { 'd', 'a', 't', 'a' });
            writer.Write(dataBytes);

            foreach (var sample in samples)
                writer.Write((short)(Mathf.Clamp(sample, -1f, 1f) * short.MaxValue));

            writer.Flush();
            return stream.ToArray();
        }
    }
}
