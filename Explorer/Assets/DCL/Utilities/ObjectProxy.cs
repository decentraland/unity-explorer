namespace DCL.Utilities
{
    public class ObjectProxy<T>
    {
        public T? Object { get; private set; }

        public bool Configured { get; private set; }

        public void SetObject(T targetObject)
        {
            Object = targetObject;
            Configured = true;
        }

        public void ReleaseObject()
        {
            if (!Configured) return;
            Configured = false;

            Object = default(T?);
        }
    }
}
