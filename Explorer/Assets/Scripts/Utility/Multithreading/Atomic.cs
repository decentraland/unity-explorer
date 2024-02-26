namespace Utility.Multithreading
{
    public class Atomic<T> where T: struct
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
