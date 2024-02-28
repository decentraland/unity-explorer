using DCL.Multiplayer.Connections.Messaging.Pipe;

namespace DCL.Multiplayer.Connections.Messaging.Hubs
{
    public interface IMessagePipesHub
    {
        IMessagePipe ScenePipe();

        IMessagePipe IslandPipe();
    }
}
