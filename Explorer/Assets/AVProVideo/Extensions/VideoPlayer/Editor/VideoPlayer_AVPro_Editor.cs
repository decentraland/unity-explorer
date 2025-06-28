using System.Collections.Generic;
using UnityEditor;
using UnityEditor.AnimatedValues;
using UnityEngine;
using static RenderHeads.Media.AVProVideo.MediaPlayer;

//-----------------------------------------------------------------------------
// Copyright 2015-2024 RenderHeads Ltd.  All rights reserved.
//-----------------------------------------------------------------------------

namespace RenderHeads.Media.AVProVideo.Editor
{
    [CanEditMultipleObjects]
    [CustomEditor(typeof(VideoPlayer_AVPro))]
    public partial class VideoPlayer_AVPro_Editor : UnityEditor.Editor
    {
        #region Variables
        SerializedProperty m_source;
        SerializedProperty m_clip;
        SerializedProperty m_url;
        SerializedProperty m_playOnAwake;
        SerializedProperty m_autoOpen;
        SerializedProperty m_loop;
        SerializedProperty m_skipOnDrop;
        SerializedProperty m_playbackSpeed;
        SerializedProperty m_renderMode;
        SerializedProperty m_targetMaterialRenderer;
        SerializedProperty m_targetMaterialName;
        SerializedProperty m_targetMaterial;
        SerializedProperty m_color;
        SerializedProperty m_aspectRatio;
        SerializedProperty m_aspectRatioRenderTexture;
        SerializedProperty m_fullscreen;
        SerializedProperty m_audioOutputMode;
        SerializedProperty m_controlledTracks;
        SerializedProperty m_volume;
        SerializedProperty m_muted;
        SerializedProperty m_audioSource;
        SerializedProperty m_canvas;
        SerializedProperty m_targetTexture;
        SerializedProperty m_uGUIComponent;
        SerializedProperty m_UVRect;
        SerializedProperty m_alpha;


        public readonly GUIContent sourceContent =          EditorGUIUtility.TrTextContent("Source", "Type of source the media will be read from\n Reference - AVPro's VideoClip\n URL - Either path or URL");
        public readonly GUIContent clipContent =            EditorGUIUtility.TrTextContent("Media Reference", "Can be created through the context menu. \n AVPro's VideoClip");
        public readonly GUIContent urlContent =             EditorGUIUtility.TrTextContent("URL", "URLs\n Either URL or filepath");
        public readonly GUIContent playOnAwakeContent =     EditorGUIUtility.TrTextContent("Play On Awake", "Start playback as soon as the game is started.");
        public readonly GUIContent autoOpenContent =        EditorGUIUtility.TrTextContent("Auto Open", "Automatically opens the selected media as soon as the game is started");
        public readonly GUIContent loopContent =            EditorGUIUtility.TrTextContent("Loop", "Start playback at the beginning when end is reached.");
        public readonly GUIContent playbackSpeedContent =   EditorGUIUtility.TrTextContent("Playback Speed", "Increase or decrease the playback speed. 1.0 is the normal speed.");
        public readonly GUIContent renderModeContent =      EditorGUIUtility.TrTextContent("Render Mode", "Type of object on which the played images will be drawn.");
        public readonly GUIContent rendererContent =        EditorGUIUtility.TrTextContent("Renderer", "Renderer that the images will be displayed on");
        public readonly GUIContent texturePropertyContent = EditorGUIUtility.TrTextContent("Material Property", "Texture Property of the current material that will recive the images");
        public readonly GUIContent materialContent =        EditorGUIUtility.TrTextContent("Material", "Material that the images will be writted to");
        public readonly GUIContent colorContent =           EditorGUIUtility.TrTextContent("Color", "Default color");
        public readonly GUIContent aspectRatioContent =     EditorGUIUtility.TrTextContent("Aspect Ratio", "How the video content will be fit into the target area");
        public readonly GUIContent fullscreenContent =      EditorGUIUtility.TrTextContent("Fullscreen", "Force the video to take up the entire screen");
        public readonly GUIContent audioOutputModeContent = EditorGUIUtility.TrTextContent("Audio Output Mode", "where the audio of the video will be output");
        public readonly GUIContent volumeContent =          EditorGUIUtility.TrTextContent("Volume", "Volume of the output audio");
        public readonly GUIContent mutedContent =           EditorGUIUtility.TrTextContent("Mute", "Mute the audio");
        public readonly GUIContent audioSourceContent =     EditorGUIUtility.TrTextContent("Audio Source", "AudioSource component that will recive this videos audio samples");
        public readonly GUIContent targetTextureContent =   EditorGUIUtility.TrTextContent("Render Texture", "Render texture to draw the current frame to");
        public readonly GUIContent browseContent =          EditorGUIUtility.TrTextContent("Browse...", "Click to set a file:// URL.  http:// URLs have to be written or copy-pasted manually.");
        public readonly GUIContent uvRectContent =          EditorGUIUtility.TrTextContent("UV", "Sets the UV rect of the image allowing, scaling and positioning of the image");
        public readonly GUIContent displayUGUIContent =     EditorGUIUtility.TrTextContent("uGUI Component", "The UGUI componenet that will handle the rendering");
        public readonly GUIContent nativeSizeContent =      EditorGUIUtility.TrTextContent("Set Native Size", "Sets the image to be its native size");
        public readonly GUIContent alphaContent =           EditorGUIUtility.TrTextContent("Alpha", "Set the alpha of the video stream");
        //public readonly GUIContent  = EditorGUIUtility.TrTextContent("", "");
        public readonly string uGUIUsageInformation =       "Ensure you have placed a Display uGUI component on a GameObject containing a Canvas Renderer, and linked it with this";
        // local video path selection
        public readonly string selectMovieFile =            "Select movie file.";
        public readonly string selectMovieFileRecentPath =  "VideoPlayer_AVProSelectMovieFileRecentPath";
        public readonly string[] selectMovieFileFilter = {
            L10n.Tr("Movie files"), "asf,avi,dv,m4v,mp4,mov,mpg,mpeg,m4v,ogv,vp8,webm,wmv",
            L10n.Tr("All files"), "*"
        };

#if UNITY_STANDALONE_WIN
		// facebook 360 audio options
		private readonly string _optionAudio360ChannelModeName = ".audio360ChannelMode";
#endif
        private readonly GUIContent _optionAudio360ChannelModeContent = new GUIContent("Channel Mode", "Specifies what channel mode Facebook Audio 360 needs to be initialised with");
        private readonly GUIContent[] _audio360ChannelMapGuiNames =
        {
            new GUIContent("(TBE_8_2) 8 channels of hybrid TBE ambisonics and 2 channels of head-locked stereo audio"),
            new GUIContent("(TBE_8) 8 channels of hybrid TBE ambisonics. NO head-locked stereo audio"),
            new GUIContent("(TBE_6_2) 6 channels of hybrid TBE ambisonics and 2 channels of head-locked stereo audio"),
            new GUIContent("(TBE_6) 6 channels of hybrid TBE ambisonics. NO head-locked stereo audio"),
            new GUIContent("(TBE_4_2) 4 channels of hybrid TBE ambisonics and 2 channels of head-locked stereo audio"),
            new GUIContent("(TBE_4) 4 channels of hybrid TBE ambisonics. NO head-locked stereo audio"),

            new GUIContent("(TBE_8_PAIR0) Channels 1 and 2 of TBE hybrid ambisonics"),
            new GUIContent("(TBE_8_PAIR1) Channels 3 and 4 of TBE hybrid ambisonics"),
            new GUIContent("(TBE_8_PAIR2) Channels 5 and 6 of TBE hybrid ambisonics"),
            new GUIContent("(TBE_8_PAIR3) Channels 7 and 8 of TBE hybrid ambisonics"),

            new GUIContent("(TBE_CHANNEL0) Channels 1 of TBE hybrid ambisonics"),
            new GUIContent("(TBE_CHANNEL1) Channels 2 of TBE hybrid ambisonics"),
            new GUIContent("(TBE_CHANNEL2) Channels 3 of TBE hybrid ambisonics"),
            new GUIContent("(TBE_CHANNEL3) Channels 4 of TBE hybrid ambisonics"),
            new GUIContent("(TBE_CHANNEL4) Channels 5 of TBE hybrid ambisonics"),
            new GUIContent("(TBE_CHANNEL5) Channels 6 of TBE hybrid ambisonics"),
            new GUIContent("(TBE_CHANNEL6) Channels 7 of TBE hybrid ambisonics"),
            new GUIContent("(TBE_CHANNEL7) Channels 8 of TBE hybrid ambisonics"),

            new GUIContent("(HEADLOCKED_STEREO) Head-locked stereo audio"),
            new GUIContent("(HEADLOCKED_CHANNEL0) Channels 1 or left of head-locked stereo audio"),
            new GUIContent("(HEADLOCKED_CHANNEL1) Channels 2 or right of head-locked stereo audio"),

            new GUIContent("(AMBIX_4) 4 channels of first order ambiX"),
            new GUIContent("(AMBIX_4_2) 4 channels of first order ambiX with 2 channels of head-locked audio"),
            new GUIContent("(AMBIX_9) 9 channels of second order ambiX"),
            new GUIContent("(AMBIX_9_2) 9 channels of second order ambiX with 2 channels of head-locked audio"),
            new GUIContent("(AMBIX_16) 16 channels of third order ambiX"),
            new GUIContent("(AMBIX_16_2) 16 channels of third order ambiX with 2 channels of head-locked audio"),

            new GUIContent("(MONO) Mono audio"),
            new GUIContent("(STEREO) Stereo audio"),
        };

        // material options
        private GUIContent[] _materialTextureProperties =       new GUIContent[0];
        private readonly GUIContent _guiTextTextureProperty =   new GUIContent("Texture Property", "Texture Property of the current material that will recive the images");

        // animations
        readonly AnimBool m_SourceReference =   new AnimBool();
        readonly AnimBool m_SourceUrl =         new AnimBool();
        readonly AnimBool m_RenderMesh =        new AnimBool();
        readonly AnimBool m_RenderMaterial =    new AnimBool();
        readonly AnimBool m_RenderuGUI =        new AnimBool();
        readonly AnimBool m_RenderIMGUI =       new AnimBool();
        readonly AnimBool m_RenderFarPlane =    new AnimBool();
        readonly AnimBool m_RenderTexture =     new AnimBool();
        readonly AnimBool m_AudioOutSystem =    new AnimBool();
        readonly AnimBool m_AudioOutUnity =     new AnimBool();
        readonly AnimBool m_AudioOutFacebook =  new AnimBool();
        readonly AnimBool m_AudioOutNone =      new AnimBool();

        #endregion Variables

        #region Enable/Disable
        void OnEnable()
        {
            m_SourceReference.valueChanged.AddListener(Repaint);
            m_SourceUrl.valueChanged.AddListener(Repaint);
            m_RenderMesh.valueChanged.AddListener(Repaint);
            m_RenderMaterial.valueChanged.AddListener(Repaint);
            m_RenderuGUI.valueChanged.AddListener(Repaint);
            m_RenderIMGUI.valueChanged.AddListener(Repaint);
            m_RenderFarPlane.valueChanged.AddListener(Repaint);
            m_RenderTexture.valueChanged.AddListener(Repaint);
            m_AudioOutSystem.valueChanged.AddListener(Repaint);
            m_AudioOutUnity.valueChanged.AddListener(Repaint);
            m_AudioOutFacebook.valueChanged.AddListener(Repaint);
            m_AudioOutNone.valueChanged.AddListener(Repaint);

            m_source =                      serializedObject.FindProperty("Source");
            m_clip =                        serializedObject.FindProperty("Clip");
            m_url =                         serializedObject.FindProperty("Url");
            m_playOnAwake =                 serializedObject.FindProperty("PlayOnAwake");
            m_autoOpen =                    serializedObject.FindProperty("AutoOpening");
            m_loop =                        serializedObject.FindProperty("IsLooping");
            m_skipOnDrop =                  serializedObject.FindProperty("SkipOnDrop");
            m_playbackSpeed =               serializedObject.FindProperty("PlaybackSpeed");
            m_renderMode =                  serializedObject.FindProperty("RenderMode");
            m_targetMaterialRenderer =      serializedObject.FindProperty("TargetMaterialRenderer");
            m_targetMaterialName =          serializedObject.FindProperty("TargetMateralProperty");
            m_targetMaterial =              serializedObject.FindProperty("TargetMaterial");
            m_color =                       serializedObject.FindProperty("Colour");
            m_aspectRatio =                 serializedObject.FindProperty("AspectRatio");
            m_aspectRatioRenderTexture =    serializedObject.FindProperty("AspectRatioRenderTexture");
            m_fullscreen =                  serializedObject.FindProperty("Fullscreen");
            m_audioOutputMode =             serializedObject.FindProperty("AudioOutputMode");
            m_controlledTracks =            serializedObject.FindProperty("ControlledAudioTrackCount");
            m_volume =                      serializedObject.FindProperty("Volume");
            m_muted =                       serializedObject.FindProperty("Muted");
            m_audioSource =                 serializedObject.FindProperty("AudioSourceE");
            m_canvas =                      serializedObject.FindProperty("canvasObj");
            m_targetTexture =               serializedObject.FindProperty("TargetTexture");
            m_uGUIComponent =               serializedObject.FindProperty("displayUGUI");
            m_UVRect =                      serializedObject.FindProperty("UVRect");
            m_alpha =                       serializedObject.FindProperty("TargetCameraAlpha");

            m_SourceReference.value =   m_source.intValue           == 0;
            m_SourceUrl.value =         m_source.intValue           == 1;
            m_RenderMesh.value =        m_renderMode.intValue       == 0;
            m_RenderMaterial.value =    m_renderMode.intValue       == 1;
            m_RenderuGUI.value =        m_renderMode.intValue       == 2;
            m_RenderIMGUI.value =       m_renderMode.intValue       == 3;
            m_RenderFarPlane.value =    m_renderMode.intValue       == 4;
            m_RenderTexture.value =     m_renderMode.intValue       == 5;
            m_AudioOutSystem.value =    m_audioOutputMode.intValue  == 0;
            m_AudioOutUnity.value =     m_audioOutputMode.intValue  == 1;
            m_AudioOutFacebook.value =  m_audioOutputMode.intValue  == 2;
            m_AudioOutNone.value =      m_audioOutputMode.intValue  == 3;
        }

        private void OnDisable()
        {
            m_SourceReference.valueChanged.RemoveListener(Repaint);
            m_SourceUrl.valueChanged.RemoveListener(Repaint);
            m_RenderMesh.valueChanged.RemoveListener(Repaint);
            m_RenderMaterial.valueChanged.RemoveListener(Repaint);
            m_RenderuGUI.valueChanged.RemoveListener(Repaint);
            m_RenderIMGUI.valueChanged.RemoveListener(Repaint);
            m_RenderFarPlane.valueChanged.RemoveListener(Repaint);
            m_RenderTexture.valueChanged.RemoveListener(Repaint);
            m_AudioOutSystem.valueChanged.RemoveListener(Repaint);
            m_AudioOutUnity.valueChanged.RemoveListener(Repaint);
            m_AudioOutFacebook.valueChanged.RemoveListener(Repaint);
            m_AudioOutNone.valueChanged.RemoveListener(Repaint);
        }

        #endregion Enable/Disable

        public override void OnInspectorGUI()
        {
            VideoPlayer_AVPro player = (VideoPlayer_AVPro)target;
            serializedObject.Update();

            // Media Source
            HandleSourceField(player);

            // Play on awake
            EditorGUILayout.PropertyField(m_playOnAwake, playOnAwakeContent);
            if (m_playOnAwake.serializedObject.ApplyModifiedProperties())
                player.playOnAwake = m_playOnAwake.boolValue;

            // auto open
            EditorGUILayout.PropertyField(m_autoOpen, autoOpenContent);
            if (m_autoOpen.serializedObject.ApplyModifiedProperties())
                player.AutoOpen = m_autoOpen.boolValue;

            // Loop
            EditorGUILayout.PropertyField(m_loop, loopContent);
            if (m_loop.serializedObject.ApplyModifiedProperties())
                player.isLooping = m_loop.boolValue;

            // Playback Speed
            EditorGUILayout.Slider(m_playbackSpeed, -4f, 4f, playbackSpeedContent);
            if (m_playbackSpeed.serializedObject.ApplyModifiedProperties())
                player.playbackSpeed = m_playbackSpeed.floatValue;

            EditorGUILayout.Space(10);

            // Render Mode
            HandleRenderModeField(player);

            // Audio Output Mode
            HandleAudioOutputModeField(player);

            serializedObject.ApplyModifiedProperties();
        }

        #region GUI Handlers

        #region Source Field
        /// <summary>
        /// Draws the source field, giving functionality to both the Path/URL, <see cref="MediaReference"/> fields
        /// </summary>
        /// <param name="player"><see cref="VideoPlayer_AVPro"/> to use</param>
        public void HandleSourceField(VideoPlayer_AVPro player)
        {
            // to be used within the fadegroups, resulting in only the selected group being shown.
            m_SourceReference.target = m_source.intValue == 0;
            m_SourceUrl.target = m_source.intValue == 1;

            // Source Type
            EditorGUILayout.PropertyField(m_source, sourceContent);
            // the below code will be used whenever we need to update the value on the target object
            // to reduce performnace cost, and infinite calls this is only updated when the UI for
            // that value is changed
            if (m_source.serializedObject.ApplyModifiedProperties())
                player.sourceAVPro = (MediaSource)m_source.enumValueIndex;

            // Reference (MediaReference)
            EditorGUI.indentLevel++;
            if (EditorGUILayout.BeginFadeGroup(m_SourceReference.faded))
            {
                EditorGUILayout.PropertyField(m_clip, clipContent);
                if (m_clip.serializedObject.ApplyModifiedProperties())
                {
                    player.clip = (MediaReference)m_clip.objectReferenceValue;
                }
                    
            }
            EditorGUILayout.EndFadeGroup();

            // URL
            if (EditorGUILayout.BeginFadeGroup(m_SourceUrl.faded))
            {
                // URL (web)
                EditorGUILayout.PropertyField(m_url, urlContent);
                // BROWSE (local)
                Rect browseRect = EditorGUILayout.GetControlRect(true, EditorGUIUtility.singleLineHeight);
                browseRect.xMin += EditorGUIUtility.labelWidth;
                browseRect.xMax = browseRect.xMin + GUI.skin.label.CalcSize(browseContent).x + 10;
                if (EditorGUI.DropdownButton(
                    browseRect, browseContent, FocusType.Passive, GUI.skin.button))
                {
                    string path = EditorUtility.OpenFilePanelWithFilters(
                        selectMovieFile,
                        EditorPrefs.GetString(selectMovieFileRecentPath),
                        selectMovieFileFilter);

                    if (!string.IsNullOrEmpty(path))
                    {
                        m_url.stringValue = "file://" + path;
                        EditorPrefs.SetString(selectMovieFileRecentPath, path);
                    }
                    EditorGUIUtility.ExitGUI();
                }
                if (m_url.serializedObject.ApplyModifiedProperties())
                    player.url = new MediaPath(m_url.stringValue, MediaPathType.AbsolutePathOrURL);
            }
            EditorGUILayout.EndFadeGroup();
            EditorGUI.indentLevel--;
        }
        #endregion

        #region Render Mode Field
        /// <summary>
        /// Handles Drawing all the differernt rendering options to the inspector,
        /// with only the current selected being shown.
        /// </summary>
        /// <param name="player"><see cref="VideoPlayer_AVPro "/> to use</param>
        public void HandleRenderModeField(VideoPlayer_AVPro player)
        {
            // current rendering mode
            EditorGUILayout.PropertyField(m_renderMode, renderModeContent);
            if (m_renderMode.serializedObject.ApplyModifiedProperties())
                player.rendererMode = (DisplayType)m_renderMode.enumValueIndex;

            // setup fade groups to only show the active render mode
            m_RenderMesh.target = m_renderMode.intValue == 0;
            m_RenderMaterial.target = m_renderMode.intValue == 1;
            m_RenderuGUI.target = m_renderMode.intValue == 2;
            m_RenderIMGUI.target = m_renderMode.intValue == 3;
            m_RenderFarPlane.target = m_renderMode.intValue == 4;
            m_RenderTexture.target = m_renderMode.intValue == 5;

            // Note:
            //  - the fade groups dont work here as the AddComponent breaks them (they dont fade), their does not seem to
            //    be any way of fixing it

            EditorGUI.indentLevel++;
            // render mesh
            if (EditorGUILayout.BeginFadeGroup(m_RenderMesh.faded))
            {
                if (player.applyToMesh)
                {
                    // target remderer
                    EditorGUILayout.PropertyField(m_targetMaterialRenderer, rendererContent);
                    if (m_targetMaterialRenderer.serializedObject.ApplyModifiedProperties())
                    {
                        Debug.Log("Sertting Target Renderer to: " + (Renderer)m_targetMaterialRenderer.objectReferenceValue);
                        player.applyToMesh.MeshRenderer = (Renderer)m_targetMaterialRenderer.objectReferenceValue;
                        player.targetMaterialRenderer = (Renderer)m_targetMaterialRenderer.objectReferenceValue;
                    }
                    // material properties
                    DrawMaterialPropertieDropdown(m_targetMaterialRenderer, m_targetMaterialName, DisplayType.Mesh);
                    if (m_targetMaterialName.serializedObject.ApplyModifiedProperties())
                    {
                        player.applyToMesh.TexturePropertyName = m_targetMaterialName.stringValue;
                        player.targetMateralProperty = m_targetMaterialName.stringValue;
                    }
                }
            }
            EditorGUILayout.EndFadeGroup();

            // render material
            if (EditorGUILayout.BeginFadeGroup(m_RenderMaterial.faded))
            {
                if (player.applyToMaterial)
                {
                    // target material
                    EditorGUILayout.PropertyField(m_targetMaterial, materialContent);
                    if (m_targetMaterial.serializedObject.ApplyModifiedProperties())
                    {
                        player.applyToMaterial.Material = (Material)m_targetMaterial.objectReferenceValue;
                        player.targetMaterial = (Material)m_targetMaterial.objectReferenceValue;
                    }
                    // material properties
                    DrawMaterialPropertieDropdown(m_targetMaterial, m_targetMaterialName, DisplayType.Material);
                    if (m_targetMaterialName.serializedObject.ApplyModifiedProperties())
                    {
                        player.applyToMaterial.TexturePropertyName = m_targetMaterialName.stringValue;
                        player.targetMateralProperty = m_targetMaterialName.stringValue;
                    }
                }
            }
            EditorGUILayout.EndFadeGroup();

            // uGUI
            if (EditorGUILayout.BeginFadeGroup(m_RenderuGUI.faded))
            {
                EditorGUILayout.PropertyField(m_uGUIComponent, displayUGUIContent);
                // when the user has not set a the uGUI componenet, infom them that they need to set this component
                // and where they need to set it.
                if (!player.displayUGUI)
                {
                    EditorGUILayout.HelpBox(uGUIUsageInformation, MessageType.Info, false);
                }
                else
                {
                    // color
                    EditorGUILayout.PropertyField(m_color, colorContent);
                    if (m_color.serializedObject.ApplyModifiedProperties())
                    {
                        player.displayUGUI.color = m_color.colorValue;
                        player.color = m_color.colorValue;
                    }
                    // UV
                    EditorGUILayout.PropertyField(m_UVRect, uvRectContent);
                    if (m_UVRect.serializedObject.ApplyModifiedProperties())
                    {
                        player.uvRect = m_UVRect.rectValue;
                    }
                    // Native (using full screen, as their is no need to create another serialized property,
                    // so just use fullscreen)
                    EditorGUILayout.PropertyField(m_fullscreen, nativeSizeContent);
                    if (m_fullscreen.serializedObject.ApplyModifiedProperties())
                    {
                        player.displayUGUI.ApplyNativeSize = m_fullscreen.boolValue;
                        player.fullScreen = m_fullscreen.boolValue;
                    }
                    // Scale
                    EditorGUILayout.PropertyField(m_aspectRatio, aspectRatioContent);
                    if (m_aspectRatio.serializedObject.ApplyModifiedProperties())
                    {
                        player.displayUGUI.ScaleMode = (ScaleMode)m_aspectRatio.enumValueIndex;
                        player.aspectRatio = (ScaleMode)m_aspectRatio.enumValueIndex;
                    }
                } 
            }
            EditorGUILayout.EndFadeGroup();

            // render IMGUI
            if (EditorGUILayout.BeginFadeGroup(m_RenderIMGUI.faded))
            {
                if (player.displayIMGUI)
                {
                    // Fullscreen
                    EditorGUILayout.PropertyField(m_fullscreen, fullscreenContent);
                    if (m_fullscreen.serializedObject.ApplyModifiedProperties())
                    {
                        player.displayIMGUI.IsAreaFullScreen = m_fullscreen.boolValue;
                        player.fullScreen = m_fullscreen.boolValue;
                    }
                    // color
                    EditorGUILayout.PropertyField(m_color, colorContent);
                    if (m_color.serializedObject.ApplyModifiedProperties())
                    {
                        player.displayIMGUI.Color = m_color.colorValue;
                        player.color = m_color.colorValue;
                    }
                    // scale mode
                    EditorGUILayout.PropertyField(m_aspectRatio, aspectRatioContent);
                    if (m_aspectRatio.serializedObject.ApplyModifiedProperties())
                    {
                        player.displayIMGUI.ScaleMode = (ScaleMode)m_aspectRatio.enumValueIndex;
                        player.aspectRatio = (ScaleMode)m_aspectRatio.enumValueIndex;
                    }
                }
            }
            EditorGUILayout.EndFadeGroup();

            // render far plane
            if (EditorGUILayout.BeginFadeGroup(m_RenderFarPlane.faded))
            {
                if (player.applyToFarPlane)
                {
                    // Color
                    EditorGUILayout.PropertyField(m_color, colorContent);
                    if (m_color.serializedObject.ApplyModifiedProperties())
                        player.color = m_color.colorValue;
                    // aspect ratio (scale mode)
                    EditorGUILayout.PropertyField(m_aspectRatioRenderTexture, aspectRatioContent);
                    if (m_aspectRatioRenderTexture.serializedObject.ApplyModifiedProperties())
                        player.aspectRatioRenderTexture = (VideoResolveOptions.AspectRatio)m_aspectRatioRenderTexture.enumValueIndex;
                    // alpha
                    EditorGUILayout.Slider(m_alpha, 0, 1, alphaContent);
                    if (m_alpha.serializedObject.ApplyModifiedProperties())
                        player.targetCameraAlpha = m_alpha.floatValue;
                }
            }
            EditorGUILayout.EndFadeGroup();

            // render texture
            if (EditorGUILayout.BeginFadeGroup(m_RenderTexture.faded))
            {
                if (player.applyToTexture)
                {
                    // target texture
                    EditorGUILayout.PropertyField(m_targetTexture, targetTextureContent);
                    if (m_targetTexture.serializedObject.ApplyModifiedProperties())
                    {
                        player.applyToTexture.ExternalTexture = (RenderTexture)m_targetTexture.objectReferenceValue;
                        player.targetTexture = (RenderTexture)m_targetTexture.objectReferenceValue;
                    }
                    // aspect ratio
                    EditorGUILayout.PropertyField(m_aspectRatioRenderTexture, aspectRatioContent);
                    if (m_aspectRatioRenderTexture.serializedObject.ApplyModifiedProperties())
                    {
                        var options = player.applyToTexture.VideoResolveOptions;
                        options.aspectRatio = (VideoResolveOptions.AspectRatio)m_aspectRatioRenderTexture.enumValueIndex;
                        player.applyToTexture.VideoResolveOptions = options;
                        player.aspectRatioRenderTexture = (VideoResolveOptions.AspectRatio)m_aspectRatioRenderTexture.enumValueIndex;
                    }
                }
            }
            EditorGUILayout.EndFadeGroup();

            EditorGUI.indentLevel--;
            EditorGUILayout.Space(10);
        }
        #endregion Render Mode Field

        #region Audio Output Field
        /// <summary>
        /// Handles drawing the Audio Output options for the inspector, based on the current
        /// output mode that has been selected
        /// </summary>
        /// <param name="player"><see cref="VideoPlayer_AVPro"/> that is being used</param>
        public void HandleAudioOutputModeField(VideoPlayer_AVPro player)
        {
            // output mode
            EditorGUILayout.PropertyField(m_audioOutputMode, audioOutputModeContent);
            if (m_audioOutputMode.serializedObject.ApplyModifiedProperties())
            {
#if UNITY_STANDALONE_WIN
                player.audioOutputModeAVPro = (Windows.AudioOutput)m_audioOutputMode.enumValueIndex;
#elif UNITY_WSA_10_0
                player.audioOutputModeAVPro = (WindowsUWP.AudioOutput)m_audioOutputMode.enumValueIndex;
#elif UNITY_ANDROID || UNITY_IOS || UNITY_STANDALONE_OSX || UNITY_TVOS || UNITY_VISIONOS
                player.audioOutputModeAVPro = (PlatformOptions.AudioMode)m_audioOutputMode.enumValueIndex;
#endif
            }

            // setup fade groups to only show the active audio mode
            m_AudioOutSystem.target = m_audioOutputMode.intValue == 0;
            m_AudioOutUnity.target = m_audioOutputMode.intValue == 1;
            m_AudioOutFacebook.target = m_audioOutputMode.intValue == 2;
            m_AudioOutNone.target = m_audioOutputMode.intValue == 3;

#if UNITY_STANDALONE_WIN
            EditorGUI.indentLevel++;
            // system audio
            if (EditorGUILayout.BeginFadeGroup(m_AudioOutSystem.faded))
            {
                // volume
                EditorGUILayout.Slider(m_volume, 0f, 1f, volumeContent);
                if (m_volume.serializedObject.ApplyModifiedProperties())
                    player.volume = m_volume.floatValue;
                // muted
                EditorGUILayout.PropertyField(m_muted, mutedContent);
                if (m_muted.serializedObject.ApplyModifiedProperties())
                    player.muted = m_muted.boolValue;
            }
            EditorGUILayout.EndFadeGroup();

            // unity audio
            if (EditorGUILayout.BeginFadeGroup(m_AudioOutUnity.faded))
            {
                // audio source
                EditorGUILayout.PropertyField(m_audioSource, audioSourceContent);
                if (m_audioSource.serializedObject.ApplyModifiedProperties())
                    player.audioSource = (AudioSource)m_audioSource.objectReferenceValue;
                // vlumvolume
                EditorGUILayout.Slider(m_volume, 0f, 1f, volumeContent);
                if (m_volume.serializedObject.ApplyModifiedProperties())
                    player.volume = m_volume.floatValue;
                // muted
                EditorGUILayout.PropertyField(m_muted, mutedContent);
                if (m_muted.serializedObject.ApplyModifiedProperties())
                    player.muted = m_muted.boolValue;
            }
            EditorGUILayout.EndFadeGroup();

            // facebook audio
            if (EditorGUILayout.BeginFadeGroup(m_AudioOutFacebook.faded))
            {
                // Channel Mode (Only Windows and Android can change this value)
#if UNITY_STANDALONE_WIN || UNITY_ANDROID
                var optionsVarName = "_optionsWindows";
                DisplayPlatformOptionEnum(this.serializedObject, optionsVarName + _optionAudio360ChannelModeName, _optionAudio360ChannelModeContent, _audio360ChannelMapGuiNames);
#endif

                // Device (Windows Only)
#if UNITY_STANDALONE_WIN
                SerializedProperty propForceAudioOutputDeviceName = serializedObject.FindProperty(optionsVarName + ".forceAudioOutputDeviceName");
                if (propForceAudioOutputDeviceName != null)
                {
                    string[] deviceNames = { "Default", Windows.AudioDeviceOutputName_Rift, Windows.AudioDeviceOutputName_Vive, "Custom" };
                    int index = 0;
                    if (!string.IsNullOrEmpty(propForceAudioOutputDeviceName.stringValue))
                    {
                        switch (propForceAudioOutputDeviceName.stringValue)
                        {
                            case Windows.AudioDeviceOutputName_Rift:
                                index = 1;
                                break;
                            case Windows.AudioDeviceOutputName_Vive:
                                index = 2;
                                break;
                            default:
                                index = 3;
                                break;
                        }
                    }
                    int newIndex = EditorGUILayout.Popup("Audio Device Name", index, deviceNames);
                    if (newIndex == 0)
                    {
                        propForceAudioOutputDeviceName.stringValue = string.Empty;
                    }
                    else if (newIndex == 3)
                    {
                        if (index != newIndex)
                        {
                            if (string.IsNullOrEmpty(propForceAudioOutputDeviceName.stringValue) ||
                                    propForceAudioOutputDeviceName.stringValue == Windows.AudioDeviceOutputName_Rift ||
                                    propForceAudioOutputDeviceName.stringValue == Windows.AudioDeviceOutputName_Vive)
                            {
                                propForceAudioOutputDeviceName.stringValue = "?";
                            }
                        }
                        EditorGUILayout.PropertyField(propForceAudioOutputDeviceName, new GUIContent("Audio Device Name", "Useful for VR when you need to output to the VR audio device"));
                    }
                    else
                    {
                        propForceAudioOutputDeviceName.stringValue = deviceNames[newIndex];
                    }
                }
#endif
                // volume
                EditorGUILayout.Slider(m_volume, 0f, 1f, volumeContent);
                if (m_volume.serializedObject.ApplyModifiedProperties())
                    player.volume = m_volume.floatValue;
                // muted
                EditorGUILayout.PropertyField(m_muted, mutedContent);
                if (m_muted.serializedObject.ApplyModifiedProperties())
                    player.muted = m_muted.boolValue;
            }
            EditorGUILayout.EndFadeGroup();

            if (EditorGUILayout.BeginFadeGroup(m_AudioOutNone.faded))
            {
                // Nothing for this little guy
            }
            EditorGUILayout.EndFadeGroup();

            EditorGUI.indentLevel--;
#endif
        }
#endregion Audio Output Field

#endregion GUI Handlers

        #region Helpers

        /// <summary>
        /// Draws the material properties as a dropdown
        /// </summary>
        /// <param name="renderer"><see cref="SerializedProperty"/> containing either a <see cref="Renderer"/> or <see cref="Material"/> to get the list of materials from</param>
        /// <param name="name">the name of the current selected material propertie</param>
        /// <param name="type">what the current <see cref="DisplayType"/> is</param>
        public void DrawMaterialPropertieDropdown(SerializedProperty renderer, SerializedProperty name, DisplayType type)
        {
            bool hasKeywords = false;
            int materialCount = 0;
            int texturePropertyIndex = 0;
            _materialTextureProperties = new GUIContent[0];
            if (renderer.objectReferenceValue != null)
            {
                List<Material> nonNullMaterials = new List<Material>();
                if (type == DisplayType.Mesh || type == DisplayType.CameraFarPlane || type == DisplayType.RenderTexture)
                {
                    Renderer r = (Renderer)(renderer.objectReferenceValue);
                    materialCount = r.sharedMaterials.Length;
                    nonNullMaterials = new List<Material>(r.sharedMaterials);
                }
                else if (type == DisplayType.Material)
                {
                    Material m = (Material)renderer.objectReferenceValue;
                    materialCount = 1;
                    nonNullMaterials = new List<Material>() { m };
                }
                // Remove any null materials (otherwise MaterialEditor.GetMaterialProperties() errors)
                for (int i = 0; i < nonNullMaterials.Count; i++)
                {
                    if (nonNullMaterials[i] == null)
                    {
                        nonNullMaterials.RemoveAt(i);
                        i--;
                    }
                }

                if (nonNullMaterials.Count > 0)
                {
                    // Detect if there are any keywords
                    foreach (Material mat in nonNullMaterials)
                    {
                        if (mat.shaderKeywords.Length > 0)
                        {
                            hasKeywords = true;
                            break;
                        }
                    }

                    // Get unique list of texture property names
                    List<GUIContent> items = new List<GUIContent>(16);
                    List<string> textureNames = new List<string>(8);
                    foreach (Material mat in nonNullMaterials)
                    {
                        // NOTE: we process each material separately instead of passing them all into  MaterialEditor.GetMaterialProperties() as it errors if the materials have different properties
                        MaterialProperty[] matProps = MaterialEditor.GetMaterialProperties(new UnityEngine.Object[] { mat });
                        foreach (MaterialProperty matProp in matProps)
                        {
                            if (matProp.type == MaterialProperty.PropType.Texture)
                            {
                                if (!textureNames.Contains(matProp.name))
                                {
                                    if (matProp.name == name.stringValue)
                                    {
                                        texturePropertyIndex = items.Count;
                                    }
                                    textureNames.Add(matProp.name);
                                    items.Add(new GUIContent(matProp.name));
                                }
                            }
                        }
                    }
                    _materialTextureProperties = items.ToArray();
                }
            }

            int newTexturePropertyIndex = EditorGUILayout.Popup(_guiTextTextureProperty, texturePropertyIndex, _materialTextureProperties);
            if (newTexturePropertyIndex >= 0 && newTexturePropertyIndex < _materialTextureProperties.Length)
            {
                name.stringValue = _materialTextureProperties[newTexturePropertyIndex].text;
            }

            if (hasKeywords && name.stringValue != Helper.UnityBaseTextureName)
            {
                EditorGUILayout.HelpBox("When using an uber shader you may need to enable the keywords on a material for certain texture slots to take effect.  You can sometimes achieve this (eg with Standard shader) by putting a dummy texture into the texture slot.", MessageType.Info);
            }
        }

        /// <summary>
        /// Displays the platform options as an enum, this is used by facebook audio to convert <see cref="_audio360ChannelMapGuiNames"/>
        /// to a selectable enum
        /// </summary>
        /// <param name="so">The <see cref="SerializedObject"/> to use </param>
        /// <param name="fieldName">Name of the propertie attatched to the <see cref="SerializedObject"/></param>
        /// <param name="description"><see cref="GUIContent"/> to show as description</param>
        /// <param name="enumNames">list of names to convert into enum <see cref="_audio360ChannelMapGuiNames"/></param>
        /// <returns>The created <see cref="SerializedProperty"/> Enum containing all of the data from the inputterd <c>enumNames</c></returns>
        private static SerializedProperty DisplayPlatformOptionEnum(SerializedObject so, string fieldName, GUIContent description, GUIContent[] enumNames)
        {
            SerializedProperty prop = so.FindProperty(fieldName);
            if (prop != null)
                prop.enumValueIndex = EditorGUILayout.Popup(description, prop.enumValueIndex, enumNames);
            else
                Debug.LogWarning("Can't find property `" + fieldName + "`");
            return prop;
        }

        #endregion Helpers
    }
}