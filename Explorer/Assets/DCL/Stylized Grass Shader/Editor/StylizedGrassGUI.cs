using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEditor.AnimatedValues;
using UnityEditor.Rendering;

namespace StylizedGrass
{
    public class StylizedGrassGUI : Editor
    {
        private const string AssetIconData = "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";
        private static Texture m_AssetIcon;
        public static Texture AssetIcon
        {
            get
            {
                if(m_AssetIcon == null) m_AssetIcon = CreateIcon(AssetIconData);
                return m_AssetIcon;
            }
        }
        
        public static Texture CreateIcon(string data)
        {
            byte[] bytes = System.Convert.FromBase64String(data);

            Texture2D icon = new Texture2D(32, 32, TextureFormat.RGBA32, false, false);
            icon.LoadImage(bytes, true);
            return icon;
        }

        
        private static GUIStyle _Header;
        public static GUIStyle Header
        {
            get
            {
                if (_Header == null)
                {
                    _Header = new GUIStyle(GUI.skin.label)
                    {
                        richText = true,
                        alignment = TextAnchor.MiddleCenter,
                        wordWrap = true,
                        fontSize = 18,
                        fontStyle = FontStyle.Normal
                    };
                }

                return _Header;
            }
        }

        private static GUIStyle _Tab;
        public static GUIStyle Tab
        {
            get
            {
                if (_Tab == null)
                {
                    _Tab = new GUIStyle(EditorStyles.miniButtonMid)
                    {
                        alignment = TextAnchor.MiddleCenter,
                        stretchWidth = true,
                        richText = true,
                        wordWrap = true,
                        fontSize = 12,
                        fixedHeight = 27.5f,
                        fontStyle = FontStyle.Bold,
                        padding = new RectOffset()
                        {
                            left = 14,
                            right = 14,
                            top = 8,
                            bottom = 8
                        }
                    };
                }

                return _Tab;
            }
        }

        private static GUIStyle _Button;
        public static GUIStyle Button
        {
            get
            {
                if (_Button == null)
                {
                    _Button = new GUIStyle(UnityEngine.GUI.skin.button)
                    {
                        alignment = TextAnchor.MiddleLeft,
                        stretchWidth = true,
                        richText = true,
                        wordWrap = true,
                        padding = new RectOffset()
                        {
                            left = 14,
                            right = 14,
                            top = 8,
                            bottom = 8
                        }
                    };
                }

                return _Button;
            }
        }
        
        public new static void DrawHeader()
        {
            EditorGUILayout.LabelField($"{AssetInfo.ASSET_NAME} v{AssetInfo.INSTALLED_VERSION}", EditorStyles.centeredGreyMiniLabel);
            EditorGUILayout.Space(5f);
        }

        public static void DrawFooter()
        {
            GUILayout.Space(5f);

            Rect r = EditorGUILayout.GetControlRect();
            
            if (r.Contains(Event.current.mousePosition))
            {
                //EditorGUIUtility.AddCursorRect(new Rect(Event.current.mousePosition.x, Event.current.mousePosition.y, 27, 27), MouseCursor.Link);
            }
            
            if (GUI.Button(r,new GUIContent("- Staggart Creations -", "Open website"), EditorStyles.centeredGreyMiniLabel))
            {
                //Application.OpenURL("http://staggart.xyz");
            }
        }
        
        public static void DrawActionBox(string text, string label, MessageType messageType, Action action)
        {
            DrawActionBox(text, new GUIContent(label), messageType, action);
        }

        public static void DrawActionBox(string text, GUIContent buttonContent, MessageType messageType, Action action)
        {
            Assert.IsNotNull(action);

            EditorGUILayout.HelpBox(text, messageType);

            GUILayout.Space(-32);
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();

                if (GUILayout.Button(buttonContent, GUILayout.Width(EditorStyles.miniButton.CalcSize(new GUIContent(buttonContent)).x + 5f)))
                    action();

                GUILayout.Space(8);
            }
            GUILayout.Space(11);
        }

        public static class Material
        {
            //Section toggles
            public class Section
            {
                private const float ANIMATION_SPEED = 16f;
                
                public bool Expanded
                {
                    get { return SessionState.GetBool(id, false); }
                    set { SessionState.SetBool(id, value); }
                }

                public AnimBool anim;

                private readonly string id;
                public string title;

                public Section(MaterialEditor target, string id, string title)
                {
                    this.id = "SGS_" + id + "_SECTION";
                    this.title = title;

                    anim = new AnimBool(false);
                    anim.valueChanged.AddListener(target.Repaint);
                    anim.speed = ANIMATION_SPEED;
                }

                public void SetTarget()
                {
                    anim.target = Expanded;
                }
            }

            //https://github.com/Unity-Technologies/Graphics/blob/d0473769091ff202422ad13b7b764c7b6a7ef0be/com.unity.render-pipelines.core/Editor/CoreEditorUtils.cs#L460
            public static bool DrawHeader(string title, bool isExpanded, Action clickAction = null)
            {
#if URP
                CoreEditorUtils.DrawSplitter();
#endif

                var backgroundRect = GUILayoutUtility.GetRect(1f, 25f);
 
                var labelRect = backgroundRect;
                labelRect.xMin += 8f;
                labelRect.xMax -= 20f + 16 + 5;

                var foldoutRect = backgroundRect;
                
                #if UNITY_2022_1_OR_NEWER
                //As of this version extra padding is added, to make room for property override indicators
                foldoutRect.x -= 16f;
                #endif
                
                foldoutRect.xMin -= 8f;
                foldoutRect.y += 0f;
                foldoutRect.width = 25f;
                foldoutRect.height = 25f;

                // Background rect should be full-width
                backgroundRect.xMin = 0f;
                backgroundRect.width += 4f;

                // Background
                float backgroundTint = EditorGUIUtility.isProSkin ? 0.1f : 1f;
                EditorGUI.DrawRect(backgroundRect, new Color(backgroundTint, backgroundTint, backgroundTint, 0.2f));

                // Title
                EditorGUI.LabelField(labelRect, title, EditorStyles.boldLabel);

                // Foldout
                isExpanded = GUI.Toggle(foldoutRect, isExpanded, new GUIContent(isExpanded ? "−" : "≡"), EditorStyles.boldLabel);

                // Context menu
                #if URP
                var menuIcon = CoreEditorStyles.paneOptionsIcon;
#else
                Texture menuIcon = null;
#endif
                var menuRect = new Rect(labelRect.xMax + 3f + 16 + 5, labelRect.y + 1f, menuIcon.width, menuIcon.height);

                //if (clickAction != null)
                //GUI.DrawTexture(menuRect, menuIcon);

                // Handle events
                var e = Event.current;

                if (e.type == EventType.MouseDown)
                {
                    if (clickAction != null && menuRect.Contains(e.mousePosition))
                    {
                        e.Use();
                    }
                    else if (labelRect.Contains(e.mousePosition))
                    {
                        if (e.button == 0)
                        {
                            isExpanded = !isExpanded;
                            if (clickAction != null) clickAction.Invoke();
                        }

                        e.Use();
                    }
                }

#if URP
                //CoreEditorUtils.DrawSplitter();
#endif

                //GUILayout.Space(5f);

                return isExpanded;
            }
        }
        
        public class ParameterGroup
        {
            static ParameterGroup()
            {
                Section = new GUIStyle(EditorStyles.helpBox)
                {
                    margin = new RectOffset(0, 0, -10, 10),
                    padding = new RectOffset(10, 10, 10, 10),
                    clipping = TextClipping.Clip,
                };

                headerLabel = new GUIStyle(EditorStyles.miniLabel);
                headerBackgroundDark = new Color(0.1f, 0.1f, 0.1f, 0.2f);
                headerBackgroundLight = new Color(1f, 1f, 1f, 0.2f);
                paneOptionsIconDark = (Texture2D)EditorGUIUtility.Load("Builtin Skins/DarkSkin/Images/pane options.png");
                paneOptionsIconLight = (Texture2D)EditorGUIUtility.Load("Builtin Skins/LightSkin/Images/pane options.png");
                splitterDark = new Color(0.12f, 0.12f, 0.12f, 1.333f);
                splitterLight = new Color(0.6f, 0.6f, 0.6f, 1.333f);
            }

            public static readonly GUIStyle headerLabel;
            public static GUIStyle Section;
            static readonly Texture2D paneOptionsIconDark;
            static readonly Texture2D paneOptionsIconLight;
            public static Texture2D paneOptionsIcon { get { return EditorGUIUtility.isProSkin ? paneOptionsIconDark : paneOptionsIconLight; } }
            static readonly Color headerBackgroundDark;
            static readonly Color headerBackgroundLight;
            public static Color headerBackground { get { return EditorGUIUtility.isProSkin ? headerBackgroundDark : headerBackgroundLight; } }

            static readonly Color splitterDark;
            static readonly Color splitterLight;
            public static Color splitter { get { return EditorGUIUtility.isProSkin ? splitterDark : splitterLight; } }

            public static void DrawHeader(GUIContent content)
            {
                //DrawSplitter();
                Rect backgroundRect = GUILayoutUtility.GetRect(1f, 20f);

                if (content.image)
                {
                    content.text = " " + content.text;
                }

                Rect labelRect = backgroundRect;
                labelRect.y += 0f;
                labelRect.xMin += 5f;
                labelRect.xMax -= 20f;

                // Background rect should be full-width
                backgroundRect.xMin = 10f;
                //backgroundRect.width -= 10f;

                // Background
                EditorGUI.DrawRect(backgroundRect, headerBackground);

                // Title
                EditorGUI.LabelField(labelRect, content, EditorStyles.boldLabel);

                DrawSplitter();
            }

            public static void DrawSplitter()
            {
                var rect = GUILayoutUtility.GetRect(1f, 1f);

                // Splitter rect should be full-width
                rect.xMin = 10f;
                //rect.width -= 10f;

                if (Event.current.type != EventType.Repaint)
                    return;

                EditorGUI.DrawRect(rect, splitter);
            }


        }

    }
}