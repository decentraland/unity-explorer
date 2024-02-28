using DCL.Multiplayer.Connections.Messaging.Pipe;

namespace DCL.Multiplayer.Connections.Messaging.Hubs
{
    public class MessagePipesHub : IMutableMessagePipesHub
    {
        private readonly InteriorMessagePipe scenePipe = new ();
        private readonly InteriorMessagePipe islandPipe = new ();

        public IMessagePipe ScenePipe() =>
            scenePipe;

        public IMessagePipe IslandPipe() =>
            islandPipe;

        public void AssignScenePipe(IMessagePipe messagePipe) =>
            scenePipe.Assign(messagePipe);

        public void AssignIslandPipe(IMessagePipe messagePipe) =>
            islandPipe.Assign(messagePipe);
    }
}
