using RenderHeads.Media.AVProVideo;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.SDKComponents.MediaStream.Playground
{
    public class MediaPlayerPlayground : MonoBehaviour
    {
        [SerializeField] private MediaPlayer player = null!;
        [SerializeField] private List<Step> steps = new ();

        [Serializable]
        public class Step
        {
            public string url = string.Empty;
            public float forDuration;
        }

        private IEnumerator Start()
        {
            while (destroyCancellationToken.IsCancellationRequested == false)
                foreach (Step step in steps)
                {
                    player.OpenMedia(MediaPathType.AbsolutePathOrURL, step.url);
                    yield return new WaitForSeconds(step.forDuration);
                }
        }
    }
}
