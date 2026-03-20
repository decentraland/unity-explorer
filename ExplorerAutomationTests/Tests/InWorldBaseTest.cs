
namespace ExplorerAutomationTests.Tests
{
    /// <summary>
    /// Base test class for tests that require the player to be in-world.
    /// Handles the authentication flow conditionally — only clicks through
    /// if the auth screen is still visible (i.e. first fixture to run).
    /// </summary>
    public class InWorldBaseTest : BaseTest
    {
        [OneTimeSetUp]
        [AllureBefore("Ensure player is in-world")]
        public void EnsureInWorld()
        {
            try
            {
                if (AuthenticationMainScreenView.IsScreenVisible())
                {
                    Reporter.Log("Authentication screen detected — entering world");
                    AuthenticationMainScreenView.WaitForScreenReady();
                    AuthenticationMainScreenView.ClickJumpIntoWorld();
                    LoadingScreenView.WaitForLoadingComplete();
                }
                else
                {
                    Reporter.Log("Already in-world — skipping authentication");
                }

                MainMenuView.WaitForMenuReady();
                Reporter.Log("Player is in-world and main menu is ready");
            }
            catch (Exception ex)
            {
                ExceptionFromOneTimeSetUp = ex;
                Reporter.Log("Exception during EnsureInWorld: " + ex.Message);
                Reporter.Log("Stack Trace: " + ex.StackTrace);
            }
        }
    }
}
