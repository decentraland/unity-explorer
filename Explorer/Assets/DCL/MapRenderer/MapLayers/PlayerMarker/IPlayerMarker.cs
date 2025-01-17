﻿using System;
using UnityEngine;

namespace DCL.MapRenderer.MapLayers.PlayerMarker
{
    internal interface IPlayerMarker : IMapRendererMarker, IDisposable
    {
        void SetPosition(Vector3 position);

        void SetRotation(Quaternion rot);

        void SetActive(bool active);

        void SetZoom(float baseZoom, float zoom);

        void ResetToBaseScale();

        void SetBackgroundVisibility(bool backgroundIsActive);

        Transform GetTransform();
    }
}
