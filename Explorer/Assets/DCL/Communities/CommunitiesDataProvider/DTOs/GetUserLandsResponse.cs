
namespace DCL.Communities
{
    public class GetUserLandsResponse
    {
        public class LandCoords
        {
            public int x;
            public int y;
        }

        public LandCoords[] lands;
        public int totalPages;
    }
}


