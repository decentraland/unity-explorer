namespace DCL.Chat
{
    public interface IChatController
    {
        public string IslandRoomSid { get; }
        public string PreviousRoomSid { get; set; }
        public bool TryGetView(out ChatView view);
    }
}
