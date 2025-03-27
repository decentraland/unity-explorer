using DCL.Profiles;
using DCL.Profiles.Self;
using DCL.UI.Utilities;
using System;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using Utility;

namespace DCL.UI.InputFieldFormatting
{
    public class HyperlinkTextFormatter : ITextFormatter
    {
        private const string LINK_OPENING_STYLE = "<#00B2FF><link=";
        private const string LINK_CLOSING_STYLE = "</link></color>";
        private const string OWN_PROFILE_OPENING_STYLE = "<#00B2FF>";
        private const string OWN_PROFILE_CLOSING_STYLE = "</color>";
        private const int ESTIMATED_LINK_TAG_LENGTH = 32; // Average length of a link tag including opening and closing styles
        private const int INITIAL_STRING_BUILDER_CAPACITY = 256;
        private const int TEMP_STRING_BUILDER_CAPACITY = 128;

        // Regex patterns for each type of link
        private const string URL_PATTERN = @"(?<url>(?<=^|\s)(https?:\/\/)([a-zA-Z0-9-]+\.)*[a-zA-Z0-9-]+\.[a-zA-Z]{2,}(\/[^\s]*)?(?=\s|$))";
        private const string SCENE_PATTERN = @"(?<scene>(?<=^|\s)(-?\d{1,3}),(-?\d{1,3})(?=\s|!|\?|\.|,|$))";
        private const string WORLD_PATTERN = @"(?<world>(?<=^|\s)*[a-zA-Z0-9]*\.dcl\.eth(?=\s|!|\?|\.|,|$))";
        private const string USERNAME_PATTERN = @"(?<username>(?<=^|\s)@([A-Za-z0-9]{3,15}(?:#[A-Za-z0-9]{4})?)(?=\s|!|\?|\.|,|$))";
        private const string RICH_TEXT_PATTERN = @"(?<richtext><(?!\/?(b|i)(>|\s))[^>]+>)";

        // Combined regex for better performance - matches all link types in one pass
        private static readonly Regex COMBINED_LINK_REGEX = new (
            $"{URL_PATTERN}|{SCENE_PATTERN}|{WORLD_PATTERN}|{USERNAME_PATTERN}|{RICH_TEXT_PATTERN}",
            RegexOptions.Compiled | RegexOptions.ExplicitCapture);

        private readonly StringBuilder mainStringBuilder;
        private readonly StringBuilder tempStringBuilder;
        private readonly IProfileCache profileCache;
        private readonly SelfProfile selfProfile;

        public HyperlinkTextFormatter(IProfileCache profileCache, SelfProfile selfProfile)
        {
            this.profileCache = profileCache;
            this.selfProfile = selfProfile;

            // Pre-allocate with reasonable capacity to reduce reallocations
            mainStringBuilder = new StringBuilder(INITIAL_STRING_BUILDER_CAPACITY);
            tempStringBuilder = new StringBuilder(TEMP_STRING_BUILDER_CAPACITY);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public string FormatText(ReadOnlySpan<char> text)
        {
            if (text.Length == 0)
                return string.Empty;

            if (text.StartsWith("/"))
                return text.ToString();

            mainStringBuilder.Clear();
            mainStringBuilder.Append(text);

            ProcessMainStringBuilder();

            return mainStringBuilder.ToString();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ProcessMainStringBuilder()
        {
            var text = mainStringBuilder.ToString();
            var matches = COMBINED_LINK_REGEX.Matches(text);

            if (matches.Count == 0)
                return;

            var estimatedCapacity = text.Length + (matches.Count * ESTIMATED_LINK_TAG_LENGTH);
            mainStringBuilder.Clear();
            mainStringBuilder.EnsureCapacity(estimatedCapacity);

            int lastIndex = 0;

            foreach (Match match in matches)
            {
                // Append text before the match
                if (match.Index > lastIndex)
                {
                    mainStringBuilder.Append(text.AsSpan(lastIndex, match.Index - lastIndex));
                }

                // Process the match based on its group
                if (match.Groups["url"].Success)
                    ProcessUrlMatch(match);
                else if (match.Groups["scene"].Success)
                    ProcessSceneMatch(match);
                else if (match.Groups["world"].Success)
                    ProcessWorldMatch(match);
                else if (match.Groups["username"].Success)
                    ProcessUsernameMatch(match);
                else if (match.Groups["richtext"].Success)
                    ProcessRichTextMatch(match);

                lastIndex = match.Index + match.Length;
            }

            // Append remaining text
            if (lastIndex < text.Length)
            {
                mainStringBuilder.Append(text.AsSpan(lastIndex));
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ProcessUrlMatch(Match match)
        {
            tempStringBuilder.Clear();
            tempStringBuilder.EnsureCapacity(match.Length + ESTIMATED_LINK_TAG_LENGTH);
            tempStringBuilder.Append(LINK_OPENING_STYLE)
                           .Append(HyperlinkConstants.URL)
                           .Append(">")
                           .Append(match)
                           .Append(LINK_CLOSING_STYLE);
            mainStringBuilder.Append(tempStringBuilder);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ProcessSceneMatch(Match match)
        {
            if (!AreCoordsValid(int.Parse(match.Groups[1].Value), int.Parse(match.Groups[2].Value)))
            {
                mainStringBuilder.Append(match);
                return;
            }

            tempStringBuilder.Clear();
            tempStringBuilder.EnsureCapacity(match.Length + ESTIMATED_LINK_TAG_LENGTH);
            tempStringBuilder.Append(LINK_OPENING_STYLE)
                           .Append(HyperlinkConstants.SCENE)
                           .Append(">")
                           .Append(match)
                           .Append(LINK_CLOSING_STYLE);
            mainStringBuilder.Append(tempStringBuilder);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ProcessWorldMatch(Match match)
        {
            tempStringBuilder.Clear();
            tempStringBuilder.EnsureCapacity(match.Length + ESTIMATED_LINK_TAG_LENGTH);
            tempStringBuilder.Append(LINK_OPENING_STYLE)
                           .Append(HyperlinkConstants.WORLD)
                           .Append(">")
                           .Append(match)
                           .Append(LINK_CLOSING_STYLE);
            mainStringBuilder.Append(tempStringBuilder);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ProcessUsernameMatch(Match match)
        {
            string username = match.Groups[1].Value;
            if (IsOwnUsername(username))
            {
                tempStringBuilder.Clear();
                tempStringBuilder.EnsureCapacity(match.Length + ESTIMATED_LINK_TAG_LENGTH);
                tempStringBuilder.Append(OWN_PROFILE_OPENING_STYLE)
                               .Append(match)
                               .Append(OWN_PROFILE_CLOSING_STYLE);
                mainStringBuilder.Append(tempStringBuilder);
                return;
            }

            if (!IsUserNameValid(username))
            {
                mainStringBuilder.Append(match);
                return;
            }

            tempStringBuilder.Clear();
            tempStringBuilder.EnsureCapacity(match.Length + ESTIMATED_LINK_TAG_LENGTH);
            tempStringBuilder.Append(LINK_OPENING_STYLE)
                           .Append(HyperlinkConstants.PROFILE)
                           .Append(">")
                           .Append(match)
                           .Append(LINK_CLOSING_STYLE);
            mainStringBuilder.Append(tempStringBuilder);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ProcessRichTextMatch(Match match)
        {
            tempStringBuilder.Clear();
            tempStringBuilder.EnsureCapacity(match.Value.Length);

            for (var i = 0; i < match.Value.Length; i++)
            {
                char c = match.Value[i];
                tempStringBuilder.Append(c == '<' ? '‹' : c == '>' ? '›' : c);
            }

            mainStringBuilder.Append(tempStringBuilder);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool AreCoordsValid(int x, int y) =>
            GenesisCityData.IsInsideBounds(x, y);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool IsOwnUsername(string username) =>
            selfProfile.OwnProfile?.DisplayName == username;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool IsUserNameValid(string username) =>
            profileCache.GetByUserName(username) != null;
    }
}
