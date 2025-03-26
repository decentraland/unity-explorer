using System;
using Newtonsoft.Json;
using UnityEngine;

namespace Utility
{
    public static class JsonUtils
    {
        public static T FromJsonWithNulls<T>(string json) =>
            JsonConvert.DeserializeObject<T>(json);

        public static T SafeFromJson<T>(string json)
        {
            T returningValue = default(T);

            if (!string.IsNullOrEmpty(json))
            {
                try { returningValue = JsonUtility.FromJson<T>(json); }
                catch (ArgumentException e) { Debug.LogError(string.Format("ArgumentException Fail!... Json = {0} {1}", json, e)); }
            }

            return returningValue;
        }
    }
}
