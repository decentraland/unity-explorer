using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Text.RegularExpressions;
using System.Web;
using UnityEngine;

namespace Global.Dynamic
{
    public class ApplicationParametersParser
    {
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
#if !UNITY_EDITOR && UNITY_STANDALONE_WIN
                else if (!deepLinkFound && arg.StartsWith("decentraland://"))
                {
                    deepLinkFound = true;
                    lastKeyStored = string.Empty;

                    // When started in local scene development mode (AKA preview mode) command line arguments are used
                    // Example (Windows) -> start decentraland://"realm=http://127.0.0.1:8000&position=100,100&otherparam=blahblah"
                    ProcessDeepLinkParameters(arg);
                }
#endif
                else if (!string.IsNullOrEmpty(lastKeyStored))
                    AppParameters[lastKeyStored] = arg;
            }

            // in MacOS the deep link string doesn't come in the cmd args...
#if !UNITY_EDITOR && UNITY_STANDALONE_OSX
            if (!string.IsNullOrEmpty(Application.absoluteURL) && Application.absoluteURL.StartsWith("decentraland"))
            {
                // Regex patch for MacOS removing the ':' from the realm parameter protocol
                ProcessDeepLinkParameters(Regex.Replace(Application.absoluteURL, @"(https?)//(.*?)$", @"$1://$2"));
            }
#endif

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
        }
    }
}
