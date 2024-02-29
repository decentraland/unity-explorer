using System.Collections;
using System.Collections.Generic;

namespace DCL.SDKComponents.AvatarModifierArea.Components
{
    public struct AvatarModifierAreaComponent
    {
        public readonly HashSet<string> ExcludedIds;

        public AvatarModifierAreaComponent(IEnumerable<string> excludedIds)
        {
            ExcludedIds = new HashSet<string>();
            SetExcludedIds(excludedIds);
        }

        public void SetExcludedIds(IEnumerable<string> excludedIds)
        {
            ExcludedIds.Clear();

            foreach (string excludedId in excludedIds)
            {
                // Profile component stores UserId in lowercase...
                ExcludedIds.Add(excludedId.ToLower());
            }
        }
    }
}
