#if MCP_TEST_AUTOMATION
using System;
using System.Collections.Generic;

namespace DCL.McpServer.Utils
{
    /// <summary>
    ///     Pure, engine-free helpers for the string identity ("path") that the UI-automation tools
    ///     (<c>list_ui_elements</c> / <c>get_ui_state</c> / <c>click_ui</c> / <c>set_ui_text</c> / <c>scroll</c> /
    ///     <c>get_component_property</c>) hand back and forth. A path prefixes the element hierarchy with the UI system
    ///     it belongs to (uGUI Canvas tree, or a UI-Toolkit UIDocument tree) so a later call can re-resolve it. Kept
    ///     separate from the engine walk so the parsing and matching rules can be unit-tested without a live Unity UI.
    ///     <para>
    ///         Lookups also accept a path expression, an XPath-like dialect over the same hierarchy. The operators:
    ///     </para>
    ///     <list type="bullet">
    ///         <item><c>//Name</c> — a node called Name at any depth below the current position.</item>
    ///         <item><c>/Name</c> — a direct child called Name; a leading <c>/</c> anchors at the hierarchy root.</item>
    ///         <item><c>*</c> — any single node, whatever it is called.</item>
    ///         <item><c>Name[i]</c> — the i-th (zero-based) of several siblings sharing that name.</item>
    ///     </list>
    ///     <para>
    ///         An expression with no leading separator is read as if it began with <c>//</c>, and it must consume the
    ///         whole candidate path, so it identifies the element itself rather than one of its ancestors. Attribute
    ///         predicates, text matching and a parent axis are deliberately absent — nothing needs them.
    ///     </para>
    /// </summary>
    public static class UiElementPath
    {
        public const string UGUI_PREFIX = "ugui:";
        public const string UITK_PREFIX = "uitk:";

        // How well a lookup answers, best first: the exact path list_ui_elements returned, a path expression,
        // an exact element name, a trailing segment, then a bare "the path mentions this somewhere".
        public const int SCORE_EXACT_PATH = 5;
        public const int SCORE_PATH_QUERY = 4;
        public const int SCORE_EXACT_NAME = 3;
        public const int SCORE_PATH_SUFFIX = 2;
        public const int SCORE_PATH_CONTAINS = 1;

        private const char SEPARATOR = '/';
        private const string SEPARATOR_TEXT = "/";
        private const string DESCENDANT = "//";
        private const string WILDCARD = "*";

        private static readonly char[] SEPARATORS = { SEPARATOR };

        public static bool IsUgui(string path) =>
            path.StartsWith(UGUI_PREFIX, StringComparison.Ordinal);

        public static bool IsUitk(string path) =>
            path.StartsWith(UITK_PREFIX, StringComparison.Ordinal);

        /// <summary>
        ///     A path segment for one node. A name unique among its siblings stays bare, so the common path keeps
        ///     reading like the Hierarchy window; a <paramref name="sharedName" /> takes the <c>Name[i]</c>
        ///     indexer (grid items, repeated slots), and an unnamed node falls back to a type-and-index token so it
        ///     still has a deterministic, re-resolvable identity within a single frame.
        /// </summary>
        public static string Segment(string? name, string typeName, int index, bool sharedName) =>
            string.IsNullOrEmpty(name)
                ? $"{typeName}[{index}]"
                : sharedName
                    ? $"{name}[{index}]"
                    : name;

        /// <summary>Appends <paramref name="segment" /> to <paramref name="parentPath" /> with a single separator.</summary>
        public static string Join(string parentPath, string segment) =>
            parentPath + SEPARATOR + segment;

        /// <summary>
        ///     Whether an element with <paramref name="name" /> at <paramref name="path" /> passes the optional
        ///     case-insensitive <paramref name="filter" /> (matched against either its name or its full path).
        ///     An empty filter matches everything.
        /// </summary>
        public static bool Matches(string name, string path, string? filter) =>
            string.IsNullOrEmpty(filter)
            || name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0
            || path.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0;

        /// <summary>
        ///     Whether <paramref name="query" /> — a path expression in the dialect documented on this class — selects
        ///     the element stored at <paramref name="candidatePath" />, consuming it in full.
        /// </summary>
        public static bool MatchesQuery(string candidatePath, string query)
        {
            if (string.IsNullOrEmpty(candidatePath) || string.IsNullOrEmpty(query))
                return false;

            string candidate = StripSystemPrefix(candidatePath, out string candidateSystem);
            string expression = StripSystemPrefix(query, out string querySystem);

            // A query that names a UI system only answers for that system's elements.
            if (querySystem.Length > 0 && !string.Equals(querySystem, candidateSystem, StringComparison.Ordinal))
                return false;

            string[] segments = candidate.Split(SEPARATORS, StringSplitOptions.RemoveEmptyEntries);
            List<Step> steps = ParseSteps(expression);

            return steps.Count > 0 && MatchFrom(segments, 0, steps, 0);
        }

        /// <summary>
        ///     How well a stored element identified by <paramref name="candidatePath" /> / <paramref name="candidateName" />
        ///     answers the lookup <paramref name="query" /> a tool call passed. Higher is better; 0 means no match. Lets
        ///     resolution prefer an exact path, then a path expression, then an exact name, then a loose
        ///     suffix/contains match.
        /// </summary>
        public static int MatchScore(string candidatePath, string candidateName, string query)
        {
            if (string.IsNullOrEmpty(query)) return 0;

            if (string.Equals(candidatePath, query, StringComparison.Ordinal)) return SCORE_EXACT_PATH;

            // Only expressions carrying a separator are read as paths; a bare word stays a name lookup.
            if (query.IndexOf(SEPARATOR) >= 0 && MatchesQuery(candidatePath, query)) return SCORE_PATH_QUERY;

            if (string.Equals(candidateName, query, StringComparison.OrdinalIgnoreCase)) return SCORE_EXACT_NAME;
            if (candidatePath.EndsWith(SEPARATOR_TEXT + query, StringComparison.OrdinalIgnoreCase)) return SCORE_PATH_SUFFIX;
            if (candidatePath.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0) return SCORE_PATH_CONTAINS;

            return 0;
        }

        private static string StripSystemPrefix(string path, out string system)
        {
            system = path.StartsWith(UGUI_PREFIX, StringComparison.Ordinal) ? UGUI_PREFIX
                : path.StartsWith(UITK_PREFIX, StringComparison.Ordinal) ? UITK_PREFIX
                : string.Empty;

            return path.Substring(system.Length);
        }

        /// <summary>
        ///     Splits a path expression into ordered steps. A step reached through <c>//</c> (or an expression that
        ///     starts without a separator at all) may skip any number of intermediate nodes; a step reached through a
        ///     single <c>/</c> must be the direct child of the previous one.
        /// </summary>
        private static List<Step> ParseSteps(string expression)
        {
            var steps = new List<Step>();
            bool rooted = expression.Length > 0 && expression[0] == SEPARATOR;
            bool descendantPrefix = expression.StartsWith(DESCENDANT, StringComparison.Ordinal);

            // A bare "Panel/Button" is read leniently, as if it were "//Panel/Button"; a single leading separator
            // instead anchors the first step at the hierarchy root.
            bool descendant = !rooted || descendantPrefix;
            int index = descendantPrefix ? 2 : rooted ? 1 : 0;

            while (index < expression.Length)
            {
                int separator = expression.IndexOf(SEPARATOR, index);
                string segment = separator < 0 ? expression.Substring(index) : expression.Substring(index, separator - index);

                if (segment.Length > 0)
                    steps.Add(new Step(segment, descendant));

                if (separator < 0)
                    break;

                index = separator + 1;
                descendant = index < expression.Length && expression[index] == SEPARATOR;

                if (descendant)
                    index++;
            }

            return steps;
        }

        /// <summary>
        ///     Matches the remaining <paramref name="steps" /> against the remaining <paramref name="segments" />,
        ///     backtracking over the candidate positions a descendant step may land on.
        /// </summary>
        private static bool MatchFrom(string[] segments, int segmentIndex, List<Step> steps, int stepIndex)
        {
            // Every step consumed: the expression identifies this element only if the whole path was consumed too.
            if (stepIndex == steps.Count)
                return segmentIndex == segments.Length;

            Step step = steps[stepIndex];

            if (!step.Descendant)
                return segmentIndex < segments.Length
                       && SegmentMatches(segments[segmentIndex], step.Token)
                       && MatchFrom(segments, segmentIndex + 1, steps, stepIndex + 1);

            for (int candidate = segmentIndex; candidate < segments.Length; candidate++)
            {
                if (SegmentMatches(segments[candidate], step.Token) && MatchFrom(segments, candidate + 1, steps, stepIndex + 1))
                    return true;
            }

            return false;
        }

        /// <summary>
        ///     One path segment against one query segment. <c>*</c> matches anything; an indexed query segment
        ///     (<c>Name[2]</c>) additionally requires that index, while a bare name matches whichever index the
        ///     stored segment carries.
        /// </summary>
        private static bool SegmentMatches(string candidateSegment, string querySegment)
        {
            if (querySegment == WILDCARD)
                return true;

            SplitIndex(querySegment, out string queryName, out int queryIndex);
            SplitIndex(candidateSegment, out string candidateName, out int candidateIndex);

            if (!string.Equals(candidateName, queryName, StringComparison.OrdinalIgnoreCase))
                return false;

            // An index-less stored segment is the only node of its name, so it answers "[0]" as well.
            return queryIndex < 0 || queryIndex == (candidateIndex < 0 ? 0 : candidateIndex);
        }

        /// <summary>Splits a trailing <c>[i]</c> indexer off a segment; <paramref name="index" /> is -1 without one.</summary>
        private static void SplitIndex(string segment, out string name, out int index)
        {
            index = -1;
            name = segment;

            if (segment.Length < 3 || segment[segment.Length - 1] != ']')
                return;

            int open = segment.LastIndexOf('[');

            if (open <= 0 || !int.TryParse(segment.Substring(open + 1, segment.Length - open - 2), out int parsed) || parsed < 0)
                return;

            index = parsed;
            name = segment.Substring(0, open);
        }

        private readonly struct Step
        {
            /// <summary>The segment text this step has to match, indexer included.</summary>
            public readonly string Token;

            /// <summary>Reached through <c>//</c>: any number of nodes may sit between the previous step and this one.</summary>
            public readonly bool Descendant;

            public Step(string token, bool descendant)
            {
                Token = token;
                Descendant = descendant;
            }
        }
    }
}
#endif
