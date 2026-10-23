using System;

namespace BevyMigration.Tools
{
    /// <summary>
    ///     Emits every UnityEngine, Unity.* and TMPro member the Explorer uses to ledger/api-inventory.json
    ///     and generates ledger/allowlist.toml. Step 1 deliverable (BevyMigration/docs/README.md § 7); skeleton at step 0.
    /// </summary>
    internal static class Program
    {
        private const int EXIT_NOT_IMPLEMENTED = 2;

        private static int Main()
        {
            Console.Error.WriteLine("api-inventory: not implemented yet; lands with step 1 (BevyMigration/docs/README.md § 7).");
            return EXIT_NOT_IMPLEMENTED;
        }
    }
}
