using Global.AppArgs;
using REnum;
using System;
using System.Collections.Generic;
using Utility.Types;

namespace DCL.RuntimeDeepLink
{
    public readonly struct DeepLink
    {
        private readonly Dictionary<string, string> map;

        private DeepLink(Dictionary<string, string> map)
        {
            this.map = map;
        }

        internal static Result<DeepLink> FromRaw(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw!))
                return Result<DeepLink>.ErrorResult("Empty Input");

            if (raw.StartsWith("decentraland://", StringComparison.Ordinal) == false)
                return Result<DeepLink>.ErrorResult("Wrong Format");

            Dictionary<string, string> map = ApplicationParametersParser.ProcessDeepLinkParameters(raw);
            return Result<DeepLink>.SuccessResult(new DeepLink(map));
        }

        public string? ValueOf(string key)
        {
            map.TryGetValue(key, out string? value);
            return value;
        }
    }
}
