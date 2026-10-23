using System.Collections.Generic;

namespace BevyMigration.Tools.ApiInventory
{
    /// <summary>
    ///     One Unity member and every file that uses it, with the use count per file.
    /// </summary>
    internal sealed class MemberUse
    {
        public string Id { get; }
        public string Kind { get; }
        public string Root { get; }
        public string ContainingType { get; }
        public SortedDictionary<string, int> Files { get; } = new (System.StringComparer.Ordinal);
        public int Count { get; private set; }

        public MemberUse(string id, string kind, string root, string containingType)
        {
            Id = id;
            Kind = kind;
            Root = root;
            ContainingType = containingType;
        }

        public void Add(string file, int count = 1)
        {
            Files[file] = Files.TryGetValue(file, out int existing) ? existing + count : count;
            Count += count;
        }

        public void Merge(MemberUse other)
        {
            foreach (KeyValuePair<string, int> entry in other.Files)
                Add(entry.Key, entry.Value);
        }
    }
}
