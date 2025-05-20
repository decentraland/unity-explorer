
namespace DCL.Communities
{
    public class GetUserWorldsResponse
    {
        public class World
        {
            public string worldName;
        }

        public World[] worlds;
        public int totalPages;
    }
}


