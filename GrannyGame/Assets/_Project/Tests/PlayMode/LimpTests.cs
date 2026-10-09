using System.Collections;
using Granny.Core;
using Granny.Gameplay.Player;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

namespace Granny.Tests
{
    /// <summary>
    /// The price of walking into a bear trap, paid after the jaws let go.
    ///
    /// The holding is only the announcement; the limp is what actually costs the
    /// player, because for twenty seconds they cannot outrun her.
    /// </summary>
    public class LimpTests : InputTestFixture
    {
        const string PrefabPath = "Assets/_Project/Prefabs/Player.prefab";

        GameObject floor;
        GameObject player;
        PlayerMotor motor;
        Keyboard keyboard;

        public override void Setup()
        {
            base.Setup();

            keyboard = InputSystem.AddDevice<Keyboard>();

            floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "TestFloor";
            floor.layer = GameLayers.LevelGeometry;
            floor.transform.position = new Vector3(0f, -0.5f, 0f);
            floor.transform.localScale = new Vector3(60f, 1f, 60f);

            GameObject prefab = null;
#if UNITY_EDITOR
            prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
#endif
            Assert.IsNotNull(prefab, $"No player prefab at {PrefabPath}.");

            player = Object.Instantiate(prefab, new Vector3(0f, 0.2f, 0f), Quaternion.identity);
            motor = player.GetComponent<PlayerMotor>();
        }

        public override void TearDown()
        {
            if (player != null) Object.DestroyImmediate(player);
            if (floor != null) Object.DestroyImmediate(floor);
            base.TearDown();
        }

        void Hold(params Key[] keys) => InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys));

        /// <summary>Average speed over <paramref name="seconds"/> of holding W.</summary>
        IEnumerator MeasureWalk(float seconds, System.Action<float> report)
        {
            Hold(Key.W);

            var settle = 0f;
            while (settle < 0.5f) { settle += Time.deltaTime; yield return null; }

            var from = player.transform.position;
            var elapsed = 0f;
            while (elapsed < seconds) { elapsed += Time.deltaTime; yield return null; }

            report(Vector3.Distance(from, player.transform.position) / elapsed);
            Hold();
            yield return null;
        }

        [UnityTest]
        public IEnumerator ALimpIsSlowerThanAWalk()
        {
            var walk = 0f;
            yield return MeasureWalk(0.6f, v => walk = v);

            motor.Limp(0.6f, 20f);
            Assert.IsTrue(motor.IsLimping);

            var limping = 0f;
            yield return MeasureWalk(0.6f, v => limping = v);

            Assert.Less(limping, walk * 0.8f,
                $"Limping {limping:F2} m/s should be clearly under walking {walk:F2} m/s.");
        }

        [UnityTest]
        public IEnumerator ALimpWearsOff()
        {
            motor.Limp(0.6f, 0.4f);
            Assert.IsTrue(motor.IsLimping);

            var elapsed = 0f;
            while (elapsed < 0.8f) { elapsed += Time.deltaTime; yield return null; }

            Assert.IsFalse(motor.IsLimping, "Twenty seconds, then it is over.");
        }

        [UnityTest]
        public IEnumerator WakingUpInBedStartsTheDayWhole()
        {
            motor.Limp(0.6f, 20f);
            motor.Warp(new Vector3(5f, 0.2f, 5f), 0f);

            yield return null;

            Assert.IsFalse(motor.IsLimping, "A new morning is a new leg.");
        }
    }
}
