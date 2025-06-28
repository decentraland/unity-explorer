//-----------------------------------------------------------------------------
// Copyright 2015-2025 RenderHeads Ltd.  All rights reserved.
//-----------------------------------------------------------------------------

#if UNITY_2017_2_OR_NEWER && (UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX || (!UNITY_EDITOR && (UNITY_IOS || UNITY_TVOS || UNITY_VISIONOS || UNITY_ANDROID || UNITY_OPENHARMONY)))

using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace RenderHeads.Media.AVProVideo
{
	public sealed partial class PlatformMediaPlayer
	{
		internal partial struct Native
		{
#if UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
			private const string PluginName = "AVProVideo";
#elif UNITY_IOS || UNITY_TVOS || UNITY_VISIONOS
			private const string PluginName = "__Internal";
#elif UNITY_ANDROID
			private const string PluginName = "AVProVideo2Native";
#elif UNITY_OPENHARMONY
			private const string PluginName = "avprovideolib";
#endif

			internal const int kAVPPlayerRenderEventId = 0x5d5ac000;
			internal const int kAVPPlayerRenderEventMask = 0x7ffff000;
			internal const int kAVPPlayerRenderEventTypeMask = 0x00000f00;
			internal const int kAVPPlayerRenderEventTypeShift = 8;
			internal const int kAVPPlayerRenderEventDataPlayerIDMask = 0xffff;
			internal const int kAVPPlayerRenderEventDataPlayerIDShift = 0;
			internal const int kAVPPlayerRenderEventDataOptionsMask = 0xff;
			internal const int kAVPPlayerRenderEventDataOptionsShift = 16;

			internal enum AVPPluginRenderEvent: int
			{
				None,
				PlayerSetup,
				PlayerRender,
				PlayerFreeResources,
			}

			[Flags]
			internal enum AVPPlayerRenderEventPlayerSetupFlags: int
			{
				AndroidUseOESFastPath = 1 << 0,
				LinearColourSpace     = 1 << 1,
				GenerateMipmaps       = 1 << 2,
			}

			// Video settings

			internal enum AVPPlayerVideoAPI: int
			{
				// Apple - just included for completeness
				AVFoundation,

				// Android - Matches Android.VideoApi
				MediaPlayer = Android.VideoApi.MediaPlayer,
				ExoPlayer = Android.VideoApi.ExoPlayer,
			}

			internal enum AVPPlayerVideoPixelFormat: int
			{
				Invalid,
				Bgra,
				YCbCr420
			}

			[Flags]
			internal enum AVPPlayerFeatureFlags: int
			{
				Caching = 1 << 0,
			}

			[Flags]
			internal enum AVPPlayerVideoOutputSettingsFlags: int
			{
				None                                      = 0,
				LinearColorSpace                          = 1 << 0,
				GenerateMipmaps                           = 1 << 1,
				PreferSoftwareDecoder                     = 1 << 2,
				ForceEnableMediaCodecAsynchronousQueueing = 1 << 3,
			}

			// Audio settings

			internal enum AVPPlayerAudioOutputMode : int
			{
				SystemDirect,
				Unity,
				SystemDirectWithCapture,
				FacebookAudio360,
			}

			// Network settings

			[Flags]
			internal enum AVPPlayerNetworkSettingsFlags: int
			{
				None                     = 0,
				PlayWithoutBuffering     = 1 << 0,
				UseSinglePlayerItem      = 1 << 1,
				ForceStartHighestBitrate = 1 << 2,
				ForceRtpTCP              = 1 << 3,
			}

			// NOTE: The layout of this structure is important - if adding anything put it at the end, make sure alignment is 4 bytes and DO NOT USE bool
			[StructLayout(LayoutKind.Sequential)]
			internal struct AVPPlayerSettings
			{
				// Video
				internal AVPPlayerVideoAPI videoApi;
				internal AVPPlayerVideoPixelFormat pixelFormat;
				internal AVPPlayerVideoOutputSettingsFlags videoFlags;
				internal float preferredMaximumResolution_width;
				internal float preferredMaximumResolution_height;
				internal float maximumPlaybackRate;

				// Audio
				internal AVPPlayerAudioOutputMode audioOutputMode;
				internal int sampleRate;
				internal int bufferLength;
				internal int audioFlags;
				internal Audio360ChannelMode audio360Channels;
				internal int audio360LatencyMS;

				// Network
				internal double preferredPeakBitRate;
				internal double preferredForwardBufferDuration;
				internal AVPPlayerNetworkSettingsFlags networkFlags;
				internal int minBufferMs;
				internal int maxBufferMs;
				internal int bufferForPlaybackMs;
				internal int bufferForPlaybackAfterRebufferMs;
			}

			internal enum AVPPlayerOpenOptionsForceFileFormat: int
			{
				Unknown,
				HLS,
				DASH,
				SmoothStreaming
			};

			[Flags]
			internal enum AVPPlayerOpenOptionsFlags: int
			{
				None = 0,
			};

			// NOTE: The layout of this structure is important - if adding anything put it at the end, make sure alignment is 4 bytes and DO NOT USE bool
			[StructLayout(LayoutKind.Sequential)]
			internal struct AVPPlayerOpenOptions
			{
				internal long fileOffset;
				internal AVPPlayerOpenOptionsForceFileFormat forceFileFormat;
				internal AVPPlayerOpenOptionsFlags flags;
			};

			[Flags]
			internal enum AVPPlayerStatus : int
			{
				Unknown                   = 0,
				ReadyToPlay               = 1 <<  0,
				Playing                   = 1 <<  1,
				Paused                    = 1 <<  2,
				Finished                  = 1 <<  3,
				Seeking                   = 1 <<  4,
				Buffering                 = 1 <<  5,
				Stalled                   = 1 <<  6,
				ExternalPlaybackActive    = 1 <<  7,
				Cached                    = 1 <<  8,
				FinishedSeeking           = 1 <<  9,

				UpdatedAssetInfo          = 1 << 16,
				UpdatedTexture            = 1 << 17,
				UpdatedBufferedTimeRanges = 1 << 18,
				UpdatedSeekableTimeRanges = 1 << 19,
				UpdatedText               = 1 << 20,
				UpdatedTextureTransform   = 1 << 21,

				HasVideo                  = 1 << 24,
				HasAudio                  = 1 << 25,
				HasText                   = 1 << 26,
				HasMetadata               = 1 << 27,
				HasVariants               = 1 << 28,

				Failed                    = 1 << 31
			}

			[Flags]
			internal enum AVPPlayerFlags : int
			{
				None                  = 0,
				Looping               = 1 <<  0,
				Muted                 = 1 <<  1,
				AllowExternalPlayback = 1 <<  2,
				ResumePlayback        = 1 << 16,	// iOS only, resumes playback after audio session route change
				Dirty                 = 1 << 31
			}

			internal enum AVPPlayerExternalPlaybackVideoGravity : int
			{
				Resize,
				ResizeAspect,
				ResizeAspectFill
			};

			[StructLayout(LayoutKind.Sequential)]
			internal struct AVPPlayerSize
			{
				internal float width;
				internal float height;
				public static readonly AVPPlayerSize Zero = new ()
				{
					width = 0.0f,
					height = 0.0f
				};
			}

			[StructLayout(LayoutKind.Sequential)]
			internal struct AVPAffineTransform
			{
				internal float a;
				internal float b;
				internal float c;
				internal float d;
				internal float tx;
				internal float ty;

				public static readonly AVPAffineTransform Identity = new()
				{
					a = 1.0f,
					b = 0.0f, 
					c = 0.0f, 
					d = 1.0f, 
					tx = 0.0f, 
					ty = 0.0f
				};

				public override string ToString()
				{
					return $"{{ {a}, {b}, {c}, {d}, {tx}, {ty} }}";
				}
            }

			[Flags]
			internal enum AVPPlayerAssetFlags : int
			{
				None                  = 0,
				CompatibleWithAirPlay = 1 << 0,
			};

			[StructLayout(LayoutKind.Sequential)]
			internal struct AVPPlayerAssetInfo
			{
				internal double duration;
				internal AVPPlayerSize dimensions;
				internal float frameRate;
				internal int videoTrackCount;
				internal int audioTrackCount;
				internal int textTrackCount;
				internal int variantCount;
				internal AVPPlayerAssetFlags flags;
			}

			[Flags]
			internal enum AVPPlayerTrackFlags: int
			{
				Default = 1 << 0,
			}

			internal enum AVPPlayerVideoTrackStereoMode: int
			{
				Unknown,
				Monoscopic,
				StereoscopicTopBottom,
				StereoscopicLeftRight,
				StereoscopicCustom,
				StereoscopicRightLeft,
				StereoscopicTwoTextures,
			}

			[Flags]
			internal enum AVPPlayerVideoTrackFlags: int
			{
				HasAlpha = 1 << 0,
			}

			[StructLayout(LayoutKind.Sequential)]
			internal struct AVPPlayerVideoTrackInfo
			{
				[MarshalAs(UnmanagedType.LPWStr)] internal string name;
				[MarshalAs(UnmanagedType.LPWStr)] internal string language;
				internal int trackId;
				internal float estimatedDataRate;
				internal uint codecSubtype;
				internal AVPPlayerTrackFlags flags;

				internal AVPPlayerSize dimensions;
				internal float frameRate;
				internal AVPAffineTransform transform;
				internal AVPPlayerVideoTrackStereoMode stereoMode;
				internal int bitsPerComponent;
				internal AVPPlayerVideoTrackFlags videoTrackFlags;

				internal Matrix4x4 yCbCrTransform;

				public static readonly AVPPlayerVideoTrackInfo Default = new()
				{
					name = null,
					language = null,
					trackId = -1,
					estimatedDataRate = 0,
					codecSubtype = 0,
					flags = 0,
					dimensions = AVPPlayerSize.Zero,
					frameRate = 0.0f,
					transform = AVPAffineTransform.Identity,
					stereoMode = AVPPlayerVideoTrackStereoMode.Unknown,
					bitsPerComponent = 0,
					videoTrackFlags = 0,
					yCbCrTransform = Matrix4x4.identity
				};
			}

			[StructLayout(LayoutKind.Sequential)]
			internal struct AVPPlayerAudioTrackInfo
			{
				[MarshalAs(UnmanagedType.LPWStr)] internal string name;
				[MarshalAs(UnmanagedType.LPWStr)] internal string language;
				internal int trackId;
				internal float estimatedDataRate;
				internal uint codecSubtype;
				internal AVPPlayerTrackFlags flags;

				internal double sampleRate;
				internal uint channelCount;
				internal uint channelLayoutTag;
				internal AudioChannelMaskFlags channelBitmap;

				public static readonly AVPPlayerAudioTrackInfo Default = new()
				{
					name = null,
					language = null,
					trackId = -1,
					estimatedDataRate = 0,
					codecSubtype = 0,
					flags = 0,
					sampleRate = 0.0,
					channelCount = 0,
					channelLayoutTag = 0,
					channelBitmap = AudioChannelMaskFlags.Unspecified
				};
			}

			[StructLayout(LayoutKind.Sequential)]
			internal struct AVPPlayerTextTrackInfo
			{
				[MarshalAs(UnmanagedType.LPWStr)] internal string name;
				[MarshalAs(UnmanagedType.LPWStr)] internal string language;
				internal int trackId;
				internal float estimatedDataRate;
				internal uint codecSubtype;
				internal AVPPlayerTrackFlags flags;

				public static readonly AVPPlayerTextTrackInfo Default = new()
				{
					name = null,
					language = null,
					trackId = -1,
					estimatedDataRate = 0,
					codecSubtype = 0,
					flags = 0
				};
			}

			internal enum AVPPlayerVideoRange : int
			{
				SDR,
				HLG,
				PQ
			}

			[StructLayout(LayoutKind.Sequential)]
			internal struct AVPPlayerVariantInfo
			{
				// Video
				internal int averageDataRate;
				internal int peakDataRate;
				internal CodecType videoCodecType;
				internal float frameRate;
				internal AVPPlayerSize dimensions;
				internal AVPPlayerVideoRange videoRange;

				// Audio
				internal CodecType audioCodecType;
			}

			[StructLayout(LayoutKind.Sequential)]
			internal struct AVPPlayerTimeRange
			{
				internal double start;
				internal double duration;
			};

			[StructLayout(LayoutKind.Sequential)]
			internal struct AVPPlayerState
			{
				internal AVPPlayerStatus status;
				internal double currentTime;
				internal double currentDate;
				internal int selectedVideoTrack;
				internal int selectedAudioTrack;
				internal int selectedTextTrack;
				internal int bufferedTimeRangesCount;
				internal int seekableTimeRangesCount;
				internal int audioCaptureBufferedSamplesCount;
				internal int selectedVariant;
			}

			internal enum AVPPlayerTextureFormat: int
			{
				Unknown,
				BGRA8,
				R8,
				RG8,
				BC1,
				BC3,
				BC4,
				BC5,
				BC7,
				BGR10A2,
				R16,
				RG16,
				BGR10XR,
				RGBA16Float,
				AndroidOES,
			}

			[StructLayout(LayoutKind.Sequential)]
			internal struct AVPPlayerTexturePlane
			{
				internal IntPtr plane;
				internal int width;
				internal int height;
				internal AVPPlayerTextureFormat textureFormat;
			}

			[Flags]
			internal enum AVPPlayerTextureFlags: int
			{
				None      = 0,
				Flipped   = 1 << 0,
				Linear    = 1 << 1,
				Mipmapped = 1 << 2,
				YCbCr     = 1 << 3,
			}

			internal enum AVPPlayerTextureYCbCrMatrix: int
			{
				Identity,
				ITU_R_601,
				ITU_R_709,
			}

			[StructLayout(LayoutKind.Sequential)]
			internal struct AVPPlayerTexture
			{
				[MarshalAs(UnmanagedType.ByValArray, SizeConst=4)]
				internal AVPPlayerTexturePlane[] planes;
				internal long itemTime;
				internal int frameCounter;
				internal int planeCount;
				internal AVPPlayerTextureFlags flags;
				internal AVPPlayerTextureYCbCrMatrix YCbCrMatrix;
			};

			[StructLayout(LayoutKind.Sequential)]
			internal struct AVPPlayerText
			{
				internal IntPtr buffer;
				internal long itemTime;
				internal int length;
				internal int sequence;
			};

			internal enum AVPPlayerTrackType: int
			{
				Video,
				Audio,
				Text
			};

			internal static string GetPluginVersion()
			{
				return System.Runtime.InteropServices.Marshal.PtrToStringAnsi(AVPPluginGetVersionStringPointer());
			}

#if !UNITY_EDITOR && (UNITY_IOS || UNITY_TVOS || UNITY_VISIONOS)
	#if UNITY_2022_1_OR_NEWER
			[DllImport(PluginName)]
			internal static extern void AVPUnityRegisterPlugin(IntPtr fn);

			delegate void UnityRegisterPluginDelegate(IntPtr loadFn, IntPtr unloadFn);

		#if UNITY_6000_0_OR_NEWER
			private const string UnityRegisterPluginEntryPoint = "UnityRegisterPlugin";
		#else
			private const string UnityRegisterPluginEntryPoint = "UnityRegisterRenderingPluginV5";
		#endif

			[DllImport(PluginName, EntryPoint = UnityRegisterPluginEntryPoint)]
			[AOT.MonoPInvokeCallback (typeof(UnityRegisterPluginDelegate))]
			internal static extern void UnityRegisterPlugin(IntPtr loadFn, IntPtr unloadFn);

			internal static void AVPPluginBootstrap()
			{
				UnityRegisterPluginDelegate unityRegisterPluginDelegate = UnityRegisterPlugin;
				IntPtr pFn = Marshal.GetFunctionPointerForDelegate(unityRegisterPluginDelegate);
				AVPUnityRegisterPlugin(pFn);
			}
	#else
			[DllImport(PluginName)]
			internal static extern void AVPPluginBootstrap();
	#endif
#elif !UNITY_EDITOR && (UNITY_ANDROID)
			internal static void AVPPluginBootstrap()
			{
				AndroidJavaClass activityClass = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
				if (activityClass != null)
				{
					AndroidJavaObject activityContext = activityClass.GetStatic<AndroidJavaObject>("currentActivity");
					if (activityContext != null)
					{
						AndroidJavaObject avProVideoManager = new AndroidJavaObject("com.renderheads.AVPro.Video.Manager");
						if (avProVideoManager != null)
						{
							avProVideoManager.CallStatic("SetContext", activityContext);
						}
					}
				}
				// TODO: Handle failure?
			}
#elif !UNITY_EDITOR && ( UNITY_OPENHARMONY )
			internal static void AVPPluginBootstrap()
			{
				Debug.Log("UNITY_OPENHARMONY: Calling Bootstrap");
				OpenHarmonyJSClass openHarmonyJSClass = new OpenHarmonyJSClass("Manager");
		        openHarmonyJSClass.CallStatic( "Bootstrap" );
			}
#endif

			[DllImport(PluginName)]
			private static extern IntPtr AVPPluginGetVersionStringPointer();

			[DllImport(PluginName)]
			internal static extern IntPtr AVPPluginGetRenderEventFunction();

			[DllImport(PluginName)]
			internal static extern IntPtr AVPPluginMakePlayer(AVPPlayerSettings settings);

			[DllImport(PluginName)]
			internal static extern void AVPPlayerRelease(IntPtr player);

			[DllImport(PluginName)]
			internal static extern AVPPlayerFeatureFlags AVPPlayerGetSupportedFeatures(IntPtr player);

			[DllImport(PluginName)]
			internal static extern void AVPPlayerUpdate(IntPtr _player);

			[DllImport(PluginName)]
			internal static extern void AVPPlayerGetState(IntPtr player, ref AVPPlayerState state);

			[DllImport(PluginName)]
			internal static extern void AVPPlayerSetFlags(IntPtr player, int flags);

			[DllImport(PluginName)]
			internal static extern void AVPPlayerGetAssetInfo(IntPtr player, ref AVPPlayerAssetInfo info);

			[DllImport(PluginName)]
			internal static extern void AVPPlayerGetVideoTrackInfo(IntPtr player, int index, ref AVPPlayerVideoTrackInfo info);

			[DllImport(PluginName)]
			internal static extern void AVPPlayerGetAudioTrackInfo(IntPtr player, int index, ref AVPPlayerAudioTrackInfo info);

			[DllImport(PluginName)]
			internal static extern void AVPPlayerGetTextTrackInfo(IntPtr player, int index, ref AVPPlayerTextTrackInfo info);

			[DllImport( PluginName )]
			internal static extern void AVPPlayerGetVariantInfo(IntPtr player, int index, ref AVPPlayerVariantInfo info);

			[DllImport( PluginName )]
			internal static extern void AVPPlayerSelectVariant(IntPtr player, int index);

			[DllImport(PluginName)]
			internal static extern void AVPPlayerGetBufferedTimeRanges(IntPtr player, AVPPlayerTimeRange[] ranges, int count);

			[DllImport(PluginName)]
			internal static extern void AVPPlayerGetSeekableTimeRanges(IntPtr player, AVPPlayerTimeRange[] ranges, int count);

			[DllImport(PluginName)]
			internal static extern void AVPPlayerGetTexture(IntPtr player, ref AVPPlayerTexture texture);

			[DllImport(PluginName)]
			internal static extern void AVPPlayerGetText(IntPtr player, ref AVPPlayerText text);

			[DllImport(PluginName)]
			internal static extern void AVPPlayerSetPlayerSettings(IntPtr player, AVPPlayerSettings settings);

			[DllImport(PluginName)]
			[return: MarshalAs(UnmanagedType.U1)]
			internal static extern bool AVPPlayerOpenURL(IntPtr player, string url, string headers, AVPPlayerOpenOptions options);

			[DllImport(PluginName)]
			internal static extern void AVPPlayerClose(IntPtr player);

			[DllImport(PluginName)]
			internal static extern int AVPPlayerGetAudio(IntPtr player, float[] buffer, int length);

			[DllImport(PluginName)]
			internal static extern void AVPPlayerSetAudioHeadRotation(IntPtr _player, float[] rotation);

			[DllImport(PluginName)]
			internal static extern void AVPPlayerSetPositionTrackingEnabled(IntPtr _player, bool enabled);

			[DllImport(PluginName)]
			internal static extern void AVPPlayerSetAudioFocusEnabled(IntPtr _player, bool enabled);

			[DllImport(PluginName)]
			internal static extern void AVPPlayerSetAudioFocusProperties(IntPtr _player, float offFocusLevel, float widthDegrees);

			[DllImport(PluginName)]
			internal static extern void AVPPlayerSetAudioFocusRotation(IntPtr _player, float[] rotation);

			[DllImport(PluginName)]
			internal static extern void AVPPlayerResetAudioFocus(IntPtr _player);

			[DllImport(PluginName)]
			internal static extern void AVPPlayerSetRate(IntPtr player, float rate);

			[DllImport(PluginName)]
			internal static extern void AVPPlayerSetVolume(IntPtr player, float volume);

			[DllImport(PluginName)]
			internal static extern void AVPPlayerSetExternalPlaybackVideoGravity(IntPtr player, AVPPlayerExternalPlaybackVideoGravity gravity);

			[DllImport(PluginName)]
			internal static extern void AVPPlayerSeek(IntPtr player, double toTime, double toleranceBefore, double toleranceAfter);

			[DllImport(PluginName)]
			internal static extern void AVPPlayerSetKeyServerAuthToken(IntPtr player, string token);

			[DllImport(PluginName)]
			internal static extern void AVPPlayerSetKeyServerURL(IntPtr player, string url);

			[DllImport(PluginName)]
			internal static extern void AVPPlayerSetDecryptionKey(IntPtr player, byte[] key, int length);

			[DllImport(PluginName)]
			[return: MarshalAs(UnmanagedType.I1)]
			internal static extern bool AVPPlayerSetTrack(IntPtr player, AVPPlayerTrackType type, int index);

			public struct MediaCachingOptions
			{
				public double minimumRequiredBitRate;
				public float minimumRequiredResolution_width;
				public float minimumRequiredResolution_height;
				public string title;
				public IntPtr artwork;
				public int artworkLength;
			}

			[DllImport(PluginName)]
			public static extern void AVPPlayerCacheMediaForURL(IntPtr player, string url, string headers, MediaCachingOptions options);

			[DllImport(PluginName)]
			public static extern void AVPPlayerCancelDownloadOfMediaForURL(IntPtr player, string url);

			[DllImport(PluginName)]
			public static extern void AVPPlayerPauseDownloadOfMediaForURL(IntPtr player, string url);

			[DllImport(PluginName)]
			public static extern void AVPPlayerResumeDownloadOfMediaForURL(IntPtr player, string url);

			[DllImport(PluginName)]
			public static extern void AVPPlayerRemoveCachedMediaForURL(IntPtr player, string url);

			[DllImport(PluginName)]
			public static extern int AVPPlayerGetCachedMediaStatusForURL(IntPtr player, string url, ref float progress);
		}
	}
}

#endif
