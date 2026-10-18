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

        // Shoulders wider than the chest, on purpose. The arms used to hang
        // inside the body's own outline, so from the front she was one mass with
        // a head on it — which is a mannequin. Pushing them out past the ribs is
        // what separates arm from torso in silhouette, and silhouette is all she
        // has at the distance she is usually seen from.
        const float ShoulderWidth = 0.255f;
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
            // Cool and slightly green, not warm. At the old values the house's
            // amber bulbs turned her a healthy tan and she read as carved wood —
            // a mannequin, which is a thing, and the one thing she must not be.
            // Pulling the red down and letting green lead by a hair is the whole
            // difference between skin and timber at this light level.
            var skin = Material("AgataSkin", new Color(0.70f, 0.72f, 0.66f));

            // The nightdress goes up rather than down. She is read as a shape
            // first: a pale body with a near-black apron and shawl across it, at
            // the far end of a corridor, before any of the face resolves. A mid
            // grey dress under a dark apron gives that shape nothing to work with.
            var cloth = Material("AgataNightdress", new Color(0.54f, 0.52f, 0.47f));
            var apron = Material("AgataApron", new Color(0.11f, 0.10f, 0.09f));
            var hair = Material("AgataHair", new Color(0.80f, 0.79f, 0.77f));
            var iron = Material("AgataIron", new Color(0.13f, 0.13f, 0.14f));
            var eyes = Material("AgataEyes", new Color(0.92f, 0.94f, 0.88f), emissive: 2.8f);

            // The inside of her: eye sockets and an open mouth. Near black rather
            // than black, so the shape of the hole still reads when a bulb is
            // directly on her instead of flattening to a sticker.
            var hollow = Material("AgataHollow", new Color(0.035f, 0.03f, 0.03f));

            var rig = new Rig();

            var body = new GameObject("Body").transform;
            body.SetParent(parent, false);
            rig.Root = body;

            // ---- spine ----------------------------------------------------
            rig.Hips = Bone("Hips", body, new Vector3(0f, HipHeight, 0f));
            Shape("Hips", rig.Hips, new Vector3(0f, 0.02f, 0f), new Vector3(0.25f, 0.16f, 0.175f), cloth, layer);

            // The nightdress, to mid-shin. It hangs from the hips, so it stays
            // vertical however far she folds over it — which is what cloth does,
            // and what the first version got wrong by hanging it off the chest.
            // Two lengths rather than one box, the lower one wider: a nightdress
            // hangs off the hips and falls away from the body, and a column of
            // uniform width is a dressing gown on a stand. It also puts a visible
            // horizontal break halfway down her, which is what stops the lower
            // half reading as legs in a sack.
            Shape("Skirt", rig.Hips, new Vector3(0f, -0.18f, 0f),
                new Vector3(0.30f, 0.34f, 0.22f), cloth, layer);
            Shape("SkirtHem", rig.Hips, new Vector3(0f, -0.44f, 0f),
                new Vector3(0.37f, 0.20f, 0.27f), cloth, layer);

            Shape("Apron", rig.Hips, new Vector3(0f, -0.23f, 0.115f),
                new Vector3(0.22f, 0.42f, 0.025f), apron, layer);

            // The hunch lives here. Everything above it inherits the lean, which
            // is why she reads as bent rather than as leaning.
            rig.Spine = Bone("Spine", rig.Hips, new Vector3(0f, 0.08f, 0f), new Vector3(14f, 0f, 0f));
            // Thin. An old woman who has stopped eating is frightening; a wide
            // one is a wrestler in a nightdress, and every box here was built a
            // size too generous.
            Shape("Belly", rig.Spine, new Vector3(0f, SpineLength * 0.5f, 0f),
                new Vector3(0.27f, SpineLength, 0.19f), cloth, layer);

            rig.Chest = Bone("Chest", rig.Spine, new Vector3(0f, SpineLength, 0f), new Vector3(8f, 0f, 5f));
            Shape("Ribs", rig.Chest, new Vector3(0f, ChestLength * 0.5f, 0f),
                new Vector3(0.285f, ChestLength, 0.185f), cloth, layer);

            // The hump. It is the one piece of her that is not a simplification
            // of a person: a mass on the upper back, so the stoop reads as
            // something she is carrying rather than as a figure leaning forward.
            // From behind it is the first thing you see of her, and from the side
            // it is the whole shape — which is why it is round. Square, it was a
            // black crate strapped between her shoulder blades.
            Blob("Hunch", rig.Chest, new Vector3(0f, ChestLength * 0.68f, -0.09f),
                new Vector3(0.28f, 0.26f, 0.25f), cloth, layer);

            // A bib, flat against her, rather than a sheet standing off it.
            Shape("Bib", rig.Chest, new Vector3(0f, ChestLength * 0.45f, 0.095f),
                new Vector3(0.22f, ChestLength * 0.8f, 0.02f), apron, layer);

            // A shawl over the shoulders. Dark, wide and square across the top: it
            // turns the hunch into a silhouette you can recognise at the far end
            // of a corridor, before any of the face resolves.
            // Square across the shoulders, and square on purpose. Rounding this
            // one too turned her front into a bosom — a sphere is widest at its
            // middle, so it bulged out where a shawl should be flat. A shawl over
            // shoulders is wide and shallow; only the thing underneath it is
            // round. It also has to reach as far out as the arms hang, or the arm
            // begins in mid-air beside her.
            Shape("Shawl", rig.Chest, new Vector3(0f, ChestLength * 0.84f, -0.03f),
                new Vector3(0.42f, 0.16f, 0.27f), apron, layer);

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

            // Loose strands that have come down out of it, at uneven lengths. Hair
            // that is all one length is a wig; hair that is not is somebody who
            // has not looked in a mirror for a long time.
            for (var strand = 0; strand < 7; strand++)
            {
                var across = Mathf.Lerp(-0.093f, 0.093f, strand / 6f);
                var back = -0.086f + (strand % 2) * 0.022f;
                var length = 0.21f + (strand % 3) * 0.07f;

                Shape($"Strand_{strand}", rig.Head,
                    new Vector3(across, 0.145f - length * 0.5f, back),
                    new Vector3(0.021f, length, 0.019f), hair, layer);
            }

            // Two in front, falling past the sockets, so the face is partly behind
            // something whichever way she is turned.
            Shape("Lock_L", rig.Head, new Vector3(-0.093f, 0.07f, 0.045f),
                new Vector3(0.023f, 0.22f, 0.03f), hair, layer);
            Shape("Lock_R", rig.Head, new Vector3(0.096f, 0.095f, 0.03f),
                new Vector3(0.02f, 0.17f, 0.028f), hair, layer);

            // ---- the face -------------------------------------------------
            //
            // She had a brow and two eyeballs and nothing else, and what that
            // reads as from across a room is a wooden mannequin: a head-shaped
            // object rather than a head. The pieces below are the cheapest set
            // that makes a face frightening rather than merely absent — sockets,
            // a nose, and a mouth that is open.
            //
            // It is all boxes. That is not a compromise being apologised for: at
            // the distance and the light level she is actually seen at, what
            // carries is the arrangement of dark holes and pale edges, and boxes
            // do that as well as anything.

            // Sunken sockets first, so the eyes sit in pits. The old brow was set
            // so far forward that it covered them completely — the one feature
            // meant to find her in the dark was behind her own forehead.
            Shape("SocketL", rig.Head, new Vector3(-0.048f, 0.128f, 0.085f),
                new Vector3(0.088f, 0.08f, 0.055f), hollow, layer);
            Shape("SocketR", rig.Head, new Vector3(0.048f, 0.128f, 0.085f),
                new Vector3(0.088f, 0.08f, 0.055f), hollow, layer);

            // A heavy brow above them, and gaunt cheekbones below. Between them
            // the sockets stay in shadow from every angle that matters.
            Shape("Brow", rig.Head, new Vector3(0f, 0.179f, 0.079f),
                new Vector3(0.205f, 0.048f, 0.072f), skin, layer);
            Shape("CheekL", rig.Head, new Vector3(-0.077f, 0.092f, 0.068f),
                new Vector3(0.048f, 0.082f, 0.078f), skin, layer);
            Shape("CheekR", rig.Head, new Vector3(0.077f, 0.092f, 0.068f),
                new Vector3(0.048f, 0.082f, 0.078f), skin, layer);

            // A long nose with a hook on the end. More than anything else here it
            // is what stops the head reading as a block with marks on it.
            Shape("Nose", rig.Head, new Vector3(0f, 0.101f, 0.108f),
                new Vector3(0.042f, 0.088f, 0.058f), skin, layer);
            Shape("NoseTip", rig.Head, new Vector3(0f, 0.063f, 0.123f),
                new Vector3(0.036f, 0.032f, 0.05f), skin, layer);

            // Open, and left open. A closed mouth is a face at rest; this one is
            // a hole, and a hole is the thing the eye finds first.
            Shape("Mouth", rig.Head, new Vector3(0f, 0.028f, 0.097f),
                new Vector3(0.092f, 0.056f, 0.042f), hollow, layer);
            Shape("Teeth", rig.Head, new Vector3(0f, 0.046f, 0.102f),
                new Vector3(0.058f, 0.013f, 0.032f), skin, layer);

            // The eyes themselves, smaller and brighter than before: two points
            // rather than two discs. In a dark corridor they are the first thing
            // the player sees of her, and usually the only thing.
            rig.Eye = Bone("Eye", rig.Head, new Vector3(0f, 0.128f, 0.094f));
            Ball("EyeL", rig.Eye, new Vector3(-0.048f, 0f, 0f), 0.034f, eyes, layer);
            Ball("EyeR", rig.Eye, new Vector3(0.048f, 0f, 0f), 0.034f, eyes, layer);

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
                new Vector3(0.1f, UpperArm, 0.1f), cloth, layer);

            // The cuff the sleeve ends in, so the arm has a joint rather than
            // changing colour halfway down.
            Shape($"Cuff{side}", upper, new Vector3(0f, -UpperArm + 0.015f, 0f),
                new Vector3(0.108f, 0.06f, 0.108f), cloth, layer);

            var lower = Bone($"Forearm{side}", upper, new Vector3(0f, -UpperArm, 0f), new Vector3(10f, 0f, 0f));
            Shape($"Forearm{side}Flesh", lower, new Vector3(0f, -LowerArm * 0.5f, 0f),
                new Vector3(0.072f, LowerArm, 0.072f), skin, layer);

            var hand = Bone($"Hand{side}", lower, new Vector3(0f, -LowerArm, 0f));
            Shape($"Hand{side}Palm", hand, new Vector3(0f, -0.045f, 0f),
                new Vector3(0.075f, 0.095f, 0.05f), skin, layer);

            // Four fingers and a thumb, curled. Her arms hang past her knees and
            // the hands are the part of her nearest the player for most of a
            // chase; a paddle on the end of an arm reads as a tool, and five thin
            // boxes read as a hand reaching.
            for (var finger = 0; finger < 4; finger++)
            {
                var across = Mathf.Lerp(-0.026f, 0.026f, finger / 3f);
                var length = finger is 1 or 2 ? 0.062f : 0.05f;

                Shape($"Hand{side}Finger{finger}", hand,
                    new Vector3(across, -0.095f - length * 0.5f, 0.004f),
                    new Vector3(0.016f, length, 0.018f), skin, layer);
            }

            Shape($"Hand{side}Thumb", hand, new Vector3(0.035f * -sign, -0.075f, 0.022f),
                new Vector3(0.019f, 0.046f, 0.02f), skin, layer);

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
                new Vector3(0.115f, Thigh * 0.8f, 0.115f), skin, layer);

            var shin = Bone($"Shin{side}", thigh, new Vector3(0f, -Thigh, 0f));
            // Thinner than the thigh, and thinner than it was. The lower leg is
            // the only part of her the player sees below the nightdress, so it is
            // carrying the whole impression of what is under it.
            Shape($"Shin{side}Flesh", shin, new Vector3(0f, -Shin * 0.5f, 0f),
                new Vector3(0.1f, Shin, 0.1f), skin, layer);

            // A knob of bone at the ankle. One box, and it is the difference
            // between a leg and a chair leg.
            Shape($"Ankle{side}", shin, new Vector3(0f, -Shin + 0.03f, 0f),
                new Vector3(0.082f, 0.075f, 0.082f), skin, layer);

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

        /// <summary>
        /// A squashed sphere.
        ///
        /// Everything else about her is boxes and that is the right call — at the
        /// light level she is seen at, what carries is the arrangement of dark and
        /// pale, and a box does that as cheaply as anything. Two shapes are the
        /// exception, and both are cloth over a curve: a hump and a shawl. Built
        /// square they do not read as a back and a wrap, they read as a crate
        /// strapped to a mannequin, which is what the first version looked like
        /// from the side.
        /// </summary>
        static void Blob(string name, Transform bone, Vector3 localPosition, Vector3 size,
            Material material, int layer)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
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

                // Without this the emission is set and does not show. A material
                // made in code carries the "emissive is black" lightmap flag, and
                // Unity honours it over the colour — which is why her eyes have
                // been two dull grey beads in every photograph rather than the one
                // thing you can see of her in the dark.
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }

            EditorUtility.SetDirty(material);
            return material;
        }
    }
}
