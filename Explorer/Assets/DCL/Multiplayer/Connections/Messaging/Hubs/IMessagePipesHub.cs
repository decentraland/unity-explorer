using DCL.Multiplayer.Connections.Messaging.Pipe;

namespace DCL.Multiplayer.Connections.Messaging.Hubs
{
    public interface IMessagePipesHub
    {
        IMessagePipe ScenePipe();

        IMessagePipe IslandPipe();

        class Fake : IMessagePipesHub
        {
            public IMessagePipe ScenePipe() =>
                IMessagePipe.Null.INSTANCE;

            public IMessagePipe IslandPipe() =>
                IMessagePipe.Null.INSTANCE;
        }
    }
}
