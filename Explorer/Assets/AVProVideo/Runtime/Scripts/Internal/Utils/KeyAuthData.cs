using System;
using UnityEngine;

//-----------------------------------------------------------------------------
// Copyright 2020-2021 RenderHeads Ltd.  All rights reserved.
//-----------------------------------------------------------------------------

namespace RenderHeads.Media.AVProVideo
{
	/// <summary>
	/// Data for handling authentication of encrypted AES-128 HLS streams
	/// </summary>
	/// 
	[Serializable]
	public class KeyAuthData : ISerializationCallbackReceiver
	{
		[SerializeField]
		public string keyServerToken;

		[SerializeField, Multiline]
		public string overrideDecryptionKeyBase64;

		public bool IsModified()
		{
			return !String.IsNullOrEmpty(keyServerToken) || !String.IsNullOrEmpty(overrideDecryptionKeyBase64);
		}

		private byte[] _overrideDecryptionKey;
		public byte[] overrideDecryptionKey
		{
			get
			{
				return _overrideDecryptionKey;
			}
			set
			{
				_overrideDecryptionKey = value;
				if (value == null)
					overrideDecryptionKeyBase64 = "";
				else
					overrideDecryptionKeyBase64 = Convert.ToBase64String(_overrideDecryptionKey);
			}
		}

		// ISerializationCallbackReceiver

		public void OnBeforeSerialize()
		{
			// Nothing to do here
		}

		public void OnAfterDeserialize()
		{
			if (!string.IsNullOrEmpty(overrideDecryptionKeyBase64))
			{
				try
				{
					// Regenerate the byte[]
					_overrideDecryptionKey = Convert.FromBase64String(overrideDecryptionKeyBase64);
				}
				catch (Exception e)
				{
					Debug.LogWarning($"Failed to decode overrideDecryptionKeyBase64, error: {e}");
				}
			}
			else
			{
				_overrideDecryptionKey = null;
			}
		}
	}
}
