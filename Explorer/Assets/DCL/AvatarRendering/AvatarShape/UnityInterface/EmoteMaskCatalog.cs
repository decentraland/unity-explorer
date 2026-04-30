using DCL.ECSComponents;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.AvatarRendering.AvatarShape.UnityInterface
{
    /// <summary>
    /// Maps <see cref="AvatarEmoteMask"/> values (the protocol-level mask choice scenes ask for)
    /// to the avatar-side <see cref="AvatarMask"/> asset that drives bone selection for the
    /// manual-blend legacy emote path used in local scene development.
    /// </summary>
    [CreateAssetMenu(menuName = "DCL/Avatar/Emote Mask Catalog", fileName = "EmoteMaskCatalog")]
    public class EmoteMaskCatalog : ScriptableObject
    {
        public const string RESOURCE_NAME = "EmoteMaskCatalog";

        [Serializable]
        private struct Entry
        {
            public AvatarEmoteMask Key;
            public AvatarMask Mask;
        }

        [SerializeField] private List<Entry> entries = new ();

        private static EmoteMaskCatalog? cachedInstance;
        private static bool resolved;

        public static EmoteMaskCatalog? GetCachedInstance()
        {
            if (resolved) return cachedInstance;

            cachedInstance = Resources.Load<EmoteMaskCatalog>(RESOURCE_NAME);
            resolved = true;
            return cachedInstance;
        }

        public bool TryGet(AvatarEmoteMask key, out AvatarMask? mask)
        {
            for (var i = 0; i < entries.Count; i++)
            {
                Entry entry = entries[i];

                if (entry.Key == key && entry.Mask != null)
                {
                    mask = entry.Mask;
                    return true;
                }
            }

            mask = null;
            return false;
        }
    }
}
