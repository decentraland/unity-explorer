using RenderHeads.Media.AVProVideo;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class OptionsVideoTrackPage : MonoBehaviour
{
	[Header("Media Player")]
	[SerializeField] private MediaPlayer	_MediaPlayer;

	[Header("Options Menu")]
	[SerializeField] private OptionsMenu	_OptionsMenu;

	[Header("Content")]
	[SerializeField] private Transform		_Content;
	[SerializeField] private RectTransform	_ScrollViewRectTransform;
	[SerializeField] private RectTransform	_ViewportRectTransform;
	[SerializeField] private GameObject		_VideoTrackLinePrefab;


	private string	m_SetupForVideoPath;


	private class CVideoTrackSet
	{
		public GameObject	m_LineGO	= null;
	}
	private List<CVideoTrackSet>	m_lVideoTrackSets		= new List<CVideoTrackSet>();


    void Start()
    {
		UpdateSets();
	}

	void Update()
    {
		UpdateSets();
	}

	private void AddVideoTrackSet( string title, bool bEnabled )
	{
		GameObject newLineGO = GameObject.Instantiate( _VideoTrackLinePrefab, _Content );
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
					int iIndex = m_lVideoTrackSets.Count;
					button.onClick.AddListener( delegate
					{
						_OptionsMenu.ChangeVideoTrack( iIndex );
					} );
				}
			}

			// Add it to the list
			CVideoTrackSet cVideoTrackSet = new CVideoTrackSet();
			cVideoTrackSet.m_LineGO = newLineGO;
			m_lVideoTrackSets.Add( cVideoTrackSet );
		}
	}

	public void UpdateSets()
	{
		if( _MediaPlayer != null && _MediaPlayer.Control.HasMetaData() )
		{
			if( m_SetupForVideoPath == null || !m_SetupForVideoPath.Equals( _MediaPlayer.MediaPath.Path ) )
			{
				m_SetupForVideoPath = _MediaPlayer.MediaPath.Path;

				foreach( CVideoTrackSet videoTrackSet in m_lVideoTrackSets )
				{
					GameObject.Destroy( videoTrackSet.m_LineGO );
					videoTrackSet.m_LineGO = null;
				}

				// Remove everything
				m_lVideoTrackSets.Clear();

				// Add all subtitle sets
				foreach( VideoTrack videoTrack in _MediaPlayer.VideoTracks.GetVideoTracks() )
				{
					AddVideoTrackSet( videoTrack.DisplayName, false );
				}

				if( m_lVideoTrackSets.Count > 1 )
				{
					// Reposition everything
					float fLineHeight = 40.0f;
					float fTotalHeight = fLineHeight * m_lVideoTrackSets.Count;

					RectTransform contentRectTransform = ( _Content != null ) ? _Content.GetComponent<RectTransform>() : null;
					if( contentRectTransform != null )
					{
						contentRectTransform.sizeDelta = new Vector2( contentRectTransform.sizeDelta.x, fTotalHeight );
					}

					float fY = (fTotalHeight * 0.5f) - (fLineHeight * 0.5f);
					foreach( CVideoTrackSet videoTrackSet in m_lVideoTrackSets )
					{
						RectTransform rectTransform = videoTrackSet.m_LineGO.GetComponent<RectTransform>();
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

						_ScrollViewRectTransform.sizeDelta = new Vector2( _ScrollViewRectTransform.sizeDelta.x, fNewHeight );
						_ViewportRectTransform.sizeDelta = new Vector2( _ViewportRectTransform.sizeDelta.x, fNewHeight );
					}
				}

				ChangeVideoTrack( _MediaPlayer.VideoTracks.GetVideoTracks().GetActiveTrackIndex(), false );
			}
		}
	}

	public void ChangeVideoTrack( int iTrackIndex, bool bSetTrack = true )
	{
		VideoTracks videoTracks = ( _MediaPlayer ) ? _MediaPlayer.VideoTracks.GetVideoTracks() : null;
		if( videoTracks != null )
		{
			if( bSetTrack )
			{
				// Change video track on the media player
				_MediaPlayer.VideoTracks.SetActiveVideoTrack( ( iTrackIndex > -1 && iTrackIndex < videoTracks.Count ) ? videoTracks[ iTrackIndex ] : null );
			}

			// Sort out UI
			int iIndex = 0;
			foreach ( CVideoTrackSet videoTrackSet in m_lVideoTrackSets )
			{
				Transform tickIconTransform = videoTrackSet.m_LineGO.transform.Find( "TickIcon" );
				Image tickIconImage = ( tickIconTransform != null ) ? tickIconTransform.GetComponent<Image>() : null;
				if ( tickIconImage != null )
				{
					tickIconImage.enabled = ( iIndex == iTrackIndex );
				}

				++iIndex;
			}
		}
	}
}
