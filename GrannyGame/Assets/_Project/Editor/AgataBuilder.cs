using UnityEditor;
using UnityEngine;

namespace Granny.EditorTools
{
    /// <summary>
    /// Builds Agata: a body with bones in it, out of boxes and spheres.
    ///
    /// This is not a stand-in capsule and it is not a bought model either. It is
    /// a jointed figure assembled in code, so the thing that makes her
    /// frightening can be tuned like any other number in the project — the hunch,
    /// the arms that hang too low, the head that leads the body by half a step.
    /// Those are the parts that read at a distance in a dark corridor. Detail is
    /// not; detail is what you notice once you are already safe.
    ///
    /// Deliberately not a copy of anyone else's character. The design is the one
    /// in the document: tall and bent, a grey braid, a stained apron over a
    /// nightdress, clouded eyes, bare feet, and an iron-shod stick.
    ///
    /// Every limb is a bone transform with its shape parented underneath, so
    /// <see cref="Granny.Gameplay.AI.AgataPose"/> can move her by rotating bones
    /// rather than by playing a clip nobody has authored yet.
    /// </summary>
    public static class AgataBuilder
    {
        const string MaterialDir = "Assets/_Project/Art/Materials";

        // Proportions. She is a shade under six foot and folded forward about
        // twenty degrees, which puts her face at roughly the player's chest —
        // close enough to loom, far enough to still read as a person.
        const float HipHeight = 0.92f;
        const float SpineLength = 0.34f;
        const float ChestLength = 0.26f;
        const float NeckLength = 0.10f;

        const float UpperArm = 0.31f;
        const float LowerArm = 0.30f;
        const float Thigh = 0.44f;
        const float Shin = 0.42f;

        const float ShoulderWidth = 0.22f;
        const float HipWidth = 0.12f;

        public sealed class Rig
        {
            public Transform Root;
            public Transform Hips, Spine, Chest, Neck, Head;
            public Transform ArmL, ForearmL, HandL;
            public Transform ArmR, ForearmR, HandR;
            public Transform ThighL, ShinL, FootL;
            public Transform ThighR, ShinR, FootR;
            public Transform Cane;
            public Transform Eye;
        }

        /// <summary>
        /// Assembles her under <paramref name="parent"/> and hands back the bones.
        /// </summary>
        public static Rig Build(Transform parent, int layer)
        {
            // Three clearly separated values, because in a house lit by four
            // bulbs that is all the information her silhouette gets to carry:
            // pale hands and face, a mid nightdress, a near-black apron. Painted
            // in one register she was a blur the size of a person.
            var skin = Material("AgataSkin", new Color(0.82f, 0.77f, 0.70f));
            var cloth = Material("AgataNightdress", new Color(0.40f, 0.39f, 0.37f));
            var apron = Material("AgataApron", new Color(0.11f, 0.10f, 0.09f));
            var hair = Material("AgataHair", new Color(0.80f, 0.79f, 0.77f));
            var iron = Material("AgataIron", new Color(0.13f, 0.13f, 0.14f));
            var eyes = Material("AgataEyes", new Color(0.92f, 0.94f, 0.88f), emissive: 1.6f);

            var rig = new Rig();

            var body = new GameObject("Body").transform;
            body.SetParent(parent, false);
            rig.Root = body;

            // ---- spine ----------------------------------------------------
            rig.Hips = Bone("Hips", body, new Vector3(0f, HipHeight, 0f));
            Shape("Hips", rig.Hips, new Vector3(0f, 0.02f, 0f), new Vector3(0.26f, 0.16f, 0.18f), cloth, layer);

            // The nightdress, to mid-shin. It hangs from the hips, so it stays
            // vertical however far she folds over it — which is what cloth does,
            // and what the first version got wrong by hanging it off the chest.
            Shape("Skirt", rig.Hips, new Vector3(0f, -0.24f, 0f),
                new Vector3(0.30f, 0.46f, 0.22f), cloth, layer);

            Shape("Apron", rig.Hips, new Vector3(0f, -0.23f, 0.115f),
                new Vector3(0.22f, 0.42f, 0.025f), apron, layer);

            // The hunch lives here. Everything above it inherits the lean, which
            // is why she reads as bent rather than as leaning.
            rig.Spine = Bone("Spine", rig.Hips, new Vector3(0f, 0.08f, 0f), new Vector3(14f, 0f, 0f));
            Shape("Belly", rig.Spine, new Vector3(0f, SpineLength * 0.5f, 0f),
                new Vector3(0.32f, SpineLength, 0.22f), cloth, layer);

            rig.Chest = Bone("Chest", rig.Spine, new Vector3(0f, SpineLength, 0f), new Vector3(8f, 0f, 5f));
            Shape("Ribs", rig.Chest, new Vector3(0f, ChestLength * 0.5f, 0f),
                new Vector3(0.34f, ChestLength, 0.21f), cloth, layer);
            // A bib, flat against her, rather than a sheet standing off it.
            Shape("Bib", rig.Chest, new Vector3(0f, ChestLength * 0.45f, 0.105f),
                new Vector3(0.22f, ChestLength * 0.8f, 0.02f), apron, layer);

            // The head is counter-rotated so she still looks where she is going
            // despite the hunch: chin up, eyes under the brow. It is the single
            // thing that stops her reading as a hat stand.
            rig.Neck = Bone("Neck", rig.Chest, new Vector3(0f, ChestLength, -0.01f), new Vector3(-12f, 0f, 0f));
            Shape("Throat", rig.Neck, new Vector3(0f, NeckLength * 0.5f, 0f),
                new Vector3(0.09f, NeckLength, 0.09f), skin, layer);

            rig.Head = Bone("Head", rig.Neck, new Vector3(0f, NeckLength, 0f), new Vector3(-10f, 0f, 0f));
            Shape("Skull", rig.Head, new Vector3(0f, 0.11f, 0f), new Vector3(0.19f, 0.23f, 0.21f), skin, layer);
            Shape("Jaw", rig.Head, new Vector3(0f, 0.04f, 0.04f), new Vector3(0.14f, 0.08f, 0.14f), skin, layer);

            // Hair: a bun and a braid down one shoulder, so her silhouette is not
            // symmetrical. Asymmetry is most of what makes a shape look alive.
            Shape("Bun", rig.Head, new Vector3(0f, 0.20f, -0.07f), new Vector3(0.16f, 0.13f, 0.14f), hair, layer);
            Shape("Braid", rig.Head, new Vector3(0.07f, 0.02f, -0.09f), new Vector3(0.06f, 0.30f, 0.06f), hair, layer);

            // A brow over them, so the eyes sit in shadow and catch the light as
            // two points rather than washing out with the rest of the face. In a
            // dark corridor they are the first thing the player sees of her, and
            // usually the only thing.
            Shape("Brow", rig.Head, new Vector3(0f, 0.165f, 0.085f),
                new Vector3(0.20f, 0.05f, 0.08f), skin, layer);

            rig.Eye = Bone("Eye", rig.Head, new Vector3(0f, 0.125f, 0.095f));
            Ball("EyeL", rig.Eye, new Vector3(-0.047f, 0f, 0f), 0.042f, eyes, layer);
            Ball("EyeR", rig.Eye, new Vector3(0.047f, 0f, 0f), 0.042f, eyes, layer);

            // ---- arms -----------------------------------------------------
            // Hung from the chest and rotated out, long enough that her hands
            // fall below her knees. Nothing about the proportion is subtle and
            // it is not meant to be.
            (rig.ArmL, rig.ForearmL, rig.HandL) = Arm("L", rig.Chest, -1f, skin, cloth, layer);
            (rig.ArmR, rig.ForearmR, rig.HandR) = Arm("R", rig.Chest, 1f, skin, cloth, layer);

            // ---- legs -----------------------------------------------------
            (rig.ThighL, rig.ShinL, rig.FootL) = Leg("L", rig.Hips, -1f, skin, cloth, layer);
            (rig.ThighR, rig.ShinR, rig.FootR) = Leg("R", rig.Hips, 1f, skin, cloth, layer);

            // ---- the stick ------------------------------------------------
            // Measured to reach the floor from where her hand actually hangs, not
            // guessed: a stick that is too long is driven through every stair
            // tread she walks over, which is what the first version did.
            const float caneLength = 0.64f;

            // Counter-rotated out of the arm it hangs from. The hand carries the
            // fold of her back and the outward swing of the shoulder, so a stick
            // parented to it with no correction points off sideways like a sword
            // instead of standing on the floor.
            // Held out from the hip rather than against it, so the stick is part
            // of her outline instead of disappearing into her skirt. It is the
            // thing the player hears; it should be the thing they see.
            rig.Cane = Bone("Cane", rig.HandR, new Vector3(0.06f, -0.04f, 0.04f), new Vector3(-16f, 0f, 14f));
            Shape("Shaft", rig.Cane, new Vector3(0f, -caneLength * 0.5f, 0f),
                new Vector3(0.035f, caneLength, 0.035f), iron, layer);
            Shape("Ferrule", rig.Cane, new Vector3(0f, -caneLength, 0f),
                new Vector3(0.05f, 0.05f, 0.05f), iron, layer);
            Shape("Handle", rig.Cane, new Vector3(0f, 0.02f, 0.03f),
                new Vector3(0.04f, 0.04f, 0.12f), iron, layer);

            return rig;
        }

        // ------------------------------------------------------------------

        static (Transform, Transform, Transform) Arm(string side, Transform chest, float sign,
            Material skin, Material cloth, int layer)
        {
            var upper = Bone($"Arm{side}", chest,
                new Vector3(ShoulderWidth * sign, ChestLength * 0.86f, 0f),
                new Vector3(-14f, 0f, -15f * sign));

            Shape($"Sleeve{side}", upper, new Vector3(0f, -UpperArm * 0.5f, 0f),
                new Vector3(0.11f, UpperArm, 0.11f), cloth, layer);

            var lower = Bone($"Forearm{side}", upper, new Vector3(0f, -UpperArm, 0f), new Vector3(10f, 0f, 0f));
            Shape($"Forearm{side}Flesh", lower, new Vector3(0f, -LowerArm * 0.5f, 0f),
                new Vector3(0.085f, LowerArm, 0.085f), skin, layer);

            var hand = Bone($"Hand{side}", lower, new Vector3(0f, -LowerArm, 0f));
            Shape($"Hand{side}Flesh", hand, new Vector3(0f, -0.06f, 0f),
                new Vector3(0.075f, 0.13f, 0.05f), skin, layer);

            return (upper, lower, hand);
        }

        static (Transform, Transform, Transform) Leg(string side, Transform hips, float sign,
            Material skin, Material cloth, int layer)
        {
            // No cloth on the thigh: the nightdress already covers it, and two
            // layers of box in the same place is how you get a figure that
            // flickers when it moves.
            var thigh = Bone($"Thigh{side}", hips, new Vector3(HipWidth * sign, -0.06f, 0f));
            Shape($"Thigh{side}Flesh", thigh, new Vector3(0f, -Thigh * 0.6f, 0f),
                new Vector3(0.13f, Thigh * 0.8f, 0.13f), skin, layer);

            var shin = Bone($"Shin{side}", thigh, new Vector3(0f, -Thigh, 0f));
            Shape($"Shin{side}Flesh", shin, new Vector3(0f, -Shin * 0.5f, 0f),
                new Vector3(0.125f, Shin, 0.125f), skin, layer);

            // Bare feet. She never wears shoes, which is why her own footsteps
            // are not the sound the player navigates by — the stick is.
            var foot = Bone($"Foot{side}", shin, new Vector3(0f, -Shin, 0f));
            Shape($"Foot{side}Flesh", foot, new Vector3(0f, -0.03f, 0.06f),
                new Vector3(0.10f, 0.06f, 0.24f), skin, layer);

            return (thigh, shin, foot);
        }

        static Transform Bone(string name, Transform parent, Vector3 localPosition, Vector3? euler = null)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            if (euler.HasValue) go.transform.localRotation = Quaternion.Euler(euler.Value);
            return go.transform;
        }

        static void Shape(string name, Transform bone, Vector3 localPosition, Vector3 size,
            Material material, int layer)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(bone, false);
            go.transform.localPosition = localPosition;
            go.transform.localScale = size;
            go.layer = layer;

            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.GetComponent<MeshRenderer>().sharedMaterial = material;
        }

        static void Ball(string name, Transform bone, Vector3 localPosition, float radius,
            Material material, int layer)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = name;
            go.transform.SetParent(bone, false);
            go.transform.localPosition = localPosition;
            go.transform.localScale = Vector3.one * (radius * 2f);
            go.layer = layer;

            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.GetComponent<MeshRenderer>().sharedMaterial = material;
        }

        /// <summary>
        /// Her eyes are emissive on purpose: in a house lit by four bulbs, two
        /// pale points that catch the light are how you find out she is in the
        /// room with you, and they work at any distance a mesh does not.
        /// </summary>
        static Material Material(string name, Color colour, float emissive = 0f)
        {
            var path = $"{MaterialDir}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);

            if (material == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                material = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }

            material.SetColor("_BaseColor", colour);
            material.SetFloat("_Smoothness", 0.1f);

            if (emissive > 0f)
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", colour * emissive);
            }

            EditorUtility.SetDirty(material);
            return material;
        }
    }
}
