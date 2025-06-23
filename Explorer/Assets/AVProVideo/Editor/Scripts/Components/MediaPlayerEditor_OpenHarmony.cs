using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

//-----------------------------------------------------------------------------
// Copyright 2015-2021 RenderHeads Ltd.  All rights reserved.
//-----------------------------------------------------------------------------

namespace RenderHeads.Media.AVProVideo.Editor
{
	/// <summary>
	/// Editor for the MediaPlayer component
	/// </summary>
	public partial class MediaPlayerEditor : UnityEditor.Editor
	{
//		private readonly static FieldDescription _optionFileOffset = new FieldDescription(".fileOffset", GUIContent.none);
		private readonly static FieldDescription _optionGenerateMipmapsOpenHarmony = new FieldDescription("._generateMipmapsOH", new GUIContent("Generate Mipmaps", "Generate a complete mipmap chain for the output texture"));

/*
		private readonly static FieldDescription _optionPreferredMaximumResolution = new FieldDescription("._preferredMaximumResolution", new GUIContent("Preferred Maximum Resolution", "The desired maximum resolution of the video."));
#if UNITY_2017_2_OR_NEWER
		private readonly static FieldDescription _optionCustomPreferredMaxResolution = new FieldDescription("._customPreferredMaximumResolution", new GUIContent(" "));
#endif
		private readonly static FieldDescription _optionCustomPreferredPeakBitRate = new FieldDescription("._preferredPeakBitRate", new GUIContent("Preferred Peak BitRate", "The desired limit of network bandwidth consumption for playback, set to 0 for no preference."));
		private readonly static FieldDescription _optionCustomPreferredPeakBitRateUnits = new FieldDescription("._preferredPeakBitRateUnits", new GUIContent());

		private readonly static FieldDescription _optionMinBufferMs = new FieldDescription(".minBufferMs", new GUIContent("Minimum Buffer Ms"));
		private readonly static FieldDescription _optionMaxBufferMs = new FieldDescription(".maxBufferMs", new GUIContent("Maximum Buffer Ms"));
		private readonly static FieldDescription _optionBufferForPlaybackMs = new FieldDescription(".bufferForPlaybackMs", new GUIContent("Buffer For Playback Ms"));
		private readonly static FieldDescription _optionBufferForPlaybackAfterRebufferMs = new FieldDescription(".bufferForPlaybackAfterRebufferMs", new GUIContent("Buffer For Playback After Rebuffer Ms"));
*/

		private void OnInspectorGUI_Override_OpenHarmony()
		{
			//MediaPlayer media = (this.target) as MediaPlayer;
			//MediaPlayer.OptionsOpenHarmony options = media._optionsOpenHarmony;

			GUILayout.Space(8f);

			string optionsVarName = MediaPlayer.GetPlatformOptionsVariable(Platform.OpenHarmony);

			{
				EditorGUILayout.BeginVertical(GUI.skin.box);

				/*
				DisplayPlatformOption(optionsVarName, _optionVideoAPI);

				{
					SerializedProperty propFileOffset = DisplayPlatformOption(optionsVarName, _optionFileOffset);
					propFileOffset.intValue = Mathf.Max(0, propFileOffset.intValue);
				}
*/

				SerializedProperty propTextureFormat = DisplayPlatformOption(optionsVarName, _optionTextureFormat);

				// Generate mipmaps
				SerializedProperty propGenerateMipmaps = DisplayPlatformOption(optionsVarName, _optionGenerateMipmapsOpenHarmony);

				EditorGUILayout.EndVertical();
			}

/*
			if (_showUltraOptions)
			{
				SerializedProperty httpHeadersProp = serializedObject.FindProperty(optionsVarName + ".httpHeaders.httpHeaders");
				if (httpHeadersProp != null)
				{
					OnInspectorGUI_HttpHeaders(httpHeadersProp);
				}

				SerializedProperty keyAuthProp = serializedObject.FindProperty(optionsVarName + ".keyAuth");
				if (keyAuthProp != null)
				{
					OnInspectorGUI_HlsDecryption(keyAuthProp);
				}
			}
*/

#if false
			// MediaPlayer API options
			{
				EditorGUILayout.BeginVertical(GUI.skin.box);
				GUILayout.Label("MediaPlayer API Options", EditorStyles.boldLabel);

				DisplayPlatformOption(optionsVarName, _optionShowPosterFrames);

				EditorGUILayout.EndVertical();
			}
#endif

/*
			{
				EditorGUILayout.BeginVertical(GUI.skin.box);

				DisplayPlatformOption(optionsVarName, _optionStartMaxBitrate);

				{
					SerializedProperty preferredMaximumResolutionProp = DisplayPlatformOption(optionsVarName, _optionPreferredMaximumResolution);
					if ((MediaPlayer.OptionsAndroid.Resolution)preferredMaximumResolutionProp.intValue == MediaPlayer.OptionsAndroid.Resolution.Custom)
					{
#if UNITY_2017_2_OR_NEWER
						DisplayPlatformOption(optionsVarName, _optionCustomPreferredMaxResolution);
#endif
					}
				}

				{
					EditorGUILayout.BeginHorizontal();
					DisplayPlatformOption(optionsVarName, _optionCustomPreferredPeakBitRate);
					DisplayPlatformOption(optionsVarName, _optionCustomPreferredPeakBitRateUnits);
					EditorGUILayout.EndHorizontal();
				}

				DisplayPlatformOption(optionsVarName, _optionMinBufferMs);
				DisplayPlatformOption(optionsVarName, _optionMaxBufferMs);
				DisplayPlatformOption(optionsVarName, _optionBufferForPlaybackMs);
				DisplayPlatformOption(optionsVarName, _optionBufferForPlaybackAfterRebufferMs);

				EditorGUILayout.EndVertical();
			}
*/
			GUI.enabled = true;

/*
			SerializedProperty propFileOffsetLow = serializedObject.FindProperty(optionsVarName + ".fileOffsetLow");
			SerializedProperty propFileOffsetHigh = serializedObject.FindProperty(optionsVarName + ".fileOffsetHigh");
			if (propFileOffsetLow != null && propFileOffsetHigh != null)
			{
				propFileOffsetLow.intValue = ;

				EditorGUILayout.PropertyField(propFileOFfset);
			}
*/
		}
	}
}