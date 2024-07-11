using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.UIElements;

namespace DCL.SDKComponents.SceneUI.Classes
{
    public class DCLImage
    {
        private static readonly Vertex[] VERTICES = new Vertex[4];
        private static readonly ushort[] INDICES = { 0, 1, 2, 2, 3, 0 };

        [CanBeNull] private VisualElement canvas;
        private DCLImageScaleMode scaleMode;
        private Texture2D texture2D;
        private Vector4 slices;
        private Color color;
        private DCLUVs uvs;

        private bool customMeshGenerationRequired;

        public DCLImageScaleMode ScaleMode
        {
            set => SetScaleMode(value);
        }

        public Texture2D Texture
        {
            get => texture2D;
            set => SetTexture(value);
        }

        public Vector4 Slices
        {
            set => SetSlices(value);
        }

        public Color Color
        {
            set => SetColor(value);
        }

        public DCLUVs UVs
        {
            set => SetUVs(value);
        }

        public void Initialize(VisualElement canvasToApply)
        {
            if (canvasToApply == null) return;

            texture2D = null;
            scaleMode = default(DCLImageScaleMode);
            slices = Vector4.zero;
            color = new Color(1, 1, 1, 0);
            uvs = default(DCLUVs);
            canvas = canvasToApply;

            canvasToApply.generateVisualContent += OnGenerateVisualContent;
        }

        public void Dispose()
        {
           if (canvas != null)
                canvas.generateVisualContent -= OnGenerateVisualContent;

            texture2D = null;

            // Reset overriden styles
            style.backgroundImage = new StyleBackground(StyleKeyword.Null);
            style.backgroundColor = new StyleColor(StyleKeyword.Null);

            // scaleMode = default(DCLImageScaleMode);
            // slices = Vector4.zero;
            // color = new Color(1, 1, 1, 0);
            // uvs = default(DCLUVs);

            // canvas = null;
        }

        private void SetScaleMode(DCLImageScaleMode scaleModeValue)
        {
            if (scaleMode == scaleModeValue)
                return;

            scaleMode = scaleModeValue;
            ResolveGenerationWay();
        }

        private void SetTexture(Texture2D texture)
        {
            if (texture2D == texture)
                return;

            texture2D = texture;
            ResolveGenerationWay();
        }

        private void SetSlices(Vector4 slicesValue)
        {
            if (slices == slicesValue)
                return;

            slices = slicesValue;
            ResolveGenerationWay();
        }

        private void SetColor(Color colorValue)
        {
            if (color == colorValue)
                return;

            color = colorValue;
            ResolveGenerationWay();
        }

        private void SetUVs(DCLUVs uvsValue)
        {
            if (uvs.Equals(uvsValue))
                return;

            uvs = uvsValue;
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

            canvas?.MarkDirtyRepaint();
        }

        private void AdjustUVs()
        {
            // check uvs
            if (uvs.Equals(default(DCLUVs)))
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
            if (canvas == null) return;

            // Instead of generating a sliced mesh manually pass it to the existing logic of background
            canvas!.style.backgroundImage = Background.FromTexture2D(texture2D);
            canvas!.style.unityBackgroundImageTintColor = new StyleColor(color);
            canvas!.style.backgroundColor = new StyleColor(StyleKeyword.None);

            int texWidth = texture2D.width;
            int texHeight = texture2D.height;

            // convert slices to absolute values
            canvas!.style.unitySliceLeft = new StyleInt((int)(slices[0] * texWidth));
            canvas!.style.unitySliceTop = new StyleInt((int)(slices[1] * texHeight));
            canvas!.style.unitySliceRight = new StyleInt((int)(slices[2] * texWidth));
            canvas!.style.unitySliceBottom = new StyleInt((int)(slices[3] * texHeight));
            customMeshGenerationRequired = false;
        }

        private void SetCentered()
        {
            if (canvas == null) return;

            canvas!.style.backgroundImage = new StyleBackground(StyleKeyword.Null);
            canvas!.style.backgroundColor = new StyleColor(StyleKeyword.None);
            customMeshGenerationRequired = true;
        }

        private void SetStretched()
        {
            if (canvas == null) return;

            canvas!.style.backgroundImage = new StyleBackground(StyleKeyword.Null);
            canvas!.style.backgroundColor = new StyleColor(StyleKeyword.None);
            customMeshGenerationRequired = true;
        }

        private void SetSolidColor()
        {
            if (canvas == null) return;

            canvas!.style.backgroundImage = new StyleBackground(StyleKeyword.None);
            canvas!.style.backgroundColor = new StyleColor(color);
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
            if (canvas == null) return;

            // in local coords
            Rect r = canvas.contentRect;

            float left = 0;
            float right = r.width;
            float top = 0;
            float bottom = r.height;

            VERTICES[0].position = new Vector3(left, bottom, Vertex.nearZ);
            VERTICES[1].position = new Vector3(left, top, Vertex.nearZ);
            VERTICES[2].position = new Vector3(right, top, Vertex.nearZ);
            VERTICES[3].position = new Vector3(right, bottom, Vertex.nearZ);

            MeshWriteData mwd = mgc.Allocate(VERTICES.Length, INDICES.Length, texture2D);

            // uv Rect [0;1] that was assigned by the Dynamic atlas by UI Toolkit
            Rect uvRegion = mwd.uvRegion;

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
            if (canvas == null) return;

            // in local coords
            Rect r = canvas.contentRect;

            Vector3 panelScale = canvas.worldTransform.lossyScale;
            float targetTextureWidth = texture2D.width * panelScale[0];
            float targetTextureHeight = texture2D.height * panelScale[1];

            // Remain the original center
            Vector2 center = r.center;

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

            MeshWriteData mwd = mgc.Allocate(VERTICES.Length, INDICES.Length, texture2D);

            // uv Rect [0;1] that was assigned by the Dynamic atlas by UI Toolkit
            Rect uvRegion = mwd.uvRegion;

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
