using DCL.Multiplayer.Connections.Messaging.Pipe;

namespace DCL.Multiplayer.Connections.Messaging.Hubs
{
    public interface IMutableMessagePipesHub
    {
        void AssignScenePipe(IMessagePipe messagePipe);

        void AssignIslandPipe(IMessagePipe messagePipe);
    }
}
