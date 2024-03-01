namespace DCL.ChangeRealmPrompt
{
    public partial class ChangeRealmPromptController
    {
        public struct Params
        {
            public string Realm { get; }

            public Params(string realm)
            {
                Realm = realm;
            }
        }
    }
}
