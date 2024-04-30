using System;
using UnityEngine.Pool;

namespace DCL.Optimization.Pools
{
    public class LogComponentPool<T> : IComponentPool<T> where T: class
    {
        private const string PREFIX = "LogComponentPool:";
        private static readonly string COMPONENT_NAME = typeof(T).Name;
        private readonly IComponentPool<T> origin;
        private readonly Action<string> log;

        public LogComponentPool(IComponentPool<T> origin, Action<string> log)
        {
            this.origin = origin;
            this.log = log;
        }

        public void ClearThrottled(int maxUnloadAmount)
        {
            log($"{PREFIX}: ClearThrottled {maxUnloadAmount} {COMPONENT_NAME}");
            origin.ClearThrottled(maxUnloadAmount);
        }

        public T Get()
        {
            log($"{PREFIX}: Get {COMPONENT_NAME}");
            return origin.Get();
        }

        public PooledObject<T> Get(out T v)
        {
            log($"{PREFIX}: Get with PooledObject {COMPONENT_NAME}");
            return origin.Get(out v);
        }

        public void Release(T element)
        {
            log($"{PREFIX}: Release {COMPONENT_NAME}");
            origin.Release(element);
        }

        public void Clear()
        {
            log($"{PREFIX}: Clear {COMPONENT_NAME}");
            origin.Clear();
        }

        public int CountInactive
        {
            get
            {
                int result = origin.CountInactive;
                log($"{PREFIX}: CountInactive {result} {COMPONENT_NAME}");
                return result;
            }
        }

        public void Dispose()
        {
            log($"{PREFIX}: Dispose {COMPONENT_NAME}");
            origin.Dispose();
        }
    }
}
