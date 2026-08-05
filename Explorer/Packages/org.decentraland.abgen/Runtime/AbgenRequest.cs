using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Decentraland.Abgen
{
    /// <summary>
    /// Builds the request blob <see cref="AbgenConverter"/> hands to native;
    /// layout in <c>crate/src/export/wire.rs</c>. Everything past
    /// <see cref="TriangleCap"/> is optional, so a build against an older
    /// library still parses.
    /// </summary>
    public sealed class AbgenRequest
    {
        private readonly List<(string Name, byte[] Data)> files = new();
        private readonly List<(string Name, string Hash)> contentTable = new();

        /// <summary>"windows", "mac", "linux" or "webgl".</summary>
        public string Platform { get; set; } = "windows";

        /// <summary>"scene", "wearable", "emote"; empty detects it.</summary>
        public string EntityType { get; set; } = string.Empty;

        public bool MagentaMissing { get; set; }

        public bool BakeLod { get; set; }

        public AbgenMode Mode { get; set; } = AbgenMode.Convert;

        public bool Crop { get; set; }

        /// <summary>Triangle budget for the LOD; 0 leaves it uncapped.</summary>
        public uint TriangleCap { get; set; }

        /// <summary>Entity hash. Empty derives it from the content table.</summary>
        public string EntityHash { get; set; } = string.Empty;

        /// <summary>In <see cref="AbgenMode.ConvertOnly"/>, the file to convert.</summary>
        public string OnlyGlb { get; set; } = string.Empty;

        /// <summary>Names are the entity's content paths.</summary>
        public AbgenRequest AddFile(string name, byte[] data)
        {
            if (name == null) throw new ArgumentNullException(nameof(name));
            if (data == null) throw new ArgumentNullException(nameof(data));
            files.Add((name, data));
            return this;
        }

        /// <summary>
        /// Supply the whole table when converting a shard, so cross-file
        /// dependency hashes resolve against the full entity.
        /// </summary>
        public AbgenRequest AddContentEntry(string name, string hash)
        {
            if (name == null) throw new ArgumentNullException(nameof(name));
            if (hash == null) throw new ArgumentNullException(nameof(hash));
            contentTable.Add((name, hash));
            return this;
        }

        public byte[] ToBytes()
        {
            using var ms = new MemoryStream();
            using var w = new BinaryWriter(ms, Encoding.UTF8, leaveOpen: true);

            w.Write((uint)files.Count);
            foreach (var (name, data) in files)
            {
                WriteBytes(w, Encoding.UTF8.GetBytes(name));
                WriteBytes(w, data);
            }

            WriteBytes(w, Encoding.UTF8.GetBytes(Platform ?? string.Empty));
            WriteBytes(w, Encoding.UTF8.GetBytes(EntityType ?? string.Empty));

            w.Write((byte)(MagentaMissing ? 1 : 0));
            w.Write((byte)(BakeLod ? 1 : 0));
            w.Write((byte)Mode);
            w.Write((byte)(Crop ? 1 : 0));

            w.Write(TriangleCap);
            WriteBytes(w, Encoding.UTF8.GetBytes(EntityHash ?? string.Empty));
            WriteBytes(w, Encoding.UTF8.GetBytes(OnlyGlb ?? string.Empty));

            w.Write((uint)contentTable.Count);
            foreach (var (name, hash) in contentTable)
            {
                WriteBytes(w, Encoding.UTF8.GetBytes(name));
                WriteBytes(w, Encoding.UTF8.GetBytes(hash));
            }

            w.Flush();
            return ms.ToArray();
        }

        /// <summary>BinaryWriter's 7-bit-prefixed strings are not the wire format.</summary>
        private static void WriteBytes(BinaryWriter w, byte[] bytes)
        {
            w.Write((uint)bytes.Length);
            w.Write(bytes);
        }
    }
}
