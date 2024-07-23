using System;

namespace DCL.Utilities
{
    public class ObjectProxy<T>
    {
        /// <summary>
        ///     Returns the object if it's configured, otherwise throws an exception
        /// </summary>
        public T StrictObject
        {
            get
            {
                if (Configured == false)
                    throw new InvalidOperationException($"{typeof(T).Name} proxy is not configured");

                return Object!;
            }
        }

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
