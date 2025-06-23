#if UNITY_ANDROID

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Android;

namespace RenderHeads.Media.AVProVideo
{

	public class RequestPermissions : MonoBehaviour
	{
		private IEnumerator Start()
		{
			switch (HasUserAuthorisation())
			{
				case AuthorisationStatus.Authorised:
					// All good, nothing to do
					break;

				case AuthorisationStatus.Unavailable:
					// Unavailable
					break;

				case AuthorisationStatus.Denied:
					// User has denied, change output path
					Debug.LogWarning("User has denied");
					break;

				case AuthorisationStatus.NotDetermined:
					// Need to ask permission
					yield return RequestUserAuthorisation();
					// Nested switch, everbodies favourite
					switch (HasUserAuthorisation())
					{
						case AuthorisationStatus.Authorised:
							// All good, nothing to do
							break;

						case AuthorisationStatus.Denied:
							// User has denied access, change output path
							Debug.LogWarning("User has denied access");
							break;

						case AuthorisationStatus.NotDetermined:
							// We were unable to request access for some reason, check the logs for any error information
							Debug.LogWarning("Authorisation to access is still undetermined");
							break;
					}
					break;
			}
		}


		public enum AuthorisationStatus
		{
			Unavailable = -1,
			NotDetermined,
			Denied,
			Authorised
		};


		private static bool _hasRequestedAuthorisation = false;
		private static AuthorisationStatus _authorisationStatus = AuthorisationStatus.NotDetermined;
		private static string AndroidPermissionReadMediaVideo = "android.permission.READ_MEDIA_VIDEO";

		private static int _androidSdkLevel = -1;
		private static int GetAndroidSDKLevel()
		{
			if (_androidSdkLevel == -1)
			{
				var clazz = AndroidJNI.FindClass("android/os/Build$VERSION");
				var fieldID = AndroidJNI.GetStaticFieldID(clazz, "SDK_INT", "I");
				_androidSdkLevel = AndroidJNI.GetStaticIntField(clazz, fieldID);
			}
			return _androidSdkLevel;
		}

		public static AuthorisationStatus HasUserAuthorisation()
		{
			if (_authorisationStatus == AuthorisationStatus.NotDetermined)
			{
				bool authorised = Permission.HasUserAuthorizedPermission(AndroidPermissionReadMediaVideo);
				if (authorised)
				{
					_authorisationStatus = AuthorisationStatus.Authorised;
					_hasRequestedAuthorisation = true;
				}
				else if (_hasRequestedAuthorisation)
				{
					_authorisationStatus = AuthorisationStatus.Denied;
				}
			}

			Debug.Log($"HasUserAuthorisation - {_authorisationStatus}");

			return _authorisationStatus;
		}

	#if UNITY_2020_2_OR_NEWER

		private static bool _waitingForAuthorisation = true;

		private class WaitForAuthorisation : CustomYieldInstruction
		{
			public override bool keepWaiting
			{
				get
				{
					return _waitingForAuthorisation;
				}
			}
		}

		public static CustomYieldInstruction RequestUserAuthorisation()
		{
			if (_authorisationStatus == AuthorisationStatus.Authorised)
			{
				// Already authorised
				return null;
			}

			if (_hasRequestedAuthorisation &&
				_authorisationStatus == AuthorisationStatus.Denied)
			{
				// Already been denied, nothing to do
				return null;
			}

			PermissionCallbacks callbacks = new PermissionCallbacks();

			callbacks.PermissionDenied += (permission) => {
				_authorisationStatus = AuthorisationStatus.Denied;
				_hasRequestedAuthorisation = false;
				_waitingForAuthorisation = false;
			};

			callbacks.PermissionGranted += (permission) => {
				_authorisationStatus = AuthorisationStatus.Authorised;
				_hasRequestedAuthorisation = true;
				_waitingForAuthorisation = false;
			};

		#if !UNITY_2023_1_OR_NEWER
			callbacks.PermissionDeniedAndDontAskAgain += (permission) => {
				_authorisationStatus = AuthorisationStatus.Denied;
				_hasRequestedAuthorisation = true;
				_waitingForAuthorisation = false;
			};
		#endif

			Permission.RequestUserPermission(AndroidPermissionReadMediaVideo, callbacks);
			return new WaitForAuthorisation();
		}

	#else

		public static CustomYieldInstruction RequestUserAuthorisation()
		{
			if (_authorisationStatus == AuthorisationStatus.Authorised)
			{
				// Already authorised
				return null;
			}

			Permission.RequestUserPermission(AndroidPermissionReadMediaVideo);

			// Unfortunately there is no way to know when the dialog has been closed so no way
			// to determine if permission has been denied as there is nothing to distinguish
			// the result of the permission check before, during and after the request unless
			// permission has been authorised.

			return null;
		}
	#endif
	}

}
#endif
