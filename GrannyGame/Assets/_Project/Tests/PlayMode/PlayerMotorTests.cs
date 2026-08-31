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
    /// Drives the real player prefab with a virtual keyboard and checks that it
    /// moves the way the stealth loop requires: crouching slower than walking,
    /// walking slower than sprinting, and sprinting collapsing back to a walk once
    /// the stamina pool runs dry.
    /// </summary>
    public class PlayerMotorTests : InputTestFixture
    {
        const string PrefabPath = "Assets/_Project/Prefabs/Player.prefab";

        GameObject floor;
        GameObject player;
        PlayerMotor motor;
        Keyboard keyboard;

        /// <summary>Average speed measured by the most recent <see cref="TravelFor"/>.</summary>
        float measuredSpeed;

        public override void Setup()
        {
            base.Setup();

            keyboard = InputSystem.AddDevice<Keyboard>();

            floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "TestFloor";
            floor.layer = GameLayers.LevelGeometry;
            floor.transform.position = new Vector3(0f, -0.5f, 0f);
            floor.transform.localScale = new Vector3(60f, 1f, 60f);

            // This assembly must stay a *runtime* assembly, or the EditMode runner
            // claims these tests and InputTestFixture refuses to simulate devices.
            // That rules out an unguarded AssetDatabase reference.
            GameObject prefab = null;
#if UNITY_EDITOR
            prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
#endif
            Assert.IsNotNull(prefab, $"No player prefab at {PrefabPath}. Run Granny > Build Player Rig.");

            player = Object.Instantiate(prefab, new Vector3(0f, 0.2f, 0f), Quaternion.identity);
            motor = player.GetComponent<PlayerMotor>();
        }

        public override void TearDown()
        {
            if (player != null) Object.DestroyImmediate(player);
            if (floor != null) Object.DestroyImmediate(floor);
            base.TearDown();
        }

        /// <summary>
        /// Sets exactly which keys are held, replacing whatever was held before.
        ///
        /// InputTestFixture.Press queues a *delta* state event, and keyboard keys
        /// are bit-packed: the event carries a stale copy of every other key in
        /// the same word. Pressing W and then Shift in a [UnityTest] - where the
        /// events are queued and only processed on the next frame - therefore
        /// silently released W. One full KeyboardState per change avoids that.
        /// </summary>
        void Hold(params Key[] keys)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys));
        }

        static IEnumerator Wait(float seconds)
        {
            var elapsed = 0f;
            while (elapsed < seconds)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }
        }

        /// <summary>
        /// Holds <paramref name="keys"/>, lets the motor finish accelerating, then
        /// records the average speed over <paramref name="seconds"/>.
        /// </summary>
        IEnumerator TravelFor(float seconds, params Key[] keys)
        {
            Hold(keys);

            yield return Wait(0.5f);

            var start = player.transform.position;
            var elapsed = 0f;
            while (elapsed < seconds)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }

            measuredSpeed = Vector3.Distance(start, player.transform.position) / elapsed;

            Hold();
            yield return null;
        }

        [UnityTest]
        public IEnumerator WalkingMovesForward()
        {
            var start = player.transform.position;
            yield return TravelFor(0.5f, Key.W);

            Assert.Greater(Vector3.Distance(start, player.transform.position), 0.5f);
            Assert.Greater(measuredSpeed, 1.5f, "Walking should be around 2.6 m/s.");
        }

        [UnityTest]
        public IEnumerator CrouchingIsSlowerThanWalking()
        {
            yield return TravelFor(0.6f, Key.W);
            var walk = measuredSpeed;

            yield return TravelFor(0.6f, Key.W, Key.LeftCtrl);
            var crouch = measuredSpeed;

            Assert.Less(crouch, walk * 0.75f,
                $"Crouch {crouch:F2} m/s should be well under walk {walk:F2} m/s.");
        }

        [UnityTest]
        public IEnumerator SprintingIsFasterThanWalking()
        {
            yield return TravelFor(0.6f, Key.W);
            var walk = measuredSpeed;

            yield return TravelFor(0.6f, Key.W, Key.LeftShift);
            var sprint = measuredSpeed;

            Assert.Greater(sprint, walk * 1.3f,
                $"Sprint {sprint:F2} m/s should clearly beat walk {walk:F2} m/s.");
        }

        [UnityTest]
        public IEnumerator CrouchingReportsTheCrouchState()
        {
            Hold(Key.LeftCtrl, Key.W);
            yield return Wait(0.5f);

            Assert.IsTrue(motor.IsCrouching);
            Assert.AreEqual(MoveState.Crouching, motor.State);

            Hold();
        }

        [UnityTest]
        public IEnumerator SprintingDrainsStaminaAndEventuallyStops()
        {
            Hold(Key.W, Key.LeftShift);

            var elapsed = 0f;
            while (elapsed < 10f && !motor.IsExhausted)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }

            Assert.IsTrue(motor.IsExhausted, "Stamina should run out within 10 seconds of sprinting.");
            Assert.AreEqual(0f, motor.StaminaNormalized, 0.01f);

            // Sprint is still held, but the motor must have dropped back to a walk.
            yield return null;
            Assert.AreEqual(MoveState.Walking, motor.State);

            Hold();
        }

        [UnityTest]
        public IEnumerator StandingStillReportsIdle()
        {
            yield return Wait(0.4f);
            Assert.AreEqual(MoveState.Idle, motor.State);
        }

        [UnityTest]
        public IEnumerator WarpMovesThePlayerAndRefillsStamina()
        {
            Hold(Key.W, Key.LeftShift);
            yield return Wait(2f);
            Hold();

            Assert.Less(motor.StaminaNormalized, 0.95f);

            motor.Warp(new Vector3(12f, 0.2f, -6f), 90f);
            yield return null;

            Assert.AreEqual(12f, player.transform.position.x, 0.3f);
            Assert.AreEqual(1f, motor.StaminaNormalized, 0.01f);
            Assert.IsFalse(motor.IsCrouching);
        }
    }
}
