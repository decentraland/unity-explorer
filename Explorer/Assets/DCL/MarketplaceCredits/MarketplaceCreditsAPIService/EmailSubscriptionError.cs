using System;

namespace DCL.MarketplaceCredits
{
	public enum EmailSubscriptionError
	{
		HandledError,
		EmptyError,
		Cancelled
	}

	[Serializable]
	public class EmailSubscriptionErrorResponse
	{
		public string error;
		public string message;
	}
}
