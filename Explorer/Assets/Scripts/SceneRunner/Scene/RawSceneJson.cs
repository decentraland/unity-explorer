using System;

namespace SceneRunner.Scene
{
    /// <summary>
    ///     Parsed local scene.json
    /// </summary>
    [Serializable]
    public struct RawSceneJson
    {
        /// <summary>
        ///     Main Json Script
        /// </summary>
        public string main;

        public string name;

        public string[] requiredPermissions;

        public string[] allowedMediaHostnames;
    }
}
