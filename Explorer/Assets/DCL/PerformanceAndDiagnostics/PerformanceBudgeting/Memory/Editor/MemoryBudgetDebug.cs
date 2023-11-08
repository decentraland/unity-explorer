using UnityEditor;

namespace DCL.PerformanceAndDiagnostics.PerformanceBudgeting.Memory.Editor
{
    public class MemoryBudgetDebug
    {
        private const string BASE_PATH = "🛠️ DCL Tools/MemoryBudget/";

        // Static flags or other mode-specific data can be stored here
        public static bool FlagNormal;
        public static bool FlagWarning;
        public static bool FlagFull;

        private static string currentMode = "NORMAL"; // Start with NORMAL as default

        [MenuItem(BASE_PATH + "NORMAL", false, 1)]
        private static void ToggleNormal() =>
            ToggleFlag("NORMAL");

        [MenuItem(BASE_PATH + "WARNING", false, 1)]
        private static void ToggleWarning() =>
            ToggleFlag("WARNING");

        [MenuItem(BASE_PATH + "FULL", false, 1)]
        private static void ToggleFull() =>
            ToggleFlag("FULL");

        private static void ToggleFlag(string mode)
        {
            currentMode = mode;

            switch (currentMode)
            {
                case "NORMAL":
                    FlagNormal = !FlagNormal;
                    FlagWarning = false;
                    FlagFull = false;
                    Menu.SetChecked(BASE_PATH + "NORMAL", FlagNormal);
                    break;
                case "WARNING":
                    FlagWarning = !FlagWarning;
                    FlagNormal = false;
                    FlagFull = false;
                    Menu.SetChecked(BASE_PATH + "WARNING", FlagWarning);
                    break;
                case "FULL":
                    FlagFull = !FlagFull;
                    FlagNormal = false;
                    FlagWarning = false;
                    Menu.SetChecked(BASE_PATH + "FULL", FlagFull);
                    break;
            }
        }

        [MenuItem(BASE_PATH + "NORMAL", true)]
        private static bool ValidateToggleNormal()
        {
            Menu.SetChecked(BASE_PATH + "NORMAL", FlagNormal);
            return true;
        }

        [MenuItem(BASE_PATH + "WARNING", true)]
        private static bool ValidateToggleWarning()
        {
            Menu.SetChecked(BASE_PATH + "WARNING", FlagWarning);
            return true;
        }

        [MenuItem(BASE_PATH + "FULL", true)]
        private static bool ValidateToggleFull()
        {
            Menu.SetChecked(BASE_PATH + "FULL", FlagFull);
            return true;
        }
    }
}
