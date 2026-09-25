using DCL.AvatarRendering.AvatarShape.UnityInterface;
using System.Reflection;
using UnityEngine;

namespace DCL.AvatarRendering.AvatarShape.Tests
{
    /// <summary>
    ///     The AvatarBase test prefab leaves its rig references unassigned, and <see cref="AvatarBase.ResetState" /> touches all
    ///     of them. Wires a throwaway component of each property's type into the private serialized backing fields.
    /// </summary>
    public static class AvatarBaseTestRigWiring
    {
        private static readonly string[] RIG_PROPERTIES =
        {
            "RigBuilder", "FeetIKRig", "HandsIKRig", "HeadIKRig", "TorsoIKRig", "CachePoseRig", "AdditiveBreathRig",
            "HipsConstraint", "LeftLegIK", "RightLegIK",
        };

        /// <summary>
        ///     Parents one holder object per rig reference under the avatar. Run it after the avatar's Awake so the holders are
        ///     not part of the rest pose the avatar captured.
        /// </summary>
        public static void WireMissingRigReferences(AvatarBase avatarBase)
        {
            foreach (string propertyName in RIG_PROPERTIES)
            {
                var holder = new GameObject($"test-rig-{propertyName}");
                holder.transform.SetParent(avatarBase.transform, false);

                Component component = holder.AddComponent(typeof(AvatarBase).GetProperty(propertyName)!.PropertyType);
                WireBackingField(avatarBase, propertyName, component);
            }
        }

        private static void WireBackingField(AvatarBase avatarBase, string propertyName, Object value) =>
            typeof(AvatarBase).GetField($"<{propertyName}>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance)!
                              .SetValue(avatarBase, value);
    }
}
