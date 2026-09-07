using System;
using UnityEngine;

namespace DCL.CharacterCamera
{
    /// <summary>
    ///     Grid the golden-frozen camera pose snaps to: 1/32 m per position axis and 2^-11 per quaternion
    ///     component, the snapped quaternion renormalized in double. Powers of two, so every snapped
    ///     position component is exact in float, and every step is IEEE arithmetic with one rounding, so
    ///     equal inputs snap to bit-identical outputs on every platform.
    /// </summary>
    public static class GoldenPoseGrid
    {
        public const double POSITION_GRID = 32.0;
        public const double ROTATION_GRID = 2048.0;

        public static Vector3 SnapPosition(Vector3 position) =>
            new ((float)Snap(position.x, POSITION_GRID), (float)Snap(position.y, POSITION_GRID), (float)Snap(position.z, POSITION_GRID));

        public static Quaternion SnapRotation(Quaternion rotation)
        {
            double qx = Snap(rotation.x, ROTATION_GRID);
            double qy = Snap(rotation.y, ROTATION_GRID);
            double qz = Snap(rotation.z, ROTATION_GRID);
            double qw = Snap(rotation.w, ROTATION_GRID);
            double inv = 1.0 / Math.Sqrt((qx * qx) + (qy * qy) + (qz * qz) + (qw * qw));

            return new Quaternion((float)(qx * inv), (float)(qy * inv), (float)(qz * inv), (float)(qw * inv));
        }

        public static double Snap(double value, double grid) =>
            Math.Round(value * grid) / grid;
    }
}
