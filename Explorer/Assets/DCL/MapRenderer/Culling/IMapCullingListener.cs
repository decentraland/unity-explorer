﻿namespace DCL.MapRenderer.Culling
{
    internal interface IMapCullingListener<in T> where T : IMapPositionProvider
    {
        void OnMapObjectBecameVisible(T obj);

        void OnMapObjectCulled(T obj);
    }
}
