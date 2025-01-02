using System;
using UnityEngine;

namespace DCL.Platforms
{
    public class Platform : IPlatform
    {
        public IPlatform.Kind CurrentPlatform() =>
            Application.platform switch
            {
                RuntimePlatform.LinuxEditor or RuntimePlatform.LinuxPlayer or RuntimePlatform.LinuxServer => IPlatform.Kind.Linux,
                RuntimePlatform.WindowsEditor or RuntimePlatform.WindowsPlayer or RuntimePlatform.WindowsServer => IPlatform.Kind.Windows,
                RuntimePlatform.OSXEditor or RuntimePlatform.OSXPlayer or RuntimePlatform.OSXServer => IPlatform.Kind.Mac,
                _ => throw new ArgumentException($"Platform {Application.platform} is not supported")
            };

        public void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
