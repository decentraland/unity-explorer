using System.Globalization;
using System.Numerics;

namespace DCL.MarketplaceCredits.Purchase
{
    /// <summary>
    ///     Display strings for credits and MANA amounts: thousands separators, and MANA cut to a few decimals
    ///     with the rounding direction chosen by what the number is (a price never reads lower than it is, a
    ///     balance never reads higher).
    /// </summary>
    public static class PaymentAmountFormat
    {
        /// <summary>Shown in place of an amount that is not known.</summary>
        public const string UNKNOWN = "-";

        private const string GROUPED_INTEGER = "N0";
        private const string GROUPED_INTEGER_ITEM = "{0:N0}";
        private const string ZERO = "0";
        private const string RATE_FORMAT = "1 Credit = {0} MANA";
        private const int WEI_DECIMALS = 18;
        private const int AMOUNT_DECIMALS = 2;
        private const int SMALL_RATE_DECIMALS = 4;
        private const int DECIMAL_BASE = 10;

        public static string Credits(long credits) =>
            credits.ToString(GROUPED_INTEGER, CultureInfo.InvariantCulture);

        public static string ManaPrice(BigInteger wei) =>
            Mana(wei, AMOUNT_DECIMALS, true);

        /// <summary>A balance held in cents, in whole credits. A null balance is one that could not be read.</summary>
        public static string CreditsBalance(long? balanceCents) =>
            balanceCents is { } cents ? Credits(cents / PaymentOptionsCalculator.CENTS_PER_CREDIT) : UNKNOWN;

        /// <summary>A null balance is one that could not be read.</summary>
        public static string ManaBalance(BigInteger? wei) =>
            wei is { } balance ? Mana(balance, AMOUNT_DECIMALS, false) : UNKNOWN;

        /// <summary>
        ///     "1 Credit = X MANA" for a price of priceCents that costs priceManaWei. A rate too small for two
        ///     decimals is given two more instead of reading as zero.
        /// </summary>
        public static string Rate(int priceCents, BigInteger priceManaWei)
        {
            if (priceCents <= 0 || priceManaWei.Sign <= 0)
                return string.Format(RATE_FORMAT, UNKNOWN);

            BigInteger weiPerCredit = priceManaWei * PaymentOptionsCalculator.CENTS_PER_CREDIT / priceCents;
            string rate = Mana(weiPerCredit, AMOUNT_DECIMALS, false);

            if (rate == ZERO)
                rate = Mana(weiPerCredit, SMALL_RATE_DECIMALS, false);

            return string.Format(RATE_FORMAT, rate);
        }

        private static string Mana(BigInteger wei, int decimals, bool roundUp)
        {
            if (wei.Sign <= 0)
                return ZERO;

            BigInteger units = BigInteger.DivRem(wei, BigInteger.Pow(DECIMAL_BASE, WEI_DECIMALS - decimals), out BigInteger remainder);

            if (roundUp && !remainder.IsZero)
                units += BigInteger.One;

            BigInteger whole = BigInteger.DivRem(units, BigInteger.Pow(DECIMAL_BASE, decimals), out BigInteger fraction);
            string wholeText = string.Format(CultureInfo.InvariantCulture, GROUPED_INTEGER_ITEM, whole);

            if (fraction.IsZero)
                return wholeText;

            string fractionText = fraction.ToString(CultureInfo.InvariantCulture).PadLeft(decimals, '0').TrimEnd('0');
            return $"{wholeText}.{fractionText}";
        }
    }
}
