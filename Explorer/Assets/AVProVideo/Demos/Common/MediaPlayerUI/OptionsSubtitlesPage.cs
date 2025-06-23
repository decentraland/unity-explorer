using RenderHeads.Media.AVProVideo;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class OptionsSubtitlesPage : MonoBehaviour
{
	[Header("Media Player")]
	[SerializeField] private MediaPlayer	_MediaPlayer;

	[Header("Options Menu")]
	[SerializeField] private OptionsMenu	_OptionsMenu;

	[Header("Content")]
	[SerializeField] private Transform		_Content;
	[SerializeField] private RectTransform	_ScrollViewRectTransform;
	[SerializeField] private RectTransform	_ViewportRectTransform;
	[SerializeField] private GameObject		_SubtitleLinePrefab;


	private string	m_SetupForVideoPath;


	private class CSubtitleSet
	{
		public GameObject	m_LineGO	= null;
	}
	private List<CSubtitleSet>	m_lSubtitleSets		= new List<CSubtitleSet>();


    void Start()
    {
		// Add 'Off'
		AddSubtitleSet( "Off", true );

		UpdateSets();
	}

	void Update()
    {
		UpdateSets();
	}

	private void AddSubtitleSet( string title, bool bEnabled )
	{
		GameObject newLineGO = GameObject.Instantiate( _SubtitleLinePrefab, _Content );
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
				TextTracks textTracks = ( _MediaPlayer ) ? _MediaPlayer.TextTracks.GetTextTracks() : null;

				Button button = newLineGO.GetComponent<Button>();
				if( button && textTracks != null )
				{
					int iIndex = m_lSubtitleSets.Count;
					button.onClick.AddListener( delegate
					{
						_OptionsMenu.ChangeSubtitleTrack( ( ( iIndex > 0 ) ? textTracks[ iIndex - 1 ].Uid : -1 ) );
					} );
				}
			}

			// Add it to the list
			CSubtitleSet cSubtitleSet = new CSubtitleSet();
			cSubtitleSet.m_LineGO = newLineGO;
			m_lSubtitleSets.Add( cSubtitleSet );
		}
	}

	public void UpdateSets()
	{
		if( _MediaPlayer != null && _MediaPlayer.Control.HasMetaData() )
		{
			if( m_SetupForVideoPath == null || !m_SetupForVideoPath.Equals( _MediaPlayer.MediaPath.Path ) )
			{
				m_SetupForVideoPath = _MediaPlayer.MediaPath.Path;

				bool bIgnoreFirst = true;
				foreach( CSubtitleSet subtitleSet in m_lSubtitleSets )
				{
					if( !bIgnoreFirst )
					{
						GameObject.Destroy( subtitleSet.m_LineGO );
						subtitleSet.m_LineGO = null;
					}

					bIgnoreFirst = false;
				}

				// Remove everything but the first one
				if( m_lSubtitleSets.Count > 1 )
				{
					m_lSubtitleSets.RemoveRange( 1, (m_lSubtitleSets.Count - 1) );
				}

				// Add all subtitle sets
				foreach( TextTrack textTrack in _MediaPlayer.TextTracks.GetTextTracks() )
				{
					AddSubtitleSet( textTrack.DisplayName, false );
				}

				if( m_lSubtitleSets.Count > 1 )
				{
					// Reposition everything
					float fLineHeight = 40.0f;
					float fTotalHeight = fLineHeight * m_lSubtitleSets.Count;

					RectTransform contentRectTransform = ( _Content != null ) ? _Content.GetComponent<RectTransform>() : null;
					if( contentRectTransform != null )
					{
						contentRectTransform.sizeDelta = new Vector2( contentRectTransform.sizeDelta.x, fTotalHeight );
					}

					float fY = (fTotalHeight * 0.5f) - (fLineHeight * 0.5f);
					foreach ( CSubtitleSet subtitleSet in m_lSubtitleSets )
					{
						RectTransform rectTransform = subtitleSet.m_LineGO.GetComponent<RectTransform>();
						if ( rectTransform )
						{
							rectTransform.anchoredPosition = new Vector2( 0.0f, fY );
							fY -= fLineHeight;
						}
					}

					if( _ViewportRectTransform && _ScrollViewRectTransform )
					{
						float fMaxHeight = 222.0f;

						float fNewHeight = Mathf.Clamp( fTotalHeight, fLineHeight, fMaxHeight );
						float fBottomPadding = 12.0f;

						RectTransform rectTransform = transform.GetComponent<RectTransform>();
						rectTransform.sizeDelta = new Vector2( rectTransform.sizeDelta.x, 60.0f + fNewHeight + fBottomPadding );

						_ScrollViewRectTransform.sizeDelta = new Vector2(_ScrollViewRectTransform.sizeDelta.x, fNewHeight );
						_ViewportRectTransform.sizeDelta = new Vector2( _ViewportRectTransform.sizeDelta.x, fNewHeight );
					}
				}

				ChangeSubtitleTrack( _MediaPlayer.TextTracks.GetTextTracks().GetActiveTrackIndex(), false );		// Add one to move past the 'Off' which is at index zero
			}
		}
	}

	public void ChangeSubtitleTrack( int iTrackUid, bool bSetTrack = true )
	{
		TextTracks textTracks = ( _MediaPlayer ) ? _MediaPlayer.TextTracks.GetTextTracks() : null;
		if( textTracks != null )
		{
			int iTrackIndex = _MediaPlayer.TextTracks.GetTextTrackArrayIndexFromUid( iTrackUid );

			if( bSetTrack )
			{
				// Change text track on the media player
				_MediaPlayer.TextTracks.SetActiveTextTrack( ( iTrackIndex > -1 ) ? textTracks[ iTrackIndex ]: null );
			}

			// Sort out UI
			int iIndex = 0;
			foreach( CSubtitleSet subtitleSet in m_lSubtitleSets )
			{
				Transform tickIconTransform = subtitleSet.m_LineGO.transform.Find( "TickIcon" );
				Image tickIconImage = ( tickIconTransform != null ) ? tickIconTransform.GetComponent<Image>() : null;
				if ( tickIconImage != null )
				{
					tickIconImage.enabled = ( iIndex == (iTrackIndex + 1) );
				}

				++iIndex;
			}
		}
	}
}
