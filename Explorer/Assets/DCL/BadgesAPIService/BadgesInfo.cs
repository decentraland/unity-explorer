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

    [Serializable]
    public class BadgeInfo : BadgeData
    {
        public bool isLocked;
        public bool isNew;
    }
}
