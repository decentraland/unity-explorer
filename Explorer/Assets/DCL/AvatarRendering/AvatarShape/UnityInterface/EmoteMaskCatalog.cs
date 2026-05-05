using DCL.ECSComponents;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.AvatarRendering.AvatarShape.UnityInterface
{
    /// <summary>
    /// Maps <see cref="AvatarEmoteMask"/> values (the protocol-level mask choice scenes ask for)
    /// to the unity-side <see cref="AvatarMask"/> asset that drives bone selection for the
    /// manual-blend legacy masked emote path used in local scene development.
    /// </summary>
    [CreateAssetMenu(menuName = "DCL/Avatar/Emote Mask Catalog", fileName = "EmoteMaskCatalog")]
    public class EmoteMaskCatalog : ScriptableObject
    {
        [Serializable]
        private struct Entry
        {
            public AvatarEmoteMask Key;
            public AvatarMask Mask;
        }

        [SerializeField] private List<Entry> entries = new ();

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
