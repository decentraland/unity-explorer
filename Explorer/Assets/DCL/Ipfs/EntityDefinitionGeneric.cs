using System;
using System.Collections.Generic;

namespace DCL.Ipfs
{
    [Serializable]
    public class EntityDefinitionGeneric<T> : IEquatable<EntityDefinitionGeneric<T>>
    {
        public const string DEFAULT_VERSION = "v3";

        public List<ContentDefinition>? content;
        public string id = string.Empty;
        public T metadata;
        public List<string>? pointers;
        public string version;
        public long timestamp;
        public string type;

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
    }
}
