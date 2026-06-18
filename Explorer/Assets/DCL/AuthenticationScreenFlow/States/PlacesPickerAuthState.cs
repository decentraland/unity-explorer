using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.CommunicationData.URLHelpers;
using DCL.Diagnostics;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Places;
using DCL.PlacesAPIService;
using DCL.RealmNavigation;
using MVC;
using System;
using System.Threading;

namespace DCL.AuthenticationScreenFlow
{
    /// <summary>
    /// Shown after the user confirms login on a lobby screen. Instead of auto-teleporting to Genesis Plaza,
    /// the user picks a destination from the places list. The selection sets the destination (a World realm or a
    /// Genesis parcel) and only then releases the init flow so the single existing loading screen takes the user there.
    /// </summary>
    public class PlacesPickerAuthState : AuthStateBase, IPayloadedState<PlacesPickerPayload>
    {
        private readonly MVCStateMachine<AuthStateBase> fsm;
        private readonly AuthenticationScreenController controller;
        private readonly PlacesController placesController;
        private readonly StartParcel startParcel;
        private readonly IGlobalRealmController realmController;
        private readonly IDecentralandUrlsSource decentralandUrlsSource;

        private bool isActive;
        private CancellationToken loginCt;
        private Action? onBackToLobby;

        public PlacesPickerAuthState(
            MVCStateMachine<AuthStateBase> fsm,
            AuthenticationScreenView viewInstance,
            AuthenticationScreenController controller,
            PlacesController placesController,
            StartParcel startParcel,
            IGlobalRealmController realmController,
            IDecentralandUrlsSource decentralandUrlsSource) : base(viewInstance)
        {
            this.fsm = fsm;
            this.controller = controller;
            this.placesController = placesController;
            this.startParcel = startParcel;
            this.realmController = realmController;
            this.decentralandUrlsSource = decentralandUrlsSource;
        }

        public void Enter(PlacesPickerPayload payload)
        {
            base.Enter();

            loginCt = payload.LoginCt;
            onBackToLobby = payload.OnBackToLobby;
            isActive = true;

            placesController.Activate();

            viewInstance.GoToGenesisButton.gameObject.SetActive(true);
            viewInstance.PlacesPickerBackButton.gameObject.SetActive(true);
            SetButtonsInteractable(true);

            viewInstance.GoToGenesisButton.onClick.AddListener(OnGoToGenesisClicked);
            viewInstance.PlacesPickerBackButton.onClick.AddListener(OnBackClicked);
        }

        public override void Exit()
        {
            isActive = false;

            viewInstance.GoToGenesisButton.onClick.RemoveListener(OnGoToGenesisClicked);
            viewInstance.PlacesPickerBackButton.onClick.RemoveListener(OnBackClicked);

            viewInstance.GoToGenesisButton.gameObject.SetActive(false);
            viewInstance.PlacesPickerBackButton.gameObject.SetActive(false);

            placesController.Deactivate();

            onBackToLobby = null;
            loginCt = CancellationToken.None;
            base.Exit();
        }

        /// <summary>
        /// Routed from the auth PlacesController when a place card is selected (Jump In button or card body).
        /// </summary>
        public void OnPlaceSelected(PlacesData.PlaceInfo placeInfo)
        {
            if (!isActive)
                return;

            SetButtonsInteractable(false);
            CommitDestinationAsync(placeInfo, loginCt).Forget();
        }

        private async UniTaskVoid CommitDestinationAsync(PlacesData.PlaceInfo placeInfo, CancellationToken ct)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(placeInfo.world_name))
                {
                    // Set the realm to the chosen World before resuming so the init flow's single loading screen
                    // enters it (and its access check / spawn coordinate apply). SetRealmAsync does not show a
                    // loading screen by itself.
                    URLDomain worldDomain = URLDomain.FromString(
                        new ENS(placeInfo.world_name).ConvertEnsToWorldUrl(decentralandUrlsSource.Url(DecentralandUrl.WorldServer)));

                    await realmController.SetRealmAsync(worldDomain, ct);
                }
                else
                {
                    // Genesis parcel: the init flow's TeleportStartupOperation consumes StartParcel.
                    AssignResult assignResult = startParcel.Assign(placeInfo.base_position_processed);

                    if (assignResult == AssignResult.ParcelAlreadyConsumed)
                        ReportHub.LogWarning(ReportCategory.AUTHENTICATION,
                            "Start parcel already consumed when picking a place; proceeding with the existing destination.");
                }

                if (ct.IsCancellationRequested)
                    return;

                Proceed();
            }
            catch (OperationCanceledException)
            { /* Expected on cancellation */
            }
            catch (Exception e)
            {
                ReportHub.LogException(e, new ReportData(ReportCategory.AUTHENTICATION));

                // Let the user pick again instead of getting stuck.
                if (isActive)
                    SetButtonsInteractable(true);
            }
        }

        private void OnGoToGenesisClicked()
        {
            // Leave StartParcel / realm at their bootstrap defaults (Genesis 0,0).
            SetButtonsInteractable(false);
            Proceed();
        }

        private void OnBackClicked()
        {
            // Exit() (triggered by re-entering the lobby) deactivates the places list and hides the buttons.
            onBackToLobby?.Invoke();
        }

        private void Proceed()
        {
            // fsm.Enter triggers this state's Exit(), which deactivates the places list and hides the buttons.
            fsm.Enter<InitAuthState>();
            controller.TrySetLifeCycle();
        }

        private void SetButtonsInteractable(bool interactable)
        {
            viewInstance.GoToGenesisButton.interactable = interactable;
            viewInstance.PlacesPickerBackButton.interactable = interactable;
        }
    }

    public readonly struct PlacesPickerPayload
    {
        public readonly CancellationToken LoginCt;

        /// <summary>
        /// Re-enters the originating lobby FSM state (existing- or new-account) with its own payload.
        /// </summary>
        public readonly Action OnBackToLobby;

        public PlacesPickerPayload(CancellationToken loginCt, Action onBackToLobby)
        {
            LoginCt = loginCt;
            OnBackToLobby = onBackToLobby;
        }
    }
}
