using Cysharp.Threading.Tasks;
using DCL.Audio;
using DCL.Character.CharacterMotion.Components;
using DCL.PlacesAPIService;
using DCL.UI;
using DCL.WebRequests;
using ECS.SceneLifeCycle.Realm;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using UnityEngine;

namespace DCL.Navmap
{
    public class FloatingPanelController : IDisposable
    {
        public event Action OnJumpIn;

        private readonly FloatingPanelView view;
        private readonly IPlacesAPIService placesAPIService;
        private readonly IRealmNavigator realmNavigator;
        private readonly Dictionary<string, GameObject> categoriesDictionary;

        private MultiStateButtonController likeButtonController;
        private MultiStateButtonController dislikeButtonController;
        private MultiStateButtonController favoriteButtonController;
        private CancellationTokenSource cts;

        private readonly ImageController placeImageController;

        public FloatingPanelController(FloatingPanelView view, IPlacesAPIService placesAPIService,
            IWebRequestController webRequestController, IRealmNavigator realmNavigator)
        {
            this.view = view;
            this.placesAPIService = placesAPIService;
            this.realmNavigator = realmNavigator;

            view.closeButton.onClick.AddListener(HidePanel);
            view.CanvasGroup.interactable = false;
            view.CanvasGroup.blocksRaycasts = false;
            placeImageController = new ImageController(view.placeImage, webRequestController);
            categoriesDictionary = new Dictionary<string, GameObject>();

            for (var i = 0; i < view.categories.Length; i++)
                categoriesDictionary.Add(view.categoryNames[i], view.categories[i]);

            ResetCategories();
            InitButtons();
        }

        public void Dispose()
        {
            likeButtonController.OnButtonClicked -= OnLike;
            dislikeButtonController.OnButtonClicked -= OnDislike;
            favoriteButtonController.OnButtonClicked -= OnFavorite;
        }

        private void InitButtons()
        {
            likeButtonController = new MultiStateButtonController(view.likeButton, true);
            dislikeButtonController = new MultiStateButtonController(view.dislikeButton, true);
            favoriteButtonController = new MultiStateButtonController(view.favoriteButton, true);
            likeButtonController.OnButtonClicked += OnLike;
            dislikeButtonController.OnButtonClicked += OnDislike;
            favoriteButtonController.OnButtonClicked += OnFavorite;
            view.backButton.onClick.AddListener(HidePanelFromBackButton);
        }

        public void HandlePanelVisibility(Vector2Int parcel, bool showBackButton)
        {
            view.backButton.gameObject.SetActive(showBackButton);

            if (showBackButton)
            {
                view.panelAnimator.Rebind();
                view.panelAnimator.Update(0f);
                view.panelAnimator.SetTrigger(AnimationHashes.TO_LEFT);
                ShowPanel(parcel, -1);
            }
            else
            {
                if (view.panelAnimator.GetCurrentAnimatorStateInfo(0).IsName("Out") || view.panelAnimator.GetCurrentAnimatorStateInfo(0).IsName("Empty"))
                {
                    view.panelAnimator.Rebind();
                    view.panelAnimator.Update(0f);
                    view.panelAnimator.SetTrigger(AnimationHashes.IN);
                    ShowPanel(parcel, AnimationHashes.LOADED);
                }
                else
                {
                    view.panelAnimator.SetTrigger(AnimationHashes.LOADING);
                    GetPlaceInfoAsync(parcel, AnimationHashes.LOADED).Forget();
                }
            }
        }

        private void ShowPanel(Vector2Int parcel, int animationTrigger)
        {
            view.gameObject.SetActive(true);
            view.CanvasGroup.interactable = true;
            view.CanvasGroup.blocksRaycasts = true;
            UIAudioEventsBus.Instance.SendPlayAudioEvent(view.OnShowAudio);

            cts = new CancellationTokenSource();
            GetPlaceInfoAsync(parcel, animationTrigger).Forget();
        }

        private async UniTaskVoid GetPlaceInfoAsync(Vector2Int parcel, int animationTrigger)
        {
            try
            {
                view.jumpInButton.onClick.RemoveAllListeners();
                view.jumpInButton.onClick.AddListener(() => JumpIn(parcel));
                PlacesData.PlaceInfo? placeInfo = await placesAPIService.GetPlaceAsync(parcel, cts.Token);
                ResetCategories();

                if (placeInfo == null)
                    SetEmptyParcelInfo(parcel);
                else
                    SetFloatingPanelInfo(placeInfo);
            }
            catch (Exception) { SetEmptyParcelInfo(parcel); }
            finally
            {
                if (animationTrigger != -1)
                    view.panelAnimator.SetTrigger(animationTrigger);
            }
        }

        private void JumpIn(Vector2Int parcel)
        {
            OnJumpIn?.Invoke();
            realmNavigator.TryInitializeTeleportToParcelAsync(parcel, cts.Token, false).Forget();
        }

        private void SetEmptyParcelInfo(Vector2Int parcel)
        {
            view.placeName.text = "Empty parcel";
            view.placeCreator.gameObject.SetActive(false);
            view.placeDescription.text = "This place doesn't have a description set";
            view.location.text = parcel.ToString().Replace("(", "").Replace(")", "");
            view.visits.text = "-";
            view.upvotes.text = "-";
            view.parcelsCount.text = "1";
            placeImageController.SetVisible(false);

            ResetCategories();
        }

        private void SetFloatingPanelInfo(PlacesData.PlaceInfo placeInfo)
        {
            placeImageController.SetVisible(true);
            placeImageController.RequestImage(placeInfo.image);
            view.placeName.text = placeInfo.title;
            view.placeCreator.text = $"created by <b>{placeInfo.contact_name}</b>";
            view.placeCreator.gameObject.SetActive(!string.IsNullOrEmpty(placeInfo.contact_name));

            view.placeDescription.text = string.IsNullOrEmpty(placeInfo.description)
                ? "This place doesn't have a description set"
                : placeInfo.description;

            view.location.text = placeInfo.base_position;
            view.visits.SetText("{0:0}", placeInfo.user_visits);
            view.parcelsCount.SetText("{0:0}", placeInfo.Positions.Length);

            SetUpVotes(placeInfo);

            if (placeInfo.categories.Length == 0)
            {
                view.appearsIn.SetActive(false);
                return;
            }

            var hasVisibleCategories = false;

            foreach (string placeInfoCategory in placeInfo.categories)
                if (categoriesDictionary.TryGetValue(placeInfoCategory, out GameObject categoryGameObject))
                {
                    hasVisibleCategories = true;
                    categoryGameObject.SetActive(true);
                }

            view.appearsIn.SetActive(hasVisibleCategories);
        }

        private void SetUpVotes(PlacesData.PlaceInfo placeInfo)
        {
            string likeRate = placeInfo.like_rate;

            if (string.IsNullOrEmpty(likeRate) || !float.TryParse(likeRate, NumberStyles.Float, CultureInfo.InvariantCulture, out float result))
                view.upvotes.SetText("-%");
            else
                view.upvotes.SetText("{0:0}%", result * 100);
        }

        private void ResetCategories()
        {
            foreach (KeyValuePair<string, GameObject> keyValuePair in categoriesDictionary)
                keyValuePair.Value.SetActive(false);
        }

        private void OnFavorite(bool isFavorite) { }

        private void OnDislike(bool isDisliked)
        {
            if (isDisliked)
                likeButtonController.SetButtonState(false);
        }

        private void OnLike(bool isLiked)
        {
            if (isLiked)
                dislikeButtonController.SetButtonState(false);
        }

        private void HidePanelFromBackButton()
        {
            view.panelAnimator.SetTrigger(AnimationHashes.TO_RIGHT);
            view.CanvasGroup.interactable = false;
            view.CanvasGroup.blocksRaycasts = false;
        }

        public void HidePanel()
        {
            view.panelAnimator.SetTrigger(AnimationHashes.OUT);
            view.CanvasGroup.interactable = false;
            view.CanvasGroup.blocksRaycasts = false;
        }
    }
}
