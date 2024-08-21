using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Text.RegularExpressions;
using System.Web;
using UnityEngine;

namespace Global
{
    public class ApplicationParametersParser
    {
        private const string REALM_PARAM = "realm";
        public Dictionary<string, string> AppParameters { get; private set; } = new ();

        public ApplicationParametersParser(string[] args)
        {
            AppParameters = ParseApplicationParameters(args);
        }

        private Dictionary<string, string> ParseApplicationParameters(string[] cmdArgs)
        {
            var deepLinkFound = false;
            string lastKeyStored = string.Empty;

            for (int i = 0; i < cmdArgs.Length; i++)
            {
                string arg = cmdArgs[i];

                if (arg.StartsWith("--"))
                {
                    if (arg.Length > 2)
                    {
                        lastKeyStored = arg.Substring(2);
                        AppParameters[lastKeyStored] = string.Empty;
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
                    AppParameters[lastKeyStored] = arg;
            }

            return AppParameters;
        }

        private void ProcessDeepLinkParameters(string deepLinkString)
        {
            // Update deep link so that Uri class allows the host name
            deepLinkString = Regex.Replace(deepLinkString, @"^decentraland:/+", "https://decentraland.org/?");

            if (!Uri.TryCreate(deepLinkString, UriKind.Absolute, out Uri? res)) return;

            var uri = new Uri(deepLinkString);
            NameValueCollection uriQuery = HttpUtility.ParseQueryString(uri.Query);

            foreach (string uriQueryKey in uriQuery.AllKeys)
                AppParameters[uriQueryKey] = uriQuery.Get(uriQueryKey);

            // Patch for WinOS sometimes affecting the 'realm' parameter in deep links putting a '/' at the end
            if (AppParameters.TryGetValue(REALM_PARAM, out string? realmParamValue) && realmParamValue.EndsWith('/'))
                AppParameters[REALM_PARAM] = realmParamValue.Remove(realmParamValue.Length - 1);
        }
    }
}
