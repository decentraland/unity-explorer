using DCL.Web3.Authenticators;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Text;

namespace DCL.Web3
{
    /// <summary>
    ///     Renders a web3 request verbatim for the raw review panel, which is what the confirmation popup
    ///     shows when <see cref="TransactionRecipientDecoder" /> maps the request to
    ///     <see cref="TransactionKind.Unknown" /> and there is nothing to summarize.
    /// </summary>
    public static class TransactionRawPayload
    {
        public static string Format(TransactionConfirmationRequest request)
        {
            var payload = new StringBuilder();

            AppendField(payload, "Method", request.Method);
            AppendField(payload, "Network", Network(request));

            if (request.IsTypedDataSignature)
                AppendField(payload, "Typed data", Indent(request.TypedData));
            else
            {
                AppendField(payload, "To", request.To);

                // A zero value and empty calldata reach the popup as null; both are still worth stating.
                AppendField(payload, "Value", string.IsNullOrEmpty(request.Value) ? "0x0" : request.Value!);
                AppendField(payload, "Data", string.IsNullOrEmpty(request.Data) ? "0x" : request.Data!);
            }

            return payload.ToString().TrimEnd();
        }

        private static string Network(TransactionConfirmationRequest request) =>
            string.IsNullOrEmpty(request.NetworkName)
                ? request.ChainId.ToString()
                : $"{request.NetworkName} ({request.ChainId})";

        private static void AppendField(StringBuilder payload, string label, string? value)
        {
            if (string.IsNullOrEmpty(value))
                return;

            payload.Append(label).Append('\n').Append(value).Append("\n\n");
        }

        /// <summary>
        ///     Re-serializes JSON with indentation so a one-line EIP-712 payload is readable, leaving
        ///     anything unparseable exactly as it arrived.
        /// </summary>
        private static string? Indent(string? json)
        {
            if (string.IsNullOrEmpty(json))
                return json;

            try { return JToken.Parse(json).ToString(Formatting.Indented); }
            catch (JsonException) { return json; }
        }
    }
}
