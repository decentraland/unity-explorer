using System;
using System.Collections.Generic;

namespace DCL.BadgesAPIService
{
    [Serializable]
    public class TiersResponse
    {
        public TiersData data;
    }

    [Serializable]
    public class TiersData
    {
        public List<TierData> tiers;
    }

    [Serializable]
    public class TierData
    {
        public string tierId;
        public string tierName;
        public string description;
        public BadgeAssetsData assets;
        public BadgeTierCriteria criteria;
    }

    [Serializable]
    public class BadgeTierCriteria
    {
        public int steps;
    }
}
