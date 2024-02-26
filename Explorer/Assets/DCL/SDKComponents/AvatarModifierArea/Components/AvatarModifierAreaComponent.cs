using Google.Protobuf.Collections;
using System.Collections.Generic;

namespace DCL.SDKComponents.AvatarModifierArea.Components
{
    public struct AvatarModifierAreaComponent
    {
        public readonly HashSet<string> ExcludedIds;

        public AvatarModifierAreaComponent(RepeatedField<string> excludedIds)
        {
            ExcludedIds = new HashSet<string>();
            SetExcludedIds(excludedIds);
        }

        public void SetExcludedIds(RepeatedField<string> excludedIds)
        {
            ExcludedIds.Clear();

            foreach (string excludedId in excludedIds) { ExcludedIds.Add(excludedId.ToLower()); }
        }
    }
}
