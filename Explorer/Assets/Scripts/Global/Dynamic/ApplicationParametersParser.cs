using ECS.SceneLifeCycle.Realm;
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
        private const string APP_PARAMETER_REALM = "realm";
        private const string APP_PARAMETER_LOCAL_SCENE = "local-scene";
        private const string APP_PARAMETER_POSITION = "position";

        public bool LocalSceneDevelopment;

        public readonly Dictionary<string, string> AppParameters = new ();

        public ApplicationParametersParser(RealmLaunchSettings launchSettings)
        {
            AppParameters = ParseApplicationParameters();

            if (AppParameters.ContainsKey(APP_PARAMETER_REALM))
                ProcessRealmAppParameter(launchSettings);

            if (AppParameters.TryGetValue(APP_PARAMETER_POSITION, out string param))
                ProcessPositionAppParameter(param, launchSettings);
        }

        private Dictionary<string, string> ParseApplicationParameters()
        {
            string[] cmdArgs = Environment.GetCommandLineArgs();

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
            deepLinkString = Regex.Replace(deepLinkString, @"^decentraland:/+", "https://decentraland.com/?");

            if (!Uri.TryCreate(deepLinkString, UriKind.Absolute, out Uri? res)) return;

            var uri = new Uri(deepLinkString);
            NameValueCollection uriQuery = HttpUtility.ParseQueryString(uri.Query);

            foreach (string uriQueryKey in uriQuery.AllKeys)
                AppParameters[uriQueryKey] = uriQuery.Get(uriQueryKey);
        }

        private void ProcessRealmAppParameter(RealmLaunchSettings launchSettings)
        {
            string realmParamValue = AppParameters[APP_PARAMETER_REALM];

            if (string.IsNullOrEmpty(realmParamValue)) return;

            LocalSceneDevelopment = AppParameters.TryGetValue(APP_PARAMETER_LOCAL_SCENE, out string localSceneParamValue)
                                    && ParseLocalSceneParameter(localSceneParamValue)
                                    && IsRealmAValidUrl(realmParamValue);

            if (LocalSceneDevelopment)
                launchSettings.SetLocalSceneDevelopmentRealm(realmParamValue);
            else if (IsRealmAWorld(realmParamValue))
                launchSettings.SetWorldRealm(realmParamValue);
        }

        private void ProcessPositionAppParameter(string positionParameterValue, RealmLaunchSettings launchSettings)
        {
            if (string.IsNullOrEmpty(positionParameterValue)) return;

            Vector2Int targetPosition = Vector2Int.zero;

            MatchCollection matches = new Regex(@"-*\d+").Matches(positionParameterValue);

            if (matches.Count > 1)
            {
                targetPosition.x = int.Parse(matches[0].Value);
                targetPosition.y = int.Parse(matches[1].Value);
            }

            launchSettings.SetTargetScene(targetPosition);
        }

        private bool ParseLocalSceneParameter(string localSceneParameter)
        {
            if (string.IsNullOrEmpty(localSceneParameter)) return false;

            var returnValue = false;
            Match match = new Regex(@"true|false").Match(localSceneParameter);

            if (match.Success)
                bool.TryParse(match.Value, out returnValue);

            return returnValue;
        }

        private bool IsRealmAWorld(string realmParam) =>
            realmParam.IsEns();

        private bool IsRealmAValidUrl(string realmParam) =>
            Uri.TryCreate(realmParam, UriKind.Absolute, out Uri? uriResult)
            && (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps);
    }
}
