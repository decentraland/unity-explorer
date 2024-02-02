namespace DCL.Backpack
{
    public struct BackpackGridSort
    {
        public NftOrderByOperation OrderByOperation;
        public bool SortAscending;

        public BackpackGridSort(NftOrderByOperation orderByOperation, bool sortAscending)
        {
            OrderByOperation = orderByOperation;
            SortAscending = sortAscending;
        }
    }

    public enum NftOrderByOperation
    {
        Date,
        Rarity,
        Name,
    }
}
