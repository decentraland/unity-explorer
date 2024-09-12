using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Text.RegularExpressions;
using System.Web;
using UnityEngine;

namespace Global.AppArgs
{
    public class ApplicationParametersParser : IAppArgs
    {
        private const string REALM_PARAM = "realm";
        private readonly Dictionary<string, string> appParameters = new ();

        private static readonly IReadOnlyDictionary<string, string> ALWAYS_IN_EDITOR = new Dictionary<string, string>
        {
            [IAppArgs.DEBUG_FLAG] = string.Empty,
        };

        public ApplicationParametersParser() : this(Environment.GetCommandLineArgs()) { }

        public ApplicationParametersParser(string[] args) : this(true, args) { }

        public ApplicationParametersParser(bool useInEditorFlags = true, params string[] args)
        {
            ParseApplicationParameters(args);

            if (useInEditorFlags && Application.isEditor)
                AddAlwaysInEditorFlags();
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

            if (appParameters.TryGetValue(REALM_PARAM, out string? realmParamValue))
            {
                // Patch for WinOS sometimes affecting the 'realm' parameter in deep links putting a '/' at the end
                if (realmParamValue.EndsWith('/'))
                    appParameters[REALM_PARAM] = realmParamValue.Remove(realmParamValue.Length - 1);

                // Patch for MacOS removing the ':' from the realm parameter protocol
                appParameters[REALM_PARAM] = Regex.Replace(appParameters[REALM_PARAM], @"(https?)//(.*?)$", @"$1://$2");
            }
        }
    }
}
