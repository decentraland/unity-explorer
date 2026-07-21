using UnityEngine;

namespace DCL.ChangeRealmPrompt
{
    public partial class ChangeRealmPromptController
    {
        public struct Params
        {
            public string Message { get; }
            public string Realm { get; }

            /// <summary>Optional target parcel to land on after the realm switch.</summary>
            public Vector2Int? Position { get; }

            public Params(string message, string realm)
            {
                Message = message;
                Realm = realm;
                Position = null;
            }

            public Params(string message, string realm, Vector2Int position)
            {
                Message = message;
                Realm = realm;
                Position = position;
            }
        }
    }
}
