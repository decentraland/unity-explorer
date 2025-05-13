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
    public class ApplicationParametersParser : IAppArgs
    {
        private readonly Dictionary<string, string> appParameters = new ();

        private static readonly IReadOnlyDictionary<string, string> ALWAYS_IN_EDITOR = new Dictionary<string, string>
        {
            [AppArgsFlags.DEBUG] = string.Empty,
        };

        public ApplicationParametersParser() : this(Environment.GetCommandLineArgs()) { }

        public ApplicationParametersParser(string[] args) : this(true, args) { }

        public ApplicationParametersParser(bool useInEditorFlags = true, params string[] args)
        {
            ParseApplicationParameters(args);

            if (useInEditorFlags && Application.isEditor)
                AddAlwaysInEditorFlags();

            LogArguments();
        }

        public bool HasFlag(string flagName) =>
            appParameters.ContainsKey(flagName);

        public bool TryGetValue(string flagName, out string? value) =>
            appParameters.TryGetValue(flagName, out value);

        public IEnumerable<string> Flags() =>
            appParameters.Keys;

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
                    ProcessDeepLinkParameters(arg);
                }
                else if (!string.IsNullOrEmpty(lastKeyStored))
                    appParameters[lastKeyStored] = arg;
            }
        }

        private void ProcessDeepLinkParameters(string deepLinkString)
        {
            // Update deep link so that Uri class allows the host name
            deepLinkString = Regex.Replace(deepLinkString, @"^decentraland:/+", "https://decentraland.org/?");

            if (!Uri.TryCreate(deepLinkString, UriKind.Absolute, out Uri? _)) return;

            var uri = new Uri(deepLinkString);
            NameValueCollection uriQuery = HttpUtility.ParseQueryString(uri.Query);

            foreach (string uriQueryKey in uriQuery.AllKeys)
            {
                // if the deep link is not constructed correctly (AKA 'decentraland://?&blabla=blabla') a 'null' parameter can be detected...
                if (uriQueryKey == null) continue;
                appParameters[uriQueryKey] = uriQuery.Get(uriQueryKey);
            }

            if (appParameters.TryGetValue(AppArgsFlags.REALM, out string? realmParamValue))
            {
                // Patch for WinOS sometimes affecting the 'realm' parameter in deep links putting a '/' at the end
                if (realmParamValue.EndsWith('/'))
                    realmParamValue = realmParamValue.Remove(realmParamValue.Length - 1);

                // Patch for MacOS removing the ':' from the realm parameter protocol
                realmParamValue = Regex.Replace(realmParamValue, @"(https?)//(.*?)$", @"$1://$2");

                appParameters[AppArgsFlags.REALM] = realmParamValue;
            }
        }

        private void LogArguments()
        {
            const int COUNT_PER_LINE = 7;
            var sb = new StringBuilder(COUNT_PER_LINE * appParameters.Count);
            var count = 1;

            sb.AppendLine("Application arguments:");

            foreach ((string? key, string? value) in appParameters)
            {
                sb.Append("Arg ").Append(count).Append(": ").Append(key).Append(" = ").Append(value).Append("\n");
                count++;
            }

            ReportHub.LogProductionInfo(sb.ToString());
        }
    }
}
