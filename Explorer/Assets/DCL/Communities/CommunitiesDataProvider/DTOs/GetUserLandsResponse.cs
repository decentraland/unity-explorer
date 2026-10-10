
using System;

namespace DCL.Communities.CommunitiesDataProvider.DTOs
{
    [Serializable]
    public class GetUserLandsResponse
    {
        [Serializable]
        public struct LandCoords
        {
            public int x;
            public int y;
        }

        public LandCoords[] lands;
        public int totalAmount;
    }
}


