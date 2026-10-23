using Global.AppArgs;
using System.IO;
using UnityEngine;

namespace DCL.BevyMigration.Inventory
{
    /// <summary>
    ///     Starts the shader inventory in player builds launched with <c>--shader-inventory[=&lt;path&gt;]</c>.
    ///     Self-contained on purpose: nothing outside this assembly may reference it (BevyMigration/docs/README.md § 4).
    /// </summary>
    public static class ShaderFamilyInventoryBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void StartFromCommandLine()
        {
            var appArgs = new ApplicationParametersParser();

            if (!appArgs.HasFlag(AppArgsFlags.SHADER_INVENTORY)) return;

            string path = appArgs.TryGetValue(AppArgsFlags.SHADER_INVENTORY, out string? value) && !string.IsNullOrEmpty(value)
                ? value
                : Path.Combine(Application.persistentDataPath, ShaderFamilyReport.DEFAULT_FILE_NAME);

            ShaderFamilyInventory.Start(path);
        }
    }
}
