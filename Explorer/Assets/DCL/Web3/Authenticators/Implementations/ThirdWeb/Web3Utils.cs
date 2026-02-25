using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace DCL.Web3
{
    public static class Web3Utils
    {
        public static (string? to, string? value, string? data) ParseSendTxRequestParams(EthApiRequest request)
        {
            Dictionary<string, object>? txParams = JsonConvert.DeserializeObject<Dictionary<string, object>>(request.@params[0].ToString());
            string? to = txParams?.TryGetValue("to", out object? toValue) == true ? toValue?.ToString() : null;
            string? value = txParams?.TryGetValue("value", out object? valueValue) == true ? valueValue?.ToString() ?? "0x0" : "0x0";
            string? data = txParams?.TryGetValue("data", out object? dataValue) == true ? dataValue?.ToString() ?? "0x" : "0x";
            return (to, value, data);
        }

        public static BigInteger ParseHexToBigInteger(string hexValue)
        {
            if (string.IsNullOrEmpty(hexValue) || hexValue == "0x" || hexValue == "0x0")
                return BigInteger.Zero;

            string hex = hexValue.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
                ? hexValue[2..]
                : hexValue;

            return BigInteger.Parse(hex, System.Globalization.NumberStyles.HexNumber);
        }
    }
}
