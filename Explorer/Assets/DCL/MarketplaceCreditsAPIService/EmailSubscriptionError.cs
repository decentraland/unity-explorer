using System;

namespace DCL.MarketplaceCreditsAPIService
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