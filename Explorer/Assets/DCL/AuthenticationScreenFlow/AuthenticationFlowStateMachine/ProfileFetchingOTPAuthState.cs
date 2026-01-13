using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.AvatarRendering.Loading.Components;
using DCL.AvatarRendering.Wearables;
using DCL.AvatarRendering.Wearables.Components;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.Diagnostics;
using DCL.FeatureFlags;
using DCL.PerformanceAndDiagnostics;
using DCL.Profiles;
using DCL.Profiles.Self;
using DCL.SceneLoadingScreens.SplashScreen;
using DCL.Utilities;
using DCL.Web3;
using DCL.Web3.Identities;
using DCL.WebRequests;
using MVC;
using System;
using System.Collections.Generic;
using System.Threading;
using ThirdWebUnity;
using UnityEngine;
using UnityEngine.Localization.SmartFormat.PersistentVariables;
using static DCL.AuthenticationScreenFlow.AuthenticationScreenController;
using Avatar = DCL.Profiles.Avatar;

namespace DCL.AuthenticationScreenFlow.AuthenticationFlowStateMachine
{
    public class ProfileFetchingOTPAuthState : AuthStateBase, IPayloadedState<(string email, IWeb3Identity identity, bool isCached, CancellationToken ct)>
    {
        private readonly MVCStateMachine<AuthStateBase> machine;
        private readonly AuthenticationScreenController controller;
        private readonly ReactiveProperty<AuthenticationStatus> currentState;
        private readonly SentryTransactionManager sentryTransactionManager;
        private readonly SplashScreen splashScreen;
        private readonly AuthenticationScreenCharacterPreviewController characterPreviewController;
        private readonly ISelfProfile selfProfile;
        private readonly StringVariable profileNameLabel;
        private readonly IWeb3IdentityCache storedIdentityProvider;
        private readonly IWearablesProvider wearablesProvider;
        private readonly IWebRequestController webRequestController;

        internal Dictionary<string, List<URN>>? maleWearablesByCategory;
        internal Dictionary<string, List<URN>>? femaleWearablesByCategory;
        private readonly List<Avatar> avatarHistory = new ();
        private int currentAvatarIndex = -1;
        private Profile newUserProfile;

        public ProfileFetchingOTPAuthState(
            MVCStateMachine<AuthStateBase> machine,
            AuthenticationScreenView viewInstance,
            AuthenticationScreenController controller,
            ReactiveProperty<AuthenticationStatus> currentState,
            SentryTransactionManager sentryTransactionManager,
            SplashScreen splashScreen,
            AuthenticationScreenCharacterPreviewController characterPreviewController,
            ISelfProfile selfProfile,
            IWeb3IdentityCache storedIdentityProvider,
            IWearablesProvider wearablesProvider,
            IWebRequestController webRequestController) : base(viewInstance)
        {
            this.machine = machine;
            this.controller = controller;
            this.currentState = currentState;
            this.sentryTransactionManager = sentryTransactionManager;
            this.splashScreen = splashScreen;
            this.characterPreviewController = characterPreviewController;
            this.selfProfile = selfProfile;
            this.storedIdentityProvider = storedIdentityProvider;
            this.wearablesProvider = wearablesProvider;
            this.webRequestController = webRequestController;

            profileNameLabel = (StringVariable)viewInstance!.ProfileNameLabel.StringReference["back_profileName"];
        }

        public void Enter((string email, IWeb3Identity identity, bool isCached, CancellationToken ct) payload)
        {
            FetchProfileFlowAsync(payload.email, payload.identity, payload.isCached, payload.ct).Forget();
        }

        private async UniTaskVoid FetchProfileFlowAsync(string email, IWeb3Identity identity, bool isCached, CancellationToken ct)
        {
            var identityValidationSpan = new SpanData
            {
                TransactionName = LOADING_TRANSACTION_NAME,
                SpanName = "IdentityValidation",
                SpanOperation = "auth.identity_validation",
                Depth = 1,
            };

            sentryTransactionManager.StartSpan(identityValidationSpan);

            if (IsUserAllowedToAccessToBeta(identity))
            {
                currentState.Value = isCached ? AuthenticationStatus.FetchingProfileCached : AuthenticationStatus.FetchingProfile;

                try
                {
                    var profileFetchSpan = new SpanData
                    {
                        TransactionName = LOADING_TRANSACTION_NAME,
                        SpanName = "FetchProfileCached",
                        SpanOperation = "auth.profile_fetch",
                        Depth = 1,
                    };

                    sentryTransactionManager.StartSpan(profileFetchSpan);

                    (Profile profile, bool isNewUser) = await FetchProfileAsync(email, identity, ct);
                    sentryTransactionManager.EndCurrentSpan(LOADING_TRANSACTION_NAME);

                    if (isNewUser)
                        machine.Enter<LobbyOTPAuthState, (Profile, bool, bool, CancellationToken)>((profile, false, baseWearablesLoaded, ct));
                    else
                        machine.Enter<LobbyAuthState, (Profile, bool)>((profile, isCached));
                }
                catch (OperationCanceledException)
                {
                    sentryTransactionManager.EndCurrentSpanWithError(LOADING_TRANSACTION_NAME, "Login process was cancelled by user");
                    machine.Enter<LoginStartAuthState>();
                }
                catch (ProfileNotFoundException e)
                {
                    sentryTransactionManager.EndCurrentSpanWithError(LOADING_TRANSACTION_NAME, $"Profile not found during {nameof(ProfileFetchingAuthState)} ({(isCached ? "cached" : "main")} flow)", e);
                    machine.Enter<LoginStartAuthState>();
                }
                catch (Exception e)
                {
                    sentryTransactionManager.EndCurrentSpanWithError(LOADING_TRANSACTION_NAME, $"Unexpected error during {nameof(ProfileFetchingAuthState)} ({(isCached ? "cached" : "main")} flow)", e);
                    ReportHub.LogException(e, new ReportData(ReportCategory.AUTHENTICATION));
                    machine.Enter<LoginStartAuthState, PopupType>(PopupType.CONNECTION_ERROR);
                }
            }
            else
            {
                sentryTransactionManager.EndCurrentSpanWithError(LOADING_TRANSACTION_NAME, $"User not allowed to access beta - restricted user in {nameof(ProfileFetchingAuthState)} ({(isCached ? "cached" : "main")} flow)");
                machine.Enter<LoginStartAuthState, PopupType>(PopupType.RESTRICTED_USER);
            }
        }

        private async UniTask<(Profile profile, bool isNewUser)> FetchProfileAsync(string email, IWeb3Identity identity, CancellationToken ct)
        {
            // var walletAddress = identity.Address.ToString();
            // bool profileExists = await CheckProfileExistsAsync(walletAddress, ct);
            Profile? profile = await selfProfile.ProfileAsync(ct);

            if (profile == null && ThirdWebManager.Instance.ActiveWallet != null)
            {
                IWeb3Identity? identity1 = storedIdentityProvider.Identity;

                if (identity1 == null)
                    throw new Web3IdentityMissingException("Web3 identity is not available when creating a default profile");

                newUserProfile = BuildDefaultProfile(identity1.Address.ToString(), email);

                // Load base wearables catalog for randomization
                await LoadBaseWearablesAsync(ct);
                InitializeAvatarHistory(newUserProfile.Avatar);

                return (newUserProfile, true);
            }

            profile!.IsDirty = true;
            profile.HasConnectedWeb3 = true;
            return (profile, false);
        }

        private static bool IsUserAllowedToAccessToBeta(IWeb3Identity storedIdentity)
        {
            if (Application.isEditor)
                return true;

            FeatureFlagsConfiguration flags = FeatureFlagsConfiguration.Instance;

            if (!flags.IsEnabled(FeatureFlagsStrings.USER_ALLOW_LIST, FeatureFlagsStrings.WALLET_VARIANT)) return true;

            if (!flags.TryGetCsvPayload(FeatureFlagsStrings.USER_ALLOW_LIST, FeatureFlagsStrings.WALLET_VARIANT, out List<List<string>>? allowedUsersCsv))
                return true;

            bool isUserAllowed = allowedUsersCsv![0]
               .Exists(s => new Web3Address(s).Equals(storedIdentity.Address));

            return isUserAllowed;
        }

        private bool baseWearablesLoaded;

        private async UniTask LoadBaseWearablesAsync(CancellationToken ct)
        {
            if (baseWearablesLoaded)
                return;

            try
            {
                // Load base wearables catalog from backend (pageSize 300 to get all)
                (IReadOnlyList<ITrimmedWearable> wearables, _) = await wearablesProvider.GetAsync(
                    pageSize: 300,
                    pageNumber: 1,
                    ct,
                    collectionType: IWearablesProvider.CollectionType.Base);

                maleWearablesByCategory = new Dictionary<string, List<URN>>();
                femaleWearablesByCategory = new Dictionary<string, List<URN>>();

                foreach (ITrimmedWearable? wearable in wearables)
                {
                    string category = wearable.GetCategory();

                    // Skip body shapes
                    if (category == "body_shape")
                        continue;

                    // Add to male dictionary if compatible
                    if (wearable.IsCompatibleWithBodyShape(BodyShape.MALE))
                    {
                        if (!maleWearablesByCategory.ContainsKey(category))
                            maleWearablesByCategory[category] = new List<URN>();

                        maleWearablesByCategory[category].Add(wearable.GetUrn());
                    }

                    // Add to female dictionary if compatible
                    if (wearable.IsCompatibleWithBodyShape(BodyShape.FEMALE))
                    {
                        if (!femaleWearablesByCategory.ContainsKey(category))
                            femaleWearablesByCategory[category] = new List<URN>();

                        femaleWearablesByCategory[category].Add(wearable.GetUrn());
                    }
                }

                baseWearablesLoaded = true;
                ReportHub.Log(ReportCategory.AUTHENTICATION, $"Base wearables catalog loaded: {wearables.Count} items, male categories: {maleWearablesByCategory.Count}, female categories: {femaleWearablesByCategory.Count}");
            }
            catch (Exception e)
            {
                ReportHub.LogException(e, new ReportData(ReportCategory.AUTHENTICATION));

                // Fallback to hardcoded defaults will be used
                baseWearablesLoaded = false;
            }
        }

        /// <summary>
        ///     Проверяет существование профиля через GET запрос к API Decentraland.
        /// </summary>
        /// <param name="walletAddress">Ethereum адрес кошелька</param>
        /// <param name="ct">Cancellation token</param>
        /// <returns>true если профиль существует (200), false если не существует (404) или ошибка</returns>
        private async UniTask<bool> CheckProfileExistsAsync(string walletAddress, CancellationToken ct)
        {
            const string PROFILES_API_URL = "https://peer.decentraland.org/lambdas/profiles/";
            var url = $"{PROFILES_API_URL}{walletAddress}";

            try
            {
                int statusCode = await webRequestController
                                      .GetAsync(new CommonArguments(URLAddress.FromString(url)), ct, ReportCategory.PROFILE)
                                      .StatusCodeAsync();

                return statusCode == 200;
            }
            catch (Exception) { return false; }
        }

        private void InitializeAvatarHistory(Avatar initialAvatar)
        {
            avatarHistory.Clear();
            avatarHistory.Add(initialAvatar);
            currentAvatarIndex = 0;
            UpdateAvatarNavigationButtons();
        }

        private void UpdateAvatarNavigationButtons()
        {
            if (viewInstance == null)
                return;

            viewInstance.PrevRandomButton.interactable = currentAvatarIndex > 0;
            viewInstance.NextRandomButton.interactable = currentAvatarIndex < avatarHistory.Count - 1;
        }

        private Profile BuildDefaultProfile(string walletAddress, string name = "")
        {
            // Randomize body shape between MALE and FEMALE
            Avatar avatar = CreateDefaultAvatar();

            // Extract name from email (everything before @) or use default
            var profile = Profile.Create(walletAddress, name, avatar);
            profile.HasClaimedName = false;
            profile.HasConnectedWeb3 = true;
            profile.Description = string.Empty;
            profile.Country = string.Empty;
            profile.EmploymentStatus = string.Empty;
            profile.Gender = string.Empty;
            profile.Pronouns = string.Empty;
            profile.RelationshipStatus = string.Empty;
            profile.SexualOrientation = string.Empty;
            profile.Language = string.Empty;
            profile.Profession = string.Empty;
            profile.RealName = string.Empty;
            profile.Hobbies = string.Empty;
            profile.TutorialStep = 0;
            profile.Version = 0;
            profile.UserNameColor = NameColorHelper.GetNameColor(profile.DisplayName);
            profile.IsDirty = true;

            return profile;
        }

        private Avatar CreateDefaultAvatar()
        {
            BodyShape bodyShape = UnityEngine.Random.value > 0.5f ? BodyShape.MALE : BodyShape.FEMALE;

            // If base wearables loaded from backend - use randomizer
            if (baseWearablesLoaded && maleWearablesByCategory != null && femaleWearablesByCategory != null)
            {
                Dictionary<string, List<URN>>? wearablesByCategory = bodyShape.Equals(BodyShape.MALE) ? maleWearablesByCategory : femaleWearablesByCategory;
                HashSet<URN> wearablesSet = GetRandomWearablesFromCategories(wearablesByCategory);

                return new Avatar(
                    bodyShape,
                    wearablesSet,
                    WearablesConstants.DefaultColors.GetRandomEyesColor(),
                    WearablesConstants.DefaultColors.GetRandomHairColor(),
                    WearablesConstants.DefaultColors.GetRandomSkinColor());
            }

            // Fallback to hardcoded defaults
            return new Avatar(
                bodyShape,
                WearablesConstants.DefaultWearables.GetDefaultWearablesForBodyShape(bodyShape),
                WearablesConstants.DefaultColors.GetRandomEyesColor(),
                WearablesConstants.DefaultColors.GetRandomHairColor(),
                WearablesConstants.DefaultColors.GetRandomSkinColor());
        }

        private static HashSet<URN> GetRandomWearablesFromCategories(Dictionary<string, List<URN>> wearablesByCategory)
        {
            var result = new HashSet<URN>();

            foreach (List<URN>? categoryWearables in wearablesByCategory.Values)
            {
                if (categoryWearables.Count > 0)
                    result.Add(categoryWearables[UnityEngine.Random.Range(0, categoryWearables.Count)]);
            }

            return result;
        }
    }
}
