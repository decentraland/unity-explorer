namespace DCL.Utilities.Extensions
{
    public static class VectorExtensions
    {
        public static string ToShortString(this UnityEngine.Vector3 vector, int decimalPlaces = 4) =>
            $"({vector.x.ToString($"F{decimalPlaces}")}, {vector.y.ToString($"F{decimalPlaces}")}, {vector.z.ToString($"F{decimalPlaces}")})";
    }
}
