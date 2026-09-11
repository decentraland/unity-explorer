using System;
using Data;
using Services;
using UnityEngine;
using UnityEngine.UIElements;

namespace UI.Elements
{
    [UxmlElement]
    public partial class WearableItemElement : PreviewButtonElement
    {
        private const string USS_BLOCK = "wearable-preview-button";

        public Action<WearableItemElement> WearableClicked { get; set; }
        public EntityDefinition Wearable { get; private set; }
        public Texture2D EmptyTexture { get; set; }
        
        private int textureRequestHandle = 0;
        
        public WearableItemElement()
        {
            AddToClassList(USS_BLOCK);

            Clicked += () => WearableClicked?.Invoke(this);
        }

        public void SetWearable(EntityDefinition wearable)
        {
            if (textureRequestHandle > 0)
            {
                RemoteTextureService.Instance.RemoveListener(textureRequestHandle);
                textureRequestHandle = 0;
            }

            Wearable = wearable;

            if (Wearable == null)
            {
                SetTexture(EmptyTexture);
            }
            else
            {
                textureRequestHandle =
                    RemoteTextureService.Instance.RequestTexture(wearable.Thumbnail, OnThumbnailLoaded,
                        OnThumbnailLoadError);
            }
        }

        private void OnThumbnailLoaded(Texture2D tex)
        {
            if (panel == null) return;

            SetTexture(tex);
            textureRequestHandle = 0;
        }

        private void OnThumbnailLoadError()
        {
            // TODO
            textureRequestHandle = 0;
        }
    }
}