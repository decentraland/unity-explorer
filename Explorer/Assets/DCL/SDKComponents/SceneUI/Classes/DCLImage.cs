﻿using UnityEngine;
using UnityEngine.UIElements;

namespace DCL.SDKComponents.SceneUI.Classes
{
    public class DCLImage
    {
        private static readonly Vertex[] VERTICES = new Vertex[4];
        private static readonly ushort[] INDICES = { 0, 1, 2, 2, 3, 0 };

        private VisualElement canvas;
        private DCLImageScaleMode scaleMode;
        private Texture2D? texture2D;
        private Vector4 slices;
        private Color color;
        private DCLUVs uvs;

        private bool customMeshGenerationRequired { get; set; }

        public DCLImageScaleMode ScaleMode
        {
            get => scaleMode;
            set => SetScaleMode(value);
        }

        public Texture2D Texture
        {
            get => texture2D;
            set => SetTexture(value);
        }

        public Vector4 Slices
        {
            get => slices;
            set => SetSlices(value);
        }

        public Color Color
        {
            get => color;
            set => SetColor(value);
        }

        public DCLUVs UVs
        {
            get => uvs;
            set => SetUVs(value);
        }

        public bool IsHidden
        {
            get => canvas.style.opacity == 0f;
            set => canvas.style.opacity = value ? 0f : 1f;
        }

        private IStyle style => canvas.style;

        public void Initialize(VisualElement canvasToApply)
        {
            this.texture2D = null;
            this.scaleMode = default(DCLImageScaleMode);
            this.slices = Vector4.zero;
            this.color = new Color(1, 1, 1, 0);
            this.uvs = default(DCLUVs);
            this.canvas = canvasToApply;
            IsHidden = false;

            canvasToApply.generateVisualContent += OnGenerateVisualContent;
        }

        public void Dispose()
        {
            texture2D = null;
            canvas.generateVisualContent -= OnGenerateVisualContent;

            // Reset overriden styles
            style.backgroundImage = new StyleBackground(StyleKeyword.Null);
            style.backgroundColor = new StyleColor(StyleKeyword.Null);
        }

        private void SetScaleMode(DCLImageScaleMode scaleModeValue)
        {
            if (this.scaleMode == scaleModeValue)
                return;

            this.scaleMode = scaleModeValue;
            ResolveGenerationWay();
        }

        private void SetTexture(Texture2D texture)
        {
            if (this.texture2D == texture)
                return;

            this.texture2D = texture;
            ResolveGenerationWay();
        }

        private void SetSlices(Vector4 slicesValue)
        {
            if (this.slices == slicesValue)
                return;

            this.slices = slicesValue;
            ResolveGenerationWay();
        }

        private void SetColor(Color colorValue)
        {
            if (this.color == colorValue)
                return;

            this.color = colorValue;
            ResolveGenerationWay();
        }

        private void SetUVs(DCLUVs uvsValue)
        {
            if (this.uvs.Equals(uvsValue))
                return;

            this.uvs = uvsValue;
            ResolveGenerationWay();
        }

        private void ResolveGenerationWay()
        {
            if (texture2D != null)
            {
                switch (scaleMode)
                {
                    case DCLImageScaleMode.Center:
                        SetCentered();
                        break;
                    case DCLImageScaleMode.Stretch:
                        AdjustUVs();
                        SetStretched();
                        break;
                    case DCLImageScaleMode.NineSlices:
                        AdjustSlices();
                        SetSliced();
                        break;
                }
            }
            else SetSolidColor();

            canvas.MarkDirtyRepaint();
        }

        private void AdjustUVs()
        {
            // check uvs
            if (uvs.Equals(default))
                uvs = DCLUVs.Default;
        }

        private void AdjustSlices()
        {
            if (slices[0] + slices[2] > 1f)
            {
                slices[0] = Mathf.Min(1f, slices[0]);
                slices[2] = 1f - slices[0];
            }

            if (slices[1] + slices[3] > 1f)
            {
                slices[1] = Mathf.Min(1f, slices[1]);
                slices[3] = 1f - slices[1];
            }
        }

        private void SetSliced()
        {
            // Instead of generating a sliced mesh manually pass it to the existing logic of background
            style.backgroundImage = Background.FromTexture2D(texture2D);
            style.unityBackgroundImageTintColor = new StyleColor(color);
            style.backgroundColor = new StyleColor(StyleKeyword.None);

            int texWidth = texture2D.width;
            int texHeight = texture2D.height;

            // convert slices to absolute values
            style.unitySliceLeft = new StyleInt((int)(slices[0] * texWidth));
            style.unitySliceTop = new StyleInt((int)(slices[1] * texHeight));
            style.unitySliceRight = new StyleInt((int)(slices[2] * texWidth));
            style.unitySliceBottom = new StyleInt((int)(slices[3] * texHeight));
            customMeshGenerationRequired = false;
        }

        private void SetCentered()
        {
            style.backgroundImage = new StyleBackground(StyleKeyword.Null);
            style.backgroundColor = new StyleColor(StyleKeyword.None);
            customMeshGenerationRequired = true;
        }

        private void SetStretched()
        {
            style.backgroundImage = new StyleBackground(StyleKeyword.Null);
            style.backgroundColor = new StyleColor(StyleKeyword.None);
            customMeshGenerationRequired = true;
        }

        private void SetSolidColor()
        {
            style.backgroundImage = new StyleBackground(StyleKeyword.None);
            style.backgroundColor = new StyleColor(color);
            customMeshGenerationRequired = false;
        }

        private void OnGenerateVisualContent(MeshGenerationContext mgc)
        {
            if (!customMeshGenerationRequired)
                return;

            switch (scaleMode)
            {
                case DCLImageScaleMode.Center:
                    GenerateCenteredTexture(mgc);
                    break;
                case DCLImageScaleMode.Stretch:
                    GenerateStretched(mgc);
                    break;
            }
        }

        private void GenerateStretched(MeshGenerationContext mgc)
        {
            // in local coords
            var r = canvas.contentRect;

            float left = 0;
            float right = r.width;
            float top = 0;
            float bottom = r.height;

            VERTICES[0].position = new Vector3(left, bottom, Vertex.nearZ);
            VERTICES[1].position = new Vector3(left, top, Vertex.nearZ);
            VERTICES[2].position = new Vector3(right, top, Vertex.nearZ);
            VERTICES[3].position = new Vector3(right, bottom, Vertex.nearZ);

            var mwd = mgc.Allocate(VERTICES.Length, INDICES.Length, texture2D);

            // uv Rect [0;1] that was assigned by the Dynamic atlas by UI Toolkit
            var uvRegion = mwd.uvRegion;

            VERTICES[0].uv = (uvs.BottomLeft * uvRegion.size) + uvRegion.min;
            VERTICES[1].uv = (uvs.TopLeft * uvRegion.size) + uvRegion.min;
            VERTICES[2].uv = (uvs.TopRight * uvRegion.size) + uvRegion.min;
            VERTICES[3].uv = (uvs.BottomRight * uvRegion.size) + uvRegion.min;

            ApplyVerticesTint();

            mwd.SetAllVertices(VERTICES);
            mwd.SetAllIndices(INDICES);
        }

        private void GenerateCenteredTexture(MeshGenerationContext mgc)
        {
            // in local coords
            var r = canvas.contentRect;

            var panelScale = canvas.worldTransform.lossyScale;
            float targetTextureWidth = texture2D.width * panelScale[0];
            float targetTextureHeight = texture2D.height * panelScale[1];

            // Remain the original center
            var center = r.center;

            float width = Mathf.Min(r.width, targetTextureWidth);
            float height = Mathf.Min(r.height, targetTextureHeight);

            float left = center.x - (width / 2f);
            float right = center.x + (width / 2f);
            float top = center.y - (height / 2f);
            float bottom = center.y + (height / 2f);

            VERTICES[0].position = new Vector3(left, bottom, Vertex.nearZ);
            VERTICES[1].position = new Vector3(left, top, Vertex.nearZ);
            VERTICES[2].position = new Vector3(right, top, Vertex.nearZ);
            VERTICES[3].position = new Vector3(right, bottom, Vertex.nearZ);

            var mwd = mgc.Allocate(VERTICES.Length, INDICES.Length, texture2D);

            // uv Rect [0;1] that was assigned by the Dynamic atlas by UI Toolkit
            var uvRegion = mwd.uvRegion;

            // the texture should be cut off if it exceeds the parent rect
            float uvsDisplacementX = (1 - (width / targetTextureWidth)) / 2f;
            float uvsDisplacementY = (1 - (height / targetTextureHeight)) / 2f;

            VERTICES[0].uv = (new Vector2(uvsDisplacementX, uvsDisplacementY) * uvRegion.size) + uvRegion.min;
            VERTICES[1].uv = (new Vector2(uvsDisplacementX, 1 - uvsDisplacementY) * uvRegion.size) + uvRegion.min;
            VERTICES[2].uv = (new Vector2(1 - uvsDisplacementX, 1 - uvsDisplacementY) * uvRegion.size) + uvRegion.min;
            VERTICES[3].uv = (new Vector2(1 - uvsDisplacementX, uvsDisplacementY) * uvRegion.size) + uvRegion.min;

            ApplyVerticesTint();

            mwd.SetAllVertices(VERTICES);
            mwd.SetAllIndices(INDICES);
        }

        private void ApplyVerticesTint()
        {
            for (var i = 0; i < VERTICES.Length; i++)
                VERTICES[i].tint = color;
        }
    }
}
