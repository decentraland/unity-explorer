using System;
using System.Collections.Generic;

namespace SceneRuntime.WebClient
{
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
