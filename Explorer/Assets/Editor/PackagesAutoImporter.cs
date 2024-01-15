using System.IO;
using UnityEditor;

namespace Editor
{
    [InitializeOnLoad]
    public class PackagesAutoImporter
    {
        const string PACKAGES_PATH = "Assets/RequiredPackages/requiredPackages.unitypackage";

        static PackagesAutoImporter()
        {
            if (!File.Exists(PACKAGES_PATH)) return;

            AssetDatabase.ImportPackage(PACKAGES_PATH, false);
            File.Delete(PACKAGES_PATH); // Optional: Delete the package after import to keep the folder clean
        }
    }
}
