namespace DCL.Utilities.Extensions
{
    public static class FloatExtensions
    {
        public static float ClampSmallValuesToZero(this float value, float eps) =>
            value > eps ? value : 0f;
    }
}
