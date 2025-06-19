using Global.AppArgs;
using REnum;
using System;
using System.Collections.Generic;

namespace DCL.RuntimeDeepLink
{
    public readonly struct DeepLink
    {
        private readonly Dictionary<string, string> map;

        private DeepLink(Dictionary<string, string> map)
        {
            this.map = map;
        }

        internal static DeepLinkCreateResult FromRaw(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw!))
                return DeepLinkCreateResult.EmptyInput();

            if (raw.StartsWith("decentraland://", StringComparison.Ordinal) == false)
                return DeepLinkCreateResult.WrongFormat();

            Dictionary<string, string> map = ApplicationParametersParser.ProcessDeepLinkParameters(raw);
            return DeepLinkCreateResult.FromDeepLink(new DeepLink(map));
        }

        public string? ValueOf(string key)
        {
            map.TryGetValue(key, out string? value);
            return value;
        }
    }

    [REnum]
    [REnumField(typeof(DeepLink))]
    [REnumFieldEmpty("WrongFormat")]
    [REnumFieldEmpty("EmptyInput")]
    public partial struct DeepLinkCreateResult { }
}
