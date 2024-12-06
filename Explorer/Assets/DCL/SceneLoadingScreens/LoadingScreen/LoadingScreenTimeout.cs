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
}
