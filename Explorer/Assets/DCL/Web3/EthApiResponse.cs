using Newtonsoft.Json;

namespace DCL.Web3
{
    public struct EthApiResponse
    {
        // Null-tolerant: servers echo "id": null for requests they could not parse, which must land as 0
        // instead of failing the whole deserialization. A long is never null, so serialization is unchanged.
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public long id;
        public string jsonrpc;
        public object? result;

        // Omitted from serialized payloads when null, so responses forwarded to SDK scenes keep their shape.
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public EthApiError? error;
    }

    // JSON-RPC 2.0 error member: https://www.jsonrpc.org/specification#error_object
    public class EthApiError
    {
        public long code;
        public string? message;
    }
}
