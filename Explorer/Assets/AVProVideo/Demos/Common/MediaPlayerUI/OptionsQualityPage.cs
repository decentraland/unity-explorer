//-----------------------------------------------------------------------------
// Copyright 2015-2024 RenderHeads Ltd.  All rights reserved.
//-----------------------------------------------------------------------------

using RenderHeads.Media.AVProVideo;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

public class OptionsQualityPage : MonoBehaviour
{
	[Header("Media Player")]
	[SerializeField] private MediaPlayer	_MediaPlayer;

	[Header("Options Menu")]
	[SerializeField] private OptionsMenu	_OptionsMenu;

	[Header("Content")]
	[SerializeField] private Transform		_Content;
	[SerializeField] private RectTransform	_ScrollViewRectTransform;
	[SerializeField] private RectTransform	_ViewportRectTransform;
	[SerializeField] private GameObject		_QualityLinePrefab;


	private string	m_SetupForVideoPath;


	private class CVariantSet
	{
		public GameObject	m_LineGO	= null;
	}
	private List<CVariantSet>	m_lVariantSets		= new List<CVariantSet>();


    void Start()
    {
		// Add 'Auto'
		AddVariantSet( "Auto", true );
	}

    void Update()
    {
		UpdateSets();
	}

	private void AddVariantSet( string title, bool bEnabled )
	{
		GameObject newLineGO = GameObject.Instantiate( _QualityLinePrefab, _Content );
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
					int iIndex = m_lVariantSets.Count;
					button.onClick.AddListener( delegate
					{
						_OptionsMenu.ChangeVideoVariant( iIndex );
					} );
				}
			}

			// Add it to the list
			CVariantSet cVariantSet = new CVariantSet();
			cVariantSet.m_LineGO = newLineGO;
			m_lVariantSets.Add( cVariantSet );
		}
	}

	public void UpdateSets()
	{
		if( _MediaPlayer != null && _MediaPlayer.Control.HasMetaData() )
		{
			if( m_SetupForVideoPath == null || !m_SetupForVideoPath.Equals( _MediaPlayer.MediaPath.Path ) )
			{
				m_SetupForVideoPath = _MediaPlayer.MediaPath.Path;

				foreach( CVariantSet VariantSet in m_lVariantSets )
				{
					GameObject.Destroy(VariantSet.m_LineGO );
					VariantSet.m_LineGO = null;
				}
				m_lVariantSets.Clear();

				// Add all variants sets
				int iNumVariants = _MediaPlayer.Variants.Count;
				for (int i = 0; i < iNumVariants; ++i)
				{
					Variant variant = _MediaPlayer.Variants[i];
					StringBuilder sb = new StringBuilder();
					if( variant.Width > 0 && variant.Height > 0 )
					{
						sb.AppendFormat("{0}x{1}", variant.Width, variant.Height);
					}
					else
					{
						sb.AppendFormat("{0}bps", variant.PeakDataRate);
					}

					if (variant.FrameRate > 0.0f)
					{
						sb.AppendFormat("@{0:G}fps", variant.FrameRate);
					}
					if (variant.VideoCodecType != CodecType.unknown)
					{
						sb.AppendFormat(" {0}", variant.VideoCodecName);
					}
					if (variant.AudioCodecType != CodecType.unknown)
					{
						sb.AppendFormat(" {0}", variant.AudioCodecName);
					}

					AddVariantSet(sb.ToString(), false);
				}

				if (iNumVariants > 0)
				{
					// Add 'Auto'
					AddVariantSet( "Auto", true );
				}

				if( m_lVariantSets.Count > 1 )
				{
					// Reposition everything
					float fLineHeight = 40.0f;
					float fTotalHeight = fLineHeight * m_lVariantSets.Count;

					RectTransform contentRectTransform = ( _Content != null ) ? _Content.GetComponent<RectTransform>() : null;
					if( contentRectTransform != null )
					{
						contentRectTransform.sizeDelta = new Vector2( contentRectTransform.sizeDelta.x, fTotalHeight );
					}

					float fY = (fTotalHeight * 0.5f) - (fLineHeight * 0.5f);
					foreach( CVariantSet variantSet in m_lVariantSets )
					{
						RectTransform rectTransform = variantSet.m_LineGO.GetComponent<RectTransform>();
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
		}
	}

	public void ChangeVideoVariant( int iVariantIndex )
	{
		if (_MediaPlayer == null || _MediaPlayer.Variants == null)
		{
			return;
		}

		Variant variant;
		if (iVariantIndex >= 0 && iVariantIndex < _MediaPlayer.Variants.Count)
		{
			variant = _MediaPlayer.Variants[iVariantIndex];
		}
		else
		{
			variant = Variant.Auto;
		}

		_MediaPlayer.Variants.SelectVariant(variant);

		// Sort out UI
		int iIndex = 0;
		foreach ( CVariantSet variantSet in m_lVariantSets )
		{
			Transform tickIconTransform = variantSet.m_LineGO.transform.Find( "TickIcon" );
			Image tickIconImage = ( tickIconTransform != null ) ? tickIconTransform.GetComponent<Image>() : null;
			if ( tickIconImage != null )
			{
				tickIconImage.enabled = iIndex == iVariantIndex;
			}

			++iIndex;
		}
	}
}
