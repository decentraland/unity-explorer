using Cysharp.Threading.Tasks;

namespace DCL.Multiplayer.Connections.Rooms.Connective
{
    public interface IActivatableConnectiveRoom : IConnectiveRoom
    {
        bool Activated { get; }

        /// <summary>
        ///     If room is activated it will return to the state that was before deactivation
        ///     or was changed while the room was deactivated
        /// </summary>
        /// <returns></returns>
        UniTask ActivateAsync();

        /// <summary>
        ///     Deactivation leads to the room being stopped and its implementation being replaced with <see cref="NullRoom" />
        /// </summary>
        /// <returns></returns>
        UniTask DeactivateAsync();
    }
}
