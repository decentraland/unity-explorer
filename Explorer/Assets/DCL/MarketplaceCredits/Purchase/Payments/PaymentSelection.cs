using System.Numerics;

namespace DCL.MarketplaceCredits.Purchase
{
    /// <summary>
    ///     Which of the two currencies the buyer ticked. The ticks always describe an option the buyer can pay
    ///     with: credits alone, MANA alone, or both for a mixed payment. It starts on the preferred option and
    ///     stays unticked only when nothing is payable.
    /// </summary>
    public sealed class PaymentSelection
    {
        private readonly PaymentOptions options;

        public bool CreditsTicked { get; private set; }

        public bool ManaTicked { get; private set; }

        /// <summary>Whether any payable option spends credits.</summary>
        public bool CreditsAvailable { get; }

        /// <summary>Whether any payable option spends MANA.</summary>
        public bool ManaAvailable { get; }

        public bool HasSelection => CreditsTicked || ManaTicked;

        /// <summary>What the credits row shows: its leg of a mixed payment, otherwise the whole price.</summary>
        public long CreditsAmount => TryGetSelected(out PaymentOption selected) && selected.Method == PaymentMethod.CreditsAndMana
            ? selected.Credits
            : (options.PriceCents + PaymentOptionsCalculator.CENTS_PER_CREDIT - 1) / PaymentOptionsCalculator.CENTS_PER_CREDIT;

        /// <summary>What the MANA row shows: its leg of a mixed payment, otherwise the whole price.</summary>
        public BigInteger ManaAmountWei => TryGetSelected(out PaymentOption selected) && selected.Method == PaymentMethod.CreditsAndMana
            ? selected.ManaWei
            : options.PriceManaWei;

        public PaymentSelection(PaymentOptions options)
        {
            this.options = options;
            CreditsAvailable = options.TryGet(PaymentMethod.Credits, out _) || options.TryGet(PaymentMethod.CreditsAndMana, out _);
            ManaAvailable = options.TryGet(PaymentMethod.Mana, out _) || options.TryGet(PaymentMethod.CreditsAndMana, out _);

            if (options.Options.Count > 0)
                Apply(options.Options[0].Method);
        }

        public bool TryGetSelected(out PaymentOption option)
        {
            if (HasSelection)
                return options.TryGet(MethodOf(CreditsTicked, ManaTicked), out option);

            option = default(PaymentOption);
            return false;
        }

        /// <summary>Returns whether the ticks changed.</summary>
        public bool ToggleCredits() =>
            Toggle(!CreditsTicked, ManaTicked, !CreditsTicked, PaymentMethod.Credits);

        /// <summary>Returns whether the ticks changed.</summary>
        public bool ToggleMana() =>
            Toggle(CreditsTicked, !ManaTicked, !ManaTicked, PaymentMethod.Mana);

        // The toggled ticks are taken when they are payable. A row ticked into an unpayable pair replaces the
        // other one when it can pay alone. Anything else leaves the ticks as they were.
        private bool Toggle(bool credits, bool mana, bool isTicking, PaymentMethod alone)
        {
            if ((credits || mana) && options.TryGet(MethodOf(credits, mana), out _))
            {
                Apply(MethodOf(credits, mana));
                return true;
            }

            if (!isTicking || !options.TryGet(alone, out _))
                return false;

            Apply(alone);
            return true;
        }

        private void Apply(PaymentMethod method)
        {
            CreditsTicked = method != PaymentMethod.Mana;
            ManaTicked = method != PaymentMethod.Credits;
        }

        private static PaymentMethod MethodOf(bool credits, bool mana)
        {
            if (credits)
                return mana ? PaymentMethod.CreditsAndMana : PaymentMethod.Credits;

            return PaymentMethod.Mana;
        }
    }
}
