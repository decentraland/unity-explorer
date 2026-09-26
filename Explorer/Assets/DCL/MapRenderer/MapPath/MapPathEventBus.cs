using DCL.MapRenderer.MapLayers.Pins;
using JetBrains.Annotations;
using System;
using UnityEngine;

namespace DCL.MapRenderer
{
    public class MapPathEventBus : IMapPathEventBus
    {
        public event Action<IPinMarker> OnShowPinInMinimapEdge;
        public event Action<Vector2Int, IPinMarker?> OnSetDestination;
        public event Action OnRemovedDestination;
        public event Action OnHidePinInMinimapEdge;
        public event Action<Vector2> OnUpdatedPlayerPosition;
        public event Action OnArrivedToDestination;
        public event Action<Vector2> OnUpdatePinPositionInMinimapEdge;

        public void SetDestination(Vector2Int parcel, IPinMarker? pinMarker)
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

        public void HidePinInMinimap()
        {
            OnHidePinInMinimapEdge?.Invoke();
        }

        public void ArrivedToDestination()
        {
            OnRemovedDestination?.Invoke();
            OnArrivedToDestination?.Invoke();
        }

        public void UpdatePinPositionInMinimapEdge(Vector2 newPosition)
        {
            OnUpdatePinPositionInMinimapEdge?.Invoke(newPosition);
        }

    }

    public interface IMapPathEventBus
    {
        public event Action<IPinMarker> OnShowPinInMinimapEdge;
        public event Action<Vector2> OnUpdatePinPositionInMinimapEdge;
        public event Action<Vector2Int, IPinMarker?> OnSetDestination;
        public event Action OnRemovedDestination;
        public event Action OnHidePinInMinimapEdge;
        public event Action OnArrivedToDestination;
        public event Action<Vector2> OnUpdatedPlayerPosition;

        void SetDestination(Vector2Int parcel, IPinMarker? pinMarker);

        void RemoveDestination();

        void ShowPinInMinimap(IPinMarker pinMarker);

        void PathUpdated(Vector2 newPosition);

        void HidePinInMinimap();

        void ArrivedToDestination();

        void UpdatePinPositionInMinimapEdge(Vector2 newPosition);

    }
}
