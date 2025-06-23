using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UIElements;
using UnityEngine.Video;

//-----------------------------------------------------------------------------
// Copyright 2015-2024 RenderHeads Ltd.  All rights reserved.
//-----------------------------------------------------------------------------

namespace RenderHeads.Media.AVProVideo
{
    /// <summary>
    /// displays the video to the far camera plane
    /// </summary>
    // Note:
    //  - This will not work if the camera ClearFlag is set to Skybox because of how it is rendered.
    //     the skybox is rendered at position 2000.5 between the Opaque and Transparent objects
    //     with a unique sphere scaled to the camera far plane, meaning that it will only render where
    //     nothing has been written to the depth bufffer. <- that is where the issue arrises, we are
    //     not writing to the depth buffer when rendering the video so the skybox will think nothing
    //     is their and draw over the top.  

    [AddComponentMenu("AVPro Video/Apply To Far Plane", 300)]
    [HelpURL("https://www.renderheads.com/products/avpro-video/")]
    public sealed class ApplyToFarPlane : ApplyToBase
    {
        [Header("Shader Options")]
        [Tooltip("The color override to apply to the material")]
        [SerializeField] Color _mainColor;
        public Color MainColor
        {
            get { return _mainColor; }
            set { if (!_material) CreateMaterial(); _material.SetColor("_Color", value); _mainColor = value; }
        }
        [Tooltip("The Main Texture that is being written to by the Media Player")]
        [SerializeField] Texture _texture;
        public Texture Texture
        {
            get { return _texture; }
            set { if (!_material) CreateMaterial(); _material.SetTexture("_MainTex", value); _texture = value; }
        }
        [Tooltip("The Chroma Texture to apply to the material")]
        [SerializeField] Texture _chroma;
        public Texture Chroma
        {
            get { return _chroma; }
            set { if (!_material) CreateMaterial(); _material.SetTexture("_ChromaTex", value); _chroma = value; }
        }
        [Tooltip("Alpha of the far plane that is drawn")]
        [SerializeField] float _alpha = 1f;
        public float Alpha
        {
            get { return _alpha; }
            set { if (!_material) CreateMaterial(); _material.SetFloat("_Alpha", value); _alpha = value; }
        }
        [Tooltip("The Camera far plane to draw to, if left empty main cam will be selected")]
        [SerializeField] Camera _camera;
        public Camera Camera
        {
            get { return _camera; }
            set { _camera = value; if (!_material) CreateMaterial(); _material.SetFloat("_TargetCamID", value.GetInstanceID());
            }
        }
        [Tooltip("The aspect ratio of the video shown, not used when a custom scaling is set")]
        [SerializeField] VideoAspectRatio _aspectRatio = VideoAspectRatio.Stretch;
        public VideoAspectRatio VideoAspectRatio
        {
            get { return _aspectRatio; }
            set { if (!_material) CreateMaterial(); _material.SetFloat("_Aspect", (int)value); _aspectRatio = value; }
        }
        [Tooltip("How much to offset the image by")]
        public Vector2 _drawOffset;
        public Vector2 DrawOffset
        {
            get { return  _drawOffset; }
            set { if (!_material) CreateMaterial(); _material.SetVector("_DrawOffset", value); _drawOffset = value; }
        }
        [Tooltip("Will replace the Aspect Ratio with custom scaling for the video, when both values are non-zero")]
        public Vector2 _customScaling;
        public Vector2 CustomScaling
        {
            get { return _customScaling; }
            set { if (!_material) CreateMaterial(); _material.SetVector("_CustomScale", value); _customScaling = value; }
        }

        // the object that is active as the holder for the camera far plane
        private GameObject _renderedObject;
        private bool _changedSkybox;

        public void Awake()
        {
            // if the camera was then set the camera to the main camera in the scene
            if (!_camera)
                _camera = Camera.main;
            if (_material)
                _material.SetFloat("_TargetCamID", _camera.GetInstanceID());
        }

        protected override void OnDisable()
        {
            // need to set background back to skybox if we disabled it
            if (_changedSkybox && _camera)
                _camera.clearFlags = CameraClearFlags.Skybox;

            base.OnDisable();
            if (_renderedObject)
                _renderedObject.SetActive(false);
        }

        private void OnDestroy()
        {
            // ensure to destroy the created object
            Destroy(_renderedObject);
        }

        public void Update()
        {
            // move the rendered object to ensure that it will allways be rendered by the camera,
            // ensuring that the shader is allways running to display the output on the far plane of the camera
            _renderedObject.transform.position = new Vector3(0, 0, _camera.nearClipPlane) + _camera.transform.position + _camera.transform.forward;
            _renderedObject.transform.rotation = _camera.transform.rotation;
        }


        /// <summary>
        /// Creates a Quad mesh used for basic rendering
        /// </summary>
        /// <returns>Quad created</returns>
        public Mesh CreateQuadMesh()
        {
            var width = 1;
            var height = 1;
            Mesh mesh = new Mesh();
            // verts
            Vector3[] vertices = new Vector3[4]
            {
                new Vector3(0, 0, 0),
                new Vector3(width, 0, 0),
                new Vector3(0, height, 0),
                new Vector3(width, height, 0)
            };
            mesh.vertices = vertices;
            // tris
            int[] tris = new int[6]
            {
                0, 2, 1,
                2, 3, 1
            };
            mesh.triangles = tris;
            // normals
            Vector3[] normals = new Vector3[4]
            {
                -_camera.transform.forward,
                -_camera.transform.forward,
                -_camera.transform.forward,
                -_camera.transform.forward
            };
            mesh.normals = normals;
            // uv's
            Vector2[] uv = new Vector2[4]
            {
                new Vector2(0, 0),
                new Vector2(1, 0),
                new Vector2(0, 1),
                new Vector2(1, 1)
            };
            mesh.uv = uv;
            return mesh;
        }

        public void CreateMaterial()
        {
            _material = new Material(Shader.Find("AVProVideo/Background/AVProVideo-ApplyToFarPlane"));
            if (_renderedObject)
            {
                if (_renderedObject.TryGetComponent(out ApplyToFarPlane_CameraApplier applier))
                    applier.Material = _material;
                else
                {
                    var applier2 = _renderedObject.AddComponent<ApplyToFarPlane_CameraApplier>();
                    applier2.Material = _material;
                }
            }
        }


        /*
            Below this point is basically the same as ApplyToMaterial, with a few unecessary functions
            removed.
            This is because other than the quad with fancy shader, this is just taking the video
            to a material, then applying it.
        */

        [Header("Display")]
        [Tooltip("Default texture to display when the video texture is preparing")]
        [SerializeField]
        Texture2D _defaultTexture = null;
        public Texture2D DefaultTexture
        {
            get { return _defaultTexture; }
            set
            {
                if (_defaultTexture != value)
                {
                    _defaultTexture = value;
                    _isDirty = true;
                }
            }
        }

        [Tooltip("The Material to use when rendering the video, if not set will use internal " +
            "\n Note: Material must use the AVProVideo/Background/AVProVideo-ApplyToFarPlane shader")]
        // this material must use the AVProVideo/Background/AVProVideo-ApplyToFarPlane shader
        // otherwise it will not render correctly
        [SerializeField] Material _material = null;

        [SerializeField]
        string _texturePropertyName = Helper.UnityBaseTextureName;
        public string TexturePropertyName
        {
            get { return _texturePropertyName; }
            set
            {
                if (_texturePropertyName != value)
                {
                    _texturePropertyName = value;
                    _propTexture = new LazyShaderProperty(_texturePropertyName);
                    _propTexture_R = new LazyShaderProperty(_texturePropertyName + "_R");
                    _isDirty = true;
                }
            }
        }

        [SerializeField]
        Vector2 _offset = Vector2.zero;
        public Vector2 Offset
        {
            get { return _offset; }
            set
            {
                if (_offset != value)
                {
                    _offset = value;
                    _isDirty = true;
                }
            }
        }

        [SerializeField]
        Vector2 _scale = Vector2.one;
        public Vector2 Scale
        {
            get { return _scale; }
            set
            {
                if (_scale != value)
                {
                    _scale = value;
                    _isDirty = true;
                }
            }
        }

        private Texture _lastTextureApplied;
        private LazyShaderProperty _propTexture;
        private LazyShaderProperty _propTexture_R; 

        private Texture _originalTexture;
        private Vector2 _originalScale = Vector2.one;
        private Vector2 _originalOffset = Vector2.zero;


        private Vector2 ImageSize
        {
            get { return new Vector2(_media.Info.GetVideoWidth(), _media.Info.GetVideoHeight()); }
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            if (!_material)
            {
                CreateMaterial();
            }
            // if the rendered object already exists just enable it otherwise
            // create a new one and set it up to be used correctly
            if (_renderedObject)
                _renderedObject.SetActive(true);
            else
            {
                _renderedObject = new GameObject("Display Background Object");
                //_renderedObject.hideFlags = HideFlags.HideAndDontSave;
                var rend = _renderedObject.AddComponent<MeshRenderer>();
                var filt = _renderedObject.AddComponent<MeshFilter>();
                Mesh mesh = CreateQuadMesh();
                filt.sharedMesh = mesh;
                //rend.sharedMaterial = _material;
                var applier = _renderedObject.AddComponent<ApplyToFarPlane_CameraApplier>();
                if (_camera)
                    _material.SetFloat("_TargetCamID", _camera.GetInstanceID());
                applier.Material = _material;
                rend.sharedMaterial = _material;
            }

            // ApplyToFarPlane does not work if the background clear mode is set to skybox, so if it is then change it to color
            if (_camera.clearFlags == CameraClearFlags.Skybox)
            {
                Debug.LogWarning("[AVProVideo] Warning: ApplyToFarPlane does not work with the background clear mode set to skybox, automatically changed to color, this will be undone when the object is disabled");
                _changedSkybox = true;
                _camera.clearFlags = CameraClearFlags.Color;
            }
        }

        // We do a LateUpdate() to allow for any changes in the texture that may have happened in Update()
        private void LateUpdate()
        {
            Apply();
        }

        /// <summary>
        /// Called via the Editor compoenent, this will allow updating of the material
        /// properties when they are changed rather than updating them each frame
        /// </summary>
        /// <param name="target">Which material property was effected</param>
        public void UpdateMaterialProperties(int target)
        {
            if (_material == null)
                CreateMaterial();
            switch (target)
            {
                case 0:
                    _material.SetColor("_Color", _mainColor); 
                    break;
                case 3:
                    _material.SetTexture("_MainTex", _texture);
                    break;
                case 4:
                    _material.SetTexture("_ChromaTex", _chroma);
                    break;
                case 5:
                    _material.SetFloat("_Alpha", _alpha);
                    break;
                case 7:
                    _material.SetFloat("_Aspect", (int)_aspectRatio);
                    break;
                case 8:
                    _material.SetVector("_DrawOffset", _drawOffset);
                    break;
                case 9:
                    _material.SetVector("_CustomScale", _customScaling);
                    break;
                default:
                    break;
            }
        }

        public override void Apply()
        {
            bool applied = false;

            if (_media != null && _media.TextureProducer != null)
            {
                Texture resamplerTex = _media.FrameResampler == null || _media.FrameResampler.OutputTexture == null ? null : _media.FrameResampler.OutputTexture[0];
                Texture texture = _media.UseResampler ? resamplerTex : _media.TextureProducer.GetTexture(0);
                if (texture != null)
                {
                    // Check for changing texture
                    if (texture != _lastTextureApplied)
                    {
                        _isDirty = true;
                    }

                    if (_isDirty)
                    {
                        bool requiresVerticalFlip = _media.TextureProducer.RequiresVerticalFlip();
                        StereoPacking stereoPacking = _media.TextureProducer.GetTextureStereoPacking();

                        int planeCount = 1;
                        if (!_media.UseResampler)
                        {
                            // We're not using the resampler so the number of planes will be the texture count
                            planeCount = _media.TextureProducer.GetTextureCount();
                            if (stereoPacking == StereoPacking.TwoTextures)
                            {
                                // Unless we're using two texture stereo in which case it'll be half the texture count
                                planeCount /= 2;
                            }
                        }

                        for (int plane = 0; plane < planeCount; ++plane)
                        {
                            Texture resamplerTexPlane = _media.FrameResampler == null || _media.FrameResampler.OutputTexture == null ? null : _media.FrameResampler.OutputTexture[plane];
                            texture = _media.UseResampler ? resamplerTexPlane : _media.TextureProducer.GetTexture(plane);
                            if (texture != null)
                            {
                                ApplyMapping(texture, requiresVerticalFlip, plane);
                            }
                        }

                        // Handle the right eye if we're using two texture stereo packing
                        if (stereoPacking == StereoPacking.TwoTextures)
                        {
                            for (int plane = 0; plane < planeCount; ++plane)
                            {
                                texture = _media.TextureProducer.GetTexture(planeCount + plane);
                                if (texture != null)
                                {
                                    ApplyMapping(texture, requiresVerticalFlip, plane, Eye.Right);
                                }
                            }
                        }
                    }

                    applied = true;
                }
            }

            // If the media didn't apply a texture, then try to apply the default texture
            if (!applied)
            {
                if (_defaultTexture != _lastTextureApplied)
                {
                    _isDirty = true;
                }
                if (_isDirty)
                {
#if UNITY_PLATFORM_SUPPORTS_YPCBCR
					if (_material != null && _material.HasProperty(VideoRender.PropUseYpCbCr.Id))
					{
						_material.DisableKeyword(VideoRender.Keyword_UseYpCbCr);
					}
#endif
                    ApplyMapping(_defaultTexture, false);
                }
            }
        }

        enum Eye
        {
            Left,
            Right
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="texture"></param>
        /// <param name="requiresYFlip"></param>
        /// <param name="plane"></param>
        /// <param name="eye">Which eye we're mapping, defaults to the left eye</param>
        private void ApplyMapping(Texture texture, bool requiresYFlip, int plane = 0, Eye eye = Eye.Left)
        {
            if (_material != null)
            {
                _isDirty = false;

                if (plane == 0)
                {
                    int propTextureId = _propTexture.Id;
                    if (eye == Eye.Left)
                    {
                        VideoRender.SetupMaterialForMedia(_material, _media, propTextureId, texture, texture == _defaultTexture);
                        _lastTextureApplied = texture;
#if !UNITY_EDITOR && UNITY_ANDROID
							if (texture == _defaultTexture)
							{
								_material.EnableKeyword("USING_DEFAULT_TEXTURE");
							}
							else
							{
								_material.DisableKeyword("USING_DEFAULT_TEXTURE");
							}
#endif
                    }
                    else
                    {
                        propTextureId = _propTexture_R.Id;
                        _material.SetTexture(propTextureId, texture);
                    }

                    if (texture != null)
                    {
                        if (requiresYFlip)
                        {
                            if (_material.HasProperty(propTextureId)) // editor error on not being initilised on first run
                            {
                                _material.SetTextureScale(propTextureId, new Vector2(_scale.x, -_scale.y));
                                _material.SetTextureOffset(propTextureId, Vector2.up + _offset);
                            }
                        }
                        else
                        {
                            _material.SetTextureScale(propTextureId, _scale);
                            _material.SetTextureOffset(propTextureId, _offset);
                        }
                    }
                }
                else if (plane == 1)
                {
                    if (texture != null)
                    {
                        if (requiresYFlip)
                        {
                            _material.SetTextureScale(VideoRender.PropChromaTex.Id, new Vector2(_scale.x, -_scale.y));
                            _material.SetTextureOffset(VideoRender.PropChromaTex.Id, Vector2.up + _offset);
                        }
                        else
                        {
                            _material.SetTextureScale(VideoRender.PropChromaTex.Id, _scale);
                            _material.SetTextureOffset(VideoRender.PropChromaTex.Id, _offset);
                        }
                    }
                }
            }
            else
                CreateMaterial();
        }

        protected override void SaveProperties()
        {
            if (_material != null)
            {
                if (string.IsNullOrEmpty(_texturePropertyName))
                {
                    _originalTexture = _material.mainTexture;
                    _originalScale = _material.mainTextureScale;
                    _originalOffset = _material.mainTextureOffset;
                }
                else
                {
                    _originalTexture = _material.GetTexture(_texturePropertyName);
                    _originalScale = _material.GetTextureScale(_texturePropertyName);
                    _originalOffset = _material.GetTextureOffset(_texturePropertyName);
                }
            }
            else
                CreateMaterial();
            _propTexture = new LazyShaderProperty(_texturePropertyName);
            _propTexture_R = new LazyShaderProperty(_texturePropertyName + "_R");
        }

        protected override void RestoreProperties()
        {
            if (_material != null)
            {
                if (string.IsNullOrEmpty(_texturePropertyName))
                {
                    _material.mainTexture = _originalTexture;
                    _material.mainTextureScale = _originalScale;
                    _material.mainTextureOffset = _originalOffset;
                }
                else
                {
                    _material.SetTexture(_texturePropertyName, _originalTexture);
                    _material.SetTextureScale(_texturePropertyName, _originalScale);
                    _material.SetTextureOffset(_texturePropertyName, _originalOffset);
                }
            }
            else
                CreateMaterial();
        }
    }
}