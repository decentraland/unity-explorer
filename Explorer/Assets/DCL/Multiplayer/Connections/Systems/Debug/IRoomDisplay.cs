namespace DCL.Multiplayer.Connections.Systems.Debug
{
    public interface IRoomDisplay
    {
        void Update();

        class Null : IRoomDisplay
        {
            public void Update()
            {
                //ignore
            }
        }
    }
}
