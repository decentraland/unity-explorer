using DCL.MapRenderer.Culling;
using System;
using UnityEngine;

namespace DCL.MapRenderer.MapLayers.Pins
{
    public interface IPinMarker : IMapRendererMarker, IMapPositionProvider, IDisposable
    {
        bool IsVisible { get; }
        bool IsDestination { get; }

        public string Title { get; }

        public string Description { get; }

        public Vector2Int ParcelPosition { get; }
        public Sprite CurrentSprite { get; }

        void SetPosition(Vector2 position, Vector2Int parcelPosition);

        void SetData(string title, string description);

        void AnimateIn();

        void AnimateOut();

        void SetAsDestination(bool isDestination);

        void SetIconOutline(bool isActive);

        void SetTexture(Texture2D texture);

        void OnBecameVisible();

        void OnBecameInvisible();

        void SetZoom(float baseScale, float baseZoom, float zoom);

        void ResetScale(float scale);
    }
}
