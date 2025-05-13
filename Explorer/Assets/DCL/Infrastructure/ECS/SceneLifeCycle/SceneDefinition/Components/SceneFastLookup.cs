using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ECS.SceneLifeCycle.IncreasingRadius
{
    public struct SceneFastLookup
    {
        private readonly bool[,] grid;
        private readonly int width, height;
        private readonly int offsetX, offsetY;

        //Allows fast lookup to check if a parcel is contained in the scene
        public SceneFastLookup(IReadOnlyList<Vector2Int> vectorList)
        {
            // Compute min/max values dynamically
            offsetX = vectorList.Min(v => v.x);
            int maxX = vectorList.Max(v => v.x);
            offsetY = vectorList.Min(v => v.y);
            int maxY = vectorList.Max(v => v.y);

            // Compute width & height
            width = maxX - offsetX + 1;
            height = maxY - offsetY + 1;

            grid = new bool[width, height];

            foreach (Vector2Int v in vectorList) { grid[v.x - offsetX, v.y - offsetY] = true; }
        }

        public readonly bool Contains(int xPoint, int yPoint)
        {
            int x = xPoint - offsetX;
            int y = yPoint - offsetY;
            return x >= 0 && y >= 0 && x < width && y < height && grid[x, y];
        }
    }
}
