using System;
using System.Collections.Generic;

namespace DCL.MarketplaceCredits.Purchase
{
    /// <summary>
    ///     The ways a buyer can pay one price, in preference order: credits, credits + MANA, MANA. Empty when
    ///     nothing the buyer holds covers it.
    /// </summary>
    public sealed class PaymentOptions
    {
        public static readonly PaymentOptions NONE = new (Array.Empty<PaymentOption>(), null);

        public readonly IReadOnlyList<PaymentOption> Options;
        public readonly ManaShortfall? ManaShortfall;

        public PaymentOptions(IReadOnlyList<PaymentOption> options, ManaShortfall? manaShortfall)
        {
            Options = options;
            ManaShortfall = manaShortfall;
        }

        public bool TryGet(PaymentMethod method, out PaymentOption option)
        {
            foreach (PaymentOption candidate in Options)
            {
                if (candidate.Method != method)
                    continue;

                option = candidate;
                return true;
            }

            option = default(PaymentOption);
            return false;
        }
    }
}
