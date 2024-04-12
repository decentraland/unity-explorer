using System;
using System.Collections.Generic;
using System.Linq;

namespace DCL.Ipfs
{
    [Serializable]
    public class EntityDefinitionGeneric<T> : IEquatable<EntityDefinitionGeneric<T>>
    {
        public const string DEFAULT_VERSION = "v3";

        public List<ContentDefinition>? content;
        public string id;
        public T metadata;
        public List<string>? pointers;
        public string version;
        public long timestamp;
        public string type;

        public EntityDefinitionGeneric() { }

        public EntityDefinitionGeneric(string id, T metadata)
        {
            this.id = id;
            this.metadata = metadata;
        }

        /// <summary>
        ///     Clear data for the future reusing
        /// </summary>
        internal static void Clear(EntityDefinitionGeneric<T> entityDefinition)
        {
            entityDefinition.content?.Clear();
            entityDefinition.id = string.Empty;
            entityDefinition.pointers?.Clear();
        }

        public bool Equals(EntityDefinitionGeneric<T> other) =>
            id.Equals(other?.id);

        public override string ToString() =>
            id;

        public string FullInfo() =>
            $"Id: {id}\n"
            + $"Content: {ContentString()}\n"
            + $"Metadata: {metadata}\n"
            + $"Pointers: {PointersString()}\n"
            + $"Version: {version}\n"
            + $"Timestamp: {timestamp}\n"
            + $"Type: {type}\n";

        private string ContentString() =>
            $"Count {content?.Count ?? 0}: {string.Join(", ", content?.Select(e => $"{e.file}: {e.hash}") ?? Array.Empty<string>())}";

        private string PointersString() =>
            $"Count {pointers?.Count ?? 0}: {string.Join(", ", pointers as IEnumerable<string> ?? Array.Empty<string>())}";
    }
}
