using System.Collections.Generic;
using UnityEngine.Pool;
using Newtonsoft.Json.Linq;

namespace Utility.Json
{
    public class JsonObjectBuilder
    {
        private const int STRING_CACHE_SIZE = 100;

        private readonly Dictionary<string, string> stringValues = new ();
        private readonly Dictionary<string, float> floatValues = new ();
        private readonly Dictionary<string, int> intValues = new ();

        private readonly IObjectPool<JObject> jObjectPool = new ObjectPool<JObject>(
            () => new JObject(),
            actionOnRelease: o => o.RemoveAll()
        );

        private readonly Dictionary<string, JToken> stringValuesCache = new ();

        private void Clear()
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

        /// <summary>
        /// <inheritdoc cref="JsonObjectBuilder.Build"/>
        /// </summary>
        public JObject Build()
        {
            var json = jObjectPool.Get()!;

            foreach ((string key, string? value) in stringValues)
                json[key] = ElementForString(value);

            foreach ((string key, float value) in floatValues)
                json[key] = value;

            foreach ((string key, int value) in intValues)
                json[key] = value;

            Clear();

            return json;
        }

        public void Release(JObject jObject)
        {
            jObjectPool.Release(jObject);

            if (stringValuesCache.Count > STRING_CACHE_SIZE)
                stringValuesCache.Clear();
        }

        private JToken? ElementForString(string value)
        {
            if (stringValuesCache.TryGetValue(value, out var element) == false)
                stringValuesCache[value] = element = value;

            return element;
        }
    }
}
