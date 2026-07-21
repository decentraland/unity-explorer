using System;

namespace DCL.MarketplaceCredits.Purchase.TopUp
{
    public interface ICreditsTopUpService : IDisposable
    {
        event Action<CreditsTopUpStatus> StatusChanged;

        CreditsTopUpStatus CurrentStatus { get; }

        bool IsOrderInFlight { get; }

        void StartTopUp(CreditPack pack);

        void StopWaitingForBrowser();

        void AcknowledgeTerminalState();
    }
}
