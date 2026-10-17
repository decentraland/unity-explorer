using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

namespace Services
{
    public class RemoteTextureService : MonoBehaviour
    {
        private const int CONCURRENT_REQUESTS = 12;

        private readonly Dictionary<string, Texture2D> _cachedTextures = new();

        private readonly LinkedList<string> _requestQueue = new();
        private readonly HashSet<string> _requests = new();

        private readonly Dictionary<string, Dictionary<int, (Action<Texture2D> success, Action error)>>
            _responseListeners = new();

        private int _handle = 1;
        private bool _paused;

        private static RemoteTextureService _instance;

        public static RemoteTextureService Instance
        {
            get
            {
                if (_instance == null)
                {
                    var go = new GameObject("RemoteTextureService");
                    _instance = go.AddComponent<RemoteTextureService>();
                }

                return _instance;
            }
        }

        public void PreloadTexture(string url)
        {
            _requestQueue.AddLast(url);
            CheckQueue();
        }

        public int RequestTexture(string url, Action<Texture2D> callback, Action error = null)
        {
            // Check if we have a cached version already
            if (_cachedTextures.TryGetValue(url, out var cachedTexture))
            {
                callback(cachedTexture);
                return -1;
            }

            // Create a new handle for this request
            var handle = _handle++;


            // Add listener
            if (_responseListeners.TryGetValue(url, out var listeners))
            {
                listeners.Add(handle, (callback, error));
            }
            else
            {
                _responseListeners.Add(url,
                    new Dictionary<int, (Action<Texture2D>, Action)> { { handle, (callback, error) } });
            }

            // Check if the request is running
            if (_requests.Contains(url))
            {
                return handle;
            }

            // Check if the url is queued and if so remove it so it will be added at the front of the line
            if (_requestQueue.Contains(url))
            {
                _requestQueue.Remove(url);
            }

            // Add request to front of queue
            _requestQueue.AddFirst(url);

            CheckQueue();

            return handle;
        }

        public void RemoveListener(int handle)
        {
            foreach (var (_, listeners) in _responseListeners)
            {
                listeners.Remove(handle);
            }
        }

        public void Pause(bool pause)
        {
            _paused = pause;

            if (!_paused) CheckQueue();
        }

        private void CheckQueue()
        {
            if (_paused) return;

            while (_requests.Count < CONCURRENT_REQUESTS && _requestQueue.Count > 0)
            {
                var url = _requestQueue.First.Value;
                _requestQueue.RemoveFirst();

                if (_cachedTextures.ContainsKey(url) || _requests.Contains(url)) continue;

                StartCoroutine(LoadImage(url));
            }
        }

        private async Awaitable LoadImage(string url)
        {
            _requests.Add(url);

            var request = UnityWebRequestTexture.GetTexture(url);

            await request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                // Debug.Log($"Texture loaded: {url}");
                var tex = ((DownloadHandlerTexture)request.downloadHandler).texture;
                _cachedTextures.Add(url, tex);

                if (_responseListeners.TryGetValue(url, out var listeners))
                {
                    foreach (var (_, (success, _)) in listeners)
                    {
                        success(tex);
                    }

                    _responseListeners.Remove(url);
                }
            }
            else
            {
                Debug.LogError($"Failed to load texture: {url} - {request.error}");
                if (_responseListeners.TryGetValue(url, out var listeners))
                {
                    foreach (var (_, (_, error)) in listeners)
                    {
                        error();
                    }

                    _responseListeners.Remove(url);
                }
            }

            _requests.Remove(url);

            CheckQueue();
        }
    }
}