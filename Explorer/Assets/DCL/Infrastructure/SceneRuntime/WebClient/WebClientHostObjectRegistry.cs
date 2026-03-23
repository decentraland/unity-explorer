using System;
using System.Collections.Generic;

namespace SceneRuntime.WebClient
{
    /// <summary>
    ///     Process-wide registry that maps <c>(contextId, objectId)</c> pairs to the C# objects exposed to JavaScript as host
    ///     objects. The jslib layer calls back into C# with these IDs when JavaScript invokes a method on a host object.
    ///     <para>
    ///         All objects registered for a given context are removed at once when the engine is disposed via
    ///         <see cref="UnregisterAll" />.
    ///     </para>
    /// </summary>
    internal static class WebClientHostObjectRegistry
    {
        private static readonly Dictionary<string, Dictionary<string, object>> CONTEXT_OBJECTS = new ();

        public static void Register(string contextId, string objectId, object obj)
        {
            if (contextId == null) throw new ArgumentNullException(nameof(contextId));
            if (objectId == null) throw new ArgumentNullException(nameof(objectId));

            if (!CONTEXT_OBJECTS.TryGetValue(contextId, out Dictionary<string, object> objects))
            {
                objects = new Dictionary<string, object>();
                CONTEXT_OBJECTS[contextId] = objects;
            }

            objects[objectId] = obj;
        }

        public static object? Get(string contextId, string objectId)
        {
            if (contextId == null) throw new ArgumentNullException(nameof(contextId));
            if (objectId == null) throw new ArgumentNullException(nameof(objectId));

            if (CONTEXT_OBJECTS.TryGetValue(contextId, out Dictionary<string, object> objects))
            {
                objects.TryGetValue(objectId, out object? obj);
                return obj;
            }

            return null;
        }

        public static void Unregister(string contextId, string objectId)
        {
            if (contextId == null) throw new ArgumentNullException(nameof(contextId));
            if (objectId == null) throw new ArgumentNullException(nameof(objectId));

            if (CONTEXT_OBJECTS.TryGetValue(contextId, out Dictionary<string, object> objects))
                objects.Remove(objectId);
        }

        public static void UnregisterAll(string contextId)
        {
            if (contextId == null) throw new ArgumentNullException(nameof(contextId));
            CONTEXT_OBJECTS.Remove(contextId);
        }
    }
}
