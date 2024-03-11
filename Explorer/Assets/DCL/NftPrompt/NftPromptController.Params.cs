namespace DCL.NftPrompt
{
    public partial class NftPromptController
    {
        public struct Params
        {
            public string Urn { get; }

            public Params(string urn)
            {
                Urn = urn;
            }
        }
    }
}
