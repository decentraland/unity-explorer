using System.Linq;
using UnityEngine;

namespace Utility
{
    public class StringUtils
    {
        private static string chars = " ABCDEFGHIJ KLMNOPQRSTU VWXYZ0123456789 ";

        public static string GenerateRandomString(int length)
        {
            return new string(Enumerable.Repeat(chars, length).Select(s => s[Random.Range(0, s.Length)]).ToArray());
        }
    }
}
