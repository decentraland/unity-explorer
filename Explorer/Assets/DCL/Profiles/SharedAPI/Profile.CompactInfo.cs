using CommunicationData.URLHelpers;
using DCL.Diagnostics;
using DCL.Utilities;
using DCL.Web3;
using ECS.StreamableLoading.Common.Components;
using ECS.StreamableLoading.Textures;
using System;
using UnityEngine;

namespace DCL.Profiles
{
    public partial class Profile
    {
        /// <summary>
        ///     Use it for internal assignments only
        /// </summary>
        /// <returns></returns>
        internal ref CompactInfo GetCompact() =>
            ref compact;

        public Color UserNameColor => compact.UserNameColor;

        public string UserId
        {
            get => compact.UserId;
            set => compact.UserId = value;
        }

        public string Name
        {
            get => compact.Name;
            set => compact.Name = value;
        }

        public bool HasClaimedName
        {
            get => compact.HasClaimedName;
            set => compact.HasClaimedName = value;
        }

        public Color? ClaimedNameColor
        {
            get => compact.ClaimedNameColor;
            set => compact.ClaimedNameColor = value;
        }

        public string? WalletId => compact.WalletId;
        public string ValidatedName => compact.ValidatedName;
        public string UnclaimedName => compact.UnclaimedName;
        public string DisplayName => compact.DisplayName;

        public string MentionName => compact.MentionName;

        /// <summary>
        ///     A small slice of the profile info used when the full information is not required <br />
        ///     CompactInfo is never requested with a specific version/timestamp
        /// </summary>
        public struct CompactInfo : IEquatable<CompactInfo>, IDisposable
        {
            private string name;
            private string userId;
            private bool hasClaimedName;

            /// <summary>
            ///     The custom color selected by users with claimed names for their username display
            /// </summary>
            private Color? claimedNameColor;

            // TODO it's not unified with SpriteCache from where UI requests profile thumbnails
            public StreamableLoadingResult<SpriteData>.WithFallback? ProfilePicture { get; set; }

            public CompactInfo(string userId) : this(userId, "", false, "", null) { }

            public CompactInfo(string userId, string name, bool hasClaimedName = false, string faceUrl = "", Color? claimedNameColor = null) : this()
            {
                this.name = name;
                UpdateUserId(userId, true);
                HasClaimedName = hasClaimedName;
                FaceSnapshotUrl = string.IsNullOrEmpty(faceUrl) ? default(URLAddress) : URLAddress.FromString(faceUrl);
                this.claimedNameColor = claimedNameColor;
            }

            public void Clear()
            {
                HasClaimedName = false;
                UserId = "";
                Name = "";
                FaceSnapshotUrl = default(URLAddress);
            }

            /// <summary>
            ///     Is the complete wallet address of the user
            /// </summary>
            public string UserId
            {
                get => userId;

                set => UpdateUserId(value, true);
            }

            /// <summary>
            ///     Lowercase of <see cref="UserId" />
            /// </summary>
            public Web3Address Address { get; private set; }

            public string Name
            {
                get => name;

                set
                {
                    name = value;
                    GenerateAndValidateName();
                }
            }

            /// <summary>
            ///     The custom color for users with claimed names. When set, overrides the default username color.
            ///     Can only be set if the user has a claimed name.
            /// </summary>
            public Color? ClaimedNameColor
            {
                get => claimedNameColor;

                set
                {
                    if (!hasClaimedName)
                    {
                        ReportHub.LogWarning(ReportCategory.PROFILE, "Cannot set claimed name color for a user without a claimed name");
                        return;
                    }

                    claimedNameColor = value;
                    if (value != null)
                        UserNameColor = value.Value;
                }
            }

            /// <summary>
            ///     The color calculated for this username
            /// </summary>
            public Color UserNameColor { get; internal set; }

            /// <summary>
            ///     The name of the user after passing character validation, without the # part
            ///     For users with claimed names would be the same as DisplayName
            /// </summary>
            public string ValidatedName { get; private set; }

            /// <summary>
            ///     The # part of the name for users without claimed name, will be null for users with a claimed name, includes the # character at the beginning
            /// </summary>
            public string? WalletId { get; private set; }

            /// <summary>
            ///     The name of the user after passing character validation, including the
            ///     Wallet Id (if the user doesnt have a claimed name)
            /// </summary>
            public string DisplayName { get; private set; }

            public string UnclaimedName { get; internal set; }

            public URLAddress FaceSnapshotUrl { get; internal set; }

            public bool HasClaimedName
            {
                get => hasClaimedName;

                internal set
                {
                    hasClaimedName = value;
                    GenerateAndValidateName();
                }
            }

            /// <summary>
            ///     The Display Name with @ before it. Cached here to avoid re-allocations.
            /// </summary>
            public string MentionName { get; private set; }

            private void UpdateUserId(string value, bool generateAndValidateName = true)
            {
                userId = value;
                Address = new Web3Address(value);

                if (generateAndValidateName)
                    GenerateAndValidateName();
            }

            private void GenerateAndValidateName()
            {
                ValidatedName = string.Empty;
                DisplayName = string.Empty;
                WalletId = null;

                if (string.IsNullOrEmpty(Name)) return;

                // Use stackalloc for reasonable-sized names, fallback to heap for very long names
                const int MAX_STACK_SIZE = 256;

                Span<char> buffer = Name.Length <= MAX_STACK_SIZE
                    ? stackalloc char[Name.Length]
                    : new char[Name.Length];

                int validLength = 0;

                // Filter valid alphanumeric characters without allocations
                foreach (char c in Name)
                {
                    if (char.IsLetterOrDigit(c))
                        buffer[validLength++] = c;
                }

                if (validLength == 0) return;

                ReadOnlySpan<char> validatedSpan = buffer[..validLength];
                ValidatedName = new string(validatedSpan);

                if (!HasClaimedName && !string.IsNullOrEmpty(UserId) && UserId.Length > 4)
                {
                    ReadOnlySpan<char> lastFourChars = UserId.AsSpan(UserId.Length - 4, 4);

                    // Build WalletId: #XXXX
                    Span<char> walletBuffer = stackalloc char[5];
                    walletBuffer[0] = '#';
                    lastFourChars.CopyTo(walletBuffer[1..]);
                    WalletId = new string(walletBuffer);

                    // Build DisplayName: {validated}#XXXX
                    Span<char> displayBuffer = validLength + 5 <= MAX_STACK_SIZE
                        ? stackalloc char[validLength + 5]
                        : new char[validLength + 5];

                    validatedSpan.CopyTo(displayBuffer);
                    displayBuffer[validLength] = '#';
                    lastFourChars.CopyTo(displayBuffer[(validLength + 1)..]);
                    DisplayName = new string(displayBuffer);

                    // Build MentionName: @{validated}#XXXX
                    Span<char> mentionBuffer = validLength + 6 <= MAX_STACK_SIZE
                        ? stackalloc char[validLength + 6]
                        : new char[validLength + 6];

                    mentionBuffer[0] = '@';
                    validatedSpan.CopyTo(mentionBuffer[1..]);
                    mentionBuffer[validLength + 1] = '#';
                    lastFourChars.CopyTo(mentionBuffer[(validLength + 2)..]);
                    MentionName = new string(mentionBuffer);
                }
                else
                {
                    DisplayName = ValidatedName;

                    // Build MentionName: @{validated}
                    Span<char> mentionBuffer = validLength + 1 <= MAX_STACK_SIZE
                        ? stackalloc char[validLength + 1]
                        : new char[validLength + 1];

                    mentionBuffer[0] = '@';
                    validatedSpan.CopyTo(mentionBuffer[1..]);
                    MentionName = new string(mentionBuffer);
                }

                if (claimedNameColor.HasValue)
                    UserNameColor = claimedNameColor.Value;
                else
                    UserNameColor = NameColorHelper.GetNameColor(DisplayName);
            }

            public bool Equals(CompactInfo other) =>
                name == other.name && userId == other.userId && hasClaimedName == other.hasClaimedName;

            public override bool Equals(object? obj)
            {
                if (obj is null) return false;
                if (ReferenceEquals(this, obj)) return true;
                if (obj.GetType() != GetType()) return false;
                return Equals((CompactInfo)obj);
            }

            public override int GetHashCode() =>
                HashCode.Combine(name, userId, hasClaimedName);

            public void Dispose() =>
                ProfilePicture.TryDereference();
        }
    }
}
