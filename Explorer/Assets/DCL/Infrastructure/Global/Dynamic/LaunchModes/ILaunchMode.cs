namespace Global.Dynamic.LaunchModes
{
    public interface ILaunchMode
    {
        LaunchMode CurrentMode { get; }

        private class Const : ILaunchMode
        {
            public Const(LaunchMode currentMode)
            {
                CurrentMode = currentMode;
            }

            public LaunchMode CurrentMode { get; }
        }

        static readonly ILaunchMode PLAY = new Const(LaunchMode.Play);

        static readonly ILaunchMode LOCAL_SCENE_DEVELOPMENT = new Const(LaunchMode.LocalSceneDevelopment);
    }

    public enum LaunchMode
    {
        Play,
        LocalSceneDevelopment,
    }
}
