using System.Collections;
using System.Collections.Generic;
using Granny.Core;
using Granny.Gameplay.Interaction;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Granny.Tests
{
    /// <summary>
    /// The house's own voice: boards that give you away, and a wire that does.
    ///
    /// Walking is silent everywhere else, so these are the only two things that
    /// turn moving around into a sound she can follow — which makes them the
    /// difference between a house you cross at will and one you have to learn.
    /// </summary>
    public class NoisyHouseTests
    {
        GameObject root;
        readonly List<Noise> heard = new();

        void Listen(Noise noise) => heard.Add(noise);

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("NoisyHouseTestRoot");
            heard.Clear();
            NoiseBus.Heard += Listen;
        }

        [TearDown]
        public void TearDown()
        {
            NoiseBus.Heard -= Listen;
            if (root != null) Object.DestroyImmediate(root);
        }

        CreakyFloor MakeBoard()
        {
            var go = new GameObject("CreakyFloor");
            go.transform.SetParent(root.transform, false);
            go.AddComponent<BoxCollider>().isTrigger = true;
            return go.AddComponent<CreakyFloor>();
        }

        TripwireBell MakeWire()
        {
            var go = new GameObject("TripwireBell");
            go.transform.SetParent(root.transform, false);
            go.AddComponent<BoxCollider>().isTrigger = true;
            return go.AddComponent<TripwireBell>();
        }

        // ------------------------------------------------------------------
        // Floorboards
        // ------------------------------------------------------------------

        [UnityTest]
        public IEnumerator WalkingOverALiveBoardIsHeard()
        {
            yield return null;

            var board = MakeBoard();
            Assert.IsTrue(board.TryCreak(root, speed: 2.6f, crouching: false));

            Assert.AreEqual(1, heard.Count);
            Assert.AreEqual(NoiseKind.Creak, heard[0].Kind);
        }

        [UnityTest]
        public IEnumerator CrouchingOverTheSameBoardIsNot()
        {
            yield return null;

            var board = MakeBoard();

            Assert.IsFalse(board.TryCreak(root, speed: 1.3f, crouching: true),
                "Crouching is the whole answer to a board you already know about.");
            Assert.IsEmpty(heard);
        }

        [UnityTest]
        public IEnumerator StandingStillOnABoardIsNot()
        {
            yield return null;

            var board = MakeBoard();

            Assert.IsFalse(board.TryCreak(root, speed: 0f, crouching: false));
            Assert.IsEmpty(heard);
        }

        [UnityTest]
        public IEnumerator OneCrossingIsOneSound()
        {
            yield return null;

            var board = MakeBoard();

            // A trigger volume reports every physics step. Without the cooldown a
            // single walk across would be a siren, not a creak.
            for (var i = 0; i < 20; i++)
                board.TryCreak(root, speed: 2.6f, crouching: false);

            Assert.AreEqual(1, heard.Count, "A board that creaks every frame is not a creak.");
        }

        [UnityTest]
        public IEnumerator ASilencedBoardSaysNothing()
        {
            yield return null;

            var board = MakeBoard();
            board.SetArmed(false);

            Assert.IsFalse(board.TryCreak(root, speed: 2.6f, crouching: false),
                "Difficulty silences boards by disarming them.");
            Assert.IsEmpty(heard);
        }

        // ------------------------------------------------------------------
        // The wire
        // ------------------------------------------------------------------

        [UnityTest]
        public IEnumerator TheWireRingsTheFirstTimeAndNeverAgain()
        {
            yield return null;

            var wire = MakeWire();
            var rings = 0;
            wire.Rang += () => rings++;

            Assert.IsTrue(wire.Trip(root));
            Assert.IsFalse(wire.Trip(root), "A rung bell hangs slack for the rest of the day.");
            Assert.IsFalse(wire.Trip(root));

            Assert.AreEqual(1, rings);
            Assert.AreEqual(1, heard.Count);
            Assert.AreEqual(NoiseKind.Bell, heard[0].Kind);
            Assert.IsTrue(wire.HasRung);
        }

        [UnityTest]
        public IEnumerator TheMorningSetsTheWireAgain()
        {
            yield return null;

            var wire = MakeWire();
            wire.Trip(root);
            wire.Rearm();

            Assert.IsFalse(wire.HasRung);
            Assert.IsTrue(wire.Trip(root), "Each day starts with the wire set again.");
            Assert.AreEqual(2, heard.Count);
        }

        // ------------------------------------------------------------------
        // The rule these rely on
        // ------------------------------------------------------------------

        [UnityTest]
        public IEnumerator BothCarryTheirFloorWithThem()
        {
            yield return null;

            var board = MakeBoard();
            board.transform.position = new Vector3(3f, HouseLayout.FloorHeight, -2f);
            board.TryCreak(root, speed: 2.6f, crouching: false);

            Assert.AreEqual(HouseLayout.Upper, heard[0].Floor,
                "She routes to a coordinate on a floor, so the floor has to be right.");
        }
    }
}
