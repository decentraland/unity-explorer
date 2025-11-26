namespace DCL.Web3.Authenticators
{
    public partial class DappWeb3Authenticator
    {
        public interface ICodeVerificationFeatureFlag
        {
            public bool ShouldWaitForCodeVerificationFromServer { get; }
        }
    }
}
