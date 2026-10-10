using System.Collections.Generic;
using UnityEngine.Pool;

namespace DCL.SDKComponents.AvatarModifierArea.Components
{
    public struct AvatarModifierAreaComponent
    {
        public readonly HashSet<string> ExcludedIds;

        public AvatarModifierAreaComponent(IEnumerable<string> excludedIds)
        {
            ExcludedIds = HashSetPool<string>.Get();
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

        public void Dispose()
        {
            HashSetPool<string>.Release(ExcludedIds);
        }
    }
}
