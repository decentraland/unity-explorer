using System;

namespace DCL.ChangeRealmPrompt
{
    public partial class ChangeRealmPromptController
    {
        public struct Params
        {
            public string Realm { get; }
            public Action ChangeRealmCallback { get; }

            public Params(string realm, Action changeRealmCallback)
            {
                Realm = realm;
                ChangeRealmCallback = changeRealmCallback;
            }
        }
    }
}
