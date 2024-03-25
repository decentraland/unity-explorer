namespace DCL.NftPrompt
{
    public partial class NftPromptController
    {
        public struct Params
        {
            public string ContractAddress { get; }
            public string TokenId { get; }

            public Params(string contractAddress, string tokenId)
            {
                ContractAddress = contractAddress;
                TokenId = tokenId;
            }
        }
    }
}
