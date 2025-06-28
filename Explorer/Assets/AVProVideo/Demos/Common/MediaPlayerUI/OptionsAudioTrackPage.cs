using RenderHeads.Media.AVProVideo;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class OptionsAudioTrackPage : MonoBehaviour
{
	[Header("Media Player")]
	[SerializeField] private MediaPlayer	_MediaPlayer;

	[Header("Options Menu")]
	[SerializeField] private OptionsMenu	_OptionsMenu;

	[Header("Content")]
	[SerializeField] private Transform		_Content;
	[SerializeField] private RectTransform	_ScrollViewRectTransform;
	[SerializeField] private RectTransform	_ViewportRectTransform;
	[SerializeField] private GameObject		_AudioTrackLinePrefab;


	private string	m_SetupForAudioPath;


	private class CAudioTrackSet
	{
		public GameObject	m_LineGO	= null;
	}
	private List<CAudioTrackSet>	m_lAudioTrackSets		= new List<CAudioTrackSet>();


    void Start()
    {
		UpdateSets();
	}

	void Update()
    {
		UpdateSets();
	}

	private void AddAudioTrackSet( string title, bool bEnabled )
	{
		GameObject newLineGO = GameObject.Instantiate( _AudioTrackLinePrefab, _Content );
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
					int iIndex = m_lAudioTrackSets.Count;
					button.onClick.AddListener( delegate
					{
						_OptionsMenu.ChangeAudioTrack( iIndex );
					} );
				}
			}

			// Add it to the list
			CAudioTrackSet cAudioTrackSet = new CAudioTrackSet();
			cAudioTrackSet.m_LineGO = newLineGO;
			m_lAudioTrackSets.Add( cAudioTrackSet );
		}
	}

	public void UpdateSets()
	{
		if( _MediaPlayer != null && _MediaPlayer.Control.HasMetaData() )
		{
			if( m_SetupForAudioPath == null || !m_SetupForAudioPath.Equals( _MediaPlayer.MediaPath.Path ) )
			{
				m_SetupForAudioPath = _MediaPlayer.MediaPath.Path;

				foreach( CAudioTrackSet AudioTrackSet in m_lAudioTrackSets )
				{
					GameObject.Destroy( AudioTrackSet.m_LineGO );
					AudioTrackSet.m_LineGO = null;
				}

				// Remove everything
				m_lAudioTrackSets.Clear();

				// Add all subtitle sets
				foreach( AudioTrack AudioTrack in _MediaPlayer.AudioTracks.GetAudioTracks() )
				{
					AddAudioTrackSet( AudioTrack.DisplayName, false );
				}

				if( m_lAudioTrackSets.Count > 1 )
				{
					// Reposition everything
					float fLineHeight = 40.0f;
					float fTotalHeight = fLineHeight * m_lAudioTrackSets.Count;

					RectTransform contentRectTransform = ( _Content != null ) ? _Content.GetComponent<RectTransform>() : null;
					if( contentRectTransform != null )
					{
						contentRectTransform.sizeDelta = new Vector2( contentRectTransform.sizeDelta.x, fTotalHeight );
					}

					float fY = (fTotalHeight * 0.5f) - (fLineHeight * 0.5f);
					foreach( CAudioTrackSet AudioTrackSet in m_lAudioTrackSets )
					{
						RectTransform rectTransform = AudioTrackSet.m_LineGO.GetComponent<RectTransform>();
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

				ChangeAudioTrack( _MediaPlayer.AudioTracks.GetAudioTracks().GetActiveTrackIndex(), false );
			}
		}
	}

	public void ChangeAudioTrack( int iTrackIndex, bool bSetTrack = true )
	{
		AudioTracks audioTracks = ( _MediaPlayer ) ? _MediaPlayer.AudioTracks.GetAudioTracks() : null;
		if( audioTracks != null )
		{
			if( bSetTrack )
			{
				// Change Audio track on the media player
				_MediaPlayer.AudioTracks.SetActiveAudioTrack( ( iTrackIndex > -1 && iTrackIndex < audioTracks.Count ) ? audioTracks[ iTrackIndex ] : null );
			}

			// Sort out UI
			int iIndex = 0;
			foreach ( CAudioTrackSet AudioTrackSet in m_lAudioTrackSets )
			{
				Transform tickIconTransform = AudioTrackSet.m_LineGO.transform.Find( "TickIcon" );
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
