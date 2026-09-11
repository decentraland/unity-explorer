using System;

namespace DCL.Web3.Authenticators
{
    /// <summary>
    ///     The guest wallet of this device belongs to an account that was upgraded with an email, so it cannot be
    ///     signed in as a guest any more: the account is reachable only through the OTP sent to <see cref="Email" />.
    /// </summary>
    public class GuestAccountUpgradedException : Exception
    {
        public string Email { get; }

        public GuestAccountUpgradedException(string email)
            : base("The guest account was upgraded with an email, an OTP login is required")
        {
            Email = email;
        }
    }
}