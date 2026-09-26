using System.Collections.Generic;

namespace DCL.Communities.CommunitiesCard
{
    public class SectionFetchData<T>
    {
        public int PageNumber;
        public int TotalFetched;
        public int TotalToFetch;

        public readonly List<T> Items;

        public SectionFetchData(int pageSize)
        {
            PageNumber = 0;
            TotalFetched = 0;
            TotalToFetch = 0;
            Items = new List<T>(pageSize);
        }

        public void Reset()
        {
            PageNumber = 0;
            TotalFetched = 0;
            TotalToFetch = 0;
            Items.Clear();
        }
    }
}
