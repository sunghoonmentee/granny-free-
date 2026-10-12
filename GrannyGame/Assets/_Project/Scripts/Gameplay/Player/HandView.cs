using Granny.Core;
using UnityEngine;

namespace Granny.Gameplay.Player
{
    /// <summary>
    /// Puts what you are carrying where you can see it.
    ///
    /// Until now the one hand rule was enforced everywhere and shown nowhere: the
    /// only evidence that you were holding a hammer was the word "Hammer" in the
    /// corner of the screen. That is a rule you have to remember instead of a
    /// thing you can see, and it costs the game the moment that matters most —
    /// standing at a door with the wrong tool, in the dark, deciding whether it is
    /// worth the walk back.
    ///
    /// The model is the item's own world prefab with everything that makes it a
    /// physical object taken off it. There is no second hand model to keep in step
    /// with the first: what you see in your hand is the thing you will drop.
    /// </summary>
    public sealed class HandView : MonoBehaviour
    {
        [SerializeField] PlayerInventory inventory;
        [SerializeField] PlayerMotor motor;

        /// <summary>
        /// Where a carried thing sits, relative to the camera: low and to the
        /// right, the way a hand is. Public so that the tool which photographs it
        /// frames the same pose the game uses — a hand tuned in a screenshot that
        /// does not match the game is worse than no screenshot.
        /// </summary>
        public static readonly Vector3 Rest = new(0.30f, -0.27f, 0.46f);
        public static readonly Vector3 RestAngles = new(10f, 18f, 34f);

        [Header("Where it sits")]
        [SerializeField] Vector3 restPosition = Rest;
        [SerializeField] Vector3 restRotation = RestAngles;
        [SerializeField, Min(0f)] float scale = 1f;

        [Header("Life")]
        [Tooltip("How far it rises and falls with each pace.")]
        [SerializeField, Min(0f)] float bob = 0.016f;
        [Tooltip("How far it lags behind the camera when you turn.")]
        [SerializeField, Min(0f)] float sway = 0.03f;
        [SerializeField, Min(0.1f)] float settle = 9f;

        ItemDefinition showing;
        GameObject model;

        float stride;
        Vector2 drift;
        float lastYaw;
        float lastPitch;

        void Awake()
        {
            if (inventory == null) inventory = GetComponentInParent<PlayerInventory>();
            if (motor == null) motor = GetComponentInParent<PlayerMotor>();

            lastYaw = transform.eulerAngles.y;
            lastPitch = transform.eulerAngles.x;
        }

        void LateUpdate()
        {
            var held = inventory != null ? inventory.Held : null;

            if (held != showing) Show(held);
            if (model == null) return;

            // Walking lifts and drops it; turning lets it fall behind and catch
            // up. Both are small on purpose — a hand that swings as far as the
            // real thing would reads as a flag being waved.
            var speed = motor != null ? motor.Speed : 0f;
            stride += speed * 2.6f * Time.deltaTime;

            var yaw = Mathf.DeltaAngle(lastYaw, transform.eulerAngles.y);
            var pitch = Mathf.DeltaAngle(lastPitch, transform.eulerAngles.x);
            lastYaw = transform.eulerAngles.y;
            lastPitch = transform.eulerAngles.x;

            drift = Vector2.Lerp(
                drift,
                new Vector2(Mathf.Clamp(-yaw, -2.5f, 2.5f), Mathf.Clamp(pitch, -2.5f, 2.5f)) * sway,
                1f - Mathf.Exp(-settle * Time.deltaTime));

            var swing = new Vector3(
                Mathf.Sin(stride) * bob * 0.6f,
                Mathf.Abs(Mathf.Cos(stride)) * -bob,
                0f);

            model.transform.localPosition = restPosition + swing + new Vector3(drift.x, drift.y, 0f);
            model.transform.localRotation = Quaternion.Euler(restRotation) *
                                            Quaternion.Euler(drift.y * 120f, drift.x * 120f, 0f);
        }

        void Show(ItemDefinition item)
        {
            showing = item;

            if (model != null)
            {
                Destroy(model);
                model = null;
            }

            if (item == null || item.WorldPrefab == null) return;

            model = Instantiate(item.WorldPrefab, transform);
            model.name = $"InHand_{item.Id}";
            model.transform.localScale = Vector3.one * scale;

            Strip(model);

            model.transform.localPosition = restPosition;
            model.transform.localRotation = Quaternion.Euler(restRotation);
        }

        /// <summary>
        /// Takes the world off the copy in your hand.
        ///
        /// It is the same prefab that gets thrown across a room, so it arrives
        /// with a rigidbody, colliders, and the component that lets it be picked
        /// up. Left on, the thing in your hand falls out of it, trips the
        /// interaction ray at point-blank range, and offers to be picked up by the
        /// person already holding it.
        /// </summary>
        static void Strip(GameObject instance)
        {
            // Scripts first, and immediately.
            //
            // PickupItem requires a Collider, so taking the collider off while the
            // script is still there is refused outright — and ordinary Destroy is
            // deferred to the end of the frame, so removing the script first does
            // not help: both are still present when the second call is made. Doing
            // it immediately, in order, is what actually takes them off. It also
            // means the copy in your hand never exists as a findable PickupItem,
            // not even for the one frame it was created in.
            foreach (var behaviour in instance.GetComponentsInChildren<MonoBehaviour>())
                DestroyImmediate(behaviour);

            foreach (var body in instance.GetComponentsInChildren<Rigidbody>())
                DestroyImmediate(body);

            foreach (var collider in instance.GetComponentsInChildren<Collider>())
                DestroyImmediate(collider);

            // Half a metre from the lens, a cast shadow is a black bar across the
            // room rather than a shadow of a hammer.
            foreach (var renderer in instance.GetComponentsInChildren<Renderer>())
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
    }
}
