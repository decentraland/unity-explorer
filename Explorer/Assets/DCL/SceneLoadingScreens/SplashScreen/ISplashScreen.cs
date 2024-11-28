namespace DCL.SceneLoadingScreens.SplashScreen
{
    public interface ISplashScreen
    {
        void Show(string? message = null);

        void Hide();

        readonly struct ShowContext : System.IDisposable
        {
            private readonly ISplashScreen splashScreen;

            public ShowContext(ISplashScreen splashScreen, string? message = null)
            {
                this.splashScreen = splashScreen;
                splashScreen.Show(message);
            }

            public void Dispose()
            {
                splashScreen.Hide();
            }
        }
    }

    public static class SplashScreenExtensions
    {
        public static ISplashScreen.ShowContext ShowWithContext(this ISplashScreen splashScreen, string? message = null) =>
            new (splashScreen, message);
    }
}
