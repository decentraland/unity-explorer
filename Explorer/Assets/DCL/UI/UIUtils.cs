namespace DCL.UI
{
    public static class UIUtils
    {
        public static string GetKFormat(int num)
        {
            if (num < 1000)
                return num.ToString();

            float divided = num / 1000.0f;
            divided = (int)(divided * 100) / 100f;
            return $"{divided:F2}k";
        }
    }
}
