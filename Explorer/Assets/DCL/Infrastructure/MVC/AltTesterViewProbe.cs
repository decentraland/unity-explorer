#if ALTTESTER
using System.Collections.Generic;
using System.Text;

namespace MVC
{
    /// <summary>
    ///     Static view-state probe queryable from AltTester via <c>AltDriver.CallStaticMethod</c>.
    ///     Answers without a GameObject lookup, so a hidden or never-instantiated view is still
    ///     addressable. Gated by the <c>ALTTESTER</c> define, so the type is absent from shipping
    ///     binaries.
    /// </summary>
    public static class AltTesterViewProbe
    {
        // State names are returned as strings: AltTester serializes enums by numeric value, so an
        // enum here would silently repoint every test the moment the client reorders it.
        public const string SHOWING = "Showing";
        public const string SHOWN = "Shown";
        public const string HIDING = "Hiding";
        public const string HIDDEN = "Hidden";
        public const string UNKNOWN = "Unknown";

        // Keyed by concrete view type name. Values are strings, never Unity objects, so nothing
        // here keeps a destroyed view alive and no teardown hook is needed.
        private static readonly Dictionary<string, string> STATES = new (64);
        private static readonly object GATE = new ();

        internal static void Report(string viewName, string state)
        {
            lock (GATE) { STATES[viewName] = state; }
        }

        public static string GetState(string viewName)
        {
            lock (GATE) { return STATES.TryGetValue(viewName, out string state) ? state : UNKNOWN; }
        }

        /// <summary>Comma-joined names of every view that has reported at least once.</summary>
        public static string GetKnownViews()
        {
            lock (GATE) { return string.Join(",", STATES.Keys); }
        }

        /// <summary>
        ///     <c>{"ViewName":"Shown","Other":"Hidden"}</c>. Hand-rolled to avoid a serializer
        ///     dependency in this assembly.
        /// </summary>
        public static string Snapshot()
        {
            var sb = new StringBuilder(256);
            sb.Append('{');

            lock (GATE)
            {
                var first = true;

                foreach (KeyValuePair<string, string> entry in STATES)
                {
                    if (!first) sb.Append(',');
                    first = false;
                    sb.Append('"').Append(entry.Key).Append("\":\"").Append(entry.Value).Append('"');
                }
            }

            sb.Append('}');
            return sb.ToString();
        }
    }
}
#endif
