using AssetManagement;
using System;
using System.Collections.Generic;

namespace ECS.StreamableLoading.Cache
{
	public class IntentionsComparer<TLoadingIntention> : IEqualityComparer<TLoadingIntention>
		where TLoadingIntention: IEquatable<TLoadingIntention>
	{
		public static readonly IntentionsComparer<TLoadingIntention> INSTANCE = new ();

		public bool Equals(TLoadingIntention x, TLoadingIntention y) =>
			x.Equals(y);

		public int GetHashCode(TLoadingIntention obj) =>
			obj.GetHashCode();

		public readonly struct SourcedIntentionId : IEquatable<SourcedIntentionId>
		{
			private readonly TLoadingIntention intention;
			private readonly AssetSource source;

			public SourcedIntentionId(TLoadingIntention intention, AssetSource source)
			{
				this.intention = intention;
				this.source = source;
			}

			public bool Equals(SourcedIntentionId other) =>
				INSTANCE.Equals(intention, other.intention) && source == other.source;

			public override bool Equals(object? obj) =>
				obj is SourcedIntentionId other && Equals(other);

			public override int GetHashCode() =>
				HashCode.Combine(intention, (int)source);

			public static bool operator ==(SourcedIntentionId left, SourcedIntentionId right) =>
				left.Equals(right);

			public static bool operator !=(SourcedIntentionId left, SourcedIntentionId right) =>
				!left.Equals(right);
		}
	}
}