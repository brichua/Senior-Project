using TMPro;
using UnityEngine;

namespace VocaloidTCG
{
    [RequireComponent(typeof(TMP_Text))]
    public sealed class StatusTextFade : MonoBehaviour
    {
        [Min(0)] public float fadeInSeconds = 0.25f;
        [Min(0)] public float visibleSeconds = 10f;
        [Min(0)] public float fadeOutSeconds = 0.5f;
        private TMP_Text label;
        private float fullAlpha, elapsed;
        private bool showing;

        private void Awake() { Initialize(); }

        private void Initialize(){
            if(label) return;
            label = GetComponent<TMP_Text>();
            fullAlpha = label.alpha > 0 ? label.alpha : 1f;
            Clear();
        }

        public static void Show(TMP_Text target, string message){
            if(!target) return;
            var fade = target.GetComponent<StatusTextFade>();

            if(!fade) fade = target.gameObject.AddComponent<StatusTextFade>();
            fade.Initialize();
            if(string.IsNullOrEmpty(message)) {
                fade.Clear(); return;
            }

            target.richText = false;
            target.text = message;
            fade.elapsed = 0;
            fade.showing = true;
            target.alpha = fade.fadeInSeconds > 0 ? 0 : fade.fullAlpha;
        }

        private void Update(){
            if(!showing) return;
            elapsed += Time.unscaledDeltaTime;

            float fadeIn = Mathf.Max(0, fadeInSeconds);
            float holdEnd = fadeIn + Mathf.Max(0, visibleSeconds);
            float fadeOut = Mathf.Max(0, fadeOutSeconds);
            
            if(elapsed < fadeIn) label.alpha = fullAlpha * elapsed / fadeIn;
            else if(elapsed < holdEnd) label.alpha = fullAlpha;
            else if(fadeOut > 0 && elapsed < holdEnd + fadeOut)
                label.alpha = fullAlpha * (1 - (elapsed - holdEnd) / fadeOut);
            else Clear();
        }

        private void Clear(){
            showing = false;
            elapsed = 0;
            if(label) { label.text = ""; label.alpha = 0; }
        }

        private void OnDisable() { Clear(); }
    }
}
