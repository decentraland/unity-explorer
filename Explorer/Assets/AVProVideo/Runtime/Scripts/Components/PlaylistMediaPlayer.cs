//-----------------------------------------------------------------------------
// Copyright 2015-2025 RenderHeads Ltd.  All rights reserved.
//-----------------------------------------------------------------------------

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RenderHeads.Media.AVProVideo
{
	[System.Serializable]
	public class MediaPlaylist
	{
		[System.Serializable]
		public class MediaItem
		{
			public enum SourceType
			{
				AVProVideoPlayer,
			}

			[SerializeField] 
			public string name = string.Empty;
			
			[SerializeField] 
			public SourceType sourceType = SourceType.AVProVideoPlayer;
			
			[SerializeField] 
			public MediaPath mediaPath = new MediaPath();
			
			[SerializeField] 
			public Texture2D texture = null;
			
			[SerializeField] 
			public float textureDuration;
			
			[SerializeField] 
			public bool loop = false;
			
			[SerializeField] 
			public PlaylistMediaPlayer.StartMode startMode = PlaylistMediaPlayer.StartMode.Immediate;
			
			[SerializeField] 
			public PlaylistMediaPlayer.ProgressMode progressMode = PlaylistMediaPlayer.ProgressMode.OnFinish;
			
			[SerializeField] 
			public float progressTimeSeconds = 0.5f;
			
			[SerializeField] 
			public bool isOverrideTransition = false;
			
			[SerializeField] 
			public PlaylistMediaPlayer.Transition overrideTransition = PlaylistMediaPlayer.Transition.None;
			
			[SerializeField] 
			public float overrideTransitionDuration = 1f;
			
			[SerializeField] 
			public Easing.Preset overrideTransitionEasing = Easing.Preset.Linear;
		}


		[SerializeField] 
		List<MediaItem> _items = new List<MediaItem>(8);
		
		public List<MediaItem> Items
		{
			get
			{
				return _items;
			}
		}

		public bool HasItemAt(int index)
		{
			return index >= 0 && index < _items.Count;
		}
	}

	/// <summary>
	/// This is a BETA component
	/// </summary>
	[AddComponentMenu("AVPro Video/Playlist Media Player", -80)]
	[HelpURL("https://www.renderheads.com/products/avpro-video/")]
	public class PlaylistMediaPlayer : MediaPlayer, ITextureProducer
	{
		public enum Transition
		{
			None,
			Fade,
			Black,
			White,
			Transparent,
			Horiz,
			Vert,
			Diag,
			MirrorH,
			MirrorV,
			MirrorD,
			ScrollV,
			ScrollH,
			Circle,
			Diamond,
			Blinds,
			Arrows,
			SlideH,
			SlideV,
			Zoom,
			RectV,
			Random,
		}

		public enum PlaylistLoopMode
		{
			None,
			Loop,
		}

		public enum StartMode
		{
			Immediate,
			//AfterSeconds,
			Manual,
		}

		public enum ProgressMode
		{
			OnFinish,
			BeforeFinish,
			//AfterTime,
			Manual,
		}
		
		[SerializeField]
		Shader _transitionShader = null;

		[SerializeField]
		MediaPlayer _playerA = null;
		
		[SerializeField]
		MediaPlayer _playerB = null;
		
		[SerializeField] 
		bool _playlistAutoProgress = true;

		[Tooltip("Close the video on the other MediaPlayer when it is not visible any more. This is useful for freeing up memory and GPU decoding resources.")]
		[SerializeField]
		bool _autoCloseVideo = true;

		[SerializeField]
		PlaylistLoopMode _playlistLoopMode = PlaylistLoopMode.None;
		
		[SerializeField]
		MediaPlaylist _playlist = new MediaPlaylist();

		[Tooltip("Pause the previously playing video. This is useful for systems that will struggle to play 2 videos at once")]
		[SerializeField]
		bool _pausePreviousOnTransition = true;

		[SerializeField]
		Transition _defaultTransition = Transition.None;
		
		[SerializeField]
		float _defaultTransitionDuration = 1f;
		
		[SerializeField]
		Easing.Preset _defaultTransitionEasing = Easing.Preset.Linear;

		[SerializeField, Range(0.0f, 1.0f)]
		float _playlistAudioVolume = 1.0f;
		
		[SerializeField]
		bool _playlistAudioMuted = false;

		private static readonly LazyShaderProperty PropFromTex = new LazyShaderProperty("_FromTex");
		private static readonly LazyShaderProperty PropFade = new LazyShaderProperty("_Fade");

		private bool _isPaused = false;
		private int _playlistIndex = 0;
		private MediaPlayer _nextPlayer;
		private Material _material;
		private Transition _currentTransition = Transition.None;
		private string _currentTransitionName = "LERP_NONE";
		private float _currentTransitionDuration = 1f;
		private Easing.Preset _currentTransitionEasing = Easing.Preset.Linear;
		private float _transitionTimer = float.MaxValue;
		private System.Func<float, float> _easeFunc;
		private RenderTexture _rt;
		private MediaPlaylist.MediaItem _currentItem;
		private MediaPlaylist.MediaItem _nextItem;

		public MediaPlayer CurrentPlayer
		{
			get
			{
				if (NextPlayer == _playerA)
				{
					return _playerB;
				}
				return _playerA;
			}
		}

		public MediaPlayer NextPlayer
		{
			get
			{
				return _nextPlayer;
			}
		}

		public MediaPlaylist Playlist
		{
			get
			{
				return _playlist;
			}
		}

		public int PlaylistIndex
		{
			get
			{
				return _playlistIndex;
			}
		}

		public MediaPlaylist.MediaItem PlaylistItem
		{
			get
			{
				return _playlist.HasItemAt(_playlistIndex) ? _playlist.Items[_playlistIndex] : null;
			}
		}

		/// <summary>
		/// The default transition to use if the transition is not overridden in the MediaItem
		/// </summary>
		public Transition DefaultTransition
		{
			get
			{
				return _defaultTransition;
			} 
			set
			{
				_defaultTransition = value;
			}
		}
		
		/// <summary>
		/// The default duration the transition will take (in seconds) if the transition is not overridden in the MediaItem
		/// </summary>
		public float DefaultTransitionDuration
		{
			get
			{
				return _defaultTransitionDuration;
			}
			set
			{
				_defaultTransitionDuration = value;
			}
		}

		/// <summary>
		/// The default easing the transition will use if the transition is not overridden in the MediaItem
		/// </summary>
		public Easing.Preset DefaultTransitionEasing
		{
			get
			{
				return _defaultTransitionEasing;
			}
			set
			{
				_defaultTransitionEasing = value;
			}
		}

		/// <summary>
		/// Closes videos that aren't playing.  This will save memory but adds extra overhead
		/// </summary>
		public bool AutoCloseVideo
		{
			get
			{
				return _autoCloseVideo;
			} 
			set
			{
				_autoCloseVideo = value;
			}
		}

		/// <summary>
		/// None: Do not loop the playlist when the end is reached.<br/>Loop: Rewind the playlist and play again when the each is reached
		/// </summary>
		public PlaylistLoopMode LoopMode
		{
			get
			{
				return _playlistLoopMode;
			}
			set
			{
				_playlistLoopMode = value;
			}
		}

		/// <summary>
		/// Enable the playlist to progress to the next item automatically, or wait for manual trigger via scripting
		/// </summary>
		public bool AutoProgress
		{
			get
			{
				return _playlistAutoProgress;
			} 
			set
			{
				_playlistAutoProgress = value;
			}
		}

		/// <summary>
		/// Returns the IMediaInfo interface for the MediaPlayer that is playing the current active item in the playlist (returned by CurrentPlayer property).  This will change during each transition.
		/// </summary>
		public override IMediaInfo Info
		{
			get
			{
				return CurrentPlayer != null ? CurrentPlayer.Info : null;
			}
		}

		/// <summary>
		/// Returns the IMediaControl interface for the MediaPlayer that is playing the current active item in the playlist (returned by CurrentPlayer property).  This will change during each transition.
		/// </summary>
		public override IMediaControl Control
		{
			get
			{
				return CurrentPlayer != null ? CurrentPlayer.Control : null;
			}
		}

		public override ITextureProducer TextureProducer
		{
			get
			{
				return this;
			}
		}

		public override float AudioVolume
		{
			get
			{
				return _playlistAudioVolume;
			}
			set
			{
				_playlistAudioVolume = Mathf.Clamp01(value);
				if (!IsTransitioning() && CurrentPlayer != null)
				{
					CurrentPlayer.AudioVolume = _playlistAudioVolume;
				}
			}
		}

		public override bool AudioMuted
		{
			get
			{
				return _playlistAudioMuted;
			} 
			set
			{
				_playlistAudioMuted = value;
				if (!IsTransitioning() && CurrentPlayer != null)
				{
					CurrentPlayer.AudioMuted = _playlistAudioMuted;
				}
			}
		}

		public override void Play()
		{
			_isPaused = false;
			if (Control != null)
			{
				Control.Play();
			}
			if (IsTransitioning())
			{
				if (!_pausePreviousOnTransition && NextPlayer.Control != null)
				{
					NextPlayer.Control.Play();
				}
			}
		}

		public override void Pause()
		{
			_isPaused = true;
			if (Control != null)
			{
				Control.Pause();
			}
			if (IsTransitioning())
			{
				if (NextPlayer.Control != null)
				{
					NextPlayer.Control.Pause();
				}
			}
		}

		public bool IsPaused()
		{
			return _isPaused;
		}

		private WaitForEndOfFrame _waitForEndOfFrame = new WaitForEndOfFrame();

		private IEnumerator SwapPlayers()
		{
			// Need to wait for rendering to complete before swapping the players
			yield return _waitForEndOfFrame;

			// Pause the previously playing video
			// This is useful for systems that will struggle to play 2 videos at once
			if (_pausePreviousOnTransition)
			{
				CurrentPlayer.Pause();
			}

			// Tell listeners that the playlist item has changed
			Events.Invoke(this, MediaPlayerEvent.EventType.PlaylistItemChanged, ErrorCode.None);
			
			// Start the transition
			if (_currentTransition != Transition.None)
			{
				// Create a new transition texture if required
				Texture currentTexture = GetCurrentPlayerTexture();
				Texture nextTexture = GetNextTexture();
				if (currentTexture != null && nextTexture != null)
				{
					int maxWidth = Mathf.Max(nextTexture.width, currentTexture.width);
					int maxHeight = Mathf.Max(nextTexture.height, currentTexture.height);
					if (_rt != null)
					{
						if (_rt.width != maxWidth || _rt.height != maxHeight)
						{
							RenderTexture.ReleaseTemporary(_rt);
							_rt = null;
						}
					}

					if (_rt == null)
					{
						_rt = RenderTexture.GetTemporary(maxWidth, maxHeight, 0, RenderTextureFormat.Default, RenderTextureReadWrite.Default, 1);
					}

					_material.SetTexture(PropFromTex.Id, currentTexture);
					_material.SetFloat(PropFade.Id, 0.0f);
					Graphics.Blit(nextTexture, _rt, _material);

					_easeFunc = Easing.GetFunction(_currentTransitionEasing);
					_transitionTimer = 0.0f;
				}
				else
				{
					// Immediately complete the transition
					_transitionTimer = float.MaxValue;

					// Immediately update the audio volume
					NextPlayer.AudioVolume = this.AudioVolume;
					CurrentPlayer.AudioVolume = 0f;

					if (_autoCloseVideo)
					{
						CurrentPlayer.MediaPath.Path = string.Empty;
						CurrentPlayer.CloseMedia();
					}
				}
			}

			// Swap the videos
			if (NextPlayer == _playerA)
			{
				_nextPlayer = _playerB;
			}
			else
			{
				_nextPlayer = _playerA;
			}

			// Swap the items
			_currentItem = _nextItem;
			_nextItem = null;
		}

		private Texture GetCurrentPlayerTexture(int index = 0)
		{
			if (CurrentPlayer != null && CurrentPlayer.TextureProducer != null)
			{
				return CurrentPlayer.TextureProducer.GetTexture(index);
			}
			else
			{
				return null;
			}
		}

		private Texture GetNextTexture(int index = 0)
		{
			if (_nextPlayer != null && _nextPlayer.TextureProducer != null)
			{
				return _nextPlayer.TextureProducer.GetTexture(index);
			}
			else
			{
				return null;
			}
		}

		private void Awake()
		{
			_nextPlayer = _playerA;
			if (_transitionShader == null)
			{
				_transitionShader = Shader.Find("AVProVideo/Internal/Transition");
				if (_transitionShader == null)
				{
					Debug.LogError("[AVProVideo] Missing transition shader");
				}
			}
			_material = new Material(_transitionShader);
			_easeFunc = Easing.GetFunction(_defaultTransitionEasing);
		}

		protected override void OnDestroy()
		{
			if (_rt != null)
			{
				RenderTexture.ReleaseTemporary(_rt);
				_rt = null;
			}
			if (_material != null)
			{
				if (Application.isPlaying)
				{
					Material.Destroy(_material);
				}
				else
				{
					Material.DestroyImmediate(_material);
				}
				_material = null;
			}
			base.OnDestroy();
		}

		private void Start()
		{
			if (Application.isPlaying)
			{
				if (CurrentPlayer)
				{
					CurrentPlayer.Events.AddListener(OnMediaPlayerEvent);

					if (NextPlayer)
					{
						NextPlayer.Events.AddListener(OnMediaPlayerEvent);
					}
				}

				JumpToItem(0);
			}
		}

		public void OnMediaPlayerEvent(MediaPlayer mediaPlayer, MediaPlayerEvent.EventType eventType, ErrorCode errorCode)
		{
			if (mediaPlayer == CurrentPlayer)
			{
				Events.Invoke(mediaPlayer, eventType, errorCode);
			}

			switch (eventType)
			{
				case MediaPlayerEvent.EventType.FirstFrameReady:
					if (mediaPlayer == NextPlayer)
					{
						StartCoroutine(SwapPlayers());
						Events.Invoke(mediaPlayer, eventType, errorCode);
					}
					break;
				
				case MediaPlayerEvent.EventType.FinishedPlaying:
					if (mediaPlayer == CurrentPlayer)
					{
						if (_playlistAutoProgress && _currentItem.progressMode == ProgressMode.OnFinish)
						{
							NextItem();
						}
					}
					break;
			}
		}

		public bool PrevItem()
		{
			return JumpToItem(_playlistIndex - 1);
		}

		public bool NextItem()
		{
			bool result = JumpToItem(_playlistIndex + 1);
			if (!result)
			{
				Events.Invoke(this, MediaPlayerEvent.EventType.PlaylistFinished, ErrorCode.None);
			}
			return result;
		}

		public bool CanJumpToItem(int index)
		{
			if (_playlistLoopMode == PlaylistLoopMode.Loop)
			{
				if (_playlist.Items.Count > 0)
				{
					index %= _playlist.Items.Count;
					if (index < 0)
					{
						index += _playlist.Items.Count;
					}
				}
			}
			return _playlist.HasItemAt(index);
		}

		public bool JumpToItem(int index)
		{
			if (_playlistLoopMode == PlaylistLoopMode.Loop)
			{
				if (_playlist.Items.Count > 0)
				{
					index %= _playlist.Items.Count;
					if (index < 0)
					{
						index += _playlist.Items.Count;
					}
				}
			}
			if (_playlist.HasItemAt(index))
			{
				_playlistIndex = index;
				_nextItem = _playlist.Items[_playlistIndex];
				OpenVideoFile(_nextItem);
				return true;
			}
			return false;
		}

		public void OpenVideoFile(MediaPlaylist.MediaItem mediaItem)
 		{
			bool isMediaAlreadyLoaded = false;
			if (NextPlayer.MediaPath == mediaItem.mediaPath)
			{
				isMediaAlreadyLoaded = true;
			}

			if (!mediaItem.isOverrideTransition)
			{
				SetTransition(_defaultTransition, _defaultTransitionDuration, _defaultTransitionEasing);
			}
			else
			{
				SetTransition(mediaItem.overrideTransition, mediaItem.overrideTransitionDuration, mediaItem.overrideTransitionEasing);
			}

			this.Loop = NextPlayer.Loop = mediaItem.loop;
			NextPlayer.MediaPath = new MediaPath(mediaItem.mediaPath);
			this.MediaPath = new MediaPath(mediaItem.mediaPath);
			NextPlayer.AudioMuted = _playlistAudioMuted;
			NextPlayer.AudioVolume = _playlistAudioVolume;
			if (_transitionTimer < _currentTransitionDuration && _currentTransition != Transition.None)
			{
				NextPlayer.AudioVolume = 0f;
			}

			if (isMediaAlreadyLoaded)
			{
				NextPlayer.Rewind(false);
				if (_nextItem.startMode == StartMode.Immediate)
				{
					NextPlayer.Play();
				}
				// TODO: We probably want to wait until the new frame arrives before swapping after a Rewind()
				StartCoroutine(SwapPlayers());
			}
			else
			{
				if (string.IsNullOrEmpty(NextPlayer.MediaPath.Path))
				{
					NextPlayer.CloseMedia();
				}
				else
				{
					NextPlayer.OpenMedia(NextPlayer.MediaPath.PathType, NextPlayer.MediaPath.Path, _nextItem.startMode == StartMode.Immediate);
				}
			}
		}

		private bool IsTransitioning()
		{
			bool hasTransition = _currentTransition != Transition.None;
			bool inTransition = _transitionTimer < _currentTransitionDuration;
			return hasTransition && inTransition;
		}

		private void SetTransition(Transition transition, float duration, Easing.Preset easing)
		{
			if (transition == Transition.Random)
			{
				transition = (Transition)Random.Range(0, (int)Transition.Random);
			}

			if (transition != _currentTransition)
			{
				// Disable the previous transition
				if (!string.IsNullOrEmpty(_currentTransitionName))
				{
					_material.DisableKeyword(_currentTransitionName);
				}

				// Enable the next transition
				_currentTransition = transition;
				_currentTransitionName = GetTransitionName(transition);
				_material.EnableKeyword(_currentTransitionName);
			}

			_currentTransitionDuration = duration;
			_currentTransitionEasing = easing;
		}

		protected override void Update()
		{
			if (!Application.isPlaying)
			{
				return;
			}

			if (!IsPaused())
			{
				if (IsTransitioning())
				{
					_transitionTimer += Time.deltaTime;
					float t = _easeFunc(Mathf.Clamp01(_transitionTimer / _currentTransitionDuration));

					// Fade the audio volume
					NextPlayer.AudioVolume = (1f - t) * this.AudioVolume;
					CurrentPlayer.AudioVolume = t * this.AudioVolume;

					// TODO: support going from mono to stereo
					// TODO: support videos of different aspect ratios by rendering with scaling to fit
					// This can be done by blitting twice, once for each eye
					// If the stereo mode is different for playera/b then both should be set to stereo during the transition
					// if (CurrentPlayer.m_StereoPacking == StereoPacking.TopBottom)....
					_material.SetFloat(PropFade.Id, t);
					_rt.DiscardContents();
					Graphics.Blit(GetCurrentPlayerTexture(), _rt, _material);

					// After the transition is now complete, close/pause the previous video if required
					bool isTransitioning = IsTransitioning();
					if (!isTransitioning)
					{
						if (_autoCloseVideo)
						{
							if (NextPlayer != null)
							{
								NextPlayer.MediaPath.Path = string.Empty;
								NextPlayer.CloseMedia();
							}
						}
						else if (!_pausePreviousOnTransition)
						{
							if (NextPlayer != null && NextPlayer.Control.IsPlaying())
							{
								NextPlayer.Pause();
							}
						}
					}
				}
				else
				if (_playlistAutoProgress)
				{
					if (_nextItem == null && 
						_currentItem != null &&
						_currentItem.progressMode == ProgressMode.BeforeFinish && 
						Control != null && 
						Control.HasMetaData() && 
						Control.GetCurrentTime() >= (Info.GetDuration() - _currentItem.progressTimeSeconds))
					{
						this.NextItem();
					}
					else
					if (_currentItem == null)
					{
						JumpToItem(_playlistIndex);
					}
				}
			}

			base.Update();
		}

		#region Implementing ITextureProducer
		
		public Texture GetTexture(int index = 0)
		{
			bool isTransitioning = IsTransitioning();
			if (isTransitioning)
			{
				return _rt;
			}
			else if (CurrentPlayer.TextureProducer != null)
			{
				return CurrentPlayer.TextureProducer.GetTexture(index);
			}
			else
			{
				return null;
			}
		}

		public int GetTextureCount()
		{
			return CurrentPlayer.TextureProducer != null
				? CurrentPlayer.TextureProducer.GetTextureCount() : 0;
		}

		public int GetTextureFrameCount()
		{
			return CurrentPlayer.TextureProducer != null
				? CurrentPlayer.TextureProducer.GetTextureFrameCount() : 0;
		}

		public bool SupportsTextureFrameCount()
		{
			return CurrentPlayer.TextureProducer != null
				? CurrentPlayer.TextureProducer.SupportsTextureFrameCount() : false;
		}

		public long GetTextureTimeStamp()
		{
			return CurrentPlayer.TextureProducer != null
				? CurrentPlayer.TextureProducer.GetTextureTimeStamp() : 0;
		}

		public float GetTexturePixelAspectRatio()
		{
			return CurrentPlayer.TextureProducer != null
				? CurrentPlayer.TextureProducer.GetTexturePixelAspectRatio() : 0.0f;
		}

		public bool RequiresVerticalFlip()
		{
			return CurrentPlayer.TextureProducer != null
				? CurrentPlayer.TextureProducer.RequiresVerticalFlip() : false;
		}

		public Matrix4x4 GetYpCbCrTransform()
		{
			return CurrentPlayer.TextureProducer != null
				? CurrentPlayer.TextureProducer.GetYpCbCrTransform() : Matrix4x4.identity;
		}

		public StereoPacking GetTextureStereoPacking()
		{
			return CurrentPlayer.TextureProducer != null
				? CurrentPlayer.TextureProducer.GetTextureStereoPacking() : StereoPacking.None;
		}

		public TransparencyMode GetTextureTransparency()
		{
			return CurrentPlayer.TextureProducer != null
				? CurrentPlayer.TextureProducer.GetTextureTransparency() : TransparencyMode.Opaque;
		}

		public AlphaPacking GetTextureAlphaPacking()
		{
			return CurrentPlayer.TextureProducer != null ? 
				CurrentPlayer.TextureProducer.GetTextureAlphaPacking() : AlphaPacking.None;
		}

		public float[] GetAffineTransform()
		{
			if (CurrentPlayer.TextureProducer != null)
			{
				return CurrentPlayer.TextureProducer.GetAffineTransform();
			}
			else
			{
				return new float[6] { 1.0f, 0.0f, 0.0f, 0.0f, 1.0f, 0.0f };
			}
		}

		public Matrix4x4 GetTextureMatrix()
		{
			return CurrentPlayer.TextureProducer != null
				? CurrentPlayer.TextureProducer.GetTextureMatrix() : Matrix4x4.identity;
		}

		public RenderTextureFormat GetCompatibleRenderTextureFormat(
			GetCompatibleRenderTextureFormatOptions options,
			 int plane
		) {
			return CurrentPlayer.TextureProducer != null
				? CurrentPlayer.TextureProducer.GetCompatibleRenderTextureFormat(options, plane)
				: RenderTextureFormat.Default;
		}

		#endregion Implementing ITextureProducer

		private static string GetTransitionName(Transition transition)
		{
			switch (transition)
			{
				case Transition.None:		
					return "LERP_NONE";
				
				case Transition.Fade: 		
					return "LERP_FADE";
				
				case Transition.Black:		
					return "LERP_BLACK";
				
				case Transition.White:		
					return "LERP_WHITE";
				
				case Transition.Transparent:
					return "LERP_TRANSP";
				
				case Transition.Horiz:		
					return "LERP_HORIZ";
				
				case Transition.Vert:		
					return "LERP_VERT";
				
				case Transition.Diag:		
					return "LERP_DIAG";
				
				case Transition.MirrorH:	
					return "LERP_HORIZ_MIRROR";
				
				case Transition.MirrorV:	
					return "LERP_VERT_MIRROR";
				
				case Transition.MirrorD:	
					return "LERP_DIAG_MIRROR";
				
				case Transition.ScrollV:	
					return "LERP_SCROLL_VERT";
				
				case Transition.ScrollH:	
					return "LERP_SCROLL_HORIZ";
				
				case Transition.Circle:		
					return "LERP_CIRCLE";
				
				case Transition.Diamond:	
					return "LERP_DIAMOND";
				
				case Transition.Blinds:		
					return "LERP_BLINDS";
				
				case Transition.Arrows:		
					return "LERP_ARROW";
				
				case Transition.SlideH:		
					return "LERP_SLIDE_HORIZ";
				
				case Transition.SlideV:		
					return "LERP_SLIDE_VERT";
				
				case Transition.Zoom:		
					return "LERP_ZOOM_FADE";
				
				case Transition.RectV:		
					return "LERP_RECTS_VERT";
			}
			
			return string.Empty;
		}
	}
}
