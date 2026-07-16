using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace DCL.Ipfs
{
    [Serializable]
    public class EntityDefinitionGeneric<T> : EntityDefinitionBase, IEquatable<EntityDefinitionGeneric<T>>
    {
        public const string DEFAULT_VERSION = "v3";

        public T metadata;

        public EntityDefinitionGeneric() { }

        public EntityDefinitionGeneric(string id, T metadata) : base(id)
        {
            this.metadata = metadata;
        }

        /// <summary>
        ///     Clear data for the future reusing
        /// </summary>
        internal static void Clear(EntityDefinitionGeneric<T> entityDefinition)
        {
            entityDefinition.content = Array.Empty<ContentDefinition>();
            entityDefinition.id = string.Empty;
            entityDefinition.pointers = Array.Empty<string>();
        }

        public bool Equals(EntityDefinitionGeneric<T> other) =>
            id.Equals(other?.id);

        public string FullInfo() =>
            $"Id: {id}\n"
            + $"Content: {ContentString()}\n"
            + $"Metadata: {metadata}\n"
            + $"Pointers: {PointersString()}\n"
            + $"Version: {version}\n"
            + $"Timestamp: {timestamp}\n"
            + $"Type: {type}\n";

        private string ContentString() =>
            $"Count {content?.Length ?? 0}: {string.Join(", ", content?.Select(e => $"{e.file}: {e.hash}") ?? Array.Empty<string>())}";

        private string PointersString() =>
            $"Count {pointers?.Length ?? 0}: {string.Join(", ", pointers as IEnumerable<string> ?? Array.Empty<string>())}";
    }
}
