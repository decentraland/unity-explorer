
using System;

namespace DCL.Communities.CommunitiesDataProvider.DTOs
{
    [Serializable]
    public class GetUserWorldsResponse
    {
        [Serializable]
        public struct World
        {
            public string worldName;
        }

        public World[] worlds;
        public int totalAmount;
    }
}


