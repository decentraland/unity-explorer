using System;
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

        public const string INSTALLED_VERSION = "1.4.5";
        public const string MIN_UNITY_VERSION = "2021.2";
        public const string MIN_URP_VERSION = "12.1.8";

        public const string DOC_URL = "http://staggart.xyz/unity/stylized-grass-shader/sgs-docs/";
        public const string FORUM_URL = "https://forum.unity.com/threads/804000/";

        public static bool IS_UPDATED = true;
        public static bool compatibleVersion = true;
        public static bool untestedVersion;

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

        public static void OpenInPackageManager()
        {
            Application.OpenURL("com.unity3d.kharma:content/" + ASSET_ID);
        }

        public static string PACKAGE_ROOT_FOLDER
        {
            get => SessionState.GetString(ASSET_ABRV + "_BASE_FOLDER", string.Empty);
            set => SessionState.SetString(ASSET_ABRV + "_BASE_FOLDER", value);
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

            public static string LATEST_VERSION
            {
                get => SessionState.GetString("SGS_LATEST_VERSION", INSTALLED_VERSION);
                set => SessionState.SetString("SGS_LATEST_VERSION", value);
            }

            public static bool UPDATE_AVAILABLE => new Version(LATEST_VERSION) > new Version(INSTALLED_VERSION);

            private static bool showPopup;

            public enum VersionStatus
            {
                UpToDate,
                Outdated,
            }

            public enum QueryStatus
            {
                Fetching,
                Completed,
                Failed,
            }

            public static QueryStatus queryStatus = QueryStatus.Completed;

#if SGS_DEV
            [MenuItem("SGS/Check for update")]
#endif
            public static void GetLatestVersionPopup()
            {
                CheckForUpdate(true);
            }

            public static void CheckForUpdate(bool showPopup = false)
            {
                VersionChecking.showPopup = showPopup;

                queryStatus = QueryStatus.Fetching;

                var url = $"https://api.assetstore.unity3d.com/package/latest-version/{ASSET_ID}";

                using (var webClient = new WebClient())
                {
                    webClient.DownloadStringCompleted += OnRetrievedAPIContent;
                    webClient.DownloadStringAsync(new Uri(url), apiResult);
                }
            }

            public static string apiResult;

            private class AssetStoreItem
            {
                public string name;
                public string version;
            }

            private static void OnRetrievedAPIContent(object sender, DownloadStringCompletedEventArgs e)
            {
                if (e.Error == null && !e.Cancelled)
                {
                    string result = e.Result;

                    var asset = (AssetStoreItem)JsonUtility.FromJson(result, typeof(AssetStoreItem));

                    LATEST_VERSION = asset.version;

#if SGS_DEV
                    Debug.Log("<b>PackageVersionCheck</b> Update available = " + UPDATE_AVAILABLE + " (Installed:" + INSTALLED_VERSION + ") (Remote:" + LATEST_VERSION + ")");
#endif

                    queryStatus = QueryStatus.Completed;

                    if (showPopup)
                    {
                        if (UPDATE_AVAILABLE)
                        {
                            if (EditorUtility.DisplayDialog(ASSET_NAME + ", version " + INSTALLED_VERSION, "An updated version is available: " + LATEST_VERSION, "Open Package Manager", "Close")) { OpenInPackageManager(); }
                        }
                        else
                        {
                            if (EditorUtility.DisplayDialog(ASSET_NAME + ", version " + INSTALLED_VERSION, "Installed version is up-to-date!", "Close")) { }
                        }
                    }
                }
                else
                {
                    Debug.LogWarning("[" + ASSET_NAME + "] Contacting update server failed: " + e.Error.Message);
                    queryStatus = QueryStatus.Failed;
                }
            }
        }
    }
}
