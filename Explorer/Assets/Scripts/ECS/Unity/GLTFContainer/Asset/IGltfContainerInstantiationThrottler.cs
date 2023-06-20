namespace ECS.Unity.GLTFContainer.Asset
{
    /// <summary>
    ///     Limits the number of synchronous instantiations per frame
    /// </summary>
    public interface IGltfContainerInstantiationThrottler
    {
        /// <summary>
        ///     Returns true if instantiation allowed
        /// </summary>
        /// <returns></returns>
        bool Acquire(int count = 1);

        /// <summary>
        ///     Reset, should be called at the end of the accounting period
        /// </summary>
        void Reset();
    }
}
