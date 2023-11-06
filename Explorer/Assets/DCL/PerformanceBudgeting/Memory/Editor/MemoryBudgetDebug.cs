using UnityEditor;

namespace DCL.PerformanceBudgeting.Memory.Editor
{
    public class MemoryBudgetDebug
    {
        private const string BASE_PATH = "🛠️ DCL Tools/MemoryBudget/";

        // Static flags or other mode-specific data can be stored here
        public static bool flagNormal;
        public static bool flagWarning;
        public static bool flagFull;
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

        // This method toggles flags based on the given mode and updates the menu item checkmarks
        private static void ToggleFlag(string mode)
        {
            currentMode = mode;

            // Set the flags according to the selected mode
            switch (currentMode)
            {
                case "NORMAL":
                    flagNormal = !flagNormal;
                    flagWarning = false;
                    flagFull = false;
                    Menu.SetChecked(BASE_PATH + "NORMAL", flagNormal);
                    break;
                case "WARNING":
                    flagWarning = !flagWarning;
                    flagNormal = false;
                    flagFull = false;
                    Menu.SetChecked(BASE_PATH + "WARNING", flagWarning);
                    break;
                case "FULL":
                    flagFull = !flagFull;
                    flagNormal = false;
                    flagWarning = false;
                    Menu.SetChecked(BASE_PATH + "FULL", flagFull);
                    break;

                // You can add more cases for other modes if necessary
            }
        }

        // This method updates the menu item checkmarks when the menu is opened
        [MenuItem(BASE_PATH + "NORMAL", true)]
        private static bool ValidateToggleNormal()
        {
            Menu.SetChecked(BASE_PATH + "NORMAL", flagNormal);
            return true;
        }

        // This method updates the menu item checkmarks when the menu is opened
        [MenuItem(BASE_PATH + "WARNING", true)]
        private static bool ValidateToggleWarning()
        {
            Menu.SetChecked(BASE_PATH + "WARNING", flagWarning);
            return true;
        }

        [MenuItem(BASE_PATH + "FULL", true)]
        private static bool ValidateToggleFull()
        {
            Menu.SetChecked(BASE_PATH + "FULL", flagFull);
            return true;
        }
    }
}
