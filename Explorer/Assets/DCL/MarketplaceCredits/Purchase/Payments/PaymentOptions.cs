using System;
using System.Collections.Generic;
using System.Numerics;

namespace DCL.MarketplaceCredits.Purchase
{
    /// <summary>
    ///     The ways a buyer can pay one price, in preference order: credits, credits + MANA, MANA. Empty when
    ///     nothing the buyer holds covers it. Carries the price and the balances the options were computed from.
    /// </summary>
    public sealed class PaymentOptions
    {
        public static readonly PaymentOptions NONE = new (Array.Empty<PaymentOption>(), null, 0, BigInteger.Zero, null, null, true);

        public readonly IReadOnlyList<PaymentOption> Options;
        public readonly ManaShortfall? ManaShortfall;
        public readonly int PriceCents;

        /// <summary>Zero when the price has no MANA equivalent.</summary>
        public readonly BigInteger PriceManaWei;

        /// <summary>Null when the balance could not be read.</summary>
        public readonly long? CreditsBalanceCents;

        /// <summary>Null when the balance was not read or could not be read.</summary>
        public readonly BigInteger? ManaBalanceWei;

        /// <summary>
        ///     False when MANA alone cannot pay this price whatever the balance: a sale made straight on its
        ///     contract that the relayer refuses for being below its minimum value, or a purchase that only
        ///     CreditsManager can settle, which needs a credit.
        /// </summary>
        public readonly bool CanManaPayAlone;

        /// <summary>Whether a MANA balance was read and is above zero.</summary>
        public bool HoldsMana => ManaBalanceWei is { Sign: > 0 };

        /// <summary>Whether the buyer holds MANA and this price has a MANA equivalent.</summary>
        public bool CanOfferMana => HoldsMana && PriceManaWei.Sign > 0;

        /// <summary>Whether the buyer holds the MANA this price costs and still may not pay with it alone.</summary>
        public bool IsManaAloneRefused => !CanManaPayAlone && PriceManaWei.Sign > 0 && ManaBalanceWei is { } balanceWei && balanceWei >= PriceManaWei;

        public PaymentOptions(IReadOnlyList<PaymentOption> options, ManaShortfall? manaShortfall, int priceCents, BigInteger priceManaWei,
            long? creditsBalanceCents, BigInteger? manaBalanceWei, bool canManaPayAlone)
        {
            CanManaPayAlone = canManaPayAlone;
            Options = options;
            ManaShortfall = manaShortfall;
            PriceCents = priceCents;
            PriceManaWei = priceManaWei;
            CreditsBalanceCents = creditsBalanceCents;
            ManaBalanceWei = manaBalanceWei;
        }

        /// <summary>The same price and balances with one method taken out of the offer.</summary>
        public PaymentOptions Without(PaymentMethod method)
        {
            var remaining = new List<PaymentOption>(Options.Count);

            foreach (PaymentOption option in Options)
            {
                if (option.Method != method)
                    remaining.Add(option);
            }

            return new PaymentOptions(remaining, ManaShortfall, PriceCents, PriceManaWei, CreditsBalanceCents, ManaBalanceWei, CanManaPayAlone);
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
