using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace VocaloidTCG.BoardUI
{
    public sealed class TileView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IDropHandler
    {
        public Image tile, soloPopup, duoPopup;
        public GameObject solo, duo;
        public CardPointer soloCard, playerCard, enemyCard, playerAssist, enemyAssist;
        public TMP_Text soloInfluence, duoInfluence;   
        [Header("Protected original influence")]
        public Image soloShieldPopup, playerShieldPopup, enemyShieldPopup;
        public TMP_Text soloShieldInfluence, playerShieldInfluence, enemyShieldInfluence;
        private BoardUIController board;
        private Sprite normalSprite;
        private int column, row;
        private bool hovered;
        private Sprite normalSoloPopup;
        private string normalSoloInfluence;
        private bool enemyOnly;
        private CardPointer soloAssist;

        private void Awake(){
            // Use the existing named prefab children, including those initially hidden.
            foreach(var image in GetComponentsInChildren<Image>(true)){
                if(image.name == "shield popup" && !soloShieldPopup) soloShieldPopup = image;
                if(image.name == "shield popup 1" && !playerShieldPopup) playerShieldPopup = image;
                if(image.name == "shield popup 2" && !enemyShieldPopup) enemyShieldPopup = image;
            }
            foreach(var text in GetComponentsInChildren<TMP_Text>(true)){
                if(text.name == "shield influence" && !soloShieldInfluence) soloShieldInfluence = text;
                if(text.name == "shield influence 1" && !playerShieldInfluence) playerShieldInfluence = text;
                if(text.name == "shield influence 2" && !enemyShieldInfluence) enemyShieldInfluence = text;
            }
        }

        private static void ShieldPopup(Image popup, TMP_Text label, CardState card, CardState partner, Sprite fallback){
            bool active = (card != null && card.shieldActive) || (partner != null && partner.shieldActive);
            var shielded = card != null && card.shieldActive ? card : partner;
            if(popup){
                var cls = shielded?.data ? shielded.data.cardClass : null;
                BoardUIController.SetImage(popup, cls && cls.popupImage ? cls.popupImage : fallback);
                popup.gameObject.SetActive(active);
            }
            if(label){
                int original = (card != null && card.shieldActive && card.data ? (card.token ? card.tokenInfluence : card.data.influence) : 0) +
                    (partner != null && partner.shieldActive && partner.data ? (partner.token ? partner.tokenInfluence : partner.data.influence) : 0);
                label.text = active ? original.ToString() : "";
                label.gameObject.SetActive(active);
            }
        }

        public void Bind(BoardUIController owner, int x, int y, TileState state){
            board = owner; column = x; row = y;
            var s = state ?? new TileState();
            var theme = board.setup;
            bool local0 = board.State.localPlayerId == 0;
            CardState p = local0 ? s.side0 : s.side1, e = local0 ? s.side1 : s.side0;
            CardState pa = local0 ? s.assist0 : s.assist1, ea = local0 ? s.assist1 : s.assist0;
            int pt = local0 ? s.total0 : s.total1, et = local0 ? s.total1 : s.total0;
            bool contested = p != null && e != null;
            enemyOnly = p == null && e != null;
            normalSoloPopup = p != null ? theme.player.performerPopup : theme.enemy.performerPopup;
            normalSoloInfluence = (p != null ? pt : et).ToString();
            normalSprite = contested ? theme.contestedTile : p != null ? theme.playerTile : e != null ? theme.enemyTile : theme.defaultTile;
            
            if(solo) solo.SetActive(!contested && (p != null || e != null));
            if(duo) duo.SetActive(contested);
            if(soloAssist) soloAssist.gameObject.SetActive(false);
            ShieldPopup(soloShieldPopup, soloShieldInfluence, contested ? null : p ?? e,
                contested ? null : p != null ? pa : ea, normalSoloPopup);
            ShieldPopup(playerShieldPopup, playerShieldInfluence, contested ? p : null, contested ? pa : null, theme.player.performerPopup);
            ShieldPopup(enemyShieldPopup, enemyShieldInfluence, contested ? e : null, contested ? ea : null, theme.enemy.performerPopup);

            if(contested){
                BindCard(playerCard, p, true, null); BindCard(enemyCard, e, false, null);
                BindCard(playerAssist, pa, false, theme.player.assistance, true);
                BindCard(enemyAssist, ea, false, theme.enemy.assistance, true);
                BoardUIController.SetImage(duoPopup, pt > et ? theme.player.performerPopup : et > pt ? theme.enemy.performerPopup : theme.tiedPopup);
                CardInfoView.Put(duoInfluence, Mathf.Abs(pt - et).ToString());
            }
            else{
                BindCard(soloCard, p ?? e, p != null, null);
                var partner = p != null ? pa : e != null ? ea : null;
                if(partner != null){
                    EnsureSoloAssist();
                    BindCard(soloAssist, partner, false, p != null ? theme.player.assistance : theme.enemy.assistance, true);
                }
                BoardUIController.SetImage(soloPopup, p != null ? theme.player.performerPopup : theme.enemy.performerPopup);
                CardInfoView.Put(soloInfluence, (p != null ? pt : et).ToString());
            }
            RefreshHover();
        }

        private void EnsureSoloAssist(){
            if(soloAssist || !solo) return;
            var template = playerAssist ? playerAssist : enemyAssist;
            if(!template) return;
            // Reuse the small partner slot, centered below the solo performer.
            soloAssist = Instantiate(template, solo.transform, false);
            soloAssist.name = "Solo stage-share partner";
            var rect = (RectTransform)soloAssist.transform;
            rect.anchoredPosition = new Vector2(0, rect.anchoredPosition.y);
        }

        private void BindCard(CardPointer view, CardState card, bool movable, Sprite fallback, bool useIcon = false){
            if(!view) return;
            if(card == null){ view.gameObject.SetActive(false); return; }
            Sprite sprite = card.data ? (useIcon ? card.data.iconImage : card.data.characterImage) : null;
            if(!sprite) sprite = fallback;
            view.Bind(board, card, sprite, false, movable, column, row);
        }

        public void RefreshHover(){
            if(!board) return;

            var theme = board.setup;
            bool legalHover = hovered && board.CanDrop(column, row);
            Sprite tileSprite = board.IsMovingFrom(column, row) ? (theme.movingTile ? theme.movingTile : normalSprite) : legalHover ? theme.hoverTile : normalSprite;
            BoardUIController.SetImage(tile, tileSprite);
            BoardUIController.SetImage(soloPopup, normalSoloPopup);
            CardInfoView.Put(soloInfluence, normalSoloInfluence);
            int localTotal, enemyTotal;

            if(legalHover) {if (board.sfx) board.sfx.PlayTileHoverSound(); }
                
            if(enemyOnly && legalHover && board.TryGetContestPreview(column, row, out localTotal, out enemyTotal))
            {
                BoardUIController.SetImage(soloPopup, localTotal > enemyTotal ? theme.player.performerPopup : enemyTotal > localTotal ? theme.enemy.performerPopup : theme.tiedPopup);
                CardInfoView.Put(soloInfluence, Mathf.Abs(localTotal - enemyTotal).ToString());
            }
        }

        public void OnPointerEnter(PointerEventData e){
            hovered = true; RefreshHover();
        }
        
        public void OnPointerExit(PointerEventData e){
            hovered = false; RefreshHover();
        }
        
        public void OnDrop(PointerEventData e){
            if (e.button == PointerEventData.InputButton.Left)
            {
                board.Drop(column, row);
            }
        }
    }
}
