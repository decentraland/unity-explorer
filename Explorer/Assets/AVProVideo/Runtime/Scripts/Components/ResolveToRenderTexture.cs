using UnityEngine;

//-----------------------------------------------------------------------------
// Copyright 2019-2023 RenderHeads Ltd.  All rights reserved.
//-----------------------------------------------------------------------------

namespace RenderHeads.Media.AVProVideo
{
	/// Renders the video texture to a RenderTexture - either one provided by the user (external) or to an internal one.
	/// The video frames can optionally be "resolved" to unpack packed alpha, display a single stereo eye, generate mip maps, and apply colorspace conversions
	[AddComponentMenu("AVPro Video/Resolve To RenderTexture", 330)]
	[HelpURL("https://www.renderheads.com/products/avpro-video/")]
	public class ResolveToRenderTexture : MonoBehaviour
	{
		[SerializeField] MediaPlayer _mediaPlayer = null;
		[SerializeField] VideoResolveOptions _options = VideoResolveOptions.Create();
		[SerializeField] VideoRender.ResolveFlags _resolveFlags = (VideoRender.ResolveFlags.ColorspaceSRGB | VideoRender.ResolveFlags.Mipmaps | VideoRender.ResolveFlags.PackedAlpha | VideoRender.ResolveFlags.StereoLeft);
		[SerializeField] RenderTexture _externalTexture = null;

		private Material _materialResolve;
		private bool _isMaterialSetup;
		private bool _isMaterialDirty;
		private bool _isMaterialOES;
		private RenderTexture _internalTexture;
		private int _textureFrameCount = -1;

		// Material used for blitting the texture as we need a shader to provide clamp to border colour style texture sampling
		private Material _materialBlit;
		private int _srcTexId;

		public MediaPlayer MediaPlayer
		{
			get
			{
				return _mediaPlayer;
			}
			set
			{
				ChangeMediaPlayer(value);
			}
		}

		public VideoResolveOptions VideoResolveOptions
		{
			get
			{
				return _options;
			}
			set
			{
				_options = value;
				_isMaterialDirty = true;
			}
		}

		public RenderTexture ExternalTexture
		{
			get
			{
				return _externalTexture;
			}
			set
			{
				_externalTexture = value;
			}
		}

		public RenderTexture TargetTexture
		{
			get
			{
				if (_externalTexture == null)
					return _internalTexture;
				return _externalTexture;
			}
		}

		public void SetMaterialDirty()
		{
			_isMaterialDirty = true;
		}

		private void ChangeMediaPlayer(MediaPlayer mediaPlayer)
		{
			if (_mediaPlayer != mediaPlayer)
			{
				_mediaPlayer = mediaPlayer;
				_textureFrameCount = -1;
				_isMaterialSetup = false;
				_isMaterialDirty = true;
				Resolve();
			}
		}

		void Start()
		{
			_isMaterialOES = _mediaPlayer != null ? _mediaPlayer.IsUsingAndroidOESPath() : false;
			_materialResolve = VideoRender.CreateResolveMaterial(_isMaterialOES);
			VideoRender.SetupMaterialForMedia(_materialResolve, _mediaPlayer, -1);

			_materialBlit = new Material(Shader.Find("AVProVideo/Internal/Blit"));
			_srcTexId = Shader.PropertyToID("_SrcTex");
		}

		void LateUpdate()
		{
			Resolve();
		}

		public void Resolve()
		{
			ITextureProducer textureProducer = _mediaPlayer != null ? _mediaPlayer.TextureProducer : null;
			if (textureProducer == null)
				return;

			if (textureProducer.GetTexture())
			{
				// Check for a swap between OES and none-OES
				bool playerIsOES = _mediaPlayer.IsUsingAndroidOESPath();
				if (_isMaterialOES != playerIsOES)
				{
					_isMaterialOES = playerIsOES;
					_materialResolve = VideoRender.CreateResolveMaterial(playerIsOES);
				}

				if (!_isMaterialSetup)
				{
					VideoRender.SetupMaterialForMedia(_materialResolve, _mediaPlayer, -1);
					_isMaterialSetup = true;
					_isMaterialDirty = true;
				}

				if (_isMaterialDirty)
				{
					VideoRender.SetupResolveMaterial(_materialResolve, _options);
					_isMaterialDirty = false;
				}

				int textureFrameCount = textureProducer.GetTextureFrameCount();
				if (textureFrameCount != _textureFrameCount)
				{
					_internalTexture = VideoRender.ResolveVideoToRenderTexture(_materialResolve, _internalTexture, textureProducer, _resolveFlags);
					_textureFrameCount = textureFrameCount;

					if (_internalTexture && _externalTexture)
					{
						float srcAspectRatio = (float)_internalTexture.width / (float)_internalTexture.height;
						float dstAspectRatio = (float)_externalTexture.width / (float)_externalTexture.height;

						Vector2 offset = Vector2.zero;
						Vector2 scale = new Vector2(1.0f, 1.0f);

						// No point in handling the aspect ratio if the textures dimension's are the same
						if (srcAspectRatio != dstAspectRatio)
						{
							switch (_options.aspectRatio)
							{
							case VideoResolveOptions.AspectRatio.NoScaling:
								scale.x = (float)_externalTexture.width / (float)_internalTexture.width;
								scale.y = (float)_externalTexture.height / (float)_internalTexture.height;
								offset.x = (1.0f - scale.x) * 0.5f;
								offset.y = (1.0f - scale.y) * 0.5f;
								break;

							case VideoResolveOptions.AspectRatio.FitVertically:
								scale.x = (float)_internalTexture.height / (float)_internalTexture.width * dstAspectRatio;
								offset.x = (1.0f - scale.x) * 0.5f;
								break;

							case VideoResolveOptions.AspectRatio.FitHorizontally:
								scale.y = (float)_externalTexture.height / (float)_externalTexture.width * srcAspectRatio;
								offset.y = (1.0f - scale.y) * 0.5f;
								break;

							case VideoResolveOptions.AspectRatio.FitInside:
							{
								if (srcAspectRatio > dstAspectRatio)
									goto case VideoResolveOptions.AspectRatio.FitHorizontally;
								else if (srcAspectRatio < dstAspectRatio)
									goto case VideoResolveOptions.AspectRatio.FitVertically;
							}	break;

							case VideoResolveOptions.AspectRatio.FitOutside:
							{
								if (srcAspectRatio > dstAspectRatio)
									goto case VideoResolveOptions.AspectRatio.FitVertically;
								else if (srcAspectRatio < dstAspectRatio)
									goto case VideoResolveOptions.AspectRatio.FitHorizontally;
							}	break;

							case VideoResolveOptions.AspectRatio.Stretch:
								break;
							}
						}

						// NOTE: This blit can be removed once we can ResolveVideoToRenderTexture is made not to recreate textures
						// NOTE: This blit probably doesn't do correct linear/srgb conversion if the colorspace settings differ, may have to use GL.sRGBWrite
						// NOTE: Cannot use _MainTex as Graphics.Blit replaces the texture offset and scale when using a material
						_materialBlit.SetTexture(_srcTexId, _internalTexture);
						_materialBlit.SetTextureOffset(_srcTexId, offset);
						_materialBlit.SetTextureScale(_srcTexId, scale);
						Graphics.Blit(null, _externalTexture, _materialBlit, 0);
					}
				}
			}
		}

		void OnDisable()
		{
			if (_internalTexture)
			{
				RenderTexture.ReleaseTemporary(_internalTexture);
				_internalTexture = null;
			}
		}

		void OnDestroy()
		{
			if (_materialResolve)
			{
				Destroy(_materialResolve);
				_materialResolve = null;
			}
		}
#if false
		void OnGUI()
		{
			if (TargetTexture)
			{
				GUI.DrawTexture(new Rect(0f, 0f, Screen.width * 0.8f, Screen.height * 0.8f), TargetTexture, ScaleMode.ScaleToFit, true);
			}
		}
#endif
	}
}
