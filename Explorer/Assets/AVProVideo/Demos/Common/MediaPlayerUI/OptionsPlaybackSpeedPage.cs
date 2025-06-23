using RenderHeads.Media.AVProVideo;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class OptionsPlaybackSpeedPage : MonoBehaviour
{
	[Header("Media Player")]
	[SerializeField] private MediaPlayer	_MediaPlayer;

	[Header("Options Menu")]
	[SerializeField] private OptionsMenu	_OptionsMenu;

	[Header("Content")]
	[SerializeField] private Transform		_Content;
	[SerializeField] private RectTransform	_ScrollViewRectTransform;
	[SerializeField] private RectTransform	_ViewportRectTransform;
	[SerializeField] private GameObject		_PlaybackSpeedLinePrefab;


	private string	m_SetupForVideoPath;


	private class CPlaybackSpeedSet
	{
		public GameObject	m_LineGO		= null;
		public string		m_DisplayName	= "";
		public float		m_fRate			= 1.0f;
	}
	private List<CPlaybackSpeedSet>	m_lPlaybackSpeedSets		= new List<CPlaybackSpeedSet>();

	private bool	m_bSetsDirty	= false;



	void Start()
    {
		// Add defaults
		AddPlaybackSpeedSet( "0.25", 0.25f, false );
		AddPlaybackSpeedSet( "0.5", 0.5f, false );
		AddPlaybackSpeedSet( "0.75", 0.75f, false );
		AddPlaybackSpeedSet( "Normal", 1.0f, true );
		AddPlaybackSpeedSet( "1.25", 1.25f, false );
		AddPlaybackSpeedSet( "1.5", 1.5f, false );
		AddPlaybackSpeedSet( "1.75", 1.75f, false );
	}

	void Update()
    {
		if( m_bSetsDirty )
		{
			UpdateSets();
			m_bSetsDirty = false;
		}
	}

	private void AddPlaybackSpeedSet( string title, float fRate, bool bEnabled )
	{
		GameObject newLineGO = GameObject.Instantiate( _PlaybackSpeedLinePrefab, _Content );
		if( newLineGO != null )
		{
			// Setup text
			Transform titleTransform = newLineGO.transform.Find( "TitleText" );
			Text titleText = ( titleTransform != null ) ? titleTransform.GetComponent<Text>() : null;
			if( titleText != null )
			{
				titleText.text = title;
			}

			// Tick icon
			if( bEnabled )
			{
				Transform tickIconTransform = newLineGO.transform.Find( "TickIcon" );
				Image tickIconImage = ( tickIconTransform != null ) ? tickIconTransform.GetComponent<Image>() : null;
				if( tickIconImage != null )
				{
					tickIconImage.enabled = true;
				}
			}

			// On click
			if( _OptionsMenu )
			{
				Button button = newLineGO.GetComponent<Button>();
				if( button )
				{
					int iIndex = m_lPlaybackSpeedSets.Count;
					button.onClick.AddListener( delegate
					{
						_OptionsMenu.ChangePlaybackSpeed( iIndex );
					} );
				}
			}

			// Add it to the list
			CPlaybackSpeedSet cPlaybackSpeedSet = new CPlaybackSpeedSet();
			cPlaybackSpeedSet.m_LineGO = newLineGO;
			cPlaybackSpeedSet.m_fRate = fRate;
			cPlaybackSpeedSet.m_DisplayName = title;
			m_lPlaybackSpeedSets.Add( cPlaybackSpeedSet );
		}

		m_bSetsDirty = true;
	}

	public string GetDisplayNameForIndex( int iIndex )
	{
		if( iIndex > -1 && iIndex < m_lPlaybackSpeedSets.Count )
		{
			return m_lPlaybackSpeedSets[ iIndex ].m_DisplayName;
		}
		return "Normal";
	}

	public void UpdateSets()
	{
		if( m_lPlaybackSpeedSets.Count > 1 )
		{
			// Reposition everything
			float fLineHeight = 40.0f;
			float fTotalHeight = fLineHeight * m_lPlaybackSpeedSets.Count;

			RectTransform contentRectTransform = ( _Content != null ) ? _Content.GetComponent<RectTransform>() : null;
			if( contentRectTransform != null )
			{
				contentRectTransform.sizeDelta = new Vector2( contentRectTransform.sizeDelta.x, fTotalHeight );
			}

			float fY = (fTotalHeight * 0.5f) - (fLineHeight * 0.5f);
			foreach( CPlaybackSpeedSet PlaybackSpeedSet in m_lPlaybackSpeedSets )
			{
				RectTransform rectTransform = PlaybackSpeedSet.m_LineGO.GetComponent<RectTransform>();
				if ( rectTransform )
				{
					rectTransform.anchoredPosition = new Vector2( 0.0f, fY );
					fY -= fLineHeight;
				}
			}

			if( _ViewportRectTransform && _ScrollViewRectTransform )
			{
				float fMaxHeight = 330.0f;

				float fNewHeight = Mathf.Clamp( fTotalHeight, fLineHeight, fMaxHeight );
				float fBottomPadding = 12.0f;

				RectTransform rectTransform = transform.GetComponent<RectTransform>();
				rectTransform.sizeDelta = new Vector2( rectTransform.sizeDelta.x, 60.0f + fNewHeight + fBottomPadding );

				_ScrollViewRectTransform.sizeDelta = new Vector2(_ScrollViewRectTransform.sizeDelta.x, fNewHeight );
				_ViewportRectTransform.sizeDelta = new Vector2( _ViewportRectTransform.sizeDelta.x, fNewHeight );
			}
		}
	}

	public void ChangeVideoPlaybackSpeed( int iPlaybackSpeedIndex )
	{
		VideoTrack currentVideoTrack = _MediaPlayer.VideoTracks.GetActiveVideoTrack();
		if( _MediaPlayer )
		{
			// Change current playback speed on the media player
			_MediaPlayer.Control.SetPlaybackRate( m_lPlaybackSpeedSets[ iPlaybackSpeedIndex ].m_fRate );

			// Sort out UI
			int iIndex = 0;
			foreach ( CPlaybackSpeedSet PlaybackSpeedSet in m_lPlaybackSpeedSets )
			{
				Transform tickIconTransform = PlaybackSpeedSet.m_LineGO.transform.Find( "TickIcon" );
				Image tickIconImage = ( tickIconTransform != null ) ? tickIconTransform.GetComponent<Image>() : null;
				if ( tickIconImage != null )
				{
					tickIconImage.enabled = ( iIndex == iPlaybackSpeedIndex );
				}

				++iIndex;
			}
		}
	}
}
