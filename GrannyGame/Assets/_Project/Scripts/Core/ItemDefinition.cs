using UnityEngine;

namespace Granny.Core
{
    /// <summary>
    /// Where an item lives once picked up.
    /// </summary>
    public enum ItemCarry
    {
        /// <summary>Occupies the hands — a hammer, a crowbar, a shotgun. Only one at a time.</summary>
        Held,

        /// <summary>Small enough to pocket into the item bar — keys, cogs, batteries.</summary>
        Pocketed,
    }

    /// <summary>
    /// Everything the game needs to know about one kind of item. Authored as an
    /// asset so the item table, the spawn tables and the locks can be retuned
    /// without touching code.
    /// </summary>
    [CreateAssetMenu(menuName = "Granny/Item Definition", fileName = "Item_")]
    public sealed class ItemDefinition : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Stable id used by saves and by locks. Never renamed once shipped.")]
        [SerializeField] string id = "item";
        [SerializeField] string displayName = "Item";
        [TextArea(2, 4)]
        [SerializeField] string description = "";
        [SerializeField] Sprite icon;

        [Header("Carrying")]
        [SerializeField] ItemCarry carry = ItemCarry.Held;

        [Tooltip("World prefab spawned when the item is dropped or thrown.")]
        [SerializeField] GameObject worldPrefab;

        [Header("Use")]
        [Tooltip("Tags a lock or puzzle can require, e.g. 'pry', 'cut', 'key.front'. " +
                 "Matching on tags rather than item ids lets a crowbar and a hammer " +
                 "both open a planked door without either knowing about the door.")]
        [SerializeField] string[] tags = System.Array.Empty<string>();

        [Tooltip("Destroyed after a successful use — a single-use key, a battery.")]
        [SerializeField] bool consumedOnUse;

        [Header("Throwing")]
        [Tooltip("How loud this makes when it lands. Scales the noise radius.")]
        [SerializeField, Range(0f, 2f)] float impactLoudness = 1f;

        public string Id => id;
        public string DisplayName => displayName;
        public string Description => description;
        public Sprite Icon => icon;
        public ItemCarry Carry => carry;
        public GameObject WorldPrefab => worldPrefab;
        public bool ConsumedOnUse => consumedOnUse;
        public float ImpactLoudness => impactLoudness;

        public bool HasTag(string tag)
        {
            if (string.IsNullOrEmpty(tag)) return false;

            foreach (var candidate in tags)
                if (string.Equals(candidate, tag, System.StringComparison.OrdinalIgnoreCase))
                    return true;

            return false;
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            // A blank id would collide with every other blank id in save data.
            if (string.IsNullOrWhiteSpace(id))
                id = name.StartsWith("Item_") ? name[5..].ToLowerInvariant() : name.ToLowerInvariant();
        }
#endif
    }
}
