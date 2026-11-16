using UnityEngine;

namespace DCL.CharacterCamera
{
    /// <summary>
    ///     Present on the camera entity only while a scene's CameraModeArea holds one of the
    ///     <see cref="CameraComponent.CameraInputLocks" />. Describes the newest area entered, which is the one in effect,
    ///     so a refusal to change the camera mode can name what holds it.
    /// </summary>
    public struct CameraModeAreaLock
    {
        public string SceneName;
        public Vector2Int SceneBaseParcel;
        public int CrdtEntityId;
        public CameraMode Mode;
        public Vector3 AreaSize;

        /// <summary>Areas the player currently stands in; above one means the areas nest.</summary>
        public int ActiveAreaCount;
    }
}
