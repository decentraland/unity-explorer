using System;
using System.Collections.Generic;

namespace DCL.BadgesAPIService
{
    [Serializable]
    public class BadgesInfo
    {
        public List<BadgeInfo> achieved;
        public List<BadgeInfo> notAchieved;
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
