namespace DCL.SceneLoadingScreens.SplashScreen
{
    public interface ISplashScreen
    {
        void Show(string? message = null);

        void Hide();

        readonly struct ShowContext : System.IDisposable
        {
            private readonly ISplashScreen splashScreen;
            private readonly bool hideOnFinish;

            public ShowContext(ISplashScreen splashScreen, string? message = null, bool hideOnFinish = true)
            {
                this.splashScreen = splashScreen;
                this.hideOnFinish = hideOnFinish;
                splashScreen.Show(message);
            }

            public void Dispose()
            {
                if (hideOnFinish)
                    splashScreen.Hide();
            }
        }
    }

    public static class SplashScreenExtensions
    {
        public static ISplashScreen.ShowContext ShowWithContext(this ISplashScreen splashScreen, string? message = null, bool hideOnFinish = true) =>
            new (splashScreen, message, hideOnFinish);
    }
}
