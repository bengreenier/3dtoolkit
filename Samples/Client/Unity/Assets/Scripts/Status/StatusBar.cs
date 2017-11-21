using System;
using UnityEngine;
using UnityEngine.Events;

#if UNITY_WSA || UNITY_STANDALONE_WIN
using UnityEngine.Windows.Speech;
#endif

namespace Status
{
    /// <summary>
    /// Logical component for controlling a webrtc status bar
    /// </summary>
    public class StatusBar : MonoBehaviour
    {
        /// <summary>
        /// Event with string parameter
        /// </summary>
        [Serializable]
        public class UnityStringEvent : UnityEvent<string> { }
        
        /// <summary>
        /// The voice command to listen for, to toggle visibility
        /// </summary>
        public string VoiceActivationCommand = "debug";
        
        /// <summary>
        /// Event that should be raised when connection status changes
        /// </summary>
        public UnityStringEvent OnConnectionStatusChange;

        /// <summary>
        /// Event that should be raised when ice status changes
        /// </summary>
        public UnityStringEvent OnIceStatusChange;
        
        /// <summary>
        /// Event that should be raised when network latency changes
        /// </summary>
        public UnityStringEvent OnNetworkLatencyChange;

#if UNITY_WSA || UNITY_STANDALONE_WIN
        /// <summary>
        /// Voice recognizer
        /// </summary>
        private DictationRecognizer recognizer;
#endif

        /// <summary>
        /// Flag indicting if the status bar is currently visible
        /// </summary>
        private bool isCurrentlyVisible = false;

        /// <summary>
        /// Unity object Start() hook
        /// </summary>
        private void Start()
        {
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            SetVisible(true);
#else
            SetVisible(false);
#endif

#if UNITY_WSA || UNITY_STANDALONE_WIN
            this.recognizer = new DictationRecognizer();
            this.recognizer.DictationResult += Recognizer_DictationResult;
#endif
        }

#if UNITY_WSA || UNITY_STANDALONE_WIN
        /// <summary>
        /// Recognition callback for when a voice result arrives
        /// </summary>
        /// <param name="text">the text of the voice</param>
        /// <param name="confidence">the confidence level</param>
        private void Recognizer_DictationResult(string text, ConfidenceLevel confidence)
        {
            // if we're confident we heard the <see cref="VoiceActivationCommand"/>
            if (confidence != ConfidenceLevel.Rejected && text.Equals(this.VoiceActivationCommand, StringComparison.OrdinalIgnoreCase))
            {
                // we toggle visibility
                SetVisible(!isCurrentlyVisible);
            }
        }
#endif

        /// <summary>
        /// Changes the status bar visiblity
        /// </summary>
        /// <param name="visible">indicates visibility</param>
        private void SetVisible(bool visible)
        {
            for (var i = 0; i < this.transform.childCount; i++)
            {
                var child = this.transform.GetChild(i);

                child.gameObject.SetActive(visible);
            }

            isCurrentlyVisible = visible;
        }

        /// <summary>
        /// Shows the status bar
        /// </summary>
        public void Show()
        {
            SetVisible(true);
        }

        /// <summary>
        /// Hides the status bar
        /// </summary>
        public void Hide()
        {
            SetVisible(false);
        }
    }
}