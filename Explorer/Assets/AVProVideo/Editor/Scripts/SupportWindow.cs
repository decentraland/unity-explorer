//-----------------------------------------------------------------------------
// Copyright 2016-2025 RenderHeads Ltd.  All rights reserved.
//-----------------------------------------------------------------------------

using UnityEngine;
using UnityEditor;
using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.Compilation;

namespace RenderHeads.Media.AVProVideo.Editor
{
	/// <summary>
	/// A window to display options to the user to help them report bugs
	/// Also collects some metadata about the machine specs, plugin version etc
	/// </summary>
	public class SupportWindow : EditorWindow
	{
		private class MyPopupWindow : PopupWindowContent
		{
			private string _text;
			private string _url;
			private string _buttonMessage;

			public MyPopupWindow(string text, string buttonMessage,string url)
			{
				_text = text;
				_url = url;
				_buttonMessage = buttonMessage;
			}

			public override Vector2 GetWindowSize()
			{
				return new Vector2(400, 520);
			}

			public override void OnGUI(Rect rect)
			{
				GUILayout.BeginHorizontal();
				GUILayout.Label("Copy-Paste this text, then ", EditorStyles.boldLabel);
				GUI.color = Color.green;
				if (GUILayout.Button(_buttonMessage, GUILayout.ExpandWidth(true)))
				{
					Application.OpenURL(_url);
				}
				GUILayout.EndHorizontal();
				GUI.color = Color.white;
				EditorGUILayout.TextArea(_text);
			}
		}

		private static bool _isCreated = false;
		private static bool _isInit = false;

		private int _selectionIndex = 0;
		private static string[] _gridNames = { "Help Resources", "Update v2.x to v3.x" };

		[MenuItem("Window/AVPro Video Support")]
		public static void Init()
		{
			// Close window if it is already open
			if (_isInit || _isCreated)
			{
				SupportWindow window = (SupportWindow)EditorWindow.GetWindow(typeof(SupportWindow));
				window.Close();
				return;
			}

			_isCreated = true;

			// Get existing open window or if none, make a new one:
			SupportWindow window2 = ScriptableObject.CreateInstance<SupportWindow>();
			if (window2 != null)
			{
				window2.SetupWindow();
			}
		}

		private void SetupWindow()
		{
			_isCreated = true;
			float width = 512f;
			float height = 512f;
			this.position = new Rect((Screen.width / 2) - (width / 2f), (Screen.height / 2) - (height / 2f), width, height);
			this.minSize = new Vector2(530f, 510f);
			this.titleContent = new GUIContent("AVPro Video - Help & Support");
			this.CreateGUI();
			this.ShowUtility();
			this.Repaint();
		}

		private void CreateGUI()
		{
			_isInit = true;
		}

		void OnEnable()
		{
			if (!_isCreated)
			{
				SetupWindow();
			}
		}

		void OnDisable()
		{
			_isInit = false;
			_isCreated = false;
			Repaint();
		}

		void OnGUI()
		{
			if (!_isInit)
			{
				EditorGUILayout.LabelField("Initialising...");
				return;
			}

			GUILayout.Label("Having problems? We'll do our best to help.\n\nBelow is a collection of resources to help solve any issues you may encounter.", EditorStyles.wordWrappedLabel);
			GUILayout.Space(16f);

			/*GUI.color = Color.white;
			GUI.backgroundColor = Color.clear;
			if (_trySelfSolve)
			{
				GUI.color = Color.white;
				GUI.backgroundColor = new Color(0.8f, 0.8f, 0.8f, 0.1f);
				if (EditorGUIUtility.isProSkin)
				{
					GUI.backgroundColor = Color.black;
				}
			}
			GUILayout.BeginVertical("box");
			GUI.backgroundColor = Color.white;*/

			_selectionIndex = GUILayout.Toolbar(_selectionIndex, _gridNames);

			GUILayout.Space(16f);
			/*if (GUILayout.Button("Try Solve the Issue Yourself", EditorStyles.toolbarButton))
			{
				//_trySelfSolve = !_trySelfSolve;
				_trySelfSolve = true;
			}
			GUI.color = Color.white;
			if (_trySelfSolve)*/
			if (_selectionIndex == 0)
			{
				GUILayout.BeginHorizontal();
				GUILayout.Label("1) ");
				GUILayout.Label("Check you're using the latest version of AVPro Video via the Asset Store.  This is version " + Helper.AVProVideoVersion, EditorStyles.wordWrappedLabel);
				GUILayout.FlexibleSpace();
				GUILayout.EndHorizontal();

				GUILayout.BeginHorizontal();
				GUILayout.Label("2) ");
				GUILayout.Label("Look at the example projects and scripts in the Demos folder");
				GUILayout.FlexibleSpace();
				GUILayout.EndHorizontal();

				GUILayout.BeginHorizontal();
				GUILayout.Label("3) ");
				GUI.color = Color.green;
				if (GUILayout.Button("Read the Documentation", GUILayout.ExpandWidth(false)))
				{
					Application.OpenURL(MediaPlayerEditor.LinkUserManual);
				}
				GUI.color = Color.white;
				GUILayout.FlexibleSpace();
				GUILayout.EndHorizontal();

				GUILayout.BeginHorizontal();
				GUILayout.Label("4) ");
				GUI.color = Color.green;
				if (GUILayout.Button("Read the GitHub Issues", GUILayout.ExpandWidth(false)))
				{
					Application.OpenURL(MediaPlayerEditor.LinkGithubIssues);
				}
				GUI.color = Color.white;
				GUILayout.FlexibleSpace();
				GUILayout.EndHorizontal();

				GUILayout.BeginHorizontal();
				GUILayout.Label("5) ");
				GUI.color = Color.green;
				if (GUILayout.Button("Read the Scripting Reference", GUILayout.ExpandWidth(false)))
				{
					Application.OpenURL(MediaPlayerEditor.LinkScriptingClassReference);
				}
				GUI.color = Color.white;
				GUILayout.FlexibleSpace();
				GUILayout.EndHorizontal();

				GUILayout.BeginHorizontal();
				GUILayout.Label("6) ");
				GUI.color = Color.green;
				if (GUILayout.Button("Visit the AVPro Video Website", GUILayout.ExpandWidth(false)))
				{
					Application.OpenURL(MediaPlayerEditor.LinkPluginWebsite);
				}
				GUI.color = Color.white;
				GUILayout.FlexibleSpace();
				GUILayout.EndHorizontal();

				GUILayout.BeginHorizontal();
				GUILayout.Label("7) ");
				GUI.color = Color.green;
				if (GUILayout.Button("Browse the Unity Forum", GUILayout.ExpandWidth(false)))
				{
					Application.OpenURL(MediaPlayerEditor.LinkForumPage);
				}
				GUI.color = Color.white;
				GUILayout.FlexibleSpace();
				GUILayout.EndHorizontal();
			}
			else if (_selectionIndex == 1)
			{
				GUILayout.Label("There are a number of files/folders that need to be removed going from AVPro Video version 2.x to AVPro Video v3.x in order for v3.x to build and run correctly.\n\nIn order to complete a smooth upgrade a project using AVPro Video v2.x to v3.x please follow the following steps:", EditorStyles.wordWrappedLabel);
				GUILayout.Space(16f);

				GUILayout.BeginHorizontal();
				GUILayout.Label("1) Import the latest AVPro Video v3.x asset bundle into a project that already contains AVPro Video v2.x", EditorStyles.wordWrappedLabel);
				GUILayout.FlexibleSpace();
				GUILayout.EndHorizontal();

				GUILayout.BeginHorizontal();
				GUILayout.Label("2) Click the update button");
				if (GUILayout.Button("Update", GUILayout.ExpandWidth(true)))
				{
					List<SFileToDelete> aFilesToDelete = new List<SFileToDelete>();

					aFilesToDelete.Add( new SFileToDelete( "Assets/AVProVideo/Runtime/Plugins/Android/exoplayer.aar", "d04cd71ba09f0a548ac774e50236a6f7", false) );
					aFilesToDelete.Add( new SFileToDelete( "Assets/AVProVideo/Runtime/Plugins/Android/exoplayer-common.aar", "782210c1836944347b3b8315635ef044", false) );
					aFilesToDelete.Add( new SFileToDelete( "Assets/AVProVideo/Runtime/Plugins/Android/exoplayer-container.aar", "2232bec870b56e04aa0107d97204456e", false) );
					aFilesToDelete.Add( new SFileToDelete( "Assets/AVProVideo/Runtime/Plugins/Android/exoplayer-core.aar", "782210c1836944347b3b8315658ef044", false) );
					aFilesToDelete.Add( new SFileToDelete( "Assets/AVProVideo/Runtime/Plugins/Android/exoplayer-dash.aar", "d06cd71ba09f0a548ac774e50236a6f7", false) );
					aFilesToDelete.Add( new SFileToDelete( "Assets/AVProVideo/Runtime/Plugins/Android/exoplayer-database.aar", "a35ee71df09a0a348ac774e75237a6a1", false) );
					aFilesToDelete.Add( new SFileToDelete( "Assets/AVProVideo/Runtime/Plugins/Android/exoplayer-datasource.aar", "d06cd71df09a0a348ac774e75237a6a1", false) );
					aFilesToDelete.Add( new SFileToDelete( "Assets/AVProVideo/Runtime/Plugins/Android/exoplayer-decoder.aar", "d06cd71ba09f0a548ac774e75236a6a1", false) );
					aFilesToDelete.Add( new SFileToDelete( "Assets/AVProVideo/Runtime/Plugins/Android/exoplayer-extractor.aar", "782210c2926744347b3b8315658ef044", false) );
					aFilesToDelete.Add( new SFileToDelete( "Assets/AVProVideo/Runtime/Plugins/Android/exoplayer-hls.aar", "d07cd71ba09f0a548ac774e50236a6f7", false) );
					aFilesToDelete.Add( new SFileToDelete( "Assets/AVProVideo/Runtime/Plugins/Android/exoplayer-rtsp.aar", "782210a1816945347b3b8315658ef052", false) );
					aFilesToDelete.Add( new SFileToDelete( "Assets/AVProVideo/Runtime/Plugins/Android/exoplayer-smoothstreaming.aar", "d08cd71ba09f0a548ac774e50236a6f7", false) );
					aFilesToDelete.Add( new SFileToDelete( "Assets/AVProVideo/Runtime/Plugins/Android/extension-rtmp.aar", "782210c1836944347b3b8315658ef041", false) );
					aFilesToDelete.Add( new SFileToDelete( "Assets/AVProVideo/Runtime/Plugins/Android/rtmp-client-3.2.0.aar", "282210c1236912347b4b8315658af638", false) );
					aFilesToDelete.Add( new SFileToDelete( "Assets/AVProVideo/Runtime/Plugins/Android/guava-31.1-android.jar", "986510c1836944347b3ba313758af501", false) );
					//
					aFilesToDelete.Add( new SFileToDelete( "Assets/AVProVideo/Runtime/Plugins/iOS/AVProVideo.framework", "2a1facf97326449499b63c03811b1ab2", true) );
					aFilesToDelete.Add( new SFileToDelete( "Assets/AVProVideo/Runtime/Plugins/iOS/AVProVideoBootstrap.m", "4df32662530a57c4f83b79e6313690dc", false) );
					//
					aFilesToDelete.Add( new SFileToDelete( "Assets/AVProVideo/Runtime/Plugins/tvOS/AVProVideo.framework", "bcf659e3a94d748d6a100d5531540d1a", true) );
					aFilesToDelete.Add( new SFileToDelete( "Assets/AVProVideo/Runtime/Plugins/tvOS/AVProVideoBootstrap.m", "154f23675acd6c54e8667de25ac31b67", false) );
					//
					aFilesToDelete.Add( new SFileToDelete( "Assets/AVProVideo/Runtime/Scripts/Internal/Players/AndroidMediaPlayer.cs", "80eb525dd677aa440823910b09b23ae0", false) );
					aFilesToDelete.Add( new SFileToDelete( "Assets/AVProVideo/Runtime/Scripts/Internal/Players/AppleMediaPlayer.cs", "3f68628a1ef6349648e502d1c66b5114", false) );
					aFilesToDelete.Add( new SFileToDelete( "Assets/AVProVideo/Runtime/Scripts/Internal/Players/AppleMediaPlayer+Native.cs", "0bf374b5848b649e6b3840fe1dc03cd2", false) );
					aFilesToDelete.Add( new SFileToDelete( "Assets/AVProVideo/Runtime/Scripts/Internal/Players/AppleMediaPlayerExtensions.cs", "e27ea5523e11f44c09e8d368eb1f2983", false) );
//					aFilesToDelete.Add( new SFileToDelete( "Assets/AVProVideo/Runtime/Scripts/Internal/Players/WindowsMediaPlayer_BufferedFrames.cs", "2b36cc2d6962ce34e86c5a83a0de6d4a", false) );

					int iNumberFilesDeleted = DeleteFiles_V2_To_V3(aFilesToDelete, new[] { ".aar", ".jar", ".m", ".cs" } );

					EditorUtility.DisplayDialog("Complete", "Update from AVPro Video v2.x to v3.x is complete.\n\n" + iNumberFilesDeleted + " files/folders were removed in the process", "ok");

					AssetDatabase.Refresh();
					CompilationPipeline.RequestScriptCompilation();
				}
				GUI.color = Color.white;
				GUILayout.FlexibleSpace();
				GUILayout.EndHorizontal();
			}
			//GUILayout.EndVertical();

			GUILayout.FlexibleSpace();

			if (GUILayout.Button("Close"))
			{
				this.Close();
			}
		}

		private class SFileToDelete
		{
			public SFileToDelete( string filename, string guid, bool bDirectory )
			{
				m_Filename = filename;
				m_guid = guid;
				m_FullPath = null;
				m_bIsDirectory = bDirectory;
			}

			public string	m_Filename;
			public string	m_guid;
			public string	m_FullPath;
			public bool		m_bIsDirectory;
		};

		private int DeleteFiles_V2_To_V3( List<SFileToDelete> aFilesToDelete, string[] allowedExtensions )
		{
			int iNumRemoved = 0;

			try
			{
				// Folders first
				IEnumerable<string> aAllFoders = Directory.GetDirectories( Application.dataPath, "*", SearchOption.AllDirectories );
				foreach( string directoryPath in aAllFoders )
				{
					Uri relativeDirectory = (new Uri(Application.dataPath)).MakeRelativeUri(new Uri(directoryPath));
					UnityEngine.Object asssetObject = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>( relativeDirectory.ToString() );
					if(asssetObject)
					{
						string guid;
						long file;
						if( AssetDatabase.TryGetGUIDAndLocalFileIdentifier( asssetObject, out guid, out file ) )
						{
							// Is this a file we want to delete?
							foreach( SFileToDelete sFileToDelete in aFilesToDelete )
							{
								if( !string.IsNullOrEmpty( sFileToDelete.m_guid ) &&
									sFileToDelete.m_bIsDirectory &&
									sFileToDelete.m_guid.Equals( guid ) )
								{
									// A hit, delete
									Directory.Delete( directoryPath, true );
									File.Delete( directoryPath + ".meta" );

									iNumRemoved += 2;
								}
							}
						}
					}
				}

				// Files
				IEnumerable<string> aAllFiles = Directory.GetFiles( Application.dataPath, "*.*", SearchOption.AllDirectories ).Where(file => allowedExtensions.Any(file.ToLower().EndsWith));
				foreach( string filePath in aAllFiles )
				{
					Uri relativeFilename = (new Uri(Application.dataPath)).MakeRelativeUri(new Uri(filePath));
					UnityEngine.Object assetObject = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>( relativeFilename.ToString() );
					if(assetObject)
					{
						string guid;
						long file;
						if( AssetDatabase.TryGetGUIDAndLocalFileIdentifier( assetObject, out guid, out file ) )
						{
							// Is this a file we want to delete?
							foreach( SFileToDelete sFileToDelete in aFilesToDelete )
							{
								if( !string.IsNullOrEmpty( sFileToDelete.m_guid ) &&
									!sFileToDelete.m_bIsDirectory &&
									sFileToDelete.m_guid.Equals( guid ) )
								{
									// A hit, delete
									File.Delete( filePath );
									File.Delete( filePath + ".meta" );

									iNumRemoved += 2;
								}
							}
						}
					}
				}
			}
			catch (UnauthorizedAccessException UAEx)
			{
				Console.WriteLine(UAEx.Message);
			}
			catch (PathTooLongException PathEx)
			{
				Console.WriteLine(PathEx.Message);
			}

			return iNumRemoved;
		}


		private Rect buttonRect;
	}
}