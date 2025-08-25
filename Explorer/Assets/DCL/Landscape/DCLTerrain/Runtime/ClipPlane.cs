using Unity.Mathematics;
using Unity.Mathematics.Geometry;

namespace Decentraland.Terrain
{
    public struct ClipPlane
    {
        public Plane Plane;
        public int FarCornerIndex;

        public ClipPlane(float4 coefficients)
        {
            Plane = new Plane { NormalAndDistance = Plane.Normalize(coefficients) };

            if (Plane.NormalAndDistance.x < 0f)
            {
                if (Plane.NormalAndDistance.y < 0f)
                    FarCornerIndex = Plane.NormalAndDistance.z < 0f ? 0b000 : 0b001;
                else
                    FarCornerIndex = Plane.NormalAndDistance.z < 0f ? 0b010 : 0b011;
            }
            else
            {
                if (Plane.NormalAndDistance.y < 0f)
                    FarCornerIndex = Plane.NormalAndDistance.z < 0f ? 0b100 : 0b101;
                else
                    FarCornerIndex = Plane.NormalAndDistance.z < 0f ? 0b110 : 0b111;
            }
        }
    }
}
