using UnityEngine;

//-----------------------------------------------------------------------------
// Copyright 2015-2022 RenderHeads Ltd.  All rights reserved.
//-----------------------------------------------------------------------------

namespace RenderHeads.Media.AVProVideo
{
	public partial class MediaPlayer : MonoBehaviour
	{
		// Event state
		private bool _eventFired_MetaDataReady = false;
		private bool _eventFired_ReadyToPlay = false;
		private bool _eventFired_Started = false;
		private bool _eventFired_FirstFrameReady = false;
		private bool _eventFired_FinishedPlaying = false;
		private bool _eventState_PlaybackBuffering = false;
		private bool _eventState_PlaybackSeeking = false;
		private bool _eventState_PlaybackStalled = false;
		private int _eventState_PreviousWidth = 0;
		private int _eventState_PreviousHeight = 0;
		private int _previousSubtitleIndex = -1;
		private bool _finishedFrameOpenCheck = false;
		private bool _eventState_Paused = false;

		#if UNITY_EDITOR
		public static MediaPlayerLoadEvent InternalMediaLoadedEvent = new MediaPlayerLoadEvent();
		#endif

		private void ResetEvents()
		{
			_eventFired_MetaDataReady = false;
			_eventFired_ReadyToPlay = false;
			_eventFired_Started = false;
			_eventFired_FirstFrameReady = false;
			_eventFired_FinishedPlaying = false;
			_eventState_PlaybackBuffering = false;
			_eventState_PlaybackSeeking = false;
			_eventState_PlaybackStalled = false;
			_eventState_PreviousWidth = 0;
			_eventState_PreviousHeight = 0;
			_previousSubtitleIndex = -1;
			_finishedFrameOpenCheck = false;
		}

		private void CheckAndClearStartedAndFinishedEvents()
		{
			//NOTE: Fixes a bug where the event was being fired immediately, so when a file is opened, the finishedPlaying fired flag gets set but
			//is then set to true immediately afterwards due to the returned value
			_finishedFrameOpenCheck = false;
			if (IsHandleEvent(MediaPlayerEvent.EventType.FinishedPlaying))
			{
				if (FireEventIfPossible(MediaPlayerEvent.EventType.FinishedPlaying, _eventFired_FinishedPlaying))
				{
					_eventFired_FinishedPlaying = !_finishedFrameOpenCheck;
				}
			}

			if (_eventFired_FinishedPlaying &&
				IsHandleEvent(MediaPlayerEvent.EventType.FinishedPlaying) &&
				_controlInterface.IsPlaying() &&
				!_controlInterface.IsFinished())
			{
				bool reset = true;
// RJT NOTE: Commented out for now as seems over-aggressive and can lead to freeze conditions as seen in: https://github.com/RenderHeads/UnityPlugin-AVProVideo/issues/1692
// - If we need to reinstate then we'd likely need considerably more tolerance, especially on slower machines
#if false//UNITY_EDITOR_WIN || (!UNITY_EDITOR && (UNITY_STANDALONE_WIN || UNITY_WSA))
				reset = false;
				if (_infoInterface.HasVideo())
				{
					// Some streaming HLS/Dash content don't provide a frame rate
					if (_infoInterface.GetVideoFrameRate() > 0f)
					{
						// Don't reset if within a frame of the end of the video, important for time > duration workaround
						float secondsPerFrame = 1f / _infoInterface.GetVideoFrameRate();
						if (_infoInterface.GetDuration() - _controlInterface.GetCurrentTime() > secondsPerFrame)
						{
							reset = true;
						}
					}
					else
					{
						// Just check if we're not beyond the duration
						if (_controlInterface.GetCurrentTime() < _infoInterface.GetDuration())
						{
							reset = true;
						}
					}
				}
				else
				{
					// For audio only media just check if we're not beyond the duration
					if (_controlInterface.GetCurrentTime() < _infoInterface.GetDuration())
					{
						reset = true;
					}
				}
#endif
				if (reset)
				{
					//Debug.Log("Reset");
					_eventFired_FinishedPlaying = false;
				}
			}
		}

		private void HandleOneShotEvents()
		{
			_eventFired_MetaDataReady = FireEventIfPossible(MediaPlayerEvent.EventType.MetaDataReady, _eventFired_MetaDataReady);
			_eventFired_ReadyToPlay = FireEventIfPossible(MediaPlayerEvent.EventType.ReadyToPlay, _eventFired_ReadyToPlay);
			_eventFired_Started = FireEventIfPossible(MediaPlayerEvent.EventType.Started, _eventFired_Started);
			_eventFired_FirstFrameReady = FireEventIfPossible(MediaPlayerEvent.EventType.FirstFrameReady, _eventFired_FirstFrameReady);
		}

		private void HandleRecurringEvents()
		{
			// Subtitle changing
			if (FireEventIfPossible(MediaPlayerEvent.EventType.SubtitleChange, false))
			{
				_previousSubtitleIndex = _subtitlesInterface.GetSubtitleIndex();
			}

			// Resolution changing
			if (FireEventIfPossible(MediaPlayerEvent.EventType.ResolutionChanged, false))
			{
				_eventState_PreviousWidth = _infoInterface.GetVideoWidth();
				_eventState_PreviousHeight = _infoInterface.GetVideoHeight();
			}

			// Stalling
			if (IsHandleEvent(MediaPlayerEvent.EventType.Stalled))
			{
				bool newState = _infoInterface.IsPlaybackStalled();
				if (newState != _eventState_PlaybackStalled)
				{
					_eventState_PlaybackStalled = newState;

					var newEvent = _eventState_PlaybackStalled ? MediaPlayerEvent.EventType.Stalled : MediaPlayerEvent.EventType.Unstalled;
					FireEventIfPossible(newEvent, false);
				}
			}
			// Seeking
			if (IsHandleEvent(MediaPlayerEvent.EventType.StartedSeeking))
			{
				bool newState = _controlInterface.IsSeeking();
				if (newState != _eventState_PlaybackSeeking)
				{
					_eventState_PlaybackSeeking = newState;

					var newEvent = _eventState_PlaybackSeeking ? MediaPlayerEvent.EventType.StartedSeeking : MediaPlayerEvent.EventType.FinishedSeeking;
					FireEventIfPossible(newEvent, false);
				}
			}
			// Buffering
			if (IsHandleEvent(MediaPlayerEvent.EventType.StartedBuffering))
			{
				bool newState = _controlInterface.IsBuffering();
				if (newState != _eventState_PlaybackBuffering)
				{
					_eventState_PlaybackBuffering = newState;

					var newEvent = _eventState_PlaybackBuffering ? MediaPlayerEvent.EventType.StartedBuffering : MediaPlayerEvent.EventType.FinishedBuffering;
					FireEventIfPossible(newEvent, false);
				}
			}

			// Pausing
			if (IsHandleEvent(MediaPlayerEvent.EventType.Paused))
			{
				bool newState = _controlInterface.IsPaused();
				if (newState != _eventState_Paused)
				{
					_eventState_Paused = newState;
					var newEvent = _eventState_Paused ? MediaPlayerEvent.EventType.Paused : MediaPlayerEvent.EventType.Unpaused;
					FireEventIfPossible(newEvent, false);
				}
			}
		}

		private void UpdateEvents()
		{
			if (_controlInterface == null)
				return;
			if (_events == null || !_events.HasListeners())
				return;

			// Reset some event states that can reset during playback
			CheckAndClearStartedAndFinishedEvents();
			
			// Events that can only fire once
			HandleOneShotEvents();

			// Events that can fire multiple times
			HandleRecurringEvents();
		}

		protected bool IsHandleEvent(MediaPlayerEvent.EventType eventType)
		{
			return ((uint)_eventMask & (1 << (int)eventType)) != 0;
		}

		private bool FireEventIfPossible(MediaPlayerEvent.EventType eventType, bool hasFired)
		{
			if (CanFireEvent(eventType, hasFired))
			{
				#if UNITY_EDITOR
				// Special internal global event, called when media is loaded
				// Currently used by the RecentItem class
				if (eventType == MediaPlayerEvent.EventType.Started)
				{
					string fullPath = GetResolvedFilePath(_mediaPath.Path, _mediaPath.PathType);
					InternalMediaLoadedEvent.Invoke(fullPath);
				}
				#endif

				hasFired = true;
				_events.Invoke(this, eventType, ErrorCode.None);
			}
			return hasFired;
		}

		private bool CanFireEvent(MediaPlayerEvent.EventType et, bool hasFired)
		{
			if (_controlInterface == null)
				return false;
			if (_events == null)
				return false;
			if (hasFired)
				return false;
			if (!IsHandleEvent(et))
				return false;

			bool result = false;
			switch (et)
			{
				case MediaPlayerEvent.EventType.FinishedPlaying:
					result = (!_controlInterface.IsLooping() && _controlInterface.CanPlay() && _controlInterface.IsFinished());
					break;
				case MediaPlayerEvent.EventType.MetaDataReady:
					result = (_controlInterface.HasMetaData());
					break;
				case MediaPlayerEvent.EventType.FirstFrameReady:
					// [MOZ 20/1/21] Removed HasMetaData check as preventing the event from being triggered on (i|mac|tv)OS
					result = (_textureInterface != null && _controlInterface.CanPlay() /*&& _controlInterface.HasMetaData()*/ && _textureInterface.GetTextureFrameCount() > 0);
					break;
				case MediaPlayerEvent.EventType.ReadyToPlay:
					result = (!_controlInterface.IsPlaying() && _controlInterface.CanPlay() && !_autoPlayOnStart);
					break;
				case MediaPlayerEvent.EventType.Started:
					result = (_controlInterface.IsPlaying());
					break;
				case MediaPlayerEvent.EventType.SubtitleChange:
				{
					result = (_previousSubtitleIndex != _subtitlesInterface.GetSubtitleIndex());
					if (!result)
					{
						result = _baseMediaPlayer.InternalIsChangedTextCue();
					}
					break;
				}
				case MediaPlayerEvent.EventType.Stalled:
					result = _infoInterface.IsPlaybackStalled();
					break;
				case MediaPlayerEvent.EventType.Unstalled:
					result = !_infoInterface.IsPlaybackStalled();
					break;
				case MediaPlayerEvent.EventType.StartedSeeking:
					result = _controlInterface.IsSeeking();
					break;
				case MediaPlayerEvent.EventType.FinishedSeeking:
					result = !_controlInterface.IsSeeking();
					break;
				case MediaPlayerEvent.EventType.StartedBuffering:
					result = _controlInterface.IsBuffering();
					break;
				case MediaPlayerEvent.EventType.FinishedBuffering:
					result = !_controlInterface.IsBuffering();
					break;
				case MediaPlayerEvent.EventType.ResolutionChanged:
					result = (_infoInterface != null && (_eventState_PreviousWidth != _infoInterface.GetVideoWidth() || _eventState_PreviousHeight != _infoInterface.GetVideoHeight()));
					break;

				case MediaPlayerEvent.EventType.Paused:
					result = _controlInterface.IsPaused();
					break;

				case MediaPlayerEvent.EventType.Unpaused:
					result = !_controlInterface.IsPaused();
					break;

				default:
					Debug.LogWarning("[AVProVideo] Unhandled event type");
					break;
			}
			return result;
		}
	}

}	// namespace RenderHeads.Media.AVProVideo