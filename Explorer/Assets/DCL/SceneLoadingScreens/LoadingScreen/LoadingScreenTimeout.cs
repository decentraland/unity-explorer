using System;
using UnityEngine;

namespace DCL.SceneLoadingScreens.LoadingScreen
{
    public class LoadingScreenTimeout
    {
        public TimeSpan Value { get; private set; }

        public LoadingScreenTimeout() : this(TimeSpan.FromMinutes(2)) { }

        public LoadingScreenTimeout(TimeSpan timeout)
        {
            Value = timeout;
        }

        public void Set(float seconds)
        {
            Value = TimeSpan.FromSeconds(Mathf.Max(seconds, 0));
        }
    }

    /// <summary>
    ///     Reported to Sentry when the loading screen hits its hard timeout, so we can track
    ///     how often users fail to load into a scene because it took too long.
    /// </summary>
    public class LoadingScreenTimeoutException : Exception
    {
        public LoadingScreenTimeoutException(TimeSpan timeout, float lastProgress)
            : base("Loading screen timed out")
        {
            Data["timeout_seconds"] = timeout.TotalSeconds;
            Data["last_progress"] = lastProgress;
        }
    }
}
