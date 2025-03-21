using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ECS.SceneLifeCycle.IncreasingRadius
{
    public class Vector2IntGridLookup
    {
        private readonly bool[,] _grid;
        private readonly int _width, _height;
        private readonly int _offsetX, _offsetY;

        public Vector2IntGridLookup(IEnumerable<Vector2Int> vectorList)
        {
            // Compute min/max values dynamically
            _offsetX = vectorList.Min(v => v.x); // minX
            int maxX = vectorList.Max(v => v.x); // maxX
            _offsetY = vectorList.Min(v => v.y); // minY
            int maxY = vectorList.Max(v => v.y); // maxY

            // Compute width & height
            _width = maxX - _offsetX + 1;
            _height = maxY - _offsetY + 1;

            // Create and populate the boolean grid
            _grid = new bool[_width, _height];

            foreach (Vector2Int v in vectorList) { _grid[v.x - _offsetX, v.y - _offsetY] = true; }
        }

        public bool Contains(int xPoint, int yPoint)
        {
            int x = xPoint - _offsetX;
            int y = yPoint - _offsetY;
            return x >= 0 && y >= 0 && x < _width && y < _height && _grid[x, y];
        }
    }
}
