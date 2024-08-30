using Segment.Serialization;
using System.Collections.Generic;
using UnityEngine.Pool;

namespace Utility.Json
{
    public class JsonObjectBuilder : IJsonObjectBuilder
    {
        private const int STRING_CACHE_SIZE = 100;

        private readonly Dictionary<string, string> stringValues = new ();
        private readonly Dictionary<string, float> floatValues = new ();
        private readonly Dictionary<string, int> intValues = new ();

        private readonly IObjectPool<JsonObject> jsonObjectPool = new ObjectPool<JsonObject>(
            () => new JsonObject(),
            actionOnRelease: o => o.Clear()
        );

        private readonly Dictionary<string, JsonElement> stringValuesCache = new ();

        public void Clear()
        {
            stringValues.Clear();
            floatValues.Clear();
            intValues.Clear();
        }

        public void Set(string key, string value)
        {
            stringValues[key] = value;
        }

        public void Set(string key, float value)
        {
            floatValues[key] = value;
        }

        public void Set(string key, int value)
        {
            intValues[key] = value;
        }

        public JsonObject Build()
        {
            var json = jsonObjectPool.Get()!;

            foreach ((string key, string? value) in stringValues)
                json[key] = ElementForString(value);

            foreach ((string key, float value) in floatValues)
                json[key] = value;

            foreach ((string key, int value) in intValues)
                json[key] = value;

            return json;
        }

        public void Release(JsonObject jsonObject)
        {
            jsonObjectPool.Release(jsonObject);
        }

        public void DisposeCacheIfNeeded()
        {
            if (stringValuesCache.Count > STRING_CACHE_SIZE)
                stringValuesCache.Clear();
        }

        private JsonElement ElementForString(string value)
        {
            if (stringValuesCache.TryGetValue(value, out var element) == false)
                stringValuesCache[value] = element = value;

            return element;
        }
    }
}
