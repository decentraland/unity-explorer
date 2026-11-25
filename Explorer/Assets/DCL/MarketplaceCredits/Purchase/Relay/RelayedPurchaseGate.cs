using System;

namespace DCL.MarketplaceCredits.Purchase
{
    /// <summary>
    ///     Lets one relayed purchase run at a time. A meta-transaction is signed over the nonce read from its
    ///     contract, so two purchases relaying together would sign the same one.
    /// </summary>
    public class RelayedPurchaseGate
    {
        private Lease? current;

        /// <summary>Null while another purchase holds the gate. Disposing the lease frees it.</summary>
        public Lease? TryEnter()
        {
            if (current != null)
                return null;

            current = new Lease(this);
            return current;
        }

        public sealed class Lease : IDisposable
        {
            private readonly RelayedPurchaseGate gate;

            internal Lease(RelayedPurchaseGate gate)
            {
                this.gate = gate;
            }

            public void Dispose()
            {
                if (gate.current == this)
                    gate.current = null;
            }
        }
    }
}
