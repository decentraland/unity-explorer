using CommunicationData.URLHelpers;
using System;
using UnityEngine;

namespace DCL.Ipfs
{
    public static class IpfsHelper
    {
        private const string URN_PREFIX = "urn:decentraland:entity:";

        public static Vector2Int DecodePointer(string pointer)
        {
            int commaPosition = pointer.IndexOf(",", StringComparison.Ordinal);
            ReadOnlySpan<char> span = pointer.AsSpan();

            ReadOnlySpan<char> firstPart = span[..commaPosition];
            ReadOnlySpan<char> secondPart = span[(commaPosition + 1)..];

            return new Vector2Int(int.Parse(firstPart), int.Parse(secondPart));
        }

        // Example:
        // urn:decentraland:entity:bafkreia2eo3hbl74iddvaxbx7wuzdjjhbvdyyrywsviamdejppza6vrl4y?=&baseUrl=https://sdk-team-cdn.decentraland.org/ipfs/
        public static IpfsPath ParseUrn(string urn)
        {
            const string BASE_URL_TOKEN = "baseUrl=";

            ReadOnlySpan<char> urnSpan = urn.AsSpan();

            // skip the prefix entirely we don't need it
            urnSpan = urnSpan[URN_PREFIX.Length..];

            // isolate entity id
            string entityId;
            string baseUrl;
            int qmIndex = urnSpan.IndexOf('?');

            if (qmIndex > -1)
            {
                entityId = urnSpan[..qmIndex].ToString();

                // isolate base URL
                int indexOfBaseUrl = urnSpan.IndexOf(BASE_URL_TOKEN);

                if (indexOfBaseUrl > -1)
                {
                    urnSpan = urnSpan[(indexOfBaseUrl + BASE_URL_TOKEN.Length)..];

                    // Take the rest of the string until next '&'
                    int indexOfAmp = urnSpan.IndexOf('&');
                    baseUrl = indexOfAmp > -1 ? urnSpan[..indexOfAmp].ToString() : urnSpan.ToString();
                }
                else
                    baseUrl = string.Empty;
            }
            else
            {
                entityId = urnSpan.ToString();

                // No base URL
                baseUrl = string.Empty;
            }

            return new IpfsPath(entityId, URLDomain.FromString(baseUrl));
        }
    }
}
