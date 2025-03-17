using UnityEngine.Pool;

namespace DCL.Optimization.Pools
{
    public interface IExtendedObjectPool<T> : IThrottledClearable, IObjectPool<T> where T: class { }

    public interface IThrottledClearable
    {
        void ClearThrottled(int maxUnloadAmount);
    }
}
