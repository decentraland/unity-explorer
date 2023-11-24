namespace DCL.PerformanceAndDiagnostics.Optimization.Pools
{
    /// <summary>
    ///     Thread-safe Component Pool
    /// </summary>
    /// <typeparam name="T">Type of Component</typeparam>
    public interface IComponentPoolDCL<T> : IObjectPoolDCL<T>, IComponentPool where T: class
    {
        void IComponentPool.Release(object component) =>
            Release((T)component);

        object IComponentPool.Rent() =>
            Get();
    }
}
