using System.Text;
using UnityEngine;

namespace Utility
{
    public class StringUtils
    {
        private const string CHARS = "ABCDEFGHIJ KLMNOPQRSTU VWXYZ 0123456789 abcdefghij klmnopqrstu vwxyz";
        private const int MAX_RANDOM_STRING_LENGTH = 250;
        
        private static readonly StringBuilder RANDOM_STRING_BUILDER_RESULT = new (MAX_RANDOM_STRING_LENGTH);

        public static string GenerateRandomString(int length)
        {
            RANDOM_STRING_BUILDER_RESULT.Clear();

            for (var i = 0; i < length; i++)
                RANDOM_STRING_BUILDER_RESULT.Append(CHARS[Random.Range(0, CHARS.Length)]);

            return RANDOM_STRING_BUILDER_RESULT.ToString();
        }
    }
}
