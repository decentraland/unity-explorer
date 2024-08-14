using System;
using System.Collections.Generic;

namespace DCL.BadgesAPIService
{
    [Serializable]
    public class BadgesResponse
    {
        public List<BadgeData> data;
    }

    [Serializable]
    public class BadgeData
    {
        public string badge_id;
        public string awarded_at;
    }
}
