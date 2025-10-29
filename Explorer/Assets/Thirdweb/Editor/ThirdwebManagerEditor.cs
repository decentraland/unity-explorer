using System;
using System.Reflection;
using Thirdweb.Unity;
using UnityEditor;
using UnityEngine;

namespace Thirdweb.Editor
{
    public abstract class ThirdwebManagerBaseEditor<T> : UnityEditor.Editor
        where T: MonoBehaviour
    {
        protected SerializedProperty InitializeOnAwakeProp;
        protected SerializedProperty ShowDebugLogsProp;
        protected SerializedProperty AutoConnectLastWalletProp;
        protected SerializedProperty RedirectPageHtmlOverrideProp;
        protected SerializedProperty RpcOverridesProp;

        protected int SelectedTab;
        protected GUIStyle ButtonStyle;
        protected Texture2D BannerImage;

        protected virtual string[] TabTitles => new[] { "Client/Server", "Preferences", "Misc", "Debug" };

        protected virtual void OnEnable()
        {
            InitializeOnAwakeProp = FindProp("InitializeOnAwake");
            ShowDebugLogsProp = FindProp("ShowDebugLogs");
            AutoConnectLastWalletProp = FindProp("AutoConnectLastWallet");
            RedirectPageHtmlOverrideProp = FindProp("RedirectPageHtmlOverride");
            RpcOverridesProp = FindProp("RpcOverrides");

            BannerImage = Resources.Load<Texture2D>("EditorBanner");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            if (ButtonStyle == null) { InitializeStyles(); }

            DrawBannerAndTitle();
            DrawTabs();
            GUILayout.Space(10);
            DrawSelectedTabContent();

            _ = serializedObject.ApplyModifiedProperties();
        }

        protected virtual void DrawSelectedTabContent()
        {
            switch (SelectedTab)
            {
                case 0:
                    DrawClientOrServerTab();
                    break;
                case 1:
                    DrawPreferencesTab();
                    break;
                case 2:
                    DrawMiscTab();
                    break;
                case 3:
                    DrawDebugTab();
                    break;
                default:
                    GUILayout.Label("Unknown Tab", EditorStyles.boldLabel);
                    break;
            }
        }

        protected abstract void DrawClientOrServerTab();

        protected virtual void DrawPreferencesTab()
        {
            EditorGUILayout.HelpBox("Set your preferences and initialization options here.", MessageType.Info);
            DrawProperty(InitializeOnAwakeProp, "Initialize On Awake");
            DrawProperty(ShowDebugLogsProp, "Show Debug Logs");
            DrawProperty(AutoConnectLastWalletProp, "Auto-Connect Last Wallet");
        }

        protected virtual void DrawMiscTab()
        {
            EditorGUILayout.HelpBox("Configure other settings here.", MessageType.Info);
            DrawProperty(RpcOverridesProp, "RPC Overrides");
            GUILayout.Space(10);
            EditorGUILayout.LabelField("OAuth Redirect Page HTML Override", EditorStyles.boldLabel);
            RedirectPageHtmlOverrideProp.stringValue = EditorGUILayout.TextArea(RedirectPageHtmlOverrideProp.stringValue, GUILayout.MinHeight(150));
        }

        protected virtual void DrawDebugTab()
        {
            EditorGUILayout.HelpBox("Debug your settings here.", MessageType.Info);

            DrawButton(
                "Log Active Wallet Info",
                () =>
                {
                    if (!Application.isPlaying)
                    {
                        Debug.LogWarning("Debugging can only be done in Play Mode.");
                        return;
                    }

                    if (target is ThirdwebManagerBase manager)
                    {
                        IThirdwebWallet wallet = manager.ActiveWallet;

                        if (wallet != null) { Debug.Log($"Active Wallet ({wallet.GetType().Name}) Address: {wallet.GetAddress().Result}"); }
                        else { Debug.LogWarning("No active wallet found."); }
                    }
                    else { Debug.LogWarning("Active wallet information unavailable for this target."); }
                }
            );

            DrawButton(
                "Disconnect Active Wallet",
                () =>
                {
                    if (!Application.isPlaying)
                    {
                        Debug.LogWarning("Debugging can only be done in Play Mode.");
                        return;
                    }

                    if (target is ThirdwebManagerBase manager)
                    {
                        EditorApplication.delayCall += async () =>
                        {
                            await manager.DisconnectWallet();
                            Debug.Log("Active wallet disconnected.");
                        };
                    }
                    else { Debug.LogWarning("Active wallet information unavailable for this target."); }
                }
            );

            DrawButton(
                "Open Documentation",
                () => { Application.OpenURL("http://portal.thirdweb.com/unity"); }
            );
        }

        protected void DrawBannerAndTitle()
        {
            GUILayout.BeginVertical();
            GUILayout.Space(10);
            GUILayout.BeginHorizontal();

            if (BannerImage != null) { GUILayout.Label(BannerImage, GUILayout.Width(64), GUILayout.Height(64)); }

            GUILayout.Space(10);
            GUILayout.BeginVertical();
            GUILayout.Space(10);
            GUILayout.Label("Thirdweb Configuration", EditorStyles.boldLabel);
            GUILayout.Label("Configure your settings and preferences.\nYou can access ThirdwebManager.Instance from anywhere.", EditorStyles.miniLabel);
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
            GUILayout.Space(10);
            GUILayout.EndVertical();
        }

        protected void DrawTabs()
        {
            SelectedTab = GUILayout.Toolbar(SelectedTab, TabTitles, GUILayout.Height(25));
        }

        protected void InitializeStyles()
        {
            ButtonStyle = new GUIStyle(GUI.skin.button)
            {
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(10, 10, 10, 10),
            };
        }

        protected void DrawProperty(SerializedProperty property, string label)
        {
            if (property != null) { _ = EditorGUILayout.PropertyField(property, new GUIContent(label)); }
            else { EditorGUILayout.HelpBox($"Property '{label}' not found.", MessageType.Error); }
        }

        protected void DrawButton(string label, Action action)
        {
            GUILayout.FlexibleSpace();

            if (GUILayout.Button(label, ButtonStyle, GUILayout.Height(35), GUILayout.ExpandWidth(true))) { action.Invoke(); }

            GUILayout.FlexibleSpace();
        }

        protected SerializedProperty FindProp(string propName)
        {
            Type targetType = target.GetType();
            PropertyInfo property = targetType.GetProperty(propName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

            if (property == null) { return null; }

            var backingFieldName = $"<{propName}>k__BackingField";
            return serializedObject.FindProperty(backingFieldName);
        }
    }

    [CustomEditor(typeof(ThirdwebManager))]
    public class ThirdwebManagerEditor : ThirdwebManagerBaseEditor<ThirdwebManager>
    {
        private SerializedProperty _clientIdProp;
        private SerializedProperty _bundleIdProp;

        protected override void OnEnable()
        {
            base.OnEnable();
            _clientIdProp = FindProp("ClientId");
            _bundleIdProp = FindProp("BundleId");
        }

        protected override string[] TabTitles => new[] { "Client", "Preferences", "Misc", "Debug" };

        protected override void DrawClientOrServerTab()
        {
            EditorGUILayout.HelpBox("Configure your client settings here.", MessageType.Info);
            DrawProperty(_clientIdProp, "Client ID");
            DrawProperty(_bundleIdProp, "Bundle ID");

            DrawButton(
                "Create API Key",
                () => { Application.OpenURL("https://thirdweb.com/create-api-key"); }
            );
        }
    }

    [CustomEditor(typeof(ThirdwebManagerServer))]
    public class ThirdwebManagerServerEditor : ThirdwebManagerBaseEditor<ThirdwebManagerServer>
    {
        private SerializedProperty _secretKeyProp;

        protected override void OnEnable()
        {
            base.OnEnable();
            _secretKeyProp = FindProp("SecretKey");
        }

        protected override string[] TabTitles => new[] { "Client", "Preferences", "Misc", "Debug" };

        protected override void DrawClientOrServerTab()
        {
            EditorGUILayout.HelpBox("Configure your client settings here.", MessageType.Info);
            DrawProperty(_secretKeyProp, "Secret Key");

            DrawButton(
                "Create API Key",
                () => { Application.OpenURL("https://thirdweb.com/create-api-key"); }
            );
        }
    }
}
