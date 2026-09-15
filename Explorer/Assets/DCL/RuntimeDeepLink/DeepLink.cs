using DCL.Utility.Types;
using Global.AppArgs;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.RuntimeDeepLink
{
    public readonly struct DeepLink
    {
        private readonly Dictionary<string, string> map;

        private DeepLink(Dictionary<string, string> map)
        {
            this.map = map;
        }

        public static Result<DeepLink> FromRaw(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return Result<DeepLink>.ErrorResult("Empty Input");

            if (raw.StartsWith("decentraland://", StringComparison.Ordinal) == false)
                return Result<DeepLink>.ErrorResult("Wrong Format");

            Dictionary<string, string> map = ApplicationParametersParser.ProcessDeepLinkParameters(raw);
            return Result<DeepLink>.SuccessResult(new DeepLink(map));
        }

        public static Result<DeepLink> FromJson(string json)
        {
            try { return FromRaw(JsonUtility.FromJson<DeepLinkDTO>(json).deeplink); }
            catch (ArgumentException e) { return Result<DeepLink>.ErrorResult(e.Message); }
        }

        public string? ValueOf(string key)
        {
            map.TryGetValue(key, out string? value);
            return value;
        }

        public override string ToString() =>
            JsonConvert.SerializeObject(map);

        [Serializable]
        private struct DeepLinkDTO
        {
            // ReSharper disable once InconsistentNaming
            public string? deeplink;
        }
    }
}
