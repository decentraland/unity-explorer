using DCL.ECSComponents;
using System;
using System.Collections.Generic;
using System.Text;

namespace DCL.SyntheticInput
{
    /// <summary>
    ///     The driver-facing spelling of <see cref="InputAction" />: the member name without its "Ia" prefix in
    ///     snake_case ("IaPointer" is "pointer", "IaAction3" is "action_3"). Every front-end of the layer reports
    ///     and accepts actions in this spelling.
    /// </summary>
    public static class InputActionNames
    {
        private const string PREFIX = "Ia";

        private static readonly Dictionary<InputAction, string> WIRE_NAMES = BuildWireNames();

        public static string ToWire(InputAction action) =>
            WIRE_NAMES.TryGetValue(action, out string? name) ? name : action.ToString();

        /// <summary>Matches a wire name ignoring case, underscores and spaces, so "ACTION_3", "action3" and "Action 3" all parse.</summary>
        public static bool TryParse(string value, out InputAction action)
        {
            string normalized = Normalize(value);

            foreach (KeyValuePair<InputAction, string> pair in WIRE_NAMES)
            {
                if (!string.Equals(Normalize(pair.Value), normalized, StringComparison.Ordinal))
                    continue;

                action = pair.Key;
                return true;
            }

            action = default(InputAction);
            return false;
        }

        private static Dictionary<InputAction, string> BuildWireNames()
        {
            var names = new Dictionary<InputAction, string>();
            var builder = new StringBuilder();

            foreach (InputAction action in (InputAction[])Enum.GetValues(typeof(InputAction)))
            {
                string member = action.ToString();
                string bare = member.StartsWith(PREFIX, StringComparison.Ordinal) ? member[PREFIX.Length..] : member;

                builder.Clear();

                for (var i = 0; i < bare.Length; i++)
                {
                    char current = bare[i];

                    // A digit or an upper-case letter starts a new word, except at the very beginning.
                    if (i > 0 && (char.IsDigit(current) || char.IsUpper(current)) && !char.IsDigit(bare[i - 1]))
                        builder.Append('_');

                    builder.Append(char.ToLowerInvariant(current));
                }

                names[action] = builder.ToString();
            }

            return names;
        }

        private static string Normalize(string value) =>
            value.Replace("_", string.Empty).Replace(" ", string.Empty).ToLowerInvariant();
    }
}
