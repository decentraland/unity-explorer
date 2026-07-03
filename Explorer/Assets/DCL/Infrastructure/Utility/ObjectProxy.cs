using System;

namespace DCL.Utilities
{
    /// <summary>
    ///     A slot filled after construction. This is an anti-pattern for dependency wiring: it hides construction
    ///     order from the type system and turns missing dependencies into runtime failures.
    ///     <para>
    ///         DO NOT create new instances. The only legitimate uses are values with a true runtime lifecycle that
    ///         no construction order can provide (the main player's <c>AvatarBase</c>, the camera <c>Entity</c>).
    ///         For everything else use a decoupling recipe from
    ///         docs/architecture-overview.md, section "Deferred dependencies - decoupling without ObjectProxy".
    ///     </para>
    /// </summary>
    public class ObjectProxy<T>
    {
        public event Action<T> OnObjectSet;

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
            OnObjectSet?.Invoke(Object);
        }

        public void ReleaseObject()
        {
            if (!Configured) return;
            Configured = false;

            Object = default(T?);
        }
    }
}
