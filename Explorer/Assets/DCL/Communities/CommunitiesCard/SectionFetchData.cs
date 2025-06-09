using System.Collections.Generic;

namespace DCL.Communities.CommunitiesCard
{
    public class SectionFetchData<T>
    {
        private readonly int pageSize;

        public int pageNumber;
        public int totalFetched;
        public int totalToFetch;

        public readonly List<T> items;

        public SectionFetchData(int pageSize)
        {
            this.pageSize = pageSize;
            pageNumber = 0;
            totalFetched = 0;
            totalToFetch = 0;
            items = new List<T>(pageSize);
        }

        public void Reset()
        {
            pageNumber = 0;
            totalFetched = 0;
            totalToFetch = 0;
            items.Clear();
        }
    }
}
