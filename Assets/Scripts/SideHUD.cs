using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace VocaloidTCG.BoardUI
{
    public sealed class SideHUD : MonoBehaviour
    {
        public Image skill, barBackground, barFill, deckIcon, deck, infoBackground;
        public Image avatar, avatarBackground, infoIcon, energy;
        public TMP_Text score, deckCount;
        public Animator avatarAnimator;
        public TMP_Text subtitle;
        public GameObject subtitleRoot;
        private string subtitleMessage = "", timerMessage = "";

        private void Awake(){
            RenderSubtitle();
        }

        public void ShowSubtitle(string message){
            subtitleMessage = message ?? ""; RenderSubtitle();
        }

        public void HideSubtitle(){
            subtitleMessage = ""; RenderSubtitle();
        }

        public void SetTimerSubtitle(string message){
            message = message ?? "";
            if(timerMessage == message) return;
            timerMessage = message; RenderSubtitle();
        }

        public void ClearSubtitle(){
            subtitleMessage = timerMessage = ""; RenderSubtitle();
        }
        
        private void RenderSubtitle(){
            bool hasMessage = !string.IsNullOrWhiteSpace(subtitleMessage);
            bool hasTimer = !string.IsNullOrWhiteSpace(timerMessage);
            string text = hasMessage ? subtitleMessage : "";
            if(hasTimer) text += (hasMessage ? "\n" : "") + timerMessage;
            CardInfoView.Put(subtitle, text);
            var root = subtitleRoot ? subtitleRoot : subtitle ? subtitle.gameObject : null;
            if(root && root.activeSelf != (hasMessage || hasTimer)) root.SetActive(hasMessage || hasTimer);
        }

        private void OnDisable(){
            ClearSubtitle();
        }

        public void Apply(SideArt art){
            BoardUIController.SetImage(skill, art.skill);
            BoardUIController.SetImage(barBackground, art.barBackground);
            BoardUIController.SetImage(barFill, art.barFill);
            BoardUIController.SetImage(deckIcon, art.deckIcon);
            BoardUIController.SetImage(deck, art.deck);
            BoardUIController.SetImage(infoBackground, art.infoBackground);
            BoardUIController.SetImage(avatar, art.avatar);
            BoardUIController.SetImage(avatarBackground, art.avatarBackground);
            BoardUIController.SetImage(infoIcon, art.infoIcon);

            if(score) score.color = art.textColor;
            if(deckCount) deckCount.color = art.textColor;
            if(avatarAnimator) avatarAnimator.runtimeAnimatorController = art.avatarAnimator;
            if(barFill){
                barFill.type = Image.Type.Filled;
                barFill.fillMethod = Image.FillMethod.Vertical;
                barFill.fillOrigin = (int)Image.OriginVertical.Bottom;
            }
        }

        public void Render(SideState state, int winScore, SideArt art){
            CardInfoView.Put(score, state.score.ToString());
            CardInfoView.Put(deckCount, state.deckCount.ToString());

            if(score) score.color = art.textColor;
            if(deckCount) deckCount.color = art.textColor;
            if(barFill) barFill.fillAmount = Mathf.Clamp01((float)state.score / Mathf.Max(1, winScore));

            int index = Mathf.Clamp(state.energy, 0, 8);
            BoardUIController.SetImage(energy, art.energy != null && index < art.energy.Length ? art.energy[index] : null);
        }

        public void Countdown(bool active, string parameter){
            if(!avatarAnimator || !avatarAnimator.runtimeAnimatorController || string.IsNullOrEmpty(parameter)) return;
            foreach(var p in avatarAnimator.parameters){
                if(p.name == parameter && p.type == AnimatorControllerParameterType.Bool){
                    avatarAnimator.SetBool(parameter, active); break;
                }
            }
        }
    }
}
