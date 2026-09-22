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
        public TMP_Text nameText, phaseText;
        [Range(0f, 1f)] public float inactivePhaseOpacity = 0.4f;
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
            RenderName(art);
            BoardUIController.SetImage(skill, art.skill);
            BoardUIController.SetImage(barBackground, art.barBackground);
            BoardUIController.SetImage(barFill, art.barFill);
            BoardUIController.SetImage(deckIcon, art.deckIcon);
            BoardUIController.SetImage(deck, art.deck);
            BoardUIController.SetImage(infoBackground, art.infoBackground);
            BoardUIController.SetImage(avatar, art.avatar);
            BoardUIController.SetImage(avatarBackground, art.avatarBackground);
            BoardUIController.SetImage(infoIcon, art.infoIcon);

            if(avatarAnimator) avatarAnimator.runtimeAnimatorController = art.avatarAnimator;
            if(barFill){
                barFill.type = Image.Type.Filled;
                barFill.fillMethod = Image.FillMethod.Vertical;
                barFill.fillOrigin = (int)Image.OriginVertical.Bottom;
            }
        }

        public void Render(SideState state, int winScore, SideArt art){
            RenderName(art);
            CardInfoView.Put(score, state.score.ToString());
            CardInfoView.Put(deckCount, state.deckCount.ToString());

            if(barFill) barFill.fillAmount = Mathf.Clamp01((float)state.score / Mathf.Max(1, winScore));

            int index = Mathf.Clamp(state.energy, 0, 8);
            BoardUIController.SetImage(energy, art.energy != null && index < art.energy.Length ? art.energy[index] : null);
        }

        private void RenderName(SideArt art){
            if(!nameText) return;
            nameText.richText = false;
            CardInfoView.Put(nameText, art.name);
            nameText.color = art.textColor;
        }

        public void RenderPhase(RoundPhase phase, bool isCurrentTurn){
            if(!phaseText) return;
            string label;
            switch(phase){
                case RoundPhase.Preparation: label = "Preparation"; break;
                case RoundPhase.Performance: label = "Performance"; break;
                case RoundPhase.EndRound: label = "End of Round"; break;
                case RoundPhase.Finished: label = "Finished"; break;
                default: label = "Mulligan"; break;
            }
            CardInfoView.Put(phaseText, label);
            phaseText.color = new Color(1f, 1f, 1f,
                isCurrentTurn ? 1f : Mathf.Clamp01(inactivePhaseOpacity));
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
