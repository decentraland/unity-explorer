using System;
using System.Collections.Generic;

namespace DCL.BadgesAPIService
{
    [Serializable]
    public class BadgesInfo
    {
        // Runtime-only container built by BadgesAPIClient; no serializer (Unity or Newtonsoft) ever reads these fields.
#pragma warning disable UAC1001
        public List<BadgeInfo> achieved;
        public List<BadgeInfo> notAchieved;
#pragma warning restore UAC1001
    }

    public struct BadgeInfo
    {
        public readonly BadgeData data;
        public readonly bool isLocked;
        public readonly bool isNew;

        public BadgeInfo(BadgeData data, bool isLocked, bool isNew)
        {
            this.data = data;
            this.isLocked = isLocked;
            this.isNew = isNew;
        }
    }
}
