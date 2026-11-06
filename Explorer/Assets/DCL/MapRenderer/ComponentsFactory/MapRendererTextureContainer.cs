using DCL.Diagnostics;
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

        public Texture2D GetChunk(Vector2Int position)
        {
            if (chunks.TryGetValue(position, out Texture2D texture))
                return texture;

            ReportHub.LogError(ReportCategory.LANDSCAPE, $"Satellite chunk {position} is missing");
            return Texture2D.blackTexture;
        }

        public bool IsComplete() =>
            chunks.Count >= 64;
    }
}
