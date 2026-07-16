using Arch.Core;
using System;
using UnityEngine;

namespace DCL.MapPins.Bus
{
    public class MapPinsEventBus : IMapPinsEventBus
    {
        public event IMapPinsEventBus.MapPinUpdateHandler OnUpdateMapPin;
        public event Action<Entity> OnRemoveMapPin;
        public event Action<Entity, Texture2D> OnUpdateMapPinThumbnail;

        public void RemoveMapPin(Entity entity)
        {
            OnRemoveMapPin?.Invoke(entity);
        }

        public void UpdateMapPin(Entity entity, Vector2Int position, string title, string description)
        {
            OnUpdateMapPin?.Invoke(entity, position, title, description);
        }

        public void UpdateMapPinThumbnail(Entity entity, Texture2D thumbnail)
        {
            OnUpdateMapPinThumbnail?.Invoke(entity, thumbnail);
        }
    }

    public interface IMapPinsEventBus
    {
        public delegate void MapPinUpdateHandler(Entity entity, Vector2Int position,
            string title, string description);

        public event MapPinUpdateHandler OnUpdateMapPin;
        public event Action<Entity> OnRemoveMapPin;
        public event Action<Entity, Texture2D> OnUpdateMapPinThumbnail;

        void RemoveMapPin(Entity entity);

        void UpdateMapPin(Entity entity, Vector2Int position, string title, string description);

        void UpdateMapPinThumbnail(Entity entity, Texture2D thumbnail);
    }
}
