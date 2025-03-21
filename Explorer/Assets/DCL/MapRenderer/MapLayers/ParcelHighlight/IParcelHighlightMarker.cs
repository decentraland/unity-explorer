﻿using System;
using UnityEngine;

namespace DCL.MapRenderer.MapLayers.ParcelHighlight
{
    internal interface IParcelHighlightMarker : IMapRendererMarker, IDisposable
    {
        void SetCoordinates(Vector2Int coords, Vector3 position);

        void Activate();

        void Deactivate();

        void SetZoom(float baseZoom, float newZoom);
    }
}
