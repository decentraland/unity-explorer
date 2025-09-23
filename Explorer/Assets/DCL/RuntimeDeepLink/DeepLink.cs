using System;
using UnityEngine;
using Utility.Types;

namespace DCL.RuntimeDeepLink
{
    [Serializable]
    public struct JsonDeepLink
    {
        public string? deeplink;

        public static Result<JsonDeepLink> FromJson(string json)
        {
            JsonDeepLink dto = JsonUtility.FromJson<JsonDeepLink>(json);
            string? raw = dto.deeplink;
            return FromRaw(raw);
        }

        private static Result<JsonDeepLink> FromRaw(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw!))
                return Result<JsonDeepLink>.ErrorResult("Empty Input");

            if (raw.StartsWith("decentraland://", StringComparison.Ordinal) == false)
                return Result<JsonDeepLink>.ErrorResult("Wrong Format");

            return Result<JsonDeepLink>.SuccessResult(new JsonDeepLink { deeplink = raw });
        }

        public override string ToString() =>
            deeplink ?? string.Empty;
    }
}
