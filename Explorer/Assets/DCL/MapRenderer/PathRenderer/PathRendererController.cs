using DCL.MapRenderer.CoordsUtils;
using System;
using UnityEngine;

namespace DCL.MapRenderer
{
    public interface IMapPathEventBus
    {
        public event Action<Vector2, bool> OnSetDestination;
        void SetDestination(Vector2 parcel, bool toMapPin);
    }

    public class MapPathEventBus : IMapPathEventBus
    {
        public event Action<Vector2, bool> OnSetDestination;

        public void SetDestination(Vector2 parcel, bool toMapPin)
        {
            OnSetDestination?.Invoke(parcel, toMapPin);
        }
    }

    public class PathRendererController
    {
        private readonly IMapPathEventBus mapPathEventBus;
        private readonly PathRenderer pathRenderer;
        private readonly ICoordsUtils coordsUtils;

        internal PathRendererController(IMapPathEventBus mapPathEventBus, PathRenderer pathRenderer, ICoordsUtils coordsUtils)
        {
            this.mapPathEventBus = mapPathEventBus;
            this.pathRenderer = pathRenderer;
            this.coordsUtils = coordsUtils;
        }

        public void Initialize(Transform originTransform)
        {
            mapPathEventBus.OnSetDestination += OnSetDestination;
            pathRenderer.SetOrigin(originTransform);
        }

        private void OnSetDestination(Vector2 parcel, bool toMapPin)
        {
            pathRenderer.SetDestination(coordsUtils.CoordsToPositionWithOffset(parcel));

            if (!toMapPin)
            {
                //Create pin for destination
            }

        }
    }
}
