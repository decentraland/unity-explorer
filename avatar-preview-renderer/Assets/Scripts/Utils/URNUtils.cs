using System.Linq;

namespace Utils
{
    public static class URNUtils
    {
        public static string SanitizeURN(string urn)
        {
            return (urn.Count(c => c == ':') == 6
                ? urn.Remove(urn.LastIndexOf(':'))
                : urn).ToLowerInvariant();
        }
    }
}