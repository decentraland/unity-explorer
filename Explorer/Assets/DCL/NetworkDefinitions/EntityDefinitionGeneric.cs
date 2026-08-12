using System;
using System.Linq;

// ReSharper disable InconsistentNaming
namespace DCL.Ipfs
{
    // Server schema: decentraland/common-schemas src/platform/entity.ts#/Entity
    // (metadata is schema-optional, but every entity type the client consumes carries it)
    [Serializable]
    public class EntityDefinitionGeneric<T> : EntityDefinitionBase, IEquatable<EntityDefinitionGeneric<T>>
    {
        public const string DEFAULT_VERSION = "v3";

        public T metadata = default!;

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

        public bool Equals(EntityDefinitionGeneric<T>? other) =>
            string.Equals(id, other?.id);

        public string FullInfo() =>
            $"Id: {id}\n"
            + $"Content: {ContentString()}\n"
            + $"Metadata: {metadata}\n"
            + $"Pointers: {PointersString()}\n"
            + $"Version: {version}\n"
            + $"Timestamp: {timestamp}\n"
            + $"Type: {type}\n";

        private string ContentString() =>
            $"Count {content.Length}: {string.Join(", ", content.Select(e => $"{e.file}: {e.hash}"))}";

        private string PointersString() =>
            $"Count {pointers.Length}: {string.Join(", ", pointers)}";
    }
}
