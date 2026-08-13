using Microsoft.CodeAnalysis;

namespace DCL.Analyzers
{
    /// <summary>
    ///     Unity feeds a RoslynAnalyzer-labeled DLL to every compilation it drives -
    ///     including registry/git packages resolved into Library/PackageCache (observed:
    ///     DCLA005 firing in com.decentraland.pulse.transport and com.unity.cloud.ktx).
    ///     Vendored sources cannot be fixed in this repo, so every analyzer skips them;
    ///     project rules bind first-party code only.
    /// </summary>
    internal static class VendoredCode
    {
        public static bool IsVendored(SyntaxTree tree)
        {
            string path = tree.FilePath;

            if (string.IsNullOrEmpty(path))
                return false;

            return path.Replace('\\', '/').Contains("/PackageCache/");
        }
    }
}
