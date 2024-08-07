using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Text.RegularExpressions;
using System.Web;

namespace Global.Dynamic
{
    public class ApplicationParametersParser
    {
        private readonly string[] programArgs;
        private Dictionary<string, string>? parameters;

        public ApplicationParametersParser(string[] programArgs)
        {
            this.programArgs = programArgs;
        }

        public Dictionary<string, string> Get()
        {
            if (parameters != null)
                return parameters;

            Dictionary<string, string> appParameters = new ();

            var deepLinkFound = false;
            string lastKeyStored = string.Empty;

            for (int i = 0; i < programArgs.Length; i++)
            {
                string arg = programArgs[i];

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
#if !UNITY_EDITOR && UNITY_STANDALONE_WIN
                else if (!deepLinkFound && arg.StartsWith("decentraland://"))
                {
                    deepLinkFound = true;
                    lastKeyStored = string.Empty;

                    // When started in local scene development mode (AKA preview mode) command line arguments are used
                    // Example (Windows) -> start decentraland://"realm=http://127.0.0.1:8000&position=100,100&otherparam=blahblah"
                    ProcessDeepLinkParameters(arg, appParameters);
                }
#endif
                else if (!string.IsNullOrEmpty(lastKeyStored))
                    appParameters[lastKeyStored] = arg;
            }

            // in MacOS the deep link string doesn't come in the cmd args...
#if !UNITY_EDITOR && UNITY_STANDALONE_OSX
            if (!string.IsNullOrEmpty(Application.absoluteURL) && Application.absoluteURL.StartsWith("decentraland"))
            {
                // Regex patch for MacOS removing the ':' from the realm parameter protocol
                ProcessDeepLinkParameters(Regex.Replace(Application.absoluteURL, @"(https?)//(.*?)$", @"$1://$2"),
                    appParameters);
            }
#endif

            parameters = appParameters;
            return appParameters;
        }

        private void ProcessDeepLinkParameters(string deepLinkString, Dictionary<string, string> appParameters)
        {
            // Update deep link so that Uri class allows the host name
            deepLinkString = Regex.Replace(deepLinkString, @"^decentraland:/+", "https://decentraland.com/?");

            if (!Uri.TryCreate(deepLinkString, UriKind.Absolute, out Uri? res)) return;

            var uri = new Uri(deepLinkString);
            NameValueCollection uriQuery = HttpUtility.ParseQueryString(uri.Query);

            foreach (string uriQueryKey in uriQuery.AllKeys) { appParameters[uriQueryKey] = uriQuery.Get(uriQueryKey); }
        }
    }
}
