using CRDT.Attribution;
using DCL.McpServer.Core;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace DCL.McpServer.Utils
{
    /// <summary>
    ///     Renders <see cref="ICrdtWriterLog" /> readings into the shapes the state tools report, so the two tools
    ///     that surface writer attribution describe the same row the same way.
    /// </summary>
    public static class CrdtAttributionJson
    {
        /// <summary>Schema of one entry of a tool's per-address summary.</summary>
        public static McpJsonSchema WriterSchema() =>
            McpJsonSchema.Object()
                          .String("address", "Address of the peer, as the transport authenticated it.")
                          .Boolean("isAuthoritativeServer", "True when the address is the scene's authoritative server rather than a player.")
                          .Boolean("isTrustedSource", "True when the writer is the local participant or a scene admin; untrusted writers are subject to component filtering.")
                          .Integer("writes", "Live component writes this address made itself.")
                          .Integer("stateSyncWrites", "Rows this address supplied by answering a request for the scene's CRDT state. It relayed them; it did not necessarily author them.")
                          .Number("lastWriteAgeSeconds");

        /// <summary>Schema of one entry of a tool's per-component attribution.</summary>
        public static McpJsonSchema WriteSchema() =>
            McpJsonSchema.Object()
                          .Integer("componentId")
                          .String("writer", "Address the last observed write of this component came from. When viaStateSync is true this is the peer that handed the state over, NOT necessarily its author.")
                          .Boolean("isAuthoritativeServer", "True only when the authoritative server itself sent this write live; never true for a state-sync row, however it was originally authored.")
                          .Boolean("isTrustedSource")
                          .Boolean("viaStateSync", "True when the row came from a peer's answer to a CRDT state request (how a client joining mid-game hydrates) rather than a live write. Exclude these rows when testing who authored a component.")
                          .String("messageType", "CRDT message type of the write, e.g. PUT_COMPONENT_NETWORK.")
                          .Integer("crdtTimestamp", "Lamport timestamp that orders this write against other writes of the same component.")
                          .Number("ageSeconds");

        public static JArray Writers(IReadOnlyList<CrdtWriterSummary> writers)
        {
            var array = new JArray();

            foreach (CrdtWriterSummary writer in writers)
                array.Add(new JObject
                {
                    ["address"] = writer.Address,
                    ["isAuthoritativeServer"] = writer.IsAuthoritativeServer,
                    ["isTrustedSource"] = writer.IsTrustedSource,
                    ["writes"] = writer.Writes,
                    ["stateSyncWrites"] = writer.StateSyncWrites,
                    ["lastWriteAgeSeconds"] = Rounded(writer.LastWriteAgeSeconds),
                });

            return array;
        }

        public static JArray Writes(IReadOnlyList<CrdtWrite> writes)
        {
            var array = new JArray();

            foreach (CrdtWrite write in writes)
                array.Add(new JObject
                {
                    ["componentId"] = write.ComponentId,
                    ["writer"] = write.Writer,
                    ["isAuthoritativeServer"] = write.IsAuthoritativeServer,
                    ["isTrustedSource"] = write.IsTrustedSource,
                    ["viaStateSync"] = write.ViaStateSync,
                    ["messageType"] = write.MessageType.ToString(),
                    ["crdtTimestamp"] = write.CrdtTimestamp,
                    ["ageSeconds"] = Rounded(write.AgeSeconds),
                });

            return array;
        }

        /// <summary>
        ///     The human-readable mirror of <see cref="Writes" />, appended to a tool's text content. States the
        ///     "no rows" case explicitly, because an empty section reads as an error while "written only locally"
        ///     is the ordinary answer for a single-player scene.
        /// </summary>
        public static void AppendWrites(StringBuilder output, IReadOnlyList<CrdtWrite> writes)
        {
            output.AppendLine();
            output.AppendLine("Network writers (last write per component, from the scene room):");

            if (writes.Count == 0)
            {
                output.AppendLine("  none — every component of this entity was written by the scene's own code, not by a peer");
                return;
            }

            foreach (CrdtWrite write in writes)
            {
                output.Append("  component ").Append(write.ComponentId);

                // A state-sync row is phrased as a relay, never as authorship, so the text cannot be misread the way
                // "← <address>" would be: the peer that answered the state request may not have written any of it.
                if (write.ViaStateSync)
                    output.Append(" ← state sync via ").Append(write.Writer).Append(" (author unknown)");
                else
                    output.Append(" ← ")
                          .Append(write.Writer)
                          .Append(write.IsAuthoritativeServer ? " (authoritative server)" : write.IsTrustedSource ? " (trusted)" : " (untrusted peer)");

                output.Append(", ")
                      .Append(write.MessageType)
                      .Append(", ")
                      .Append(Rounded(write.AgeSeconds).ToString(CultureInfo.InvariantCulture))
                      .AppendLine("s ago");
            }
        }

        private static double Rounded(double seconds) =>
            System.Math.Round(seconds, 2);
    }
}
