using System;
using UnityEngine;

namespace DCL.SceneLoadingScreens.LoadingScreen
{
    public class LoadingScreenTimeout
    {
        public TimeSpan Value { get; private set; } = TimeSpan.FromMinutes(2);

        public void Set(float seconds)
        {
            Value = TimeSpan.FromSeconds(Mathf.Max(seconds, 0));
        }
    }
}
