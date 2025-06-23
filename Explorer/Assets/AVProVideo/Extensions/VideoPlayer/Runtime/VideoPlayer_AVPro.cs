using UnityEditor;
using UnityEngine;
using UnityEngine.Video;

//-----------------------------------------------------------------------------
// Copyright 2015-2024 RenderHeads Ltd.  All rights reserved.
//-----------------------------------------------------------------------------

namespace RenderHeads.Media.AVProVideo
{
	/// <summary>
	/// The different display types that are available within AVPro
	/// </summary>
	public enum DisplayType
	{
		Mesh,
		Material,
		uGUI,
		IMGUI,
		CameraFarPlane,
		RenderTexture,
		None
	}


	public class VideoPlayer_AVPro : MediaPlayer
	{
		#region Static Properties
		/// <summary>
		/// Maximum number of audio tracks that can be controlled. (Read Only)
		/// </summary>
		public readonly static ushort controlledAudioTrackMaxCount = 64; // currently no implementation within MediaPlayer
		#endregion Static Properties

		#region UI Properties
		// private properties that are utilized by the editor script to display the correct information
		[SerializeField] private MediaSource Source;
		[SerializeField] private MediaReference Clip;
		[SerializeField] private string Url;
		private MediaPath path;
		[SerializeField] private bool PlayOnAwake;
		[SerializeField] private bool AutoOpening = false; 
		[SerializeField] private bool IsLooping;
		[SerializeField] private float PlaybackSpeed = 1;
		public MonoBehaviour _renderModeComponent = null;
		public DisplayIMGUI displayIMGUI;
		public DisplayUGUI displayUGUI;
		public ApplyToMesh applyToMesh;
		public ApplyToMaterial applyToMaterial;
		public ApplyToFarPlane applyToFarPlane;
		public ResolveToRenderTexture applyToTexture;
		public DisplayType currentRenderMode;
		[SerializeField] private Renderer TargetMaterialRenderer;
		[SerializeField] private string TargetMateralProperty;
		[SerializeField] private DisplayType RenderMode;
		[SerializeField] private Material TargetMaterial; 
		[SerializeField] private Color Colour = Color.white; 
		[SerializeField] private ScaleMode AspectRatio;
		[SerializeField] private VideoResolveOptions.AspectRatio AspectRatioRenderTexture; 
		[SerializeField] private bool Fullscreen = true;

#pragma warning disable
		[SerializeField] private float TargetCameraAlpha = 1.0f;
#pragma warning restore

#if UNITY_STANDALONE_WIN
		[SerializeField] private Windows.AudioOutput AudioOutputMode;
#elif UNITY_WSA_10_0
		[SerializeField] public WindowsUWP.AudioOutput AudioOutputMode;
#elif UNITY_ANDROID || UNITY_IOS || UNITY_STANDALONE_OSX || UNITY_TVOS || UNITY_VISIONOS || UNITY_WEBGL || UNITY_OPENHARMONY
		[SerializeField] public PlatformOptions.AudioMode AudioOutputMode;
#endif
		[SerializeField] private ushort ControlledAudioTrackCount; // currently no implementation within MediaPlayer
		[SerializeField] private float Volume = 1f; 
		[SerializeField] private bool Muted; 
		[SerializeField] private AudioSource AudioSourceE;
		[SerializeField] private RenderTexture TargetTexture;
		[SerializeField] private Rect UVRect = new Rect(0f, 0f, 1f, 1f);
		public GameObject canvasObj;
		public bool _converted = false;
		#endregion UI Properties

		#region Accessible Properties
		// accessible via script
		/// <summary>
		/// The source that the <see cref="VideoPlayer_AVPro"/> uses for playback.
		/// <para>AVPro:</para>
		/// <list type="bullet">
		///  <item> converts from Unity's <see cref="VideoSource"/> to AVPro's <see cref="MediaSource"/> </item>
		///  <item> Stored within <see cref="sourceAVPro"/> </item>
		///  </list>
		/// </summary>
		public VideoSource source
		{
			get
			{
				if (sourceAVPro == MediaSource.Reference)
					return VideoSource.VideoClip;
				return VideoSource.Url;
			}
			set
			{
				if (value == VideoSource.VideoClip)
					sourceAVPro = MediaSource.Reference;
				else
					sourceAVPro = MediaSource.Path;
			}
		}
		/// <summary>
		/// The source that the <see cref="VideoPlayer_AVPro"/> will use for playback
		/// </summary>
		public MediaSource sourceAVPro
		{
			get { return Source; }
			set { Source = value; SetMediaSource(value); }
		}
		/// <summary>
		/// The clip being played by the <see cref="VideoPlayer_AVPro"/>.
		/// </summary>
		public MediaReference clip { get { return Clip; } set { SetMediaReference(value); Clip = value; } }
		/// <summary>
		/// The file or HTTP URL that the <see cref="VideoPlayer_AVPro"/> reads content from.
		/// <para>AVPro - utilizes <see cref="MediaPath"/> to store</para>
		/// <para>Will automatically set the path type of the <see cref="sourceAVPro"/></para>
		/// </summary>
		public MediaPath url
		{
			get { return path; }
			set
			{
				if (value.PathType == MediaPathType.AbsolutePathOrURL)
				{
					Source = MediaSource.Path;
					SetMediaSource(MediaSource.Path);
				}
				path = value;
				Url = value.Path;
				SetMediaPath(value);
			}
		}
		/// <summary>
		/// Whether the content will start playing back as soon as the component awakes.
		/// </summary>
		public bool playOnAwake { get { return PlayOnAwake; } set { PlayOnAwake = value; AutoStart = value; } }
		/// <summary>
		/// Determines whether the <see cref="VideoPlayer_AVPro"/> restarts from the beginning when it reaches the end of the clip.
		/// </summary>
		public bool isLooping { get { return IsLooping; } set { IsLooping = value; Loop = value; } }
		/// <summary>
		/// Factor by which the basic playback rate will be multiplied.
		/// </summary>
		public float playbackSpeed
		{
			get { return PlaybackSpeed; }
			set
			{
				if (!canSetPlaybackSpeed)
					return;
				PlaybackSpeed = value; PlaybackRate = value;
			}
		}
		/// <summary>
		/// <see cref="Renderer"/> which is targeted when <see cref="rendererMode"/> is set to <see cref="DisplayType.Material"/>
		/// </summary>
		public Renderer targetMaterialRenderer { get { return TargetMaterialRenderer; } set { TargetMaterialRenderer = value; } }
		/// <summary>
		/// <see cref="Material"/> texture property which is targeted when <see cref="rendererMode"/> is set to:
		/// <para>
		/// <see cref="DisplayType.Mesh"/>,
		/// <see cref="DisplayType.Material"/>
		/// </para>
		/// </summary>
		public string targetMateralProperty { get { return TargetMateralProperty; } set { TargetMateralProperty = value; } }
		/// <summary>
		/// <see cref="Material"/> that is targeted when <see cref="rendererMode"/> is set to <see cref="DisplayType.Material"/>
		/// </summary>
		public Material targetMaterial
		{
			get { return TargetMaterial; }
			set
			{
				TargetMaterial = value;
				if (displayUGUI)
					displayUGUI.material = value;
				if (applyToMaterial)
					applyToMaterial.Material = value;
			}
		}
		/// <summary>
		/// Where/how the video content will be drawn.
		/// <para>Recomended to convert to using <see cref="DisplayType"/> with <see cref="rendererMode"/> otherwise unexpected behaviour may occur</para>
		/// <para>AVPro:</para>
		/// <list type="bullet">
		///  <item> converts from Unity's <see cref="VideoRenderMode"/> to AVPro's <see cref="DisplayType"/> </item>
		///  <item> Stored within <see cref="rendererMode"/> </item>
		///  </list>
		/// </summary>
		public VideoRenderMode renderMode
		{
			get
			{
				LogAutomaticConversion("DisplayType", "VideoRenderMode");
				switch (rendererMode)
				{
					case DisplayType.IMGUI:
						return VideoRenderMode.CameraNearPlane;
					case DisplayType.uGUI:
					case DisplayType.RenderTexture:
						return VideoRenderMode.RenderTexture;
					case DisplayType.Material:
						return VideoRenderMode.MaterialOverride;
					case DisplayType.Mesh:
						return VideoRenderMode.APIOnly;
					case DisplayType.CameraFarPlane:
						return VideoRenderMode.CameraFarPlane;
					default:
						return VideoRenderMode.CameraNearPlane;
				}
			}
			set
			{
				LogAutomaticConversion("VideoRenderMode", "DisplayType");
				switch (value)
				{
					case UnityEngine.Video.VideoRenderMode.CameraFarPlane:
						rendererMode = DisplayType.CameraFarPlane;
						break;
					case UnityEngine.Video.VideoRenderMode.CameraNearPlane:
						rendererMode = DisplayType.IMGUI;
						break;
					case UnityEngine.Video.VideoRenderMode.RenderTexture:
						rendererMode = DisplayType.RenderTexture;
						break;
					case UnityEngine.Video.VideoRenderMode.MaterialOverride:
						rendererMode = DisplayType.Material;
						break;
					case UnityEngine.Video.VideoRenderMode.APIOnly:
						rendererMode = DisplayType.None;
						break;
				}
			}
		}
		/// <summary>
		/// Where/how the video content will be drawn.
		/// <para>will create the desired components when renderer mode is changed</para>
		/// </summary>
		public DisplayType rendererMode
		{
			get { return RenderMode; }
			set
			{
				RenderMode = value;
				// need to check for change and then create components
				if (currentRenderMode != rendererMode)
				{
					currentRenderMode = rendererMode;
					CreateRendererComponents();
				}
			}
		}
		/// <summary>
		/// Defines how the video content will be stretched to fill the target area.
		/// </summary>
		public ScaleMode aspectRatio
		{
			get { return AspectRatio; }
			set
			{
				AspectRatio = value;
				if (rendererMode == DisplayType.uGUI)
					displayUGUI.ScaleMode = value;
				if (rendererMode == DisplayType.IMGUI)
					displayIMGUI.ScaleMode = value;
				else
					return;
			}
		}
		/// <summary>
		/// used specifically for <see cref="DisplayType.RenderTexture"/> & <see cref="DisplayType.CameraFarPlane"/> otherwise the same as <see cref="aspectRatio"/> 
		/// </summary>
		public VideoResolveOptions.AspectRatio aspectRatioRenderTexture
		{
			get
			{
				return AspectRatioRenderTexture;
			}
			set
			{
				AspectRatioRenderTexture = value;
				if (rendererMode == DisplayType.RenderTexture)
				{
					// :(
					var options = applyToTexture.VideoResolveOptions;
					options.aspectRatio = value;
					applyToTexture.VideoResolveOptions = options;
				}
				if (applyToFarPlane)
					applyToFarPlane.VideoAspectRatio = (VideoAspectRatio)(int)value;
			}
		}



		/// <summary>
		/// Destination for the audio embedded in the video.
		/// </summary>
#if UNITY_STANDALONE_WIN
		public VideoAudioOutputMode audioOutputMode
		{
			get
			{
				switch (audioOutputModeAVPro)
				{
					case Windows.AudioOutput.None:
						return UnityEngine.Video.VideoAudioOutputMode.None;
					case Windows.AudioOutput.Unity:
						return UnityEngine.Video.VideoAudioOutputMode.AudioSource;
					case Windows.AudioOutput.System:
					case Windows.AudioOutput.FacebookAudio360:
						return UnityEngine.Video.VideoAudioOutputMode.Direct;
				}
				return UnityEngine.Video.VideoAudioOutputMode.APIOnly;
			}
			set
			{
				switch (value)
				{
					case UnityEngine.Video.VideoAudioOutputMode.None:
						audioOutputModeAVPro = Windows.AudioOutput.None;
						break;
					case UnityEngine.Video.VideoAudioOutputMode.AudioSource:
						audioOutputModeAVPro = Windows.AudioOutput.Unity;
						break;
					case UnityEngine.Video.VideoAudioOutputMode.Direct:
					case UnityEngine.Video.VideoAudioOutputMode.APIOnly:
						audioOutputModeAVPro = Windows.AudioOutput.System;
						break;
				}
			}
		}
#elif UNITY_WSA_10_0
		public VideoAudioOutputMode audioOutputMode
		{
			get
			{
				switch (audioOutputModeAVPro)
				{
					case WindowsUWP.AudioOutput.Unity:
						return UnityEngine.Video.VideoAudioOutputMode.AudioSource;
					case WindowsUWP.AudioOutput.System:
					case WindowsUWP.AudioOutput.FacebookAudio360:
						return UnityEngine.Video.VideoAudioOutputMode.Direct;
				}
				return UnityEngine.Video.VideoAudioOutputMode.Direct;
			}
			set
			{
				switch (value)
				{
					case UnityEngine.Video.VideoAudioOutputMode.None:
						audioOutputModeAVPro = WindowsUWP.AudioOutput.None;
						break;
					case UnityEngine.Video.VideoAudioOutputMode.AudioSource:
						audioOutputModeAVPro = WindowsUWP.AudioOutput.Unity;
						break;
					case UnityEngine.Video.VideoAudioOutputMode.Direct:
					case UnityEngine.Video.VideoAudioOutputMode.APIOnly:
						audioOutputModeAVPro = WindowsUWP.AudioOutput.System;
						break;
				}
			}
		}
#elif UNITY_ANDROID || UNITY_WEBGL || UNITY_IOS || UNITY_STANDALONE_OSX || UNITY_TVOS || UNITY_VISIONOS
		public VideoAudioOutputMode audioOutputMode
		{
			get
			{
				switch (audioOutputModeAVPro)
				{
					case PlatformOptions.AudioMode.Unity:
						return UnityEngine.Video.VideoAudioOutputMode.AudioSource;
					case PlatformOptions.AudioMode.SystemDirect:
					case PlatformOptions.AudioMode.FacebookAudio360:
						return UnityEngine.Video.VideoAudioOutputMode.Direct;
				}
				return UnityEngine.Video.VideoAudioOutputMode.Direct;
			}
			set
			{
				switch (value)
				{
					case UnityEngine.Video.VideoAudioOutputMode.None:
						audioOutputModeAVPro = PlatformOptions.AudioMode.SystemDirect;
						break;
					case UnityEngine.Video.VideoAudioOutputMode.AudioSource:
						audioOutputModeAVPro = PlatformOptions.AudioMode.Unity;
						break;
					case UnityEngine.Video.VideoAudioOutputMode.Direct:
					case UnityEngine.Video.VideoAudioOutputMode.APIOnly:
						audioOutputModeAVPro = PlatformOptions.AudioMode.SystemDirect;
						break;
				}
			}
		}
#endif
		/// <summary>
		/// Destination for the audio embedded in the video.
		/// </summary>
#if UNITY_STANDALONE_WIN
		public Windows.AudioOutput audioOutputModeAVPro
		{
			get { return AudioOutputMode; }
			set { AudioOutputMode = value; PlatformOptionsWindows._audioMode = value; CreateAudioComponents(); }
		}
#elif UNITY_WSA_10_0
		public WindowsUWP.AudioOutput audioOutputModeAVPro
		{
			get { return AudioOutputMode; }
			set { AudioOutputMode = value; PlatformOptionsWindowsUWP._audioMode = value; CreateAudioComponents(); }
		}
#else
		public PlatformOptions.AudioMode audioOutputModeAVPro
		{
			get { return AudioOutputMode; }
#if UNITY_ANDROID
			set { AudioOutputMode = value; PlatformOptionsAndroid.audioMode = value; CreateAudioComponents(); }
#elif UNITY_WEBGL
			set { AudioOutputMode = value; PlatformOptionsWebGL.audioMode = value; CreateAudioComponents(); }
#elif UNITY_IOS
			set { AudioOutputMode = value; PlatformOptions_iOS.audioMode = value; CreateAudioComponents(); }
#elif UNITY_STANDALONE_OSX
			set { AudioOutputMode = value; PlatformOptions_macOS.audioMode = value; CreateAudioComponents(); }
#elif UNITY_TVOS
			set { AudioOutputMode = value; PlatformOptions_tvOS.audioMode = value; CreateAudioComponents(); }
#elif UNITY_VISIONOS
			set { AudioOutputMode = value; PlatformOptions_visionOS.audioMode = value; CreateAudioComponents(); }
#endif
		}
#endif

		/// <summary>
		/// <see cref="Color"/> property which is targeted when <see cref="rendererMode"/> is set to:
		/// <para>
		/// <see cref="DisplayType.IMGUI"/>,
		/// <see cref="DisplayType.uGUI"/>,
		/// <see cref="DisplayType.RenderTexture"/>
		/// </para>
		/// </summary>
		public Color color
		{
			get { return Colour; }
			set
			{
				Colour = value;
				if (displayIMGUI)
					displayIMGUI.Color = value;
				else if (applyToTexture)
				{
					var options = applyToTexture.VideoResolveOptions;
					options.tint = value;
					applyToTexture.VideoResolveOptions = options;
				}
				else if (displayUGUI)
					displayUGUI.color = value;
				else if (applyToFarPlane)
					applyToFarPlane.MainColor = value;
			}
		}
		/// <summary>
		/// used to toggle fullscreen when <see cref="rendererMode"/> is set to:
		/// <see cref="DisplayType.IMGUI"/>
		/// </summary>
		public bool fullScreen
		{
			get { return Fullscreen; }
			set
			{
				Fullscreen = value;
				if (displayIMGUI)
					displayIMGUI.IsAreaFullScreen = value;
			}
		}
		/// <summary>
		/// Used to change the volume of the audio
		/// </summary>
		public float volume
		{
			get 
			{
				if (GetDirectAudioVolume(0) != Volume)
					Volume = GetDirectAudioVolume(0);
				return Volume; 
			}
			set
			{
				Volume = value;
				
#if UNITY_STANDALONE_WIN
				if (audioOutputModeAVPro == Windows.AudioOutput.Unity && AudioSource)
#elif UNITY_WSA_10_0
				if (audioOutputModeAVPro == WindowsUWP.AudioOutput.Unity && AudioSource)
#elif UNITY_ANDROID || UNITY_IOS || UNITY_STANDALONE_OSX || UNITY_TVOS || UNITY_VISIONOS
				if (audioOutputModeAVPro == PlatformOptions.AudioMode.Unity && AudioSource)
#endif
				{
					AudioSource.volume = value;
				}
				SetDirectAudioVolume(0, value);
			}
		}
		/// <summary>
		/// used to toggle weather or not the audio is muted
		/// </summary>
		public bool muted
		{
			get { return Muted; }
			set
			{
				Muted = value;
				AudioMuted = value;
#if UNITY_STANDALONE_WIN
				if (audioOutputModeAVPro == Windows.AudioOutput.Unity && AudioSource)
					AudioSource.mute = value;
#elif UNITY_WSA_10_0
				if (audioOutputModeAVPro == WindowsUWP.AudioOutput.Unity && AudioSource)
					AudioSource.mute = value;
#elif UNITY_ANDROID || UNITY_IOS || UNITY_STANDALONE_OSX || UNITY_TVOS || UNITY_VISIONOS
				if (audioOutputModeAVPro == PlatformOptions.AudioMode.Unity && AudioSource)
					AudioSource.mute = value;
#endif
			}
		}
		/// <summary>
		/// The <see cref="AudioSource"/> to use when <see cref="audioOutputModeAVPro"/> is set to <see cref="Windows.AudioOutput.Unity"/>
		/// </summary>
		public AudioSource audioSource { get { return AudioSourceE; } set { AudioSourceE = value; SetAudioSource(value); } }

		/// <summary>
		/// The <see cref="Rect"/> to use to control texture scaling/offset when <see cref="rendererMode"/> is set to <see cref="DisplayType.uGUI"/>
		/// </summary>
		public Rect uvRect
		{
			get { return UVRect; }
			set 
			{ 
				UVRect = value;
				if (rendererMode == DisplayType.uGUI && displayUGUI)
					displayUGUI.uvRect = uvRect;
			}
		}


		// General Parameters
		/// <summary>
		/// Number of audio tracks found in the data source currently configured. (Read Only)
		/// </summary>
		/// <returns>0 if not prepared; otherwise the number of audio tracks on the current media</returns>
		public ushort audioTrackCount { get { if (AudioTracks != null )return (ushort)AudioTracks.GetAudioTracks().Count; return 0; } private set { } }
		/// <summary>
		/// The clock time that the VideoPlayer follows to schedule its samples. The clock time is expressed in seconds. (Read Only)
		/// </summary>
		/// <returns>-1 if not initilised; otherwise the current clock time</returns>
		public double clockTime
		{
			get
			{
				if (UseResampler && FrameResampler != null) // may not be used as FrameResampler is optional
					return (double)FrameResampler.ElapsedTimeSinceBase;
				else if (Info != null)
				{
					// determin the total frames based on the fact that we know the missed frames, and we know our perfect percent
					// so turn other into a percent, calc the difference the work backwords to calculate total frames
					// from their use the framerate of the video to determine total time spent
					var qualStats = Info.GetPlaybackQualityStats();
					var totalOtherFrames = qualStats.DuplicateFrames + qualStats.UnityDroppedFrames + qualStats.SkippedFrames;
					var totalOtherPercent = 1.0f - qualStats.PerfectFramesT;
					var totalFrames = (totalOtherFrames / totalOtherPercent) + totalOtherFrames;
					var time = totalFrames * (long)(Helper.SecondsToHNS / Info.GetVideoFrameRate()); // total * time per frame
					return time;
				}
				return -1;
			}
			private set { }
		}
		/// <summary>
		/// Reference time of the external clock the VideoPlayer uses to correct its drift.
		/// <para>AVPro - this returns <see cref="BaseMediaPlayer.GetCurrentTime"/> which is not the same functionality as VideoPlayer</para>
		/// </summary>
		public double externalReferenceTime { get { if (_baseMediaPlayer != null) return _baseMediaPlayer.GetCurrentTime(); return -1; } private set { } }
		/// <summary>
		/// Returns the current video time in frames
		/// </summary>
		public long frame { get { if (Control != null) return (long)Control.GetCurrentTimeFrames(); return -1; } set { if (canSetTime && Control != null) Control.SeekToFrame((int)value); } }
		/// <summary>
		/// Number of frames in the current video content. (Read Only)
		/// </summary>
		public ulong frameCount { get { if (Info != null) return (ulong)Info.GetMaxFrameNumber(); return 0; } private set { } }
		/// <summary>
		/// he frame rate of the clip or URL in frames/second. (Read Only)
		/// </summary>
		public float frameRate { get { if (Info != null) return Info.GetVideoFrameRate(); return -1; } private set { } }
		/// <summary>
		/// The height of the images in the VideoClip, or URL, in pixels. (Read Only)
		/// </summary>
		public uint height { get { if (Info != null) return (uint)Info.GetVideoHeight(); return 0; } private set { } }
		/// <summary>
		/// Whether playback is paused. (Read Only)
		/// </summary>
		public bool isPaused { get { if (Control != null) return Control.IsPaused(); return false; } private set { } }
		/// <summary>
		/// Whether content is being played. (Read Only)
		/// </summary>
		public bool isPlaying { get { if (Control != null) return Control.IsPlaying(); return false; } private set { } }
		/// <summary>
		/// Whether the VideoPlayer has successfully prepared the content to be played. (Read Only)
		/// </summary>
		public bool isPrepared { get { if (Application.isPlaying) { return Control.CanPlay(); } return false; } private set { } }
		/// <summary>
		/// The length of the VideoClip, or the URL, in seconds. (Read Only)
		/// </summary>
		public double length { get { if (Info != null) return Info.GetDuration(); return -1; } private set { } }
		/// <summary>
		/// Internal texture in which video content is placed. (Read Only)
		/// </summary>
		public Texture texture
		{
			get { if (TextureProducer != null) return TextureProducer.GetTexture(); return null; }
			private set { }
		}
		/// <summary>
		/// The presentation time of the currently available frame in VideoPlayer.texture.
		/// </summary>
		public double time
		{
			get { if (Control != null) return Control.GetCurrentTime(); return -1; }
			set
			{
				if (canSetTime && Control != null)
					Control.SeekFast(value);
			}
		}
		/// <summary>
		/// The width of the images in the VideoClip, or URL, in pixels. (Read Only)
		/// </summary>
		public uint width { get { if (Info != null) return (uint)Info.GetVideoWidth(); return 0; } private set { } }
		/// <summary>
		/// overall alpha being applied to the color on the respective <see cref="rendererMode"/> component
		/// </summary>
		public float targetCameraAlpha
		{
			get
			{
				try
				{
					switch (rendererMode)
					{
						case DisplayType.uGUI:
							return displayUGUI.color.a;
						case DisplayType.IMGUI:
							return displayIMGUI.Color.a;
						case DisplayType.Mesh:
							return applyToMesh.MeshRenderer.material.color.a;
						case DisplayType.Material:
							return applyToMaterial.Material.color.a;
						case DisplayType.CameraFarPlane:
							return applyToFarPlane.Alpha;
						case DisplayType.RenderTexture:
							var options = applyToTexture.VideoResolveOptions;
							return options.tint.a;
					}
				}
				catch { return 0f; } // fails due to properties in display component no being set
				return 0f;
			}
			set
			{
				try
				{
					switch (rendererMode)
					{
						case DisplayType.uGUI:
							displayUGUI.color = new Color(displayUGUI.color.r, displayUGUI.color.g, displayUGUI.color.b, value);
							break;
						case DisplayType.IMGUI:
							displayIMGUI.Color = new Color(displayIMGUI.Color.r, displayIMGUI.Color.g, displayIMGUI.Color.b, value);
							break;
						case DisplayType.Mesh:
							applyToMesh.MeshRenderer.material.color = new Color(
								applyToMesh.MeshRenderer.material.color.r, applyToMesh.MeshRenderer.material.color.g, applyToMesh.MeshRenderer.material.color.b, value);
							break;
						case DisplayType.Material:
							applyToMaterial.Material.color = new Color(
								applyToMaterial.Material.color.r, applyToMaterial.Material.color.g, applyToMaterial.Material.color.b, value);
							break;
						case DisplayType.CameraFarPlane:
							applyToFarPlane.Alpha = value;
							break;
						case DisplayType.RenderTexture:
							var options = applyToTexture.VideoResolveOptions;
							options.tint = new Color(
								options.tint.r, options.tint.g, options.tint.b, value);
							applyToTexture.VideoResolveOptions = options;
							break;
					}
				}
				catch { return; }
			}
		}
		/// <summary>
		/// <see cref="RenderTexture"/> to draw to when <see cref="rendererMode"/> is set to <see cref="DisplayType.RenderTexture"/>.
		/// </summary>
		public RenderTexture targetTexture
		{
			get
			{
				if (applyToTexture)
					return applyToTexture.ExternalTexture;
				return null;
			}
			set { if (applyToTexture) applyToTexture.ExternalTexture = value; }
		}

		/// <summary>
		/// Camera component to draw to when VideoPlayer.renderMode is set to either VideoRenderMode.CameraFarPlane or VideoRenderMode.CameraNearPlane.
		/// </summary>
		public Camera targetCamera 
		{ 
			get { if (applyToFarPlane) return applyToFarPlane.Camera; return null; }
			set { if (applyToFarPlane) applyToFarPlane.Camera = value; }
		}

		/// <summary>
		/// Whether direct-output volume controls are supported for the current platform and video format. (Read Only)
		/// only possible when the video has been prepared
		/// </summary>
		public bool canSetDirectAudioVolume { get {  if (isPrepared) return true; return false; } private set { } }

		/// <summary>
		/// Whether you can change the playback speed. (Read Only)
		/// only possible when the video has been prepared
		/// </summary>
		public bool canSetPlaybackSpeed { get { if (isPrepared) return true;  return false; } private set { } }

		/// <summary>
		/// Whether frame-skipping to maintain synchronization can be controlled. (Read Only)
		/// only possible when the video has been prepared.
		/// <para><strong>Warning</strong> Although This functions, <see cref="skipOnDrop"/> has no functionality within AVPro</para>
		/// </summary>
		public bool canSetSkipOnDrop { get { if (isPrepared) return true; return false; } private set { } }
		/// <summary>
		/// Whether you can change the current time using the time or frames property. (Read Only)
		/// Only Possible when the video has been prepared.
		/// </summary>
		public bool canSetTime { get { if (isPrepared) return true; return false; } private set { } }
		/// <summary>
		/// Whether you can change the time source followed by the VideoPlayer. (Read Only)
		/// <para><strong>Warning</strong> Although This functions, <see cref="timeUpdateMode"/> has no functionality within AVPro</para>
		/// </summary>
		public bool canSetTimeUpdateMode { get { if (isPrepared) return true; return false; } private set { } }
		/// <summary>
		/// Returns true if the VideoPlayer can step forward through the video content. (Read Only)
		/// </summary>
		public bool canStep { get { if (isPrepared) return true; return false; } private set { } }

		/// <summary>
		/// Number of audio tracks that this VideoPlayer will take control of.
		/// <para><strong>Warning</strong> AVPro currently only supports 1 active audio track</para>
		/// </summary>
		public int controlledAudioTrackCount { get {  return 1; } set { /*Nothing as always 1*/ } }


		// No Implementation/Functionality within AVPro

		/// <summary>
		/// Denominator of the pixel aspect ratio (num:den) for the VideoClip or the URL. (Read Only)
		/// <para><strong>Warning</strong> AVPro Does not currently support this</para>
		/// </summary>
		public uint pixelAspectRationDenominator{ get { LogNoSimWarning("pixelAspectRationDenominator"); return 1; } private set { } }
		/// <summary>
		/// Numerator of the pixel aspect ratio (num:den) for the VideoClip or the URL. (Read Only)
		/// <para><strong>Warning</strong> AVPro Does not currently support this</para>
		/// </summary>
		public uint pixelAspectRationNumerator { get { LogNoSimWarning("pixelAspectRationNumerator"); return 1; } private set { } }
		/// <summary>
		/// Whether the VideoPlayer is allowed to skip frames to catch up with current time.
		/// <para><strong>Warning</strong> AVPro does not currently support this</para>
		/// </summary>
		public bool skipOnDrop { get { LogNoSimWarning("skipOnDrop"); return false; } set { LogNoSimWarning("skipOnDrop"); } }
		/// <summary>
		/// Type of 3D content contained in the source video media.
		/// <para><strong>Warning</strong> AVPro does not currently support this</para>
		/// </summary>
		public int targetCamera3DLayout { get { LogNoSimWarning("targetCamera3DLayout"); return 0; } set { LogNoSimWarning("targetCamera3DLayout"); } }
		/// <summary>
		/// The clock that the VideoPlayer observes to detect and correct drift.
		/// <para><strong>Warning</strong> AVPro does not currently support this</para>
		/// </summary>
		public int timeReference { get { LogNoSimWarning("timeReference"); return 0; } set { LogNoSimWarning("timeReference"); } }
		/// <summary>
		/// The clock source used by the VideoPlayer to derive its current time.
		/// <para><strong>Warning</strong> AVPro does not currently support this</para>
		/// </summary>

#if UNITY_2022_3_OR_NEWER
		public UnityEngine.Video.VideoTimeUpdateMode timeUpdateMode { get { LogNoSimWarning("timeUpdateMode"); return 0; } set { LogNoSimWarning("timeUpdateMode"); } }
		/// <summary>
		/// Determines whether the VideoPlayer will wait for the first frame to be loaded into the texture before starting playback when VideoPlayer.playOnAwake is on.
		/// <para><strong>Warning</strong> AVPro does not currently support this</para>
		/// </summary>
#endif
		public bool WaitForFirstFrame { get { LogNoSimWarning("WaitForFirstFrame"); return true; } set { LogNoSimWarning("WaitForFirstFrame"); } }
		
		/// <summary>
		/// <strong>Warning</strong> AVPro does not currently support this
		/// </summary>
		public bool sendFrameReadyEvents;
#endregion Accessible Properties

		#region Events & Delegates
		public delegate void EventHandler(VideoPlayer_AVPro source);
		public delegate void ErrorEventHandler(VideoPlayer_AVPro source, string message);
		public delegate void FrameReadyEventHandler(VideoPlayer_AVPro source, long frameIdx);
		public delegate void TimeEventHandler(VideoPlayer_AVPro source, double seconds);
		public event EventHandler prepareCompleted; // ReadyToPlay
		public event EventHandler started; // Started
		public event ErrorEventHandler errorReceived; // Error
		public event EventHandler seekCompleted; // FinishedSeeking
		public event FrameReadyEventHandler frameReady; // Not Supported
		public event EventHandler loopPointReached; // Not Supported
		public event EventHandler frameDropped; // Not Supported
		public event TimeEventHandler clockResyncOccurred; // Not Supported

		/// <summary>
		/// Used to simulate and invoke the <see cref="VideoPlayer"/> events
		/// </summary>
		/// <param name="mediaPlayer">The <see cref="MediaPlayer"/> this event came from</param>
		/// <param name="eventType">The type of event to invoke</param>
		/// <param name="errorCode">The error code to show</param>
		public void EventCallbacks(MediaPlayer mediaPlayer, MediaPlayerEvent.EventType eventType, ErrorCode errorCode)
		{
			switch (eventType)
			{
				case MediaPlayerEvent.EventType.ReadyToPlay:
					prepareCompleted?.Invoke((VideoPlayer_AVPro)mediaPlayer);
					break;
				case MediaPlayerEvent.EventType.Started:
					started?.Invoke((VideoPlayer_AVPro)mediaPlayer);
					break;
				case MediaPlayerEvent.EventType.Error:
					errorReceived?.Invoke((VideoPlayer_AVPro)mediaPlayer, errorCode.ToString());
					break;
				case MediaPlayerEvent.EventType.FinishedSeeking:
					seekCompleted?.Invoke((VideoPlayer_AVPro)mediaPlayer);
					break;
				default:
					if (loopPointReached != null)
					{
						if (loopPointReached.GetInvocationList().Length > 0)
							LogNoSimWarning("loopPointReached Event", "AVPro does not contain a event callback for when the loop point is reached");
					}
					if (frameDropped != null)
					{
						if (frameDropped.GetInvocationList().Length > 0)
							LogNoSimWarning("frameDropped Event", "AVPro does not contain a event callback for when a frame is dropped");
					}
					if (clockResyncOccurred != null)
					{
						if (clockResyncOccurred.GetInvocationList().Length > 0)
							LogNoSimWarning("clockResyncOccurred Event", "AVPro does not contain a event callback for when clock resync is restored");
					}
					if (frameReady != null)
					{
						if (frameReady.GetInvocationList().Length > 0)
							LogNoSimWarning("frameReady Event", "AVPro does not contain a event callback for when each frame is ready, it does have one for " +
								"when the first frame is ready though");
					}
					break;
			}
		}
		#endregion Events & Delegates

		#region Public Methods
		/// <summary>
		/// VideoRecord:
		///  - Enable/disable audio track decoding. Only effective when the <see cref="VideoPlayer_AVPro"/> is not currently playing
		/// AVPro:
		///  - Changes current active audio track. Only effective when not currently playing.
		///  - Will Mute the track if enabled is set to false
		/// </summary>
		/// <param name="trackIndex">Index of the audio track to change to.</param>
		/// <param name="enabled">True for enabling the track. False for disabling the track.</param>
		public void EnableAudioTrack(ushort trackIndex, bool enabled)
		{
			// cant change if already playing
			if (Control.IsPlaying())
			{
				Debug.LogWarning("Audio track not changed, warning you cannot change audio track whilst playing the media");
				return;
			}

			AudioTracks audioTracks = AudioTracks.GetAudioTracks();
			// no need to do anything if their are no audio tracks
			if (audioTracks.Count == 0)
			{
				Debug.LogWarning("Warning: their are currenrly no audio tracks");
				return;
			}
			// no need to change if already set
			if (audioTracks[trackIndex] == AudioTracks.GetActiveAudioTrack())
				return;

			if (trackIndex >= audioTracks.Count)
			{
				Debug.LogError($"Error: trackIndex ({trackIndex}) is larger than total audioTrack count ({audioTracks.Count})");
				return;
			}
			AudioTracks.SetActiveAudioTrack(audioTracks[trackIndex]);
			Control.MuteAudio(enabled);
		}


		/// <summary>
		/// The number of audio channels in the specified audio track.
		/// </summary>
		/// <param name="trackIndex">Index for the audio track being queried.</param>
		/// <returns><strong>ushort</strong> Number of audio channels.</returns>
		public ushort GetAudioChannelCount(ushort trackIndex)
		{
			AudioTracks audioTracks = AudioTracks.GetAudioTracks();
			if (trackIndex >= audioTracks.Count)
			{
				Debug.LogError($"Error: trackIndex ({trackIndex}) is larger than total audioTrack count ({audioTracks.Count})");
				return 0;
			}
			return (ushort)audioTracks[trackIndex].ChannelCount;
		}

		/// <summary>
		/// Returns the language code, if any, for the specified track.
		/// </summary>
		/// <param name="trackIndex">index of the audio track to query</param>
		/// <returns><strong>string</strong>> Language Code</returns>
		public string GetAudioLanguageCode(ushort trackIndex)
		{
			AudioTracks audioTracks = AudioTracks.GetAudioTracks();
			if (trackIndex >= audioTracks.Count)
			{
				Debug.LogError($"Error: trackIndex ({trackIndex}) is larger than total audioTrack count ({audioTracks.Count})");
				return null;
			}
			return audioTracks[trackIndex].Language;
		}

		/// <summary>
		/// VideoPlayer:
		///  - Returns the audio track sampling rate in Hertz.
		/// AVPro:
		///  - Returns the unity AudioSettings global sampleRate
		/// </summary>
		/// <param name="trackIndex">index of the audio track to query</param>
		/// <returns><strong>uint</strong>> The sampling rate in Hertz.</returns>
		public uint GetAudioSampleRate(ushort trackIndex)
		{
			return (uint)((AudioSettings.GetConfiguration().sampleRate == 0) ? 0 : AudioSettings.outputSampleRate);
		}

		/// <summary>
		/// VideoRecord: 
		///  - Gets the direct-output audio mute status for the specified track.
		/// AVPro:
		///  - retruns the global mute state, as currently, tracks have no recognition
		///  of volume and muted, these are handled globally. 
		/// </summary>
		/// <param name="trackIndex">index of the audio track to query</param>
		/// <returns>true if the selected track is muted; otherwise false</returns>
		public bool GetDirectAudioMute(ushort trackIndex)
		{
			return AudioMuted;
		}

		/// <summary>
		/// AVPro:
		///  - retruns the global volume, as currently, tracks have no recognition
		///  of volume and muted, these are handled globally. 
		/// </summary>
		/// <param name="trackIndex">index of the audio track to query</param>
		/// <returns><strong>float</strong> Volume, between 0 and 1.</returns>
		public float GetDirectAudioVolume(ushort trackIndex)
		{
			return AudioVolume;
		}

		/// <summary>
		/// Gets the AudioSource that will receive audio samples for the specified track if VideoApi is set to 
#if UNITY_STANDALONE_WIN || UNITY_WSA_10_0
		/// Media Foundation.
#elif UNITY_ANDROID
		/// ExoPlayer.
#elif UNITY_IOS || UNITY_STANDALONE_OSX || UNITY_TVOS || UNITY_VISIONOS
		/// any option
#endif
		/// and the Audio mode is set to output in unity
		/// 
		/// AVPro:
		///  - index is not used as 1 audio source is used
		/// </summary>
		/// <param name="trackIndex">Index of the audio track for which the AudioSource is wanted</param>
		/// <returns><strong>AudioSource</strong> if correct output selected</returns>
		public AudioSource GetTargetSource(ushort trackIndex)
		{
#if UNITY_STANDALONE_WIN
			if (PlatformOptionsWindows.videoApi == Windows.VideoApi.MediaFoundation && 
				PlatformOptionsWindows._audioMode == Windows.AudioOutput.Unity)
#elif UNITY_WSA_10_0
			if (PlatformOptionsWindowsUWP.videoApi == WindowsUWP.VideoApi.MediaFoundation &&
				PlatformOptionsWindowsUWP._audioMode == WindowsUWP.AudioOutput.Unity)
#elif UNITY_ANDROID
			if (PlatformOptionsAndroid.videoApi == Android.VideoApi.ExoPlayer && 
				PlatformOptionsAndroid.audioMode == PlatformOptions.AudioMode.Unity)
#elif UNITY_OPENHARMONY
			if (PlatformOptionsOpenHarmony.audioMode == PlatformOptions.AudioMode.Unity)
#elif UNITY_IOS
			if (PlatformOptions_iOS.audioMode == PlatformOptions.AudioMode.Unity)
#elif UNITY_STANDALONE_OSX
			if (PlatformOptions_macOS.audioMode == PlatformOptions.AudioMode.Unity)
#elif UNITY_TVOS
			if (PlatformOptions_tvOS.audioMode == PlatformOptions.AudioMode.Unity)
#elif UNITY_VISIONOS
			if (PlatformOptions_visionOS.audioMode == PlatformOptions.AudioMode.Unity)
#elif UNITY_WEBGL
			if (PlatformOptionsWebGL.audioMode == PlatformOptions.AudioMode.Unity)
#endif
			{
				return AudioSource;
			}
			return null;
		}

		/// <summary>
		/// Wherther the current track is the active audio track
		/// </summary>
		/// <param name="trackIndex">Index of the audio track being queried</param>
		/// <returns><strong>bool</strong> Returns <c>true</c> if the specified track is enabled</returns>
		public bool IsAudioTrackEnabled(ushort trackIndex)
		{
			AudioTracks audioTracks = AudioTracks.GetAudioTracks();
			if (trackIndex >= audioTracks.Count)
			{
				Debug.LogError($"Error: trackIndex ({trackIndex}) is larger than total audioTrack count ({audioTracks.Count})");
				return false;
			}
			AudioTrack activeTrack = AudioTracks.GetActiveAudioTrack();
			if (audioTracks[trackIndex] == activeTrack)
				return true;
			return false;
		}

		/// <summary>
		/// Pauses Playback
		/// </summary>
		public new void Pause()
		{
			base.Pause();
		}

		/// <summary>
		/// Plays the media, ensuring the appropriate display components are enabled based on the current renderer mode.
		/// </summary>
		public new void Play()
		{
			if (!MediaOpened)
			{
				base.OpenMedia();
			}
			// as the stop can disable the display and nothing might happen inbetween need to enable again if it is their and if it is active
			switch (rendererMode)
			{
				case DisplayType.Mesh:
					if (applyToMesh) applyToMesh.enabled = true;
					break;
				case DisplayType.Material:
					if (applyToMaterial) applyToMaterial.enabled = true;
					break;
				case DisplayType.uGUI:
					if (displayUGUI) displayUGUI.enabled = true;
					break;
				case DisplayType.IMGUI:
					if (displayIMGUI) displayIMGUI.enabled = true;
					break;
				case DisplayType.CameraFarPlane:
					if (applyToFarPlane) applyToFarPlane.enabled = true;
					break;
				case DisplayType.RenderTexture:
					if (applyToTexture) applyToTexture.enabled = true;
					break;
			}

			base.Play();
		}

		/// <summary>
		/// Initilises the Media Player, to start beign able to play
		/// Will Create the base media player and initilize it
		/// </summary>
		public void Prepare()
		{
			// Note: not 100% on this one, as the docs are a little short, they say that it preloads the video in this step as well,
			// so do i put a OpenMedia() call here as well, but that can automatically start the playback which we dont want to do.
			// so will have to set to false, but ehh.
			Initialise();
			OpenMedia(false);
		}

		/// <summary>
		///  - Sets the global direct-output audio mute status
		/// </summary>
		/// <param name="trackIndex">index of the audio track to query</param>
		/// <param name="mute">Mute on/off</param>
		public void SetDirectAudioMute(ushort trackIndex, bool mute)
		{
			AudioMuted = mute;
		}

		/// <summary>
		///  - Set the global direct-output volume 
		/// </summary>
		/// <param name="trackIndex">index of the audio track to query</param>
		/// <returns><strong>float</strong> Volume, between 0 and 1.</returns>
		public void SetDirectAudioVolume(ushort trackIndex, float volume)
		{
			if (canSetDirectAudioVolume)
			{
				AudioVolume = volume;
			}
		}


		/// <summary>
		/// Sets the AudioSource that will receive audio samples for the specified track if using AudioSource
		/// </summary>
		/// <param name="trackIndex">Index of the audio track to associate with the specified AudioSource.</param>
		/// <param name="source">AudioSource to associate with the audio track.</param>
		public void SetTargetAudioSource(ushort trackIndex, AudioSource source)
		{
#if UNITY_STANDALONE_WIN
			if (PlatformOptionsWindows.videoApi == Windows.VideoApi.MediaFoundation && 
				PlatformOptionsWindows._audioMode == Windows.AudioOutput.Unity)
#elif UNITY_WSA_10_0
			if (PlatformOptionsWindowsUWP.videoApi == WindowsUWP.VideoApi.MediaFoundation &&
				PlatformOptionsWindowsUWP._audioMode == WindowsUWP.AudioOutput.Unity)
#elif UNITY_ANDROID
			if (PlatformOptionsAndroid.videoApi == Android.VideoApi.ExoPlayer &&
				PlatformOptionsAndroid.audioMode == PlatformOptions.AudioMode.Unity)
#elif UNITY_OPENHARMONY
			if ( PlatformOptionsOpenHarmony.audioMode == PlatformOptions.AudioMode.Unity )
#elif UNITY_IOS
			if (PlatformOptions_iOS.audioMode == PlatformOptions.AudioMode.Unity)
#elif UNITY_STANDALONE_OSX
			if (PlatformOptions_macOS.audioMode == PlatformOptions.AudioMode.Unity)
#elif UNITY_TVOS
			if (PlatformOptions_tvOS.audioMode == PlatformOptions.AudioMode.Unity)
#elif UNITY_VISIONOS
			if (PlatformOptions_visionOS.audioMode == PlatformOptions.AudioMode.Unity)
#elif UNITY_WEBGL
			if (PlatformOptionsWebGL.audioMode == PlatformOptions.AudioMode.Unity)
#else
			return;
#endif
			{
				SetAudioSource(source);
			}
		}

		/// <summary>
		/// Advances the current time by one frame immediately
		/// </summary>
		public void StepForward()
		{
			if (!canStep)
				return;
			int currentFrame = Control.GetCurrentTimeFrames();
			Control.SeekToFrame(currentFrame + 1);
			Control.SeekToFrameRelative(1);
		}

		/// <summary>
		/// Stops playback and set current playback time to 0
		/// </summary>
		public new void Stop()
		{
			if (Control == null)
				return;
			// disable the rendering of the video (this is what VideoPlayer does)
			switch (rendererMode)
			{
				case DisplayType.Mesh:
					if (applyToMesh) applyToMesh.enabled = false;
					break;
				case DisplayType.Material:
					if (applyToMaterial) applyToMaterial.enabled = false;
					break;
				case DisplayType.uGUI:
					if (displayUGUI) displayUGUI.enabled = false;
					break;
				case DisplayType.IMGUI:
					if (displayIMGUI) displayIMGUI.enabled = false;
					break;
				case DisplayType.CameraFarPlane:
					if (applyToFarPlane) applyToFarPlane.enabled = false;
					break;
				case DisplayType.RenderTexture:
					if (applyToTexture) applyToTexture.enabled = false;
					break;
			}
			// set the current time to 0
			Control.SeekToFrame(0);
			base.Stop();
		}
		#endregion Public Methods

		#region Conversion

// only do the context menu things when in the editor, where they will run, if not like this will crash on build as 
// editor no longer exists
#if UNITY_EDITOR
		/// <summary>
		/// Converts the attached VideoPlayer componenet on an object to a VideoPlayer_AVPro, through the context menu of the script
		/// </summary>
		/// <param name="command">Allows for the getting of the object the command was sent from, to allow for the adding of the AVPro component</param>
		[MenuItem("CONTEXT/VideoPlayer/Convert To VideoPlayer_AVPro")]
		public static void VideoPlayerTo_AVPro_MenuItem(MenuCommand command)
		{
			var obj = ((VideoPlayer)command.context).gameObject;
			if (!obj.TryGetComponent(out VideoPlayer_AVPro player))
				player = obj.AddComponent<VideoPlayer_AVPro>();
			player._converted = true;
			player.VideoPlayertoAVProConversion();
		}
		/// <summary>
		/// Adds a context menu option to VideoPlayer_AVPro that will copy all of the data
		/// from a video player and paste it into the AVPro videoplayer.
		/// </summary>
		[ContextMenu("Copy from VideoPlayer")]
		void VideoPlayertoAVProConversion()
		{
			// ensure that a videoplayer is attached to this gameobject
			if (!gameObject.TryGetComponent(out VideoPlayer player))
			{
				Debug.LogWarning("No Video Player Attached to this object, Abandoning Process");
				return;
			}
			_converted = true;

			// convert all of the imformation from the VideoPlayer into the VideoPlayer_AVPro

			// MediaPath
			if (player.source == VideoSource.VideoClip)
			{
				// clip
				if (player.clip)
				{
					LogAutomaticConversionSolution("VideoClip", "MediaReference", "it is recommended that you create your own MediaReference and assign it");
					var mediaReference = ScriptableObject.CreateInstance<MediaReference>();
					mediaReference.MediaPath = new MediaPath(Application.dataPath.TrimEnd(new char[] { 'A', 's', 's', 'e', 't', 's' }) + player.clip.originalPath, MediaPathType.AbsolutePathOrURL);
					clip = mediaReference;
				}
			}
			else
			{
				// url
				url = new MediaPath(player.url, MediaPathType.AbsolutePathOrURL);
				Url = url.Path;
			}


			// Source
			switch (player.source)
			{
				// clip
				case UnityEngine.Video.VideoSource.VideoClip:
					sourceAVPro = MediaSource.Reference;
					Source = MediaSource.Reference;
					break;
				// url
				case UnityEngine.Video.VideoSource.Url:
					sourceAVPro = MediaSource.Path;
					Source = MediaSource.Path;
					break;
			}
			// Play On Awake
			playOnAwake = player.playOnAwake;
			PlayOnAwake = player.playOnAwake;
			// Auto Opening
			AutoOpening = player.playOnAwake;
			AutoOpen = player.playOnAwake;
			// isLooping
			isLooping = player.isLooping;
			IsLooping = player.isLooping;
			// playback speed
			playbackSpeed = player.playbackSpeed;
			PlaybackSpeed = player.playbackSpeed;
			// target material renderer
			targetMaterialRenderer = player.targetMaterialRenderer;
			TargetMaterialRenderer = player.targetMaterialRenderer;
			// target material property
			targetMateralProperty = player.targetMaterialProperty;
			TargetMateralProperty = player.targetMaterialProperty;
			// render mode
			switch (player.renderMode)
			{
				case UnityEngine.Video.VideoRenderMode.CameraFarPlane:
					rendererMode = DisplayType.CameraFarPlane;
					RenderMode = DisplayType.CameraFarPlane;
					break;
				case UnityEngine.Video.VideoRenderMode.CameraNearPlane:
					rendererMode = DisplayType.IMGUI;
					RenderMode = DisplayType.IMGUI;
					break;
				case UnityEngine.Video.VideoRenderMode.RenderTexture:
					rendererMode = DisplayType.RenderTexture;
					RenderMode = DisplayType.RenderTexture;
					break;
				case UnityEngine.Video.VideoRenderMode.MaterialOverride:
					rendererMode = DisplayType.Material;
					RenderMode = DisplayType.Material;
					break;
				case UnityEngine.Video.VideoRenderMode.APIOnly:
					rendererMode = DisplayType.Mesh;
					RenderMode = DisplayType.Mesh;
					break;
			}
			// aspect ratio
			switch (player.aspectRatio)
			{
				case UnityEngine.Video.VideoAspectRatio.NoScaling:
					aspectRatioRenderTexture = VideoResolveOptions.AspectRatio.NoScaling;
					AspectRatioRenderTexture = VideoResolveOptions.AspectRatio.NoScaling;
					break;
				case UnityEngine.Video.VideoAspectRatio.FitVertically:
					aspectRatioRenderTexture = VideoResolveOptions.AspectRatio.FitVertically;
					AspectRatioRenderTexture = VideoResolveOptions.AspectRatio.FitVertically;
					break;
				case UnityEngine.Video.VideoAspectRatio.FitHorizontally:
					aspectRatioRenderTexture = VideoResolveOptions.AspectRatio.FitHorizontally;
					AspectRatioRenderTexture = VideoResolveOptions.AspectRatio.FitHorizontally;
					break;
				case UnityEngine.Video.VideoAspectRatio.FitInside:
					aspectRatio = ScaleMode.ScaleToFit;
					AspectRatio = ScaleMode.ScaleToFit;
					aspectRatioRenderTexture = VideoResolveOptions.AspectRatio.FitInside;
					AspectRatioRenderTexture = VideoResolveOptions.AspectRatio.FitInside;
					break;
				case UnityEngine.Video.VideoAspectRatio.FitOutside:
					aspectRatio = ScaleMode.ScaleAndCrop;
					AspectRatio = ScaleMode.ScaleAndCrop;
					aspectRatioRenderTexture = VideoResolveOptions.AspectRatio.FitOutside;
					AspectRatioRenderTexture = VideoResolveOptions.AspectRatio.FitOutside;
					break;
				case UnityEngine.Video.VideoAspectRatio.Stretch:
					aspectRatio = ScaleMode.StretchToFill;
					AspectRatio = ScaleMode.StretchToFill;
					aspectRatioRenderTexture = VideoResolveOptions.AspectRatio.Stretch;
					AspectRatioRenderTexture = VideoResolveOptions.AspectRatio.Stretch;
					break;
			}
			// audio output mode
#if UNITY_STANDALONE_WIN
			switch (player.audioOutputMode)
			{
				case UnityEngine.Video.VideoAudioOutputMode.None:
					audioOutputModeAVPro = Windows.AudioOutput.None;
					AudioOutputMode = Windows.AudioOutput.None;
					break;
				case UnityEngine.Video.VideoAudioOutputMode.AudioSource:
					audioOutputModeAVPro = Windows.AudioOutput.Unity;
					AudioOutputMode = Windows.AudioOutput.Unity;
					CreateAudioComponents();
					break;
				case UnityEngine.Video.VideoAudioOutputMode.Direct:
				case UnityEngine.Video.VideoAudioOutputMode.APIOnly:
					audioOutputModeAVPro = Windows.AudioOutput.System;
					AudioOutputMode = Windows.AudioOutput.System;
					break;
			}
#elif UNITY_WSA_10_0
			switch (player.audioOutputMode)
			{
				case UnityEngine.Video.VideoAudioOutputMode.None:
					audioOutputModeAVPro = WindowsUWP.AudioOutput.None;
					AudioOutputMode = WindowsUWP.AudioOutput.None;
					break;
				case UnityEngine.Video.VideoAudioOutputMode.AudioSource:
					audioOutputModeAVPro = WindowsUWP.AudioOutput.Unity;
					AudioOutputMode = WindowsUWP.AudioOutput.Unity;
					CreateAudioComponents();
					break;
				case UnityEngine.Video.VideoAudioOutputMode.Direct:
				case UnityEngine.Video.VideoAudioOutputMode.APIOnly:
					audioOutputModeAVPro = WindowsUWP.AudioOutput.System;
					AudioOutputMode = WindowsUWP.AudioOutput.System;
					break;
			}
#elif UNITY_ANDROID || UNITY_IOS || UNITY_STANDALONE_OSX || UNITY_TVOS || UNITY_VISIONOS
			switch (player.audioOutputMode)
			{
				case UnityEngine.Video.VideoAudioOutputMode.None:
					audioOutputModeAVPro = PlatformOptions.AudioMode.SystemDirect;
					AudioOutputMode = PlatformOptions.AudioMode.SystemDirect;
					break;
				case UnityEngine.Video.VideoAudioOutputMode.AudioSource:
					audioOutputModeAVPro = PlatformOptions.AudioMode.Unity;
					AudioOutputMode = PlatformOptions.AudioMode.Unity;
					CreateAudioComponents();
					break;
				case UnityEngine.Video.VideoAudioOutputMode.Direct:
				case UnityEngine.Video.VideoAudioOutputMode.APIOnly:
					audioOutputModeAVPro = PlatformOptions.AudioMode.SystemDirect;
					AudioOutputMode = PlatformOptions.AudioMode.SystemDirect;
					break;
			}
#endif

			// disable the VideoPlayer
			player.enabled = false;

			CreateRendererComponents();

			// apply properties that require components to be created
			if (rendererMode == DisplayType.Material)
			{
#if UNITY_EDITOR
				targetMaterial = player.targetMaterialRenderer.sharedMaterial;
#else
				targetMaterial = player.targetMaterialRenderer.material;
#endif
			}
			else if (rendererMode == DisplayType.RenderTexture)
			{
				targetTexture = player.targetTexture;
				TargetTexture = player.targetTexture;
			}
			else if (rendererMode == DisplayType.CameraFarPlane)
			{
				applyToFarPlane.VideoAspectRatio = (VideoAspectRatio)(int)aspectRatioRenderTexture;
				applyToFarPlane.MainColor = color;
			}
		}

		/// <summary>
		/// This method will convert, VideoPlayer_AVPro to a MediaPlayer, this is alot simpler than
		/// converting from VideoPlayer to VideoPlayer_AVPro, as all the data matches so can just set it 
		/// all
		/// </summary>
		[ContextMenu("Convert to MediaPlayer")]
		public void ConvertToMediaPlayer()
		{
			MediaPlayer player = gameObject.AddComponent<MediaPlayer>();
			//if (!gameObject.TryGetComponent(out player))

			// Source
			player.SetMediaSource(sourceAVPro);

			// Set media based on source
			if (sourceAVPro == MediaSource.Reference)
				player.SetMediaReference(clip); // clip
			else
				player.SetMediaPath(new MediaPath(Url, MediaPathType.AbsolutePathOrURL)); // url
			// Play On Awake
			player.AutoStart = playOnAwake;
			// Auto Opening
			player.AutoOpen = true;
			// isLooping
			player.Loop = isLooping;
			// playback speed
			player.PlaybackRate = playbackSpeed;

			// audio source
			if (TryGetComponent(out AudioOutput audioOut))
			{
				if (TryGetComponent(out AudioSource audioSource))
					audioOut.SetAudioSource(audioSource);
			}

			// audio output mode
#if UNITY_STANDALONE_WIN
			player.PlatformOptionsWindows._audioMode = audioOutputModeAVPro;
			if (audioOutputModeAVPro == Windows.AudioOutput.Unity)
				GetComponent<AudioOutput>().Player = player;
#elif UNITY_WSA_10_0
			player.PlatformOptionsWindowsUWP._audioMode = audioOutputModeAVPro;
			if (audioOutputModeAVPro == WindowsUWP.AudioOutput.Unity)
				GetComponent<AudioOutput>().Player = player;
#elif UNITY_ANDROID
			player.PlatformOptionsAndroid.audioMode = audioOutputModeAVPro;
#elif UNITY_IOS
			player.PlatformOptions_iOS.audioMode = audioOutputModeAVPro;
#elif UNITY_STANDALONE_OSX
			player.PlatformOptions_macOS.audioMode = audioOutputModeAVPro;
#elif UNITY_TVOS
			player.PlatformOptions_tvOS.audioMode = audioOutputModeAVPro;
#elif UNITY_VISIONOS
			player.PlatformOptions_visionOS.audioMode = audioOutputModeAVPro;
#endif
#if UNITY_ANDROID || UNITY_IOS || UNITY_STANDALONE_OSX || UNITY_TVOS || UNITY_VISIONOS
			if (audioOutputModeAVPro ==  PlatformOptions.AudioMode.Unity)
				GetComponent<AudioOutput>().Player = player;
#endif
			if (_renderModeComponent != null)
			{
				switch(rendererMode)
				{
					case DisplayType.Mesh:
						((ApplyToMesh)_renderModeComponent).Player = player;
						break;
					case DisplayType.Material:
						((ApplyToMaterial)_renderModeComponent).Player = player;
						break;
					case DisplayType.uGUI:
						((DisplayUGUI)_renderModeComponent).Player = player;
						break;
					case DisplayType.IMGUI:
						((DisplayIMGUI)_renderModeComponent).Player = player;
						break;
					case DisplayType.CameraFarPlane:
						((ApplyToFarPlane)_renderModeComponent).Player = player;
						break;
					case DisplayType.RenderTexture:
						((ResolveToRenderTexture)_renderModeComponent).MediaPlayer = player;
						break;
					case DisplayType.None:
					default:
						break;
				}
			}
			
			// disable this as media player now active
			this.enabled = false;
		}
#endif

#endregion Conversion

			#region Helper Methods

			/// <summary>
			/// Creates a <see cref="AudioOutput"/> compoenent that will be used to play audio when selecting the Unity option from <see cref="Windows.AudioOutput"/>
			/// </summary>
			public void CreateAudioComponents()
		{
			DestroyAudioOutputBehaviour();
#if UNITY_STANDALONE_WIN
			if (audioOutputModeAVPro == Windows.AudioOutput.Unity)
#elif UNITY_WSA_10_0
			if (audioOutputModeAVPro == WindowsUWP.AudioOutput.Unity)
#elif UNITY_ANDROID || UNITY_IOS || UNITY_STANDALONE_OSX || UNITY_TVOS || UNITY_VISIONOS
			if (audioOutputModeAVPro == PlatformOptions.AudioMode.Unity)
#endif
			{
				var output = gameObject.AddComponent<AudioOutput>();
				if (!gameObject.TryGetComponent(out AudioSource source))
				{
					source = gameObject.AddComponent<AudioSource>();
				}
				else
				{
					audioSource = source;
				}
				output.SetAudioSource(source);
				output.Player = this;
			}
		}

		/// <summary>
		/// Creates the components for the differernt rendering modes, this method
		/// also handles the destruction of the previous rendering mode components.
		/// </summary>
		public void CreateRendererComponents()
		{
			// destroy the previous component
			DestroyRendererBehaviours();
			// Add current output type
			switch (rendererMode)
			{
				case DisplayType.Mesh:
					if (!gameObject.GetComponent<ApplyToMesh>())
						applyToMesh = gameObject.AddComponent<ApplyToMesh>();
					else
						applyToMesh = gameObject.GetComponent<ApplyToMesh>();
					applyToMesh.Player = this;
					_renderModeComponent = applyToMesh;
					break;
				case DisplayType.Material:
					if (!gameObject.GetComponent<ApplyToMaterial>())
						applyToMaterial = gameObject.AddComponent<ApplyToMaterial>();
					else
						applyToMaterial = gameObject.GetComponent<ApplyToMaterial>();
					applyToMaterial.Player = this;
					_renderModeComponent = applyToMaterial;
					// VideoPlayer's material override is for the material attached to the current object, so just get the renderer attached to this object and set the material to be the one to be overridden
					if (gameObject.TryGetComponent(out Renderer rendererMat))
					{
						targetMaterial = rendererMat.material;
						applyToMaterial.Material = rendererMat.material;
					}
					break;
				case DisplayType.uGUI:
					if (!canvasObj)
					{
						Debug.LogWarning("[AVProVideo] Warning, No Canvas Object Set For uGUI, Overriding DisplayType");
						break;
					}
					if (!canvasObj.GetComponent<DisplayUGUI>())
						displayUGUI = canvasObj.AddComponent<DisplayUGUI>();
					else
						displayUGUI = canvasObj.GetComponent<DisplayUGUI>();
					displayUGUI.Player = this;
					_renderModeComponent = displayUGUI;
					break;
				case DisplayType.IMGUI:
					if (!gameObject.GetComponent<DisplayIMGUI>())
						displayIMGUI = gameObject.AddComponent<DisplayIMGUI>();
					else
						displayIMGUI = gameObject.GetComponent<DisplayIMGUI>();
					displayIMGUI.Player = this;
					_renderModeComponent = displayIMGUI;
					displayIMGUI.Color = Color.white;
					break;
				case DisplayType.CameraFarPlane:
					if (!gameObject.GetComponent<ApplyToFarPlane>())
						applyToFarPlane = gameObject.AddComponent<ApplyToFarPlane>();
					else
						applyToFarPlane = gameObject.GetComponent<ApplyToFarPlane>();
					applyToFarPlane.Player = this;
					_renderModeComponent = applyToFarPlane;
					applyToFarPlane.VideoAspectRatio = (VideoAspectRatio)(int)aspectRatioRenderTexture;
					applyToFarPlane.MainColor = color;
					break;
				case DisplayType.RenderTexture:
					if (!gameObject.GetComponent<ResolveToRenderTexture>())
						applyToTexture = gameObject.AddComponent<ResolveToRenderTexture>();
					else
						applyToTexture = gameObject.GetComponent<ResolveToRenderTexture>();
					applyToTexture.MediaPlayer = this;
					_renderModeComponent = applyToTexture;
					applyToTexture.ExternalTexture = TargetTexture;
					break;
				case DisplayType.None:
					_renderModeComponent = null;
					break;
				default:
					Debug.LogError("Error: Invalid Render Mode selected");
					break;
			}
		}

		/// <summary>
		/// Destroys the <see cref="AudioOutput"/> compoenent attached to this object
		/// <para>This componenet is used to take the audio from the video codec and play it through a unity AudioSource compoenent</para>
		/// </summary>
		public void DestroyAudioOutputBehaviour()
		{
			if (gameObject.TryGetComponent(out AudioOutput audio))
				DestroyImmediate(audio);
			//if (gameObject.TryGetComponent(out AudioSource source))
			//    DestroyImmediate(source);
		}

		/// <summary>
		/// Destroys the rendering mode compoennts attatched to the current object,
		/// can handle more than 1 type but not multiple of the same type
		/// </summary>
		public void DestroyRendererBehaviours()
		{
			if (gameObject.TryGetComponent(out ApplyToMesh mesh))
				DestroyImmediate(mesh);
			if (gameObject.TryGetComponent(out ApplyToMaterial material))
				DestroyImmediate(material);
			if (canvasObj && canvasObj.TryGetComponent(out DisplayUGUI uGUI))
				DestroyImmediate(uGUI);
			if (gameObject.TryGetComponent(out DisplayIMGUI IMGUI))
				DestroyImmediate(IMGUI);
			if (gameObject.TryGetComponent(out ApplyToFarPlane farPlane))
				DestroyImmediate(farPlane);
			if (gameObject.TryGetComponent(out ResolveToRenderTexture texture))
				DestroyImmediate(texture);
		}

		/// <summary>
		/// This will set the output mode based on the context.
		/// <list type="bullet">
		/// <item>Material - Object containing a <see cref="Renderer"/> component with material attached</item>
		/// <item>UGUI - Object containing both a <see cref="CanvasRenderer"/> and <see cref="RectTransform"/></item>
		/// <item>IMGUI - Default Option</item>
		/// </list>
		/// </summary>
		public void SetOutputModeContextual()
		{
			// Material
			// UGUI
			// None
			// remove any previous renderers
			DestroyRendererBehaviours();
			if (gameObject.TryGetComponent(out CanvasRenderer canvas) && gameObject.TryGetComponent(out RectTransform transform))
			{
				var added = gameObject.AddComponent<DisplayUGUI>();
				if (!added)
				{
					_renderModeComponent = null;
					displayUGUI = null;
					rendererMode = DisplayType.None;
					return;
				}
				_renderModeComponent = added;
				displayUGUI = added;
				rendererMode = DisplayType.uGUI;
				added.Player = this;
				return;
			}
			else if (gameObject.TryGetComponent(out Renderer renderer))
			{
				if (renderer.sharedMaterial)
				{
					var added = gameObject.AddComponent<ApplyToMaterial>();
					_renderModeComponent = added;
					applyToMaterial = added;
					rendererMode = DisplayType.Material;
					added.Player = this;
					return;
				}
				
			}
			_renderModeComponent = null;
			rendererMode = DisplayType.None;
		}

		/// <summary>
		/// Used to check if any of the output modes have been set
		/// </summary>
		/// <returns><c>true</c> if any output mode is set; otherwise <c>false</c></returns>
		public bool OutputModeSet()
		{
			return applyToFarPlane || applyToMaterial || applyToMesh || applyToTexture || displayIMGUI || displayUGUI; 
		}

		#endregion Helper Methods

		#region General Methods

		/// <summary>
		/// this will handle setting up the rendering component, based on the object that it is placed on
		/// (if it was not generated by a conversion (i which case it will use the same output mode as 
		/// the one used on the orignal video player))
		/// </summary>
		public void Start()
		{
			if (!_converted && _renderModeComponent == null && !OutputModeSet())
			{
				// add the appropriate output mode
				SetOutputModeContextual();
			}
		}

		/// <summary>
		/// this will ensure that the media player is properly initilized when turning on
		/// </summary>
		public void Awake()
		{
			// this is just a copy of the one from Media Player so surly thats gotta work
			if (Control == null)
			{
				if (Application.isPlaying)
				{
					Initialise();
					if (Control != null)
					{
						//dont want to auto open with VideoPlayer
						if (AutoOpening)
						{
							OpenMedia(playOnAwake);
						}

						StartRenderCoroutine();
					}
				}
			}
		}

		public void OnEnable()
		{
			// add lister to callback propagator
			Events.AddListener(EventCallbacks);
		}

		public void OnDisable()
		{
			// clear all of the Event/Delegate data
			prepareCompleted = null;
			started = null;
			errorReceived = null;
			seekCompleted = null;
			loopPointReached = null;
			frameDropped = null;
			clockResyncOccurred = null;
			Events.RemoveListener(EventCallbacks);
		}

		// logs when their is no similar behaviour/Functionality to a VideoRecord function within AVPro
		private void LogAutomaticConversionSolution(string value1, string value2, string solution)
		{
			Debug.LogWarning($"[VideoPlayer_AVPro] Warning: Automatic Conversion Occuring from {value1} to {value2}. {solution}");
		}

		private void LogAutomaticConversion(string value1, string value2)
		{
			Debug.LogWarning($"[VideoPlayer_AVPro] Warning: Automatic conversion occuring from {value1} to {value2}, unexpected behaviour may occur");
		}

		private void LogNoSimWarning(string behaviourName, string reason = "")
		{
			Debug.LogWarning($"[VideoPlayer_AVPro] Warning: AVPro contains no similar Behaviour to \"{behaviourName}\". {reason}");
		}

		private void LogNoFunctionality(string behaviourName, string reason = "")
		{
			Debug.LogWarning($"[VideoPlayer_AVPro] Warning: AVPro method/Parameter: \"{behaviourName}\" does not contain any functionality. {reason}");
		}
		#endregion General Methods
	}
}