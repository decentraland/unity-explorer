using System;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.MapRenderer.ComponentsFactory
{
    public class MapRendererTextureContainer
    {
        private readonly Dictionary<Vector2Int, Texture2D> chunks = new ();

        public void AddChunk(Vector2Int position, Texture2D texture2D)
        {
            chunks[position] = texture2D;
        }

        public Texture2D? GetChunk(Vector2Int position) =>
            chunks.GetValueOrDefault(position, Texture2D.grayTexture);

        public bool IsComplete() =>
            chunks.Count >= 256;
    }
}
