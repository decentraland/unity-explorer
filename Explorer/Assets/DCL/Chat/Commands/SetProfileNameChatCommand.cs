using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Profiles;
using DCL.Profiles.Self;
using DCL.UI;
using System;
using System.Globalization;
using System.Text;
using System.Threading;

namespace DCL.Chat.Commands
{
    /// <summary>
    ///     Publishes an arbitrary, unfiltered profile name to the catalyst so the client can be tested against the
    ///     names it will actually be served. A name is a schema-valid string that no backend rejects, so the client
    ///     is the only thing standing between a crafted one and the labels that render it — and the only way to
    ///     exercise that honestly is to put a crafted one on a real profile.
    /// </summary>
    /// <remarks>
    ///     Deliberately bypasses nothing on the read side: the name goes out raw, and whatever the UI does with it
    ///     coming back is the thing under test. Debug-only, so it never reaches a retail build's command list — not
    ///     because it grants an attacker anything (the profile API is open, and this is exactly what a crafted
    ///     profile does) but because it is a testing instrument, not a feature.
    /// </remarks>
    public class SetProfileNameChatCommand : IChatCommand
    {
        private const string UNICODE_ESCAPE_PREFIX = @"\u";
        private const int UNICODE_ESCAPE_DIGITS = 4;

        private readonly ISelfProfile selfProfile;

        public string Command => "setname";

        public string Description =>
            "<b>/setname <i>name</i></b>\n"
            + "  Publish an unescaped profile name to the catalyst, for testing how the UI renders one.\n"
            + $"  Accepts {UNICODE_ESCAPE_PREFIX}XXXX escapes for characters the chat input cannot carry.";

        public bool DebugOnly => true;

        public SetProfileNameChatCommand(ISelfProfile selfProfile)
        {
            this.selfProfile = selfProfile;
        }

        public bool ValidateParameters(string[] parameters) =>
            parameters.Length > 0;

        public async UniTask<string> ExecuteCommandAsync(string[] parameters, CancellationToken ct)
        {
            // The dispatcher splits the raw message on spaces, so a name with spaces arrives in pieces and is
            // rejoined here. What it also does before splitting is collapse ", " into ",", which cannot be undone
            // at this layer — the escape form below is the way to set a name containing that sequence exactly.
            string name = DecodeUnicodeEscapes(string.Join(' ', parameters));

            Profile? profile = await selfProfile.ProfileAsync(ct);

            if (profile == null)
                return "🔴 No own profile is loaded yet.";

            string previousName = profile.Name;
            profile.Name = name;

            Profile? published;

            try { published = await selfProfile.UpdateProfileAsync(profile, ct); }
            catch (Exception exception)
            {
                // Restore the in-memory name so a failed publish does not leave the client showing a name the
                // catalyst never accepted.
                profile.Name = previousName;
                ReportHub.LogException(exception, ReportCategory.PROFILE);
                return $"🔴 Could not publish the name: {exception.Message}";
            }

            if (published == null)
            {
                profile.Name = previousName;
                return "🔴 The catalyst did not return the updated profile.";
            }

            // Echoed escaped, and only escaped: this reply is emitted as a system message, which is the one path
            // that is deliberately not sanitized, so echoing the raw name would inject it into the client's own
            // copy and confuse the very test being run.
            return $"🟢 Published a {name.Length}-character name: {RichTextSanitizer.Escape(name)}\n"
                   + $"  Validated to: {RichTextSanitizer.Escape(published.ValidatedName)}";
        }

        /// <summary>
        ///     Turns a literal <c>\uXXXX</c> into the character it denotes, so a tester can set bytes the chat input
        ///     will not carry — and can reproduce the escape sequences TMP itself decodes.
        /// </summary>
        private static string DecodeUnicodeEscapes(string value)
        {
            int firstEscape = value.IndexOf(UNICODE_ESCAPE_PREFIX, StringComparison.Ordinal);

            if (firstEscape < 0)
                return value;

            var builder = new StringBuilder(value.Length);
            builder.Append(value, 0, firstEscape);

            for (int i = firstEscape; i < value.Length;)
            {
                if (i + UNICODE_ESCAPE_PREFIX.Length + UNICODE_ESCAPE_DIGITS <= value.Length
                    && string.CompareOrdinal(value, i, UNICODE_ESCAPE_PREFIX, 0, UNICODE_ESCAPE_PREFIX.Length) == 0
                    && ushort.TryParse(value.AsSpan(i + UNICODE_ESCAPE_PREFIX.Length, UNICODE_ESCAPE_DIGITS),
                        NumberStyles.HexNumber, CultureInfo.InvariantCulture, out ushort codePoint))
                {
                    builder.Append((char)codePoint);
                    i += UNICODE_ESCAPE_PREFIX.Length + UNICODE_ESCAPE_DIGITS;
                    continue;
                }

                // Not a well-formed escape, so the backslash is part of the name the tester meant to set.
                builder.Append(value[i]);
                i++;
            }

            return builder.ToString();
        }
    }
}
