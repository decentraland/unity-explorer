namespace DCL.Utilities.Extensions
{
    public static class VectorExtensions
    {
        public static string ToShortString(this UnityEngine.Vector3 vector, int decimalPlaces = 4) =>
            $"({vector.x.ToString($"F{decimalPlaces}")}, {vector.y.ToString($"F{decimalPlaces}")}, {vector.z.ToString($"F{decimalPlaces}")})";

        public static UnityEngine.Vector3 GetDirection(this UnityEngine.Vector3 from, UnityEngine.Vector3 to) =>
            new (to.x - from.x, to.y - from.y, to.z - from.z);

        public static UnityEngine.Vector3 GetYFlattenDirection(this UnityEngine.Vector3 from, UnityEngine.Vector3 to) =>
            new (to.x - from.x, 0, to.z - from.z);
    }
}
