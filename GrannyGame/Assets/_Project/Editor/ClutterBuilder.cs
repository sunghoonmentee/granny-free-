using System.Collections.Generic;
using Granny.Core;
using UnityEditor;
using UnityEngine;

namespace Granny.EditorTools
{
    /// <summary>
    /// Fills the house with the things people leave in houses.
    ///
    /// Up to here every room has been a box with a wardrobe against one wall, and
    /// a photograph of one says it better than any description could: it does not
    /// look like a frightening house, it looks like an unfinished one. The house
    /// in the original is frightening partly because it is *full*, and clutter
    /// does three jobs at once. It breaks the sightlines, so she comes round
    /// things instead of across an empty floor. It gives the eye something to
    /// catch on in the dark, which is what makes a dark room worth searching. And
    /// it is the whole difference between a level and a place somebody lives.
    ///
    /// Everything is placed by hand rather than scattered, for the same reason the
    /// item layouts are fixed: a house that rearranges itself cannot be learned,
    /// and learning the house is the game.
    /// </summary>
    public static class ClutterBuilder
    {
        const string ModelDir = "Assets/_Project/Art/Models";

        const float Basement = -3.2f;
        const float Ground = 0f;
        const float Upper = 3.2f;
        const float Attic = 6.4f;

        /// <summary>
        /// One piece: what it is, where it stands, and how big a box the rest of
        /// the world should believe it is.
        ///
        /// A zero box means scenery — no collider at all, seen but never felt. A
        /// bottle that stops her walking is a bottle that breaks the level; a
        /// bookcase that does not is a bookcase she strolls through.
        /// </summary>
        readonly struct Piece
        {
            public readonly string Model;
            public readonly Vector3 At;
            public readonly float Yaw;
            public readonly Vector3 Box;
            public readonly int Webs;

            /// <summary>Hung on a wall rather than stood on the floor.</summary>
            public readonly bool Hangs;

            public Piece(string model, float x, float y, float z, float yaw,
                Vector3 box = default, int webs = 1, bool hangs = false)
            {
                Model = model;
                At = new Vector3(x, y, z);
                Yaw = yaw;
                Box = box;
                Webs = webs;
                Hangs = hangs;
            }
        }

        /// <summary>
        /// Where everything stands.
        ///
        /// Three rules the coordinates follow. Nothing sits in a doorway — the
        /// openings are at z = -5 and z = 6 in the two hall walls, and at
        /// x = -7, 0 and 7 in the cross wall, and a crate in one of those is a
        /// door that will not open. Nothing stands where the furniture already is:
        /// the chests are on the long walls at x = ±11.55, the wardrobes at
        /// ±11.42. And nothing blocks a stairwell, either way into the garage, or
        /// the spot the truck occupies at (22, -2).
        ///
        /// The boxes are each model's own measured size rather than a guess. The
        /// first pass guessed, and gave a 2.27 m Victorian chair a half-metre box:
        /// she would have walked straight through the back of it.
        /// </summary>
        static readonly Piece[] Pieces =
        {
            // ---- the hall: the first room seen, and the last ----------------
            new("vintage_grandfather_clock_01", -3.6f, Ground, 8.2f, 90f, new Vector3(0.63f, 2.19f, 0.43f), 2),
            new("WoodenChair_01", 3.2f, Ground, 7.2f, -115f, new Vector3(0.69f, 2.27f, 0.66f)),
            new("hanging_picture_frame_01", -3.88f, Ground + 1.8f, 3.4f, 90f, hangs: true),
            new("hanging_picture_frame_02", 3.88f, Ground + 1.75f, -2.6f, -90f, hangs: true),
            new("wooden_crate_01", -3.2f, Ground, -7.6f, 25f, new Vector3(0.83f, 0.35f, 0.41f)),

            // ---- the kitchen -------------------------------------------------
            new("dining_table", -7.6f, Ground, 4.4f, 8f, new Vector3(2.26f, 0.88f, 1.39f), 2),
            new("WoodenChair_01", -5.9f, Ground, 4.3f, 95f, new Vector3(0.69f, 2.27f, 0.66f)),
            new("WoodenChair_01", -9.3f, Ground, 4.6f, -80f, new Vector3(0.69f, 2.27f, 0.66f)),
            new("wine_bottles_01", -7.4f, Ground + 0.88f, 4.5f, 40f),
            new("wine_barrel_01", -11f, Ground, 8.8f, 0f, new Vector3(0.74f, 0.87f, 0.76f), 2),

            // ---- the parlour --------------------------------------------------
            new("ArmChair_01", -9.4f, Ground, -7.2f, 35f, new Vector3(0.85f, 1.07f, 0.77f), 2),
            new("Rockingchair_01", -6.2f, Ground, -4.2f, 115f, new Vector3(0.71f, 0.99f, 0.83f), 2),
            new("wooden_bookshelf_worn", -4.8f, Ground, -9.3f, 0f, new Vector3(1.37f, 2.06f, 0.58f), 3),

            // ---- the living room -----------------------------------------------
            new("Sofa_01", 9.8f, Ground, -4.4f, -90f, new Vector3(1.57f, 0.8f, 0.66f), 2),
            new("WoodenTable_02", 8.2f, Ground, -4.2f, -6f, new Vector3(0.3f, 0.42f, 0.3f)),
            new("wooden_bookshelf_worn", 9.6f, Ground, -9.3f, 0f, new Vector3(1.37f, 2.06f, 0.58f), 3),
            new("hanging_picture_frame_01", 7f, Ground + 1.8f, -9.85f, 0f, hangs: true),

            // ---- the stair hall --------------------------------------------------
            new("WoodenChair_01", 4.8f, Ground, 8.8f, 160f, new Vector3(0.69f, 2.27f, 0.66f)),
            new("wooden_crate_01", 10.8f, Ground, 8.8f, -15f, new Vector3(0.83f, 0.35f, 0.41f)),
            new("wine_barrel_01", 11.2f, Ground, 7.4f, 0f, new Vector3(0.74f, 0.87f, 0.76f), 2),

            // ---- upstairs ----------------------------------------------------------
            new("Rockingchair_01", 10.2f, Upper, 8.6f, 200f, new Vector3(0.71f, 0.99f, 0.83f), 2),
            new("ClassicNightstand_01", 9.6f, Upper, -4.6f, -90f, new Vector3(0.57f, 0.7f, 0.42f)),
            new("WoodenChair_01", 9.2f, Upper, -2.2f, 70f, new Vector3(0.69f, 2.27f, 0.66f)),
            new("wooden_bookshelf_worn", -8.5f, Upper, 9.3f, 180f, new Vector3(1.37f, 2.06f, 0.58f), 3),
            new("WoodenTable_02", -7.8f, Upper, -7.6f, 12f, new Vector3(0.3f, 0.42f, 0.3f)),
            new("wine_bottles_01", -7.8f, Upper + 0.42f, -7.6f, -20f),
            new("ArmChair_01", -6.4f, Upper, -4.2f, 120f, new Vector3(0.85f, 1.07f, 0.77f), 2),
            new("hanging_picture_frame_02", -3.88f, Upper + 1.75f, -1.4f, 90f, hangs: true),

            // ---- the attic: where things go to be forgotten -------------------------
            new("wooden_crate_01", -6.2f, Attic, -4.4f, 40f, new Vector3(0.83f, 0.35f, 0.41f), 2),
            new("wooden_crate_01", -5.4f, Attic, -5.4f, -10f, new Vector3(0.83f, 0.35f, 0.41f), 2),
            new("WoodenChair_01", 7f, Attic, 2f, -40f, new Vector3(0.69f, 2.27f, 0.66f), 2),
            new("Rockingchair_01", 7.4f, Attic, 7f, 220f, new Vector3(0.71f, 0.99f, 0.83f), 3),
            new("wine_barrel_01", -8f, Attic, -5.8f, 0f, new Vector3(0.74f, 0.87f, 0.76f), 3),

            // ---- the cellar -----------------------------------------------------------
            new("wooden_crate_01", -10.4f, Basement, -8.8f, 15f, new Vector3(0.83f, 0.35f, 0.41f), 2),
            new("wooden_crate_01", -9.4f, Basement, -9f, -25f, new Vector3(0.83f, 0.35f, 0.41f), 2),
            new("wooden_bookshelf_worn", -5f, Basement, -9.3f, 0f, new Vector3(1.37f, 2.06f, 0.58f), 3),
            new("wine_barrel_01", -11.1f, Basement, -6.6f, 0f, new Vector3(0.74f, 0.87f, 0.76f), 3),
            new("wine_barrel_01", -11.1f, Basement, 1.2f, 0f, new Vector3(0.74f, 0.87f, 0.76f), 3),
            new("dining_table", -7.4f, Basement, 7.4f, 3f, new Vector3(2.26f, 0.88f, 1.39f), 2),
            new("wine_bottles_01", -7.2f, Basement + 0.88f, 7.5f, 110f),
            new("wooden_crate_01", 9.8f, Basement, 8.8f, 8f, new Vector3(0.83f, 0.35f, 0.41f), 2),
            new("wine_barrel_01", 11.1f, Basement, 7.4f, 0f, new Vector3(0.74f, 0.87f, 0.76f), 3),

            // ---- the garage: clear of both ways in, and of the truck -------------------
            new("wooden_crate_01", 17.4f, Basement, -6.3f, -20f, new Vector3(0.83f, 0.35f, 0.41f), 2),
            new("wooden_crate_01", 18.4f, Basement, -6.5f, 30f, new Vector3(0.83f, 0.35f, 0.41f), 2),
            new("wine_barrel_01", 24f, Basement, 4f, 0f, new Vector3(0.74f, 0.87f, 0.76f), 2),
            new("wine_barrel_01", 25f, Basement, 4.2f, 0f, new Vector3(0.74f, 0.87f, 0.76f), 2),
            new("WoodenChair_01", 28.6f, Basement, -5.4f, 120f, new Vector3(0.69f, 2.27f, 0.66f), 2),

            // ---- lamps, shelves and the things on walls ------------------------------
            new("vintage_oil_lamp", -7.9f, Ground + 0.88f, 4.1f, 0f),
            new("vintage_oil_lamp", 9.6f, Upper + 0.7f, -4.6f, 0f),
            new("vintage_oil_lamp", -7.9f, Basement + 0.88f, 7.1f, 0f),
            new("Shelf_01", -11.6f, Basement, -2.5f, 90f, new Vector3(1f, 2.08f, 0.26f), 3),
            new("Shelf_01", 11.6f, Basement, 2.5f, -90f, new Vector3(1f, 2.08f, 0.26f), 3),
            new("Shelf_01", 29.6f, Basement, 0f, -90f, new Vector3(1f, 2.08f, 0.26f), 3),
            new("ClassicNightstand_01", -4.6f, Upper, 7.4f, 90f, new Vector3(0.57f, 0.7f, 0.42f)),
            new("vintage_wooden_drawer_01", 8.6f, Attic, 0f, -90f, new Vector3(0.86f, 0.55f, 0.46f), 2),
            new("vintage_wooden_drawer_01", -11.6f, Ground, -7.4f, 90f, new Vector3(0.86f, 0.55f, 0.46f), 2),
            new("hanging_picture_frame_01", -11.85f, Ground + 1.8f, -3f, 90f, hangs: true),
            new("hanging_picture_frame_02", 11.85f, Upper + 1.75f, 7.5f, -90f, hangs: true),
            new("hanging_picture_frame_01", 0.1f, Upper + 1.8f, -9.85f, 0f, hangs: true),
        };

        /// <summary>
        /// Stands everything up under a root of its own.
        ///
        /// Called from the house build, before the navigation bake, so that what
        /// is in the way is in the way for her too: clutter she walks through is
        /// scenery, and clutter she has to come round is level design.
        /// </summary>
        public static void Fill(Transform parent)
        {
            var root = new GameObject("Clutter").transform;
            root.SetParent(parent, false);

            var missing = new HashSet<string>();
            var placed = 0;

            for (var i = 0; i < Pieces.Length; i++)
            {
                var piece = Pieces[i];
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(
                    $"{ModelDir}/{piece.Model}/{piece.Model}.fbx");

                if (model == null)
                {
                    missing.Add(piece.Model);
                    continue;
                }

                if (Stand(piece, model, root, i)) placed++;
            }

            foreach (var name in missing)
                Debug.Log($"[Clutter] '{name}' has not been downloaded; its places are left empty.");

            Debug.Log($"[Clutter] {placed} pieces placed");
        }

        static bool Stand(Piece piece, GameObject model, Transform root, int index)
        {
            var holder = new GameObject($"{piece.Model}_{index}");
            holder.transform.SetParent(root, false);
            holder.transform.SetPositionAndRotation(piece.At, Quaternion.Euler(0f, piece.Yaw, 0f));

            var visual = (GameObject)PrefabUtility.InstantiatePrefab(model, holder.transform);
            visual.name = "Model";
            visual.transform.localPosition = Vector3.zero;

            var renderers = visual.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
            {
                Object.DestroyImmediate(holder);
                return false;
            }

            // Centred on the spot and standing on the floor, whatever the artist
            // left as the origin: these arrive with the pivot anywhere at all, and
            // the first one that went in stood half a metre off its own mark.
            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);

            visual.transform.position -= new Vector3(
                bounds.center.x - piece.At.x,
                (piece.Hangs ? bounds.center.y : bounds.min.y) - piece.At.y,
                bounds.center.z - piece.At.z);

            ModelSkin.Retint(piece.Model, visual.GetComponentsInChildren<Renderer>());

            // Whatever colliders the exporter happened to leave behind are not a
            // design decision. What the world is told about this piece is the one
            // box below, which is.
            foreach (var collider in visual.GetComponentsInChildren<Collider>())
                Object.DestroyImmediate(collider);

            foreach (var child in holder.GetComponentsInChildren<Transform>())
                child.gameObject.layer = GameLayers.Prop;

            if (piece.Box != Vector3.zero)
            {
                var box = holder.AddComponent<BoxCollider>();
                box.size = piece.Box;
                box.center = new Vector3(0f, piece.Box.y * 0.5f, 0f);
            }

            DecayBuilder.Weather(holder, SeedFor(piece.At), piece.Webs);
            return true;
        }

        /// <summary>
        /// A number from a place, rounded to the centimetre first: a coordinate
        /// that came out of a float calculation is not reliably the same number
        /// twice, and the damage has to be.
        /// </summary>
        static int SeedFor(Vector3 position)
        {
            var x = Mathf.RoundToInt(position.x * 100f);
            var y = Mathf.RoundToInt(position.y * 100f);
            var z = Mathf.RoundToInt(position.z * 100f);

            return x * 73856093 ^ y * 19349663 ^ z * 83492791;
        }
    }
}
