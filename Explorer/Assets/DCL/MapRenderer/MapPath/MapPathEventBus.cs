using DCL.MapRenderer.MapLayers.Pins;
using JetBrains.Annotations;
using System;
using UnityEngine;

namespace DCL.MapRenderer
{
    public class MapPathEventBus : IMapPathEventBus
    {
        public event Action<IPinMarker> OnShowPinInMinimapEdge;
        public event Action<Vector2Int, IPinMarker> OnSetDestination;
        public event Action OnRemovedDestination;
        public event Action<Vector2> OnUpdatedPlayerPosition;
        public event Action OnArrivedToDestination;
        public void SetDestination(Vector2Int parcel, IPinMarker pinMarker)
        {
            OnSetDestination?.Invoke(parcel, pinMarker);
        }

        public void RemoveDestination()
        {
            OnRemovedDestination?.Invoke();
        }

        public void ShowPinInMinimap(IPinMarker pinMarker)
        {
            OnShowPinInMinimapEdge?.Invoke(pinMarker);
        }

        public void PathUpdated(Vector2 newPosition)
        {
            OnUpdatedPlayerPosition?.Invoke(newPosition);
        }

        public void ArrivedToDestination()
        {
            OnRemovedDestination?.Invoke();
            OnArrivedToDestination?.Invoke();
        }

    }

    public interface IMapPathEventBus
    {
        public event Action<IPinMarker> OnShowPinInMinimapEdge;
        public event Action<Vector2Int, IPinMarker> OnSetDestination;
        public event Action OnRemovedDestination;
        public event Action OnArrivedToDestination;
        public event Action<Vector2> OnUpdatedPlayerPosition;

        void SetDestination(Vector2Int parcel, IPinMarker pinMarker);

        void RemoveDestination();

        void ShowPinInMinimap(IPinMarker pinMarker);

        void PathUpdated(Vector2 newPosition);

        void ArrivedToDestination();
    }
}
