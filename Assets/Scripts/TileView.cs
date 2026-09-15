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
        private BoardUIController board;
        private Sprite normalSprite;
        private int column, row;
        private bool hovered;
        private Sprite normalSoloPopup;
        private string normalSoloInfluence;
        private bool enemyOnly;

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

            if(contested){
                BindCard(playerCard, p, true, null); BindCard(enemyCard, e, false, null);
                BindCard(playerAssist, pa, false, theme.player.assistance);
                BindCard(enemyAssist, ea, false, theme.enemy.assistance);
                BoardUIController.SetImage(duoPopup, pt > et ? theme.player.performerPopup : et > pt ? theme.enemy.performerPopup : theme.tiedPopup);
                CardInfoView.Put(duoInfluence, Mathf.Abs(pt - et).ToString());
            }
            else{
                BindCard(soloCard, p ?? e, p != null, null);
                BoardUIController.SetImage(soloPopup, p != null ? theme.player.performerPopup : theme.enemy.performerPopup);
                CardInfoView.Put(soloInfluence, (p != null ? pt : et).ToString());
            }
            RefreshHover();
        }

        private void BindCard(CardPointer view, CardState card, bool movable, Sprite fallback){
            if(!view) return;
            if(card == null){ view.gameObject.SetActive(false); return; }
            Sprite sprite = card.data && card.data.characterImage ? card.data.characterImage : fallback;
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
            if(e.button == PointerEventData.InputButton.Left) board.Drop(column, row);
        }
    }
}
