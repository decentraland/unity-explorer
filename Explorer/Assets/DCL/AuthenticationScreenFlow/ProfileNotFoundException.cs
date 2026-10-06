using System;

namespace DCL.AuthenticationScreenFlow
{
    public class ProfileNotFoundException : Exception { }

    public class NotAllowedUserException : Exception
    {
        public NotAllowedUserException(string message) : base(message)
        {
        }
    }
}
