using CodeLess.Interfaces;
using DCL.Diagnostics;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;
using UnityEngine;

namespace Global.AppArgs
{
    [AutoInterface]
    public class ApplicationParametersParser : IAppArgs
    {
        private readonly Dictionary<string, string> appParameters = new ();

        // A decentraland:// deep link seen during parsing but not yet processed. Deferred so its whitelisted-realm
        // params can be gated once the world whitelist is available (see InitializeDeepLinks). Null once processed
        // or when the launch has no deep link.
        private string? pendingDeepLink;

        // Guards InitializeDeepLinks so the merge and the argument log happen exactly once.
        private bool deepLinksInitialized;

        private static readonly IReadOnlyDictionary<string, string> ALWAYS_IN_EDITOR = new Dictionary<string, string>
        {
            [AppArgsFlags.DEBUG] = string.Empty,
        };

        public ApplicationParametersParser() : this(Environment.GetCommandLineArgs()) { }

        public ApplicationParametersParser(string[] args) : this(true, args) { }

        public ApplicationParametersParser(bool useInEditorFlags = true, params string[] args)
            : this(useInEditorFlags, deferDeepLinks: false, args) { }

        /// <summary>
        ///     Parses CLI flags (e.g. <see cref="AppArgsFlags.FeatureFlags" />.URL) but leaves any deep link
        ///     unprocessed, so its whitelisted-realm params can be gated once the world whitelist is fetched. Call
        ///     <see cref="InitializeDeepLinks" /> afterwards to process it.
        /// </summary>
        public static ApplicationParametersParser CreateDeferringDeepLinks(string[] args) =>
            new (true, deferDeepLinks: true, args);

        private ApplicationParametersParser(bool useInEditorFlags, bool deferDeepLinks, params string[] args)
        {
            ParseApplicationParameters(args);

            if (useInEditorFlags && Application.isEditor)
                AddAlwaysInEditorFlags();

            // Deferred: InitializeDeepLinks() logs instead, so the log reports the merged deep-link params too.
            if (!deferDeepLinks)
                InitializeDeepLinks();
        }

        public bool HasFlag(string flagName) =>
            appParameters.ContainsKey(flagName);

        public bool TryGetValue(string flagName, out string? value) =>
            appParameters.TryGetValue(flagName, out value);

        public IEnumerable<string> Flags() =>
            appParameters.Keys;

        public IReadOnlyDictionary<string, string> Args() =>
            appParameters;

        private void AddAlwaysInEditorFlags()
        {
            foreach ((string? key, string? value) in ALWAYS_IN_EDITOR)
                appParameters.TryAdd(key, value);
        }

        private void ParseApplicationParameters(string[] cmdArgs)
        {
            var deepLinkFound = false;
            string lastKeyStored = string.Empty;

            foreach (string arg in cmdArgs)
            {
                if (arg.StartsWith("--"))
                {
                    if (arg.Length > 2)
                    {
                        lastKeyStored = arg.Substring(2);
                        appParameters[lastKeyStored] = string.Empty;
                    }
                    else
                        lastKeyStored = string.Empty;
                }
                else if (!deepLinkFound && arg.StartsWith("decentraland://"))
                {
                    deepLinkFound = true;
                    lastKeyStored = string.Empty;

                    // Application parameters may come embedded in a deep link:
                    // Example (Windows) -> start decentraland://"realm=http://127.0.0.1:8000&position=100,100&local-scene=true&otherparam=blahblah"
                    // Stored, not processed here: the whitelisted-realm gate needs the world whitelist, which may not
                    // be available yet. InitializeDeepLinks() processes it once it is.
                    pendingDeepLink = arg;
                }
                else if (!string.IsNullOrEmpty(lastKeyStored))
                    appParameters[lastKeyStored] = arg;
            }
        }

        /// <summary>
        ///     Whether a deep link was seen during parsing but not yet processed.
        /// </summary>
        public bool HasPendingDeepLink => pendingDeepLink != null;

        /// <summary>
        ///     Processes the deep link captured during construction (merging its allowlisted params), applying the
        ///     current <see cref="DeepLinkAllowlist" /> whitelisted-realm gate, then logs the complete argument set.
        ///     Idempotent, and safe to call when the launch carries no deep link.
        /// </summary>
        public void InitializeDeepLinks()
        {
            if (deepLinksInitialized)
                return;

            deepLinksInitialized = true;

            if (pendingDeepLink != null)
            {
                Dictionary<string, string> deepLinkParameters = ProcessDeepLinkParameters(pendingDeepLink);

                foreach ((string key, string value) in deepLinkParameters)
                    appParameters[key] = value;

                pendingDeepLink = null;
            }

            // Logged once the arguments are complete, so a deferred deep-link launch reports its params too.
            LogArguments();
        }

        public static Dictionary<string, string> ProcessDeepLinkParameters(string deepLinkString)
        {
            var output = new Dictionary<string, string>();

            // Drop the optional host segment (e.g. "open" in decentraland://open?signin=... or decentraland://open/?signin=...) so only the query remains;
            deepLinkString = Regex.Replace(deepLinkString, @"^(decentraland:/+)[A-Za-z][A-Za-z0-9_-]*/*\?", "$1?");

            // Update deep link so that Uri class allows the host name
            deepLinkString = Regex.Replace(deepLinkString, @"^decentraland:/+", "https://decentraland.org/?");

            if (!Uri.TryCreate(deepLinkString, UriKind.Absolute, out Uri? _)) return output;

            var uri = new Uri(deepLinkString);
            NameValueCollection uriQuery = HttpUtility.ParseQueryString(uri.Query);

            var droppedKeys = new List<string>();

            // Tier 1: always-permitted (base) navigation/login params.
            foreach (string uriQueryKey in uriQuery.AllKeys)
            {
                // if the deep link is not constructed correctly (AKA 'decentraland://?&blabla=blabla') a 'null' parameter can be detected...
                if (uriQueryKey == null) continue;

                if (DeepLinkAllowlist.IsPermitted(uriQueryKey))
                    output[uriQueryKey] = uriQuery.Get(uriQueryKey);
            }

            if (output.TryGetValue(AppArgsFlags.REALM, out string? realmParamValue))
            {
                // Patch for WinOS sometimes affecting the 'realm' parameter in deep links putting a '/' at the end
                if (realmParamValue.EndsWith('/'))
                    realmParamValue = realmParamValue.Remove(realmParamValue.Length - 1);

                // Patch for MacOS removing the ':' from the realm parameter protocol
                realmParamValue = Regex.Replace(realmParamValue, @"(https?)//(.*?)$", @"$1://$2");

                output[AppArgsFlags.REALM] = realmParamValue;
            }

            // Tier 2 (SEC-019/020): the local-development params Creator Hub / sdk-commands attach to preview deep
            // links (local-scene, dclenv, hub, skip-auth-screen, landscape-terrain-enabled, multi-instance,
            // scene-console) are permitted only when the target realm is whitelisted — loopback, or a world listed in
            // the deeplink-whitelisted-worlds feature flag. A remote-realm deep link from a web page cannot enable
            // them unless that exact world was explicitly whitelisted. Everything not in either tier is dropped.
            bool realmIsWhitelisted = output.TryGetValue(AppArgsFlags.REALM, out string? whitelistRealm)
                                      && DeepLinkAllowlist.IsRealmWhitelisted(whitelistRealm);

            foreach (string uriQueryKey in uriQuery.AllKeys)
            {
                if (uriQueryKey == null || output.ContainsKey(uriQueryKey)) continue;

                if (realmIsWhitelisted && DeepLinkAllowlist.IsPermittedForWhitelistedRealm(uriQueryKey))
                    output[uriQueryKey] = uriQuery.Get(uriQueryKey);
                else
                    droppedKeys.Add(uriQueryKey);
            }

            if (droppedKeys.Count > 0)
                ReportHub.LogWarning(ReportCategory.ALWAYS, $"Dropped {droppedKeys.Count} non-allowlisted deep-link param(s): {string.Join(", ", droppedKeys)}");

            return output;
        }

        private void LogArguments()
        {
            const int COUNT_PER_LINE = 7;
            var sb = new StringBuilder(COUNT_PER_LINE * appParameters.Count);
            var count = 1;

            sb.AppendLine("==================");
            sb.AppendLine("Application arguments:");
            sb.AppendLine("==================\n");

            foreach ((string? key, string? value) in appParameters)
            {
                sb.Append("Arg ").Append(count).Append(": ").Append(key).Append(" = ").Append(value).Append("\n");
                count++;
            }
            sb.AppendLine("==================\n");

            ReportHub.LogProductionInfo(sb.ToString());
        }
    }
}
