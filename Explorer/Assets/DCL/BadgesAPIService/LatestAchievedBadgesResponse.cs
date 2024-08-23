using System;
using System.Collections.Generic;

namespace DCL.BadgesAPIService
{
    [Serializable]
    public class LatestAchievedBadgesResponse
    {
        public LatestAchievedBadgesData data;
    }

    [Serializable]
    public class LatestAchievedBadgesData
    {
        public List<LatestAchievedBadgeData> latestAchievedBadges;
    }

    [Serializable]
    public class LatestAchievedBadgeData
    {
        public string id;
        public string name;
        public string image;
    }
}
