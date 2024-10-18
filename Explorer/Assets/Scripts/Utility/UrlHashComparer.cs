using System.Collections.Generic;

namespace Utility
{
    public class UrlHashComparer : IEqualityComparer<string>
    {
        public bool Equals(string? x, string? y)
        {
            if (ReferenceEquals(x, y)) return true;
            if (x == null || y == null) return false;
            if (x.Length != y.Length) return false;

            for (int i = 0; i < x.Length; i++)
            {
                char xChar = x[i];
                char yChar = y[i];

                if (xChar == yChar) continue;

                // Only do case comparison for ASCII letters
                if (xChar >= 'A' && xChar <= 'Z') xChar += (char)32;
                if (yChar >= 'A' && yChar <= 'Z') yChar += (char)32;

                if (xChar != yChar) return false;
            }

            return true;
        }

        public int GetHashCode(string? obj)
        {
            if (obj == null) return 0;

            int hash = 17;
            for (int i = 0; i < obj.Length; i++)
            {
                char c = obj[i];
                if (c >= 'A' && c <= 'Z') c += (char)32;
                hash = hash * 31 + c;
            }
            return hash;
        }
    }
}
