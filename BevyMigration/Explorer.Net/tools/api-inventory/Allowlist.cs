using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Tomlyn;
using Tomlyn.Model;

namespace BevyMigration.Tools.ApiInventory
{
    /// <summary>
    ///     ledger/allowlist.toml: one table per subsystem group with its phase, members and the members eliminated
    ///     inside Unity (removed_by_refactor). Generated once from the inventory, maintained by hand afterwards.
    /// </summary>
    internal sealed class Allowlist
    {
        private const string HEADER =
            "# Unity members the shim must provide, grouped by subsystem (BevyMigration/docs/README.md § 6).\n"
            + "# Generated once by Explorer.Net/tools/api-inventory, maintained by hand afterwards: members enter through a\n"
            + "# human-approved PR that also adds the shim implementation and its conformance recording; they leave through a\n"
            + "# refactor inside Unity that moves them to removed_by_refactor so they are not re-introduced.\n"
            + "# Phases: A rendering, B shim, C UI (not shimmed, removed by step 8), D content, F deferred to step 11.\n"
            + "# `api-inventory check` fails on any member used outside this file or listed under removed_by_refactor.\n";

        public SortedDictionary<string, Group> Groups { get; } = new (StringComparer.Ordinal);

        public static Allowlist FromInventory(Inventory inventory)
        {
            var allowlist = new Allowlist();

            foreach (MemberUse member in inventory.Members.Values)
            {
                string group = SubsystemGroups.GroupOf(member.Root, member.ContainingType);
                if (group == SubsystemGroups.EDITOR) continue;

                allowlist.GroupNamed(group).Members.Add(member.Id);
            }

            return allowlist;
        }

        public static Allowlist Read(string path)
        {
            var allowlist = new Allowlist();
            if (TomlSerializer.Deserialize(File.ReadAllText(path), typeof(TomlTable), new TomlSerializerOptions()) is not TomlTable model)
                throw new InvalidDataException($"{path}: not a TOML document");

            foreach (KeyValuePair<string, object> entry in model)
            {
                if (entry.Value is not TomlTable table)
                    throw new InvalidDataException($"{path}: top-level key '{entry.Key}' is not a group table");

                Group group = allowlist.GroupNamed(entry.Key);

                if (table.TryGetValue("phase", out object? phase) && phase is string phaseValue)
                    group.Phase = phaseValue;

                CopyStrings(table, "members", group.Members, path);
                CopyStrings(table, "removed_by_refactor", group.RemovedByRefactor, path);
            }

            return allowlist;
        }

        public void Write(string path)
        {
            var builder = new StringBuilder(HEADER);

            foreach (KeyValuePair<string, Group> entry in Groups)
            {
                builder.Append('\n').Append('[').Append(entry.Key).Append("]\n");
                builder.Append("phase = \"").Append(entry.Value.Phase ?? SubsystemGroups.PhaseOf(entry.Key)).Append("\"\n");
                AppendArray(builder, "members", entry.Value.Members);
                AppendArray(builder, "removed_by_refactor", entry.Value.RemovedByRefactor);
            }

            Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");
            File.WriteAllText(path, builder.ToString());
        }

        public AllowlistCheck Check(Inventory inventory)
        {
            var result = new AllowlistCheck();
            var allowed = new Dictionary<string, string>(StringComparer.Ordinal);
            var removed = new Dictionary<string, string>(StringComparer.Ordinal);

            foreach (KeyValuePair<string, Group> entry in Groups)
            {
                if (string.IsNullOrEmpty(entry.Value.Phase))
                    result.GroupsWithoutPhase.Add(entry.Key);

                foreach (string member in entry.Value.Members) allowed[member] = entry.Key;
                foreach (string member in entry.Value.RemovedByRefactor) removed[member] = entry.Key;
            }

            foreach (MemberUse member in inventory.Members.Values)
            {
                string group = SubsystemGroups.GroupOf(member.Root, member.ContainingType);

                if (group == SubsystemGroups.EDITOR)
                {
                    result.EditorMembers.Add(member);
                    continue;
                }

                if (removed.ContainsKey(member.Id))
                    result.Reintroduced.Add(member);
                else if (!allowed.ContainsKey(member.Id))
                    result.Missing.Add(member);
            }

            foreach (KeyValuePair<string, string> entry in allowed)
                if (!inventory.Members.ContainsKey(entry.Key))
                    result.Unused.Add(entry.Key);

            return result;
        }

        private Group GroupNamed(string name)
        {
            if (!Groups.TryGetValue(name, out Group? group))
            {
                group = new Group();
                Groups[name] = group;
            }

            return group;
        }

        private static void CopyStrings(TomlTable table, string key, SortedSet<string> target, string path)
        {
            if (!table.TryGetValue(key, out object? value)) return;

            if (value is not TomlArray array)
                throw new InvalidDataException($"{path}: '{key}' must be an array of strings");

            foreach (object? item in array)
            {
                if (item is not string member)
                    throw new InvalidDataException($"{path}: '{key}' must contain strings only");

                target.Add(member);
            }
        }

        private static void AppendArray(StringBuilder builder, string key, SortedSet<string> values)
        {
            if (values.Count == 0)
            {
                builder.Append(key).Append(" = []\n");
                return;
            }

            builder.Append(key).Append(" = [\n");

            foreach (string value in values)
                builder.Append("  \"").Append(value.Replace("\\", "\\\\").Replace("\"", "\\\"")).Append("\",\n");

            builder.Append("]\n");
        }

        internal sealed class Group
        {
            public string? Phase { get; set; }
            public SortedSet<string> Members { get; } = new (StringComparer.Ordinal);
            public SortedSet<string> RemovedByRefactor { get; } = new (StringComparer.Ordinal);
        }
    }

    internal sealed class AllowlistCheck
    {
        public List<MemberUse> Missing { get; } = new ();
        public List<MemberUse> Reintroduced { get; } = new ();
        public List<MemberUse> EditorMembers { get; } = new ();
        public List<string> GroupsWithoutPhase { get; } = new ();
        public List<string> Unused { get; } = new ();

        public bool Passed => Missing.Count == 0 && Reintroduced.Count == 0 && GroupsWithoutPhase.Count == 0;

        public void Print(TextWriter output, TextWriter error)
        {
            foreach (string group in GroupsWithoutPhase)
                error.WriteLine($"allowlist: group [{group}] has no phase");

            foreach (MemberUse member in Missing)
                error.WriteLine($"allowlist: {member.Id} is used in {member.Files.Count} file(s) but is not in allowlist.toml (first: {FirstFile(member)})");

            foreach (MemberUse member in Reintroduced)
                error.WriteLine($"allowlist: {member.Id} was removed by refactor and is used again (first: {FirstFile(member)})");

            foreach (string member in Unused)
                output.WriteLine($"allowlist: note: {member} is allowlisted but no longer used; remove it or move it to removed_by_refactor");

            foreach (MemberUse member in EditorMembers)
                output.WriteLine($"allowlist: note: UnityEditor member {member.Id} reachable from runtime code (first: {FirstFile(member)}); never shimmed, must be refactored out");

            output.WriteLine($"allowlist: {Missing.Count} missing, {Reintroduced.Count} re-introduced, {GroupsWithoutPhase.Count} group(s) without phase, {Unused.Count} allowlisted but unused, {EditorMembers.Count} UnityEditor member(s) in runtime code");
            output.WriteLine(Passed ? "allowlist: check passed" : "allowlist: check failed");
        }

        private static string FirstFile(MemberUse member)
        {
            foreach (KeyValuePair<string, int> file in member.Files)
                return file.Key;

            return "?";
        }
    }
}
