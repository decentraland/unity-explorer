using UnityEngine;

namespace Utility
{
    public class StringUtils
    {
        private const string CHARS = "ABCDEFGHIJ KLMNOPQRSTU VWXYZ 0123456789 abcdefghij klmnopqrstu vwxyz";
        private static string randomStringResult = "";

        public static string GenerateRandomString(int length)
        {
            randomStringResult = "";
            for (var i = 0; i < length; i++)
                randomStringResult += CHARS[Random.Range(0, CHARS.Length)];

            return randomStringResult;
        }
    }
}
