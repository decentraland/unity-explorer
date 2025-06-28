//-----------------------------------------------------------------------------
// Copyright 2015-2024 RenderHeads Ltd.  All rights reserved.
//-----------------------------------------------------------------------------

#if UNITY_2017_2_OR_NEWER && (UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX || (!UNITY_EDITOR && (UNITY_IOS || UNITY_TVOS || UNITY_VISIONOS || UNITY_ANDROID || UNITY_OPENHARMONY)))

using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace RenderHeads.Media.AVProVideo
{
	internal static class PlatformMediaPlayerExtensions
	{
		// AVPPlayerStatus

		internal static bool IsReadyToPlay(this PlatformMediaPlayer.Native.AVPPlayerStatus status)
		{
			return (status & PlatformMediaPlayer.Native.AVPPlayerStatus.ReadyToPlay) == PlatformMediaPlayer.Native.AVPPlayerStatus.ReadyToPlay;
		}

		internal static bool IsPlaying(this PlatformMediaPlayer.Native.AVPPlayerStatus status)
		{
			return (status & PlatformMediaPlayer.Native.AVPPlayerStatus.Playing) == PlatformMediaPlayer.Native.AVPPlayerStatus.Playing;
		}

		internal static bool IsPaused(this PlatformMediaPlayer.Native.AVPPlayerStatus status)
		{
			return (status & PlatformMediaPlayer.Native.AVPPlayerStatus.Paused) == PlatformMediaPlayer.Native.AVPPlayerStatus.Paused;
		}

		internal static bool IsFinished(this PlatformMediaPlayer.Native.AVPPlayerStatus status)
		{
			return (status & PlatformMediaPlayer.Native.AVPPlayerStatus.Finished) == PlatformMediaPlayer.Native.AVPPlayerStatus.Finished;
		}

		internal static bool IsSeeking(this PlatformMediaPlayer.Native.AVPPlayerStatus status)
		{
			return (status & PlatformMediaPlayer.Native.AVPPlayerStatus.Seeking) == PlatformMediaPlayer.Native.AVPPlayerStatus.Seeking;
		}

		internal static bool IsBuffering(this PlatformMediaPlayer.Native.AVPPlayerStatus status)
		{
			return (status & PlatformMediaPlayer.Native.AVPPlayerStatus.Buffering) == PlatformMediaPlayer.Native.AVPPlayerStatus.Buffering;
		}

		internal static bool IsStalled(this PlatformMediaPlayer.Native.AVPPlayerStatus status)
		{
			return (status & PlatformMediaPlayer.Native.AVPPlayerStatus.Stalled) == PlatformMediaPlayer.Native.AVPPlayerStatus.Stalled;
		}

		internal static bool IsExternalPlaybackActive(this PlatformMediaPlayer.Native.AVPPlayerStatus status)
		{
			return (status & PlatformMediaPlayer.Native.AVPPlayerStatus.ExternalPlaybackActive) == PlatformMediaPlayer.Native.AVPPlayerStatus.ExternalPlaybackActive;
		}

		internal static bool IsCached(this PlatformMediaPlayer.Native.AVPPlayerStatus status)
		{
			return (status & PlatformMediaPlayer.Native.AVPPlayerStatus.Cached) == PlatformMediaPlayer.Native.AVPPlayerStatus.Cached;
		}

		internal static bool HasFinishedSeeking(this PlatformMediaPlayer.Native.AVPPlayerStatus status)
		{
			return (status & PlatformMediaPlayer.Native.AVPPlayerStatus.FinishedSeeking) == PlatformMediaPlayer.Native.AVPPlayerStatus.FinishedSeeking;
		}

		internal static bool HasUpdatedAssetInfo(this PlatformMediaPlayer.Native.AVPPlayerStatus status)
		{
			return (status & PlatformMediaPlayer.Native.AVPPlayerStatus.UpdatedAssetInfo) == PlatformMediaPlayer.Native.AVPPlayerStatus.UpdatedAssetInfo;
		}

		internal static bool HasUpdatedTexture(this PlatformMediaPlayer.Native.AVPPlayerStatus status)
		{
			return (status & PlatformMediaPlayer.Native.AVPPlayerStatus.UpdatedTexture) == PlatformMediaPlayer.Native.AVPPlayerStatus.UpdatedTexture;
		}

		internal static bool HasUpdatedTextureTransform(this PlatformMediaPlayer.Native.AVPPlayerStatus status)
		{
			return (status & PlatformMediaPlayer.Native.AVPPlayerStatus.UpdatedTextureTransform) == PlatformMediaPlayer.Native.AVPPlayerStatus.UpdatedTextureTransform;
		}

		internal static bool HasUpdatedBufferedTimeRanges(this PlatformMediaPlayer.Native.AVPPlayerStatus status)
		{
			return (status & PlatformMediaPlayer.Native.AVPPlayerStatus.UpdatedBufferedTimeRanges) == PlatformMediaPlayer.Native.AVPPlayerStatus.UpdatedBufferedTimeRanges;
		}

		internal static bool HasUpdatedSeekableTimeRanges(this PlatformMediaPlayer.Native.AVPPlayerStatus status)
		{
			return (status & PlatformMediaPlayer.Native.AVPPlayerStatus.UpdatedSeekableTimeRanges) == PlatformMediaPlayer.Native.AVPPlayerStatus.UpdatedSeekableTimeRanges;
		}

		internal static bool HasUpdatedText(this PlatformMediaPlayer.Native.AVPPlayerStatus status)
		{
			return (status & PlatformMediaPlayer.Native.AVPPlayerStatus.UpdatedText) == PlatformMediaPlayer.Native.AVPPlayerStatus.UpdatedText;
		}

		internal static bool HasVideo(this PlatformMediaPlayer.Native.AVPPlayerStatus status)
		{
			return (status & PlatformMediaPlayer.Native.AVPPlayerStatus.HasVideo) == PlatformMediaPlayer.Native.AVPPlayerStatus.HasVideo;
		}

		internal static bool HasAudio(this PlatformMediaPlayer.Native.AVPPlayerStatus status)
		{
			return (status & PlatformMediaPlayer.Native.AVPPlayerStatus.HasAudio) == PlatformMediaPlayer.Native.AVPPlayerStatus.HasAudio;
		}

		internal static bool HasText(this PlatformMediaPlayer.Native.AVPPlayerStatus status)
		{
			return (status & PlatformMediaPlayer.Native.AVPPlayerStatus.HasText) == PlatformMediaPlayer.Native.AVPPlayerStatus.HasText;
		}

		internal static bool HasMetadata(this PlatformMediaPlayer.Native.AVPPlayerStatus status)
		{
			return (status & PlatformMediaPlayer.Native.AVPPlayerStatus.HasMetadata) == PlatformMediaPlayer.Native.AVPPlayerStatus.HasMetadata;
		}

		internal static bool HasFailed(this PlatformMediaPlayer.Native.AVPPlayerStatus status)
		{
			return (status & PlatformMediaPlayer.Native.AVPPlayerStatus.Failed) == PlatformMediaPlayer.Native.AVPPlayerStatus.Failed;
		}

		internal static bool HasVariants(this PlatformMediaPlayer.Native.AVPPlayerStatus status)
		{
			return (status & PlatformMediaPlayer.Native.AVPPlayerStatus.HasVariants) == PlatformMediaPlayer.Native.AVPPlayerStatus.HasVariants;
		}

		// AVPPlayerFlags

		internal static bool IsLooping(this PlatformMediaPlayer.Native.AVPPlayerFlags flags)
		{
			return (flags & PlatformMediaPlayer.Native.AVPPlayerFlags.Looping) == PlatformMediaPlayer.Native.AVPPlayerFlags.Looping;
		}

		internal static PlatformMediaPlayer.Native.AVPPlayerFlags SetLooping(this PlatformMediaPlayer.Native.AVPPlayerFlags flags, bool b)
		{
			if (flags.IsLooping() ^ b)
			{
				flags = (b ? flags | PlatformMediaPlayer.Native.AVPPlayerFlags.Looping
				           : flags & ~PlatformMediaPlayer.Native.AVPPlayerFlags.Looping) | PlatformMediaPlayer.Native.AVPPlayerFlags.Dirty;
			}
			return flags;
		}

		internal static bool IsMuted(this PlatformMediaPlayer.Native.AVPPlayerFlags flags)
		{
			return (flags & PlatformMediaPlayer.Native.AVPPlayerFlags.Muted) == PlatformMediaPlayer.Native.AVPPlayerFlags.Muted;
		}

		internal static PlatformMediaPlayer.Native.AVPPlayerFlags SetMuted(this PlatformMediaPlayer.Native.AVPPlayerFlags flags, bool b)
		{
			if (flags.IsMuted() ^ b)
			{
				flags = (b ? flags | PlatformMediaPlayer.Native.AVPPlayerFlags.Muted
				           : flags & ~PlatformMediaPlayer.Native.AVPPlayerFlags.Muted) | PlatformMediaPlayer.Native.AVPPlayerFlags.Dirty;
			}
			return flags;
		}

		internal static bool IsExternalPlaybackAllowed(this PlatformMediaPlayer.Native.AVPPlayerFlags flags)
		{
			return (flags & PlatformMediaPlayer.Native.AVPPlayerFlags.AllowExternalPlayback) == PlatformMediaPlayer.Native.AVPPlayerFlags.AllowExternalPlayback;
		}

		internal static PlatformMediaPlayer.Native.AVPPlayerFlags SetAllowExternalPlayback(this PlatformMediaPlayer.Native.AVPPlayerFlags flags, bool b)
		{
			if (flags.IsExternalPlaybackAllowed() ^ b)
			{
				flags = (b ? flags |  PlatformMediaPlayer.Native.AVPPlayerFlags.AllowExternalPlayback
				           : flags & ~PlatformMediaPlayer.Native.AVPPlayerFlags.AllowExternalPlayback) | PlatformMediaPlayer.Native.AVPPlayerFlags.Dirty;
			}
			return flags;
		}

		internal static bool ResumePlayback(this PlatformMediaPlayer.Native.AVPPlayerFlags flags)
		{
			return (flags & PlatformMediaPlayer.Native.AVPPlayerFlags.ResumePlayback) == PlatformMediaPlayer.Native.AVPPlayerFlags.ResumePlayback;
		}

		internal static PlatformMediaPlayer.Native.AVPPlayerFlags SetResumePlayback(this PlatformMediaPlayer.Native.AVPPlayerFlags flags, bool b)
		{
			if (flags.ResumePlayback() ^ b)
			{
				flags = (b ? flags | PlatformMediaPlayer.Native.AVPPlayerFlags.ResumePlayback
				           : flags & ~PlatformMediaPlayer.Native.AVPPlayerFlags.ResumePlayback) | PlatformMediaPlayer.Native.AVPPlayerFlags.Dirty;
			}
			return flags;
		}

		internal static bool IsDirty(this PlatformMediaPlayer.Native.AVPPlayerFlags flags)
		{
			return (flags & PlatformMediaPlayer.Native.AVPPlayerFlags.Dirty) == PlatformMediaPlayer.Native.AVPPlayerFlags.Dirty;
		}

		internal static PlatformMediaPlayer.Native.AVPPlayerFlags SetDirty(this PlatformMediaPlayer.Native.AVPPlayerFlags flags, bool b)
		{
			if (flags.IsDirty() ^ b)
			{
				flags = b ? flags | PlatformMediaPlayer.Native.AVPPlayerFlags.Dirty : flags & ~PlatformMediaPlayer.Native.AVPPlayerFlags.Dirty;
			}
			return flags;
		}

		// MARK: AVPPlayerAssetFlags

		internal static bool IsCompatibleWithAirPlay(this PlatformMediaPlayer.Native.AVPPlayerAssetFlags flags)
		{
			return (flags & PlatformMediaPlayer.Native.AVPPlayerAssetFlags.CompatibleWithAirPlay) == PlatformMediaPlayer.Native.AVPPlayerAssetFlags.CompatibleWithAirPlay;
		}

		// MARK: AVPPlayerTrackFlags

		internal static bool IsDefault(this PlatformMediaPlayer.Native.AVPPlayerTrackFlags flags)
		{
			return (flags & PlatformMediaPlayer.Native.AVPPlayerTrackFlags.Default) == PlatformMediaPlayer.Native.AVPPlayerTrackFlags.Default;
		}

		// AVPPlayerTextureFlags

		internal static bool IsFlipped(this PlatformMediaPlayer.Native.AVPPlayerTextureFlags flags)
		{
			return (flags & PlatformMediaPlayer.Native.AVPPlayerTextureFlags.Flipped) == PlatformMediaPlayer.Native.AVPPlayerTextureFlags.Flipped;
		}

		internal static bool IsLinear(this PlatformMediaPlayer.Native.AVPPlayerTextureFlags flags)
		{
			return (flags & PlatformMediaPlayer.Native.AVPPlayerTextureFlags.Linear) == PlatformMediaPlayer.Native.AVPPlayerTextureFlags.Linear;
		}

		internal static bool IsMipmapped(this PlatformMediaPlayer.Native.AVPPlayerTextureFlags flags)
		{
			return (flags & PlatformMediaPlayer.Native.AVPPlayerTextureFlags.Mipmapped) == PlatformMediaPlayer.Native.AVPPlayerTextureFlags.Mipmapped;
		}
	}
}

#endif
