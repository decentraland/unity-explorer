using System;
using System.IO;

namespace BevyMigration.Tools.ApiInventory
{
    internal static class RepositoryRoot
    {
        /// <summary>
        ///     Walks up from the working directory, then from the tool binary, to the directory that holds both
        ///     <c>Explorer/</c> and <c>BevyMigration/</c>.
        /// </summary>
        public static string Find()
        {
            string? fromCwd = WalkUp(Directory.GetCurrentDirectory());
            if (fromCwd != null) return fromCwd;

            string? fromBinary = WalkUp(AppContext.BaseDirectory);
            if (fromBinary != null) return fromBinary;

            throw new DirectoryNotFoundException("api-inventory must run inside the unity-explorer repository (no ancestor holds both Explorer/ and BevyMigration/)");
        }

        private static string? WalkUp(string start)
        {
            DirectoryInfo? directory = new DirectoryInfo(start);

            while (directory != null)
            {
                if (Directory.Exists(Path.Combine(directory.FullName, "Explorer")) && Directory.Exists(Path.Combine(directory.FullName, "BevyMigration")))
                    return directory.FullName;

                directory = directory.Parent;
            }

            return null;
        }
    }
}
