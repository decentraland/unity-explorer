using System;
using Newtonsoft.Json;
using UnityEngine;

public static class JsonUtils
{
    public static T FromJsonWithNulls<T>(string json)
    {
        return JsonConvert.DeserializeObject<T>(json);
    }

    public static T SafeFromJson<T>(string json)
    {
        T returningValue = default(T);

        if (!string.IsNullOrEmpty(json))
        {
            try { returningValue = JsonUtility.FromJson<T>(json); }
            catch (ArgumentException e) { Debug.LogError("ArgumentException Fail!... Json = " + json + " " + e.ToString()); }
        }

        return returningValue;
    }
}
