using Unity.Mathematics;
using UnityEngine;

namespace DCL.Landscape
{
    public class TerrainBoundariesGenerator
    {
        private readonly TerrainFactory factory;
        private readonly int parcelSize;

        public TerrainBoundariesGenerator(TerrainFactory factory, int parcelSize)
        {
            this.factory = factory;
            this.parcelSize = parcelSize;
        }

        public Transform SpawnCliffs(int2 minInUnits, int2 maxInUnits)
        {
            Transform cliffsRoot = factory.CreateCliffsRoot(factory.Root);

            factory.CreateCliffCorner(cliffsRoot, new Vector3(minInUnits.x, 0, minInUnits.y), Quaternion.Euler(0, 180, 0));
            factory.CreateCliffCorner(cliffsRoot, new Vector3(minInUnits.x, 0, maxInUnits.y), Quaternion.Euler(0, 270, 0));
            factory.CreateCliffCorner(cliffsRoot, new Vector3(maxInUnits.x, 0, minInUnits.y), Quaternion.Euler(0, 90, 0));
            factory.CreateCliffCorner(cliffsRoot, new Vector3(maxInUnits.x, 0, maxInUnits.y), Quaternion.identity);

            // Horizontal layers
            for (int i = minInUnits.x; i < maxInUnits.x; i += parcelSize)
                factory.CreateCliffSide(cliffsRoot, new Vector3(i, 0, maxInUnits.y), Quaternion.identity);

            for (int i = minInUnits.x; i < maxInUnits.x; i += parcelSize)
                factory.CreateCliffSide(cliffsRoot, new Vector3(i + parcelSize, 0, minInUnits.y), Quaternion.Euler(0, 180, 0));

            // Vertical layers
            for (int i = minInUnits.y; i < maxInUnits.y; i += parcelSize)
                factory.CreateCliffSide(cliffsRoot, new Vector3(minInUnits.x, 0, i), Quaternion.Euler(0, 270, 0));

            for (int i = minInUnits.y; i < maxInUnits.y; i += parcelSize)
                factory.CreateCliffSide(cliffsRoot, new Vector3(maxInUnits.x, 0, i + parcelSize), Quaternion.Euler(0, 90, 0));

            cliffsRoot.localPosition = Vector3.zero;

            return cliffsRoot;
        }

        public Transform SpawnBorderColliders(int2 minInUnits, int2 maxInUnits, int2 sidesLength)
        {
            Transform collidersRoot = factory.CreateCollidersRoot(factory.Root);

            const float HEIGHT = 50.0f; // Height of the collider
            const float THICKNESS = 10.0f; // Thickness of the collider

            // Create colliders along each side of the terrain
            AddCollider(minInUnits.x, minInUnits.y, sidesLength.x, "South Border Collider", new int2(0, -1), 0);
            AddCollider(minInUnits.x, maxInUnits.y, sidesLength.x, "North Border Collider", new int2(0, 1), 0);
            AddCollider(minInUnits.x, minInUnits.y, sidesLength.y, "West Border Collider", new int2(-1, 0), 90);
            AddCollider(maxInUnits.x, minInUnits.y, sidesLength.y, "East Border Collider", new int2(1, 0), 90);
            return collidersRoot;

            void AddCollider(float posX, float posY, float length, string name, int2 dir,
                float rotation)
            {
                float xShift = dir.x == 0 ? length / 2 : ((THICKNESS / 2) + parcelSize) * dir.x;
                float yShift = dir.y == 0 ? length / 2 : ((THICKNESS / 2) + parcelSize) * dir.y;

                factory.CreateBorderCollider(name, collidersRoot,
                    size: new Vector3(length, HEIGHT, THICKNESS),
                    position: new Vector3(posX + xShift, HEIGHT / 2, posY + yShift), rotation);
            }
        }
    }
}
