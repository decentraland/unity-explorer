namespace DCL.Pools
{
    /// <summary>
    ///     DCL replication of Unity object pooling codebase
    /// </summary>
    public interface IObjectPoolDCL<T> where T: class
    {
        int CountInactive { get; }

        T Get();

        PooledObjectDCL<T> Get(out T v);

        void Release(T element);

        void Clear();
    }
}
