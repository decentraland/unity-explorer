//-----------------------------------------------------------------------------
// Copyright 2015-2024 RenderHeads Ltd.  All rights reserved.
//-----------------------------------------------------------------------------

using RenderHeads.Media.AVProVideo;
using UnityEngine;
using UnityEngine.UI;

public class OptionsMenu : MonoBehaviour
{
	[Header("Media Player")]
	[SerializeField] private MediaPlayer _MediaPlayer;

	[Header("Page Game Objects")]
	[SerializeField] private GameObject					_MainMenuGO;
	[SerializeField] private GameObject					_VideoTrackMenuGO;
	[SerializeField] private GameObject					_AudioTrackMenuGO;
	[SerializeField] private GameObject					_SubtitlesMenuGO;
	[SerializeField] private GameObject					_PlaybackSpeedMenuGO;
	[SerializeField] private GameObject					_QualityMenuGO;

	[Header("Buttons")]
	[SerializeField] private Button						_VideoTrackButton;
	[SerializeField] private Button						_AudioTrackButton;
	[SerializeField] private Button						_SubtitlesButton;
	[SerializeField] private Button						_PlaybackSpeedButton;
	[SerializeField] private Button						_QualityButton;

	[Header("Pages")]
	[SerializeField] private OptionsVideoTrackPage		_OptionsVideoTrackPage;
	[SerializeField] private OptionsAudioTrackPage		_OptionsAudioTrackPage;
	[SerializeField] private OptionsSubtitlesPage		_OptionsSubtitlesPage;
	[SerializeField] private OptionsPlaybackSpeedPage	_OptionsPlaybackSpeedPage;
	[SerializeField] private OptionsQualityPage			_OptionsQualityPage;

	[Header("Text")]
	[SerializeField] private Text						_VideoTrackValueText;
	[SerializeField] private Text						_AudioTrackValueText;
	[SerializeField] private Text						_SubtitlesValueText;
	[SerializeField] private Text						_PlaybackSpeedValueText;
	[SerializeField] private Text						_QualityValueText;

	private int m_iCachedVideoWidth			= -1;
	private int m_iCachedVideoHeight		= -1;
	private float m_fCachedVideoFramerate	= -1.0f;
	private int m_iCachedVariantId			= -1;

	void Start()
	{
		if( _VideoTrackButton )
		{
			_VideoTrackButton.onClick.AddListener( delegate
			{
				if( _MediaPlayer && _MediaPlayer.VideoTracks.GetVideoTracks().Count > 1 )
				{
					MainToVideoTrack();
				}
			} );
		}

		if( _AudioTrackButton )
		{
			_AudioTrackButton.onClick.AddListener( delegate
			{
				if( _MediaPlayer && _MediaPlayer.AudioTracks.GetAudioTracks().Count > 1 )
				{
					MainToAudioTrack();
				}
			} );
		}

		if( _SubtitlesButton )
		{
			_SubtitlesButton.onClick.AddListener( delegate
			{
				if( _MediaPlayer && _MediaPlayer.TextTracks.GetTextTracks().Count > 0 )
				{
					MainToSubtitles();
				}
			} );
		}

		if( _PlaybackSpeedButton )
		{
			_PlaybackSpeedButton.onClick.AddListener( delegate
			{
				MainToPlaybackSpeed();
			} );
		}

		if( _QualityButton )
		{
			_QualityButton.onClick.AddListener( delegate
			{
				if( _MediaPlayer.Variants != null && _MediaPlayer.Variants.Count > 0 )
				{
					MainToQuality();
				}
			} );
		}
	}

	void Update()
	{
		if( _MainMenuGO && _MainMenuGO.activeInHierarchy &&
			_QualityValueText &&
			_MediaPlayer )
		{
			int iWidth = _MediaPlayer.Info.GetVideoWidth();
			int iHeight = _MediaPlayer.Info.GetVideoHeight();
			float fFramerate = _MediaPlayer.Variants.Current.FrameRate;
			int iCurrentVariantId = _MediaPlayer.Variants.Current.Id;

			//Debug.Log($"OptionsMenu.Update() - iWidth: {iWidth}, iHeight: {iHeight}, fFramerate: {fFramerate}, iCurrentVariantId: {iCurrentVariantId}");

			if (iWidth != m_iCachedVideoWidth || iHeight != m_iCachedVideoHeight || m_fCachedVideoFramerate != fFramerate || m_iCachedVariantId != iCurrentVariantId )
			{
				if (_MediaPlayer.Variants.Count > 1)
				{
					if (iCurrentVariantId == Variant.Auto.Id)
					{
						if( fFramerate > 0.0f )
						{
							_QualityValueText.text = $"Auto ({iWidth}x{iHeight}@{fFramerate}) >";
						}
						else
						{
							_QualityValueText.text = $"Auto ({iWidth}x{iHeight}) >";
						}
					}
					else
					{
						if( fFramerate > 0.0f )
						{
							_QualityValueText.text = $"{iWidth}x{iHeight}@{fFramerate} >";
						}
						else
						{
							_QualityValueText.text = $"{iWidth}x{iHeight} >";
						}
					}
				}
				else
				{
					if( fFramerate > 0.0f )
					{
						_QualityValueText.text = $"{iWidth}x{iHeight}@{fFramerate} >";
					}
					else
					{
						_QualityValueText.text = $"{iWidth}x{iHeight}";
					}
				}

				m_iCachedVideoWidth = iWidth;
				m_iCachedVideoHeight = iHeight;
				m_fCachedVideoFramerate = fFramerate;
				m_iCachedVariantId = iCurrentVariantId;
			}
		}
	}

	public void SetActive( bool bShowOptions )
	{
		if( _MainMenuGO )
		{
			_MainMenuGO.SetActive( bShowOptions );
		}

		if( _VideoTrackMenuGO )
		{
			_VideoTrackMenuGO.SetActive( false );
		}

		if( _AudioTrackMenuGO )
		{
			_AudioTrackMenuGO.SetActive( false );
		}

		if ( _SubtitlesMenuGO )
		{
			_SubtitlesMenuGO.SetActive( false );
		}

		if( _PlaybackSpeedMenuGO )
		{
			_PlaybackSpeedMenuGO.SetActive( false );
		}

		if( _QualityMenuGO )
		{
			_QualityMenuGO.SetActive( false );
		}

		if( bShowOptions )
		{
			if( _MediaPlayer)
			{
				ChangeVideoTrack( _MediaPlayer.VideoTracks.GetVideoTracks().GetActiveTrackIndex(), false );
				ChangeAudioTrack( _MediaPlayer.AudioTracks.GetAudioTracks().GetActiveTrackIndex(), false );
				ChangeSubtitleTrack( _MediaPlayer.TextTracks.GetTextTracks().GetActiveTrackIndex(), false );
			}
		}
	}

	public void ChangeVideoTrack( int iTrackIndex, bool bSetTrack = true )
	{
		if( _OptionsVideoTrackPage )
		{
			_OptionsVideoTrackPage.ChangeVideoTrack( iTrackIndex, bSetTrack );
		}
		if( _VideoTrackValueText )
		{
			VideoTrack videoTrack = ( _MediaPlayer ) ? _MediaPlayer.VideoTracks.GetActiveVideoTrack() : null;
			if( videoTrack != null )
			{
				_VideoTrackValueText.text = "None";

				int iNumVideoTracks = _MediaPlayer.VideoTracks.GetVideoTracks().Count;
				if ( iNumVideoTracks > 0 )
				{
					_VideoTrackValueText.text = videoTrack.DisplayName + ( iNumVideoTracks > 1 ? "  >" : "" );
				}
			}
		}
	}

	public void ChangeAudioTrack( int iTrackIndex, bool bSetTrack = true )
	{
		if( _OptionsAudioTrackPage )
		{
			_OptionsAudioTrackPage.ChangeAudioTrack( iTrackIndex, bSetTrack );
		}
		if( _AudioTrackValueText )
		{
			AudioTrack audioTrack = ( _MediaPlayer ) ? _MediaPlayer.AudioTracks.GetActiveAudioTrack() : null;
			if( audioTrack != null )
			{
				_AudioTrackValueText.text = "None";

				int iNumAudioTracks = _MediaPlayer.AudioTracks.GetAudioTracks().Count;
				if( iNumAudioTracks > 0 )
				{
					_AudioTrackValueText.text = audioTrack.DisplayName + ( iNumAudioTracks > 1 ? "  >" : "" );
				}
			}
		}
	}

	public void ChangeSubtitleTrack( int iTrackUid, bool bSetTrack = true )
	{
		if( _OptionsSubtitlesPage )
		{
			_OptionsSubtitlesPage.ChangeSubtitleTrack( iTrackUid, bSetTrack );
		}
		if( _SubtitlesValueText )
		{
			TextTracks textTracks = ( _MediaPlayer ) ? _MediaPlayer.TextTracks.GetTextTracks() : null;
			if( textTracks != null )
			{
				_SubtitlesValueText.text = "None";

				if ( textTracks.Count > 0 )
				{
					int iTrackIndex = _MediaPlayer.TextTracks.GetTextTrackArrayIndexFromUid( iTrackUid );
					_SubtitlesValueText.text = ( ( iTrackIndex > -1 ) ? ( textTracks[ iTrackIndex ].DisplayName ) : "Off" ) + "  >";
				}
			}
		}
	}

	public void ChangePlaybackSpeed( int iIndex )
	{
		if( _OptionsPlaybackSpeedPage )
		{
			_OptionsPlaybackSpeedPage.ChangeVideoPlaybackSpeed(iIndex);

			if( _PlaybackSpeedValueText )
			{
				TextTracks textTracks = ( _MediaPlayer ) ? _MediaPlayer.TextTracks.GetTextTracks() : null;
				if( textTracks != null )
				{
					_PlaybackSpeedValueText.text = _OptionsPlaybackSpeedPage.GetDisplayNameForIndex( iIndex ) + "  >";
				}
			}
		}
	}

	public void ChangeVideoVariant( int iVariantIndex )
	{
		if( _OptionsQualityPage )
		{
			_OptionsQualityPage.ChangeVideoVariant( iVariantIndex );
		}
	}

	public void MainToVideoTrack()
	{
		if ( _MainMenuGO )
		{
			_MainMenuGO.SetActive( false );
		}

		if( _VideoTrackMenuGO )
		{
			_VideoTrackMenuGO.SetActive( true );
		}
	}

	public void VideoTrackToMain()
	{
		if( _MainMenuGO )
		{
			_MainMenuGO.SetActive( true );
		}

		if( _VideoTrackMenuGO )
		{
			_VideoTrackMenuGO.SetActive( false );
		}
	}

	public void MainToAudioTrack()
	{
		if ( _MainMenuGO )
		{
			_MainMenuGO.SetActive( false );
		}

		if( _AudioTrackMenuGO )
		{
			_AudioTrackMenuGO.SetActive( true );
		}
	}

	public void AudioTrackToMain()
	{
		if( _MainMenuGO )
		{
			_MainMenuGO.SetActive( true );
		}

		if( _AudioTrackMenuGO )
		{
			_AudioTrackMenuGO.SetActive( false );
		}
	}

	public void MainToSubtitles()
	{
		if( _MainMenuGO )
		{
			_MainMenuGO.SetActive( false );
		}

		if( _SubtitlesMenuGO )
		{
			_SubtitlesMenuGO.SetActive( true );
		}
	}

	public void SubtitlesToMain()
	{
		if ( _MainMenuGO )
		{
			_MainMenuGO.SetActive( true );
		}

		if ( _SubtitlesMenuGO )
		{
			_SubtitlesMenuGO.SetActive( false );
		}
	}

	public void MainToPlaybackSpeed()
	{
		if ( _MainMenuGO )
		{
			_MainMenuGO.SetActive( false );
		}

		if ( _PlaybackSpeedMenuGO )
		{
			_PlaybackSpeedMenuGO.SetActive( true );
		}
	}

	public void PlaybackSpeedToMain()
	{
		if ( _MainMenuGO )
		{
			_MainMenuGO.SetActive( true );
		}

		if ( _PlaybackSpeedMenuGO )
		{
			_PlaybackSpeedMenuGO.SetActive( false );
		}
	}

	public void MainToQuality()
	{
		if (_MediaPlayer.Variants == null || _MediaPlayer.Variants.Count < 2 )
		{
			return;
		}

		if ( _MainMenuGO )
		{
			_MainMenuGO.SetActive( false );
		}

		if ( _QualityMenuGO )
		{
			_QualityMenuGO.SetActive( true );
		}
	}

	public void QualityToMain()
	{
		if ( _MainMenuGO )
		{
			_MainMenuGO.SetActive( true );
		}

		if ( _QualityMenuGO )
		{
			_QualityMenuGO.SetActive( false );
		}
	}
}
