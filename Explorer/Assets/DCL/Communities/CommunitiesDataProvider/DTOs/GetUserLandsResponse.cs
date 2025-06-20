
using System;

namespace DCL.Communities
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


