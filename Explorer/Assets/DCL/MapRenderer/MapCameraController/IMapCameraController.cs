﻿using DCL.MapRenderer.CommonBehavior;
using DCL.MapRenderer.MapLayers;
using System;
using UnityEngine;

namespace DCL.MapRenderer.MapCameraController
{
    public interface IMapCameraController
    {
        MapLayer EnabledLayers { get; }

        RenderTexture GetRenderTexture();

        void ResizeTexture(Vector2Int textureResolution);

        IMapInteractivityController GetInteractivityController();

        float GetVerticalSizeInLocalUnits();

        float Zoom { get; }

        /// <summary>
        /// Position in local coordinates
        /// </summary>
        Vector2 LocalPosition { get; }

        /// <summary>
        /// Position in parcels
        /// </summary>
        Vector2 CoordsPosition { get; }

        /// <summary>
        /// Zoom corresponds to the zoom level normalized between 0 and 1
        /// Zoom level holds the actual step in the zoom scale
        /// </summary>
        /// <param name="value"></param>
        void SetZoom(float value, int zoomLevel);

        /// <summary>
        /// Sets Camera Position
        /// </summary>
        /// <param name="coordinates">Parcel-based unclamped coordinates</param>
        void SetPosition(Vector2 coordinates);

        /// <summary>
        /// Set Camera Position in local coordinates
        /// </summary>
        /// <param name="localCameraPosition"></param>
        void SetLocalPosition(Vector2 localCameraPosition);

        void SetPositionAndZoom(Vector2 coordinates, float zoom);

        void TranslateTo(Vector2 coordinates, float duration, Action? onComplete = null);

        /// <summary>
        /// Pauses rendering without releasing
        /// (all logic under the hood keeps executing)
        /// </summary>
        void SuspendRendering();

        /// <summary>
        /// Resumes rendering
        /// </summary>
        void ResumeRendering();

        void Release(IMapActivityOwner owner);
    }
}
