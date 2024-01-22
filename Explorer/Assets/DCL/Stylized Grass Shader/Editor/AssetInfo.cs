using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEngine;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace StylizedGrass
{
    public class AssetInfo
    {
        public const string ASSET_NAME = "Stylized Grass Shader";
        public const string ASSET_ID = "143830";
        public const string ASSET_ABRV = "SGS";

        public const string INSTALLED_VERSION = "1.4.3";
        public const string MIN_UNITY_VERSION = "2021.2";
        public const string MIN_URP_VERSION = "12.1.8";

        public const string VERSION_FETCH_URL = "http://www.staggart.xyz/backend/versions/stylizedgrass.php";
        public const string DOC_URL = "http://staggart.xyz/unity/stylized-grass-shader/sgs-docs/";
        public const string FORUM_URL = "https://forum.unity.com/threads/804000/";

        public static bool IS_UPDATED = true;
        public static bool compatibleVersion = true;
        public static bool untestedVersion = false;

#if !URP //Enabled when com.unity.render-pipelines.universal is below defined version
        [InitializeOnLoad]
        sealed class PackageInstaller : Editor
        {
            [InitializeOnLoadMethod]
            public static void Initialize()
            {
                RetreivePackageList();

                if (EditorUtility.DisplayDialog("Stylized Grass Shader v" + INSTALLED_VERSION, "This package requires the Universal Render Pipeline " + MIN_URP_VERSION + " or newer, would you like to install or update it now?", "OK", "Later"))
                {
					Debug.Log("Universal Render Pipeline <b>v" + lastestURPVersion + "</b> will start installing in a moment. Please refer to the URP documentation for set up instructions");
					
                    InstallURP();
                }
            }

            private static PackageInfo[] packages;

            public const string URP_PACKAGE_ID = "com.unity.render-pipelines.universal";
            public const string SRP_PACKAGE_ID = "com.unity.render-pipelines.core";
            public const string SG_PACKAGE_ID = "com.unity.shadergraph";

#if SGS_DEV
            [MenuItem("SGS/RetreivePackageList")]
#endif
            public static void RetreivePackageList()
            {
                UnityEditor.PackageManager.Requests.SearchRequest listRequest = Client.SearchAll(true);

                while (listRequest.Status == StatusCode.InProgress)
                {
                    //Waiting...
                }

                if (listRequest.Status == StatusCode.Failure || listRequest.Result == null)
                {
                    Debug.LogError("Failed to retreived packages from Package Manager...");

                    return;
                }
                
                packages = listRequest.Result;

                foreach (PackageInfo p in packages)
                {
                    if (p.name == URP_PACKAGE_ID)
                    {
                        lastestURPVersion = p.versions.latestCompatible;
                    }
                }
            }

            private static string lastestURPVersion;

            private static void InstallURP()
            {
                RetreivePackageList();
				
				if(packages == null)
                {
                    Debug.LogError(
                        "[Stylized Grass] Failed to install URP, Package Manager did not return a list of packages. Please install manually");
					return;
				}
				
                foreach (PackageInfo p in packages)
                {
                    if (p.name == URP_PACKAGE_ID)
                    {
                        lastestURPVersion = p.versions.latestCompatible;

                        Client.Add(URP_PACKAGE_ID + "@" + lastestURPVersion);

                        //Update Core and Shader Graph packages as well, doesn't always happen automatically
                        for (int i = 0; i < p.dependencies.Length; i++)
                        {
#if SGS_DEV
                            Debug.Log("Updating URP dependency <i>" + p.dependencies[i].name + "</i> to " + p.dependencies[i].version);
#endif
                            Client.Add(p.dependencies[i].name + "@" + p.dependencies[i].version);
                        }
                        
                    }
                }
  
            }
        }
#endif

        public static void OpenStorePage()
        {
            Application.OpenURL("com.unity3d.kharma:content/" + ASSET_ID);
        }

        public static string PACKAGE_ROOT_FOLDER
        {
            get { return SessionState.GetString(ASSET_ABRV + "_BASE_FOLDER", string.Empty); }
            set { SessionState.SetString(ASSET_ABRV + "_BASE_FOLDER", value); }
        }

        public static string GetRootFolder()
        {
            //Get script path
            string[] scriptGUID = AssetDatabase.FindAssets("AssetInfo t:script");
            string scriptFilePath = AssetDatabase.GUIDToAssetPath(scriptGUID[0]);

            //Truncate to get relative path
            PACKAGE_ROOT_FOLDER = scriptFilePath.Replace("Editor/AssetInfo.cs", string.Empty);

#if SGS_DEV
            Debug.Log("<b>Package root</b> " + PACKAGE_ROOT_FOLDER);
#endif

            return PACKAGE_ROOT_FOLDER;
        }

        public static class VersionChecking
        {
            public static void CheckUnityVersion()
            {
                compatibleVersion = true;
                untestedVersion = false;

#if !UNITY_2019_3_OR_NEWER
                compatibleVersion = false;
#endif

#if UNITY_2020_3_OR_NEWER
                untestedVersion = true;
#endif
            }

            public static string fetchedVersionString;
            public static System.Version fetchedVersion;
            private static bool showPopup;

            public enum VersionStatus
            {
                UpToDate,
                Outdated
            }

            public enum QueryStatus
            {
                Fetching,
                Completed,
                Failed
            }
            public static QueryStatus queryStatus = QueryStatus.Completed;

#if SGS_DEV
            [MenuItem("SGS/Check for update")]
#endif
            public static void GetLatestVersionPopup()
            {
                CheckForUpdate(true);
            }

            private static int VersionStringToInt(string input)
            {
                //Remove all non-alphanumeric characters from version 
                input = input.Replace(".", string.Empty);
                input = input.Replace(" BETA", string.Empty);
                return int.Parse(input, System.Globalization.NumberStyles.Any);
            }

            public static void CheckForUpdate(bool showPopup = false)
            {
                VersionChecking.showPopup = showPopup;

                queryStatus = QueryStatus.Fetching;

                using (WebClient webClient = new WebClient())
                {
                    webClient.DownloadStringCompleted += new System.Net.DownloadStringCompletedEventHandler(OnRetreivedServerVersion);
                    webClient.DownloadStringAsync(new System.Uri(VERSION_FETCH_URL), fetchedVersionString);
                }
            }

            private static void OnRetreivedServerVersion(object sender, DownloadStringCompletedEventArgs e)
            {
                if (e.Error == null && !e.Cancelled)
                {
                    fetchedVersionString = e.Result;
                    fetchedVersion = new System.Version(fetchedVersionString);
                    System.Version installedVersion = new System.Version(INSTALLED_VERSION);

                    //Success
                    IS_UPDATED = (installedVersion >= fetchedVersion) ? true : false;

#if SGS_DEV
                    Debug.Log("<b>PackageVersionCheck</b> Up-to-date = " + IS_UPDATED + " (Installed:" + INSTALLED_VERSION + ") (Remote:" + fetchedVersionString + ")");
#endif

                    queryStatus = QueryStatus.Completed;

                    if (VersionChecking.showPopup)
                    {
                        if (!IS_UPDATED)
                        {
                            if (EditorUtility.DisplayDialog(ASSET_NAME + ", version " + INSTALLED_VERSION, "A new version is available: " + fetchedVersionString, "Open store page", "Close"))
                            {
                                OpenStorePage();
                            }
                        }
                        else
                        {
                            if (EditorUtility.DisplayDialog(ASSET_NAME + ", version " + INSTALLED_VERSION, "Your current version is up-to-date!", "Close")) { }
                        }
                    }
                }
                else
                {
                    Debug.LogWarning("[" + ASSET_NAME + "] Contacting update server failed: " + e.Error.Message);
                    queryStatus = QueryStatus.Failed;

                    //When failed, assume installation is up-to-date
                    IS_UPDATED = true;
                }
            }

        }
    }
}