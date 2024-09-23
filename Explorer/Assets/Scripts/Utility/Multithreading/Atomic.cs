namespace Utility.Multithreading
{
    public interface IAtomic<T> where T: struct
    {
        void Set(T newValue);

        T Value();


        /// <summary>
        ///     No atomicity! Only for testing purposes.
        /// </summary>
        class FakeAtomic : IAtomic<T>
        {
            private T value;

            public void Set(T newValue)
            {
                value = newValue;
            }

            public T Value() =>
                value;
        }
    }

    public class Atomic<T> : IAtomic<T> where T: struct
    {
        private T value;
        private readonly object locker = new ();

        public Atomic() : this(default(T)) { }

        public Atomic(T value)
        {
            this.value = value;
        }

        public void Set(T newValue)
        {
            lock (locker) { value = newValue; }
        }

        public T Value()
        {
            lock (locker) { return value; }
        }
    }
}
