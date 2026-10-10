// TRUST_WEBGL_THREAD_SAFETY_FLAG
namespace Utility.Multithreading
{
    public class Mutex<T>
    {
#if !UNITY_WEBGL
        private readonly object lockObject = new();
#endif
        private T value;

        public Mutex(T value)
        {
            this.value = value;
        }

        public Guard Lock()
        {
#if !UNITY_WEBGL
            System.Threading.Monitor.Enter(lockObject);
#endif
            return new Guard(this);
        }

        public readonly ref struct Guard
        {
            private readonly Mutex<T> mutex;

            internal Guard(Mutex<T> mutex)
            {
                this.mutex = mutex;
            }

            public ref T Value => ref mutex.value;

            public void Dispose()
            {
#if !UNITY_WEBGL
                System.Threading.Monitor.Exit(mutex.lockObject);
#endif
            }
        }
    }
}

