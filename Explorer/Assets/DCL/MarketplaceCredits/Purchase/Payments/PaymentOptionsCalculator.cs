using System;
using System.Collections.Generic;
using System.Numerics;

namespace DCL.MarketplaceCredits.Purchase
{
    /// <summary>
    ///     Which payment methods a buyer's balances support and how a mixed payment splits. Amounts are integer
    ///     cents and MANA wei, so nothing drifts through floats.
    /// </summary>
    public static class PaymentOptionsCalculator
    {
        public const int CENTS_PER_CREDIT = 10;

        // How far the MANA a purchase draws may exceed what the buyer confirmed. A USD-pegged listing is
        // converted at the oracle rate when it settles, so its MANA amount moves between the quote and the purchase.
        private const int MANA_DRIFT_TOLERANCE_PERCENT = 2;
        private const int PERCENT = 100;

        /// <summary>
        ///     priceManaWei is what the price costs in MANA right now; zero (rate unknown) leaves only credits.
        ///     A null balance is one that could not be read: it pays for nothing and is recorded as unknown.
        ///     isManaAloneRelayable false leaves MANA out as a method of its own: it can only top up credits.
        /// </summary>
        public static PaymentOptions Compute(int priceCents, BigInteger priceManaWei, long? creditsBalanceCents, BigInteger? manaBalance, bool isManaAloneRelayable = true)
        {
            if (priceManaWei.Sign < 0)
                priceManaWei = BigInteger.Zero;

            if (priceCents <= 0)
                return new PaymentOptions(Array.Empty<PaymentOption>(), null, 0, priceManaWei, creditsBalanceCents, manaBalance, isManaAloneRelayable);

            long balanceCents = Math.Max(0L, creditsBalanceCents ?? 0L);
            BigInteger manaBalanceWei = BigInteger.Max(BigInteger.Zero, manaBalance ?? BigInteger.Zero);

            var options = new List<PaymentOption>(3);
            var hasManaOption = false;

            if (balanceCents >= priceCents)
                options.Add(new PaymentOption(PaymentMethod.Credits, priceCents, CeilToCredits(priceCents), BigInteger.Zero));

            // The credits leg is the balance floored to whole credits: the credits-server rounds every charge up
            // to a whole credit, so asking for a balance with odd cents would be refused as insufficient.
            int creditsLegCents = WholeCreditCents(balanceCents);

            if (priceManaWei.Sign > 0 && creditsLegCents > 0 && creditsLegCents < priceCents)
            {
                BigInteger gapWei = ManaForRemainder(priceCents - creditsLegCents, priceCents, priceManaWei);

                if (manaBalanceWei >= gapWei)
                {
                    options.Add(new PaymentOption(PaymentMethod.CreditsAndMana, creditsLegCents, creditsLegCents / CENTS_PER_CREDIT, gapWei));
                    hasManaOption = true;
                }
            }

            if (isManaAloneRelayable && priceManaWei.Sign > 0 && manaBalanceWei >= priceManaWei)
            {
                options.Add(new PaymentOption(PaymentMethod.Mana, 0, 0, priceManaWei));
                hasManaOption = true;
            }

            ManaShortfall? shortfall = null;

            // MANA that would cover the price but may not pay alone is not a shortfall.
            if (!hasManaOption && priceManaWei.Sign > 0 && manaBalanceWei.Sign > 0 && manaBalanceWei < priceManaWei)
                shortfall = new ManaShortfall(manaBalanceWei, (int)(manaBalanceWei * priceCents / priceManaWei), priceCents);

            return new PaymentOptions(options, shortfall, priceCents, priceManaWei, creditsBalanceCents, manaBalance, isManaAloneRelayable);
        }

        /// <summary>
        ///     The MANA that covers remainderCents of a price of priceCents costing priceManaWei, rounded up so a
        ///     split never under-funds the price.
        /// </summary>
        public static BigInteger ManaForRemainder(int remainderCents, int priceCents, BigInteger priceManaWei)
        {
            if (remainderCents <= 0 || priceCents <= 0)
                return BigInteger.Zero;

            if (remainderCents >= priceCents)
                return priceManaWei;

            return ((priceManaWei * remainderCents) + priceCents - BigInteger.One) / priceCents;
        }

        /// <summary>
        ///     The MANA a mixed payment adds to a credit worth creditValueWei: the remainder's share of the price,
        ///     raised when the credit is worth less MANA than its own share, so that the credit plus the gap always
        ///     reach requiredManaWei.
        /// </summary>
        public static BigInteger ManaGap(int remainderCents, int priceCents, BigInteger requiredManaWei, BigInteger creditValueWei) =>
            BigInteger.Max(ManaForRemainder(remainderCents, priceCents, requiredManaWei), requiredManaWei - creditValueWei);

        /// <summary>
        ///     Spreads a credit balance over the units of a cart, in order: each unit takes what its price needs
        ///     from what is left, in whole credits. The result has one entry per unit.
        /// </summary>
        public static int[] DistributeCredits(IReadOnlyList<int> unitCents, long balanceCents)
        {
            var allocation = new int[unitCents.Count];
            int remaining = WholeCreditCents(balanceCents);

            for (var i = 0; i < unitCents.Count; i++)
            {
                int take = Math.Min(Math.Max(0, unitCents[i]), remaining);
                take -= take % CENTS_PER_CREDIT;
                allocation[i] = take;
                remaining -= take;
            }

            return allocation;
        }

        public static bool ExceedsConfirmedMana(BigInteger neededWei, BigInteger confirmedWei) =>
            neededWei * PERCENT > confirmedWei * (PERCENT + MANA_DRIFT_TOLERANCE_PERCENT);

        private static int WholeCreditCents(long balanceCents)
        {
            if (balanceCents <= 0)
                return 0;

            long whole = balanceCents - (balanceCents % CENTS_PER_CREDIT);
            return whole > int.MaxValue ? int.MaxValue - (int.MaxValue % CENTS_PER_CREDIT) : (int)whole;
        }

        private static int CeilToCredits(int cents) =>
            (cents + CENTS_PER_CREDIT - 1) / CENTS_PER_CREDIT;
    }
}
