namespace DCL.ChangeRealmPrompt
{
    public partial class ChangeRealmPromptController
    {
        public struct Params
        {
            public string Message { get; }
            public string Realm { get; }

            public Params(string message, string realm)
            {
                Message = message;
                Realm = realm;
            }
        }
    }
}
