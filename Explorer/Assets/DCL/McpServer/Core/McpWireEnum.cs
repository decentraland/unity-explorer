using System;
using System.Collections.Generic;
using System.Text;

namespace DCL.McpServer.Core
{
    /// <summary>
    ///     Per-enum cache of wire (snake_case) member names, so a tool's schema, its argument parsing and its
    ///     output all derive from the enum itself: "FirstPerson" ↔ "first_person", "WALK" ↔ "walk". Built once
    ///     per type on first touch; parsing and formatting allocate nothing afterwards. Renaming an enum member
    ///     therefore renames its wire value — connected agents re-read the schema every session, but skill
    ///     recipes that spell a value out (.claude/skills/mcp-scene-iteration/) must be kept in sync.
    /// </summary>
    public static class McpWireEnum<T> where T : struct, Enum
    {
        /// <summary>Wire names of all members, in declaration order.</summary>
        public static readonly string[] WIRE_NAMES;

        private static readonly T[] VALUES;
        private static readonly Dictionary<string, T> BY_WIRE_NAME;

        static McpWireEnum()
        {
            string[] names = Enum.GetNames(typeof(T));
            VALUES = (T[])Enum.GetValues(typeof(T));
            WIRE_NAMES = new string[names.Length];
            BY_WIRE_NAME = new Dictionary<string, T>(names.Length);

            for (var i = 0; i < names.Length; i++)
            {
                WIRE_NAMES[i] = ToSnakeCase(names[i]);
                BY_WIRE_NAME[WIRE_NAMES[i]] = VALUES[i];
            }
        }

        public static bool TryParse(string wireName, out T value) =>
            BY_WIRE_NAME.TryGetValue(wireName, out value);

        public static string ToWire(T value) =>
            WIRE_NAMES[Array.IndexOf(VALUES, value)];

        /// <summary>Wire names of a subset of members, for schemas that expose only part of the enum. Allocates the array.</summary>
        public static string[] WireNamesOf(T[] members)
        {
            var result = new string[members.Length];

            for (var i = 0; i < members.Length; i++)
                result[i] = ToWire(members[i]);

            return result;
        }

        /// <summary>"FirstPerson" → "first_person", "DroneView" → "drone_view", "SDKCamera" → "sdk_camera", "WAIT_TICK" → "wait_tick".</summary>
        private static string ToSnakeCase(string memberName)
        {
            var builder = new StringBuilder(memberName.Length + 4);

            for (var i = 0; i < memberName.Length; i++)
            {
                char current = memberName[i];

                if (char.IsUpper(current) && i > 0 && memberName[i - 1] != '_'
                    && (!char.IsUpper(memberName[i - 1]) || (i + 1 < memberName.Length && char.IsLower(memberName[i + 1]))))
                    builder.Append('_');

                builder.Append(char.ToLowerInvariant(current));
            }

            return builder.ToString();
        }
    }
}
