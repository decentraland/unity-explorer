using DG.Tweening;
using System;
using System.Collections.Generic;
using UnityEngine;
using Utility;
using Object = UnityEngine.Object;

namespace DCL.MapRenderer.MapLayers.Atlas.SatelliteAtlas
{
    internal class SatelliteTileViews : IDisposable
    {
        private const float FADE_IN_SECONDS = 0.25f;

        private readonly AtlasChunk template;
        private readonly int drawOrderOfBaseLevel;
        private readonly Stack<AtlasChunk> pooledViews = new ();

        public bool LayerVisible => template.transform.parent.gameObject.activeInHierarchy;

        public SatelliteTileViews(SpriteRenderer template, int drawOrderOfBaseLevel)
        {
            this.drawOrderOfBaseLevel = drawOrderOfBaseLevel;

            template.material.SetFloat(AtlasChunkConstants.SATURATION, AtlasChunkConstants.SATELLITE_SATURATION);
            this.template = template.GetComponent<AtlasChunk>();
            template.gameObject.SetActive(false);
        }

        public void Dispose()
        {
            while (pooledViews.Count > 0)
                UnityObjectUtils.SafeDestroy(pooledViews.Pop().gameObject);

            // The instance created by template.material, shared by the template and every pooled view.
            UnityObjectUtils.SafeDestroy(template.MainSpriteRenderer.sharedMaterial);
            UnityObjectUtils.SafeDestroy(template.gameObject);
        }

        public AtlasChunk Rent()
        {
            if (pooledViews.Count > 0)
                return pooledViews.Pop();

            AtlasChunk view = Object.Instantiate(template, template.transform.parent);
            view.LoadingSpriteRenderer.gameObject.SetActive(false);

#if UNITY_EDITOR
            view.gameObject.name = "Satellite detail tile";
#endif

            return view;
        }

        /// <summary>The returned sprite belongs to the caller.</summary>
        public Sprite Show(AtlasChunk view, Texture2D texture, Vector3Int id, in SatelliteTileGrid grid)
        {
            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), VectorUtilities.OneHalf, texture.width / grid.TileSize(id.z), 0,
                SpriteMeshType.FullRect, Vector4.one, false);

            SpriteRenderer renderer = view.MainSpriteRenderer;
            renderer.sprite = sprite;
            renderer.sortingOrder = drawOrderOfBaseLevel + id.z - SatelliteTileGrid.BASE_LEVEL;
            renderer.color = AtlasChunkConstants.INITIAL_COLOR;
            renderer.enabled = true;

            view.transform.localPosition = grid.TileCenter(id);
            view.gameObject.SetActive(true);
            renderer.DOColor(AtlasChunkConstants.FINAL_COLOR, FADE_IN_SECONDS);

            return sprite;
        }

        public void Return(AtlasChunk view)
        {
            // On application quit the view can already be destroyed.
            if (!view)
                return;

            SpriteRenderer renderer = view.MainSpriteRenderer;
            renderer.DOKill();
            renderer.sprite = null;
            view.gameObject.SetActive(false);
            pooledViews.Push(view);
        }
    }
}
