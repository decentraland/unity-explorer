using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

namespace Ipfs
{
    public static class IpfsHelper
    {
        public static Vector2Int DecodePointer(string pointer)
        {
            int commaPosition = pointer.IndexOf(",", StringComparison.Ordinal);
            ReadOnlySpan<char> span = pointer.AsSpan();

            ReadOnlySpan<char> firstPart = span[..commaPosition];
            ReadOnlySpan<char> secondPart = span[(commaPosition + 1)..];

            return new Vector2Int(int.Parse(firstPart), int.Parse(secondPart));
        }

        public static void GetParams(string url, ref Dictionary<string, string> ret)
        {
            ret.Clear();
            string parameters = url.TrimStart('?');
            if (parameters.Length == 0) return;

            string[] paramsArray = parameters.Split('&');

            foreach (string param in paramsArray)
            {
                string[] keyValue = param.Split('=');

                if (keyValue.Length > 1) { ret[keyValue[0]] = keyValue[1]; }
                else { ret[keyValue[0]] = ""; }
            }
        }

        // Example:
        // urn:decentraland:entity:bafkreia2eo3hbl74iddvaxbx7wuzdjjhbvdyyrywsviamdejppza6vrl4y?=&baseUrl=https://sdk-team-cdn.decentraland.org/ipfs/
        public static IpfsTypes.IpfsPath ParseUrn(string urn)
        {
            Dictionary<string, string> ret = new Dictionary<string, string>();

            var regex = new Regex("^(urn\\:decentraland\\:entity\\:(ba[a-zA-Z0-9]{57}))");
            var matches = regex.Match(urn);

            if (!matches.Success) { return null; }

            GetParams(urn, ref ret);
            string baseUrl = ret.GetValueOrDefault("baseUrl", "");

            return new IpfsTypes.IpfsPath()
            {
                Urn = matches.Groups[0].Value,
                EntityId = matches.Groups[2].Value,
                BaseUrl = baseUrl,
            };
        }
    }
}
