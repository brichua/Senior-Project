using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace VocaloidTCG.BoardUI
{
    public sealed class CardDrawAnimator : MonoBehaviour
    {
        public RectTransform playerDeck, enemyDeck;
        [Min(0.01f)] public float deckExitSeconds = 0.3f;
        [Min(0.01f)] public float handEnterSeconds = 0.4f;
        [Min(0)] public float betweenCardsSeconds = 0.08f;
        [Min(0)] public float belowScreenPadding = 40f;

        private BoardUIController board;
        private GameplayBoardBridge bridge;
        private BoardSnapshot snapshot;
        private readonly Queue<GameplayBoardBridge.DrawPresentation>[] draws = {
            new Queue<GameplayBoardBridge.DrawPresentation>(),
            new Queue<GameplayBoardBridge.DrawPresentation>()
        };
        private readonly Dictionary<string, int> pending = new Dictionary<string, int>();
        private readonly Coroutine[] sequences = new Coroutine[2];
        private readonly CardPointer[] flyingCards = new CardPointer[2];

        public bool IsPlaying { get { return pending.Count > 0 || sequences[0] != null || sequences[1] != null; } }

        public void Initialize(BoardUIController owner){
            board = owner;
            bridge = board.game as GameplayBoardBridge;
        }

        public void CollectDraws(){
            if(!bridge || !isActiveAndEnabled) return;
            if(snapshot != board.State){
                Cancel();
                snapshot = board.State;
            }
            GameplayBoardBridge.DrawPresentation draw;
            while(bridge.TryTakeDrawPresentation(out draw)){
                if(draw.card == null) continue;
                draws[draw.card.ownerId].Enqueue(draw);
                pending[draw.card.instanceId] = draw.card.ownerId;
            }
            bridge.SetDrawAnimationPlaying(IsPlaying);
        }

        public void PresentDraws(){
            if(!isActiveAndEnabled) return;
            foreach(var id in pending.Keys) SetVisible(id, false);
            for(int actor = 0; actor < 2; actor++)
                if(draws[actor].Count > 0 && sequences[actor] == null)
                    sequences[actor] = StartCoroutine(PlayDraws(actor));
        }

        private void SetVisible(string id, bool visible){
            var view = board ? board.GetHandView(id) : null;
            if(!view) return;
            var group = view.GetComponent<CanvasGroup>();
            if(!group) return;
            group.alpha = visible ? 1 : 0;
            group.interactable = visible;
            group.blocksRaycasts = visible;
        }

        private IEnumerator PlayDraws(int actor){
            yield return null;
            try{
                while(draws[actor].Count > 0){
                    var draw = draws[actor].Dequeue();
                    if (board.sfx) board.sfx.PlayCardDrawSound();
                    yield return AnimateCard(draw);
                    pending.Remove(draw.card.instanceId);
                    SetVisible(draw.card.instanceId, true);
                    float elapsed = 0;
                    while(elapsed < betweenCardsSeconds){
                        if(!bridge.IsPaused) elapsed += Time.unscaledDeltaTime;
                        yield return null;
                    }
                }
            }
            finally{
                DestroyFlyingCard(actor);
                foreach(var id in new List<string>(pending.Keys)){
                    if(pending[id] != actor) continue;
                    SetVisible(id, true);
                    pending.Remove(id);
                }
                draws[actor].Clear();
                sequences[actor] = null;
                if(bridge) bridge.SetDrawAnimationPlaying(IsPlaying);
            }
        }

        private IEnumerator AnimateCard(GameplayBoardBridge.DrawPresentation draw){
            int actor = draw.card.ownerId;
            bool local = draw.card.ownerId == board.State.localPlayerId;
            var art = local ? board.setup.player : board.setup.enemy;
            var deck = local ? playerDeck : enemyDeck;
            var hud = local ? board.playerHUD : board.enemyHUD;
            if(!deck && hud && hud.deck) deck = hud.deck.rectTransform;
            if(!deck){
                Debug.LogWarning("Card draw animation needs a deck RectTransform for each side.", this);
                yield break;
            }

            Canvas.ForceUpdateCanvases();
            var flyingCard = Instantiate(board.handPrefab, board.dragLayer);
            flyingCards[actor] = flyingCard;
            flyingCard.name = "Drawing card";
            flyingCard.Bind(board, null, art.cardBack, false, false);
            flyingCard.MakeDragPreview();
            var layout = flyingCard.GetComponent<LayoutElement>();
            if(!layout) layout = flyingCard.gameObject.AddComponent<LayoutElement>();
            layout.ignoreLayout = true;
            var rect = flyingCard.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one * 0.5f;
            rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.identity;
            SizeLike(rect, deck);
            Vector3 start = CenterInLayer(deck);
            Vector3 belowDeck = BelowScreen(start.x, rect);
            yield return Slide(rect, start, belowDeck, deckExitSeconds, null);
            DestroyFlyingCard(actor);

            if(draw.discarded) yield break;
            var target = board.GetHandView(draw.card.instanceId);
            if(!target) yield break;

            flyingCard = Instantiate(board.handPrefab, board.dragLayer);
            flyingCards[actor] = flyingCard;
            flyingCard.name = "Drawn hand card";
            flyingCard.Bind(board, local ? draw.card : null,
                local && draw.card.data ? draw.card.data.cardImage : art.cardBack, false, false);
            flyingCard.MakeDragPreview();
            layout = flyingCard.GetComponent<LayoutElement>();
            if(!layout) layout = flyingCard.gameObject.AddComponent<LayoutElement>();
            layout.ignoreLayout = true;
            rect = flyingCard.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one * 0.5f;
            rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.identity;
            Canvas.ForceUpdateCanvases();
            var targetRect = target.GetComponent<RectTransform>();
            SizeLike(rect, targetRect);
            Vector3 destination = CenterInLayer(targetRect);
            yield return Slide(rect, BelowScreen(destination.x, rect), destination,
                handEnterSeconds, draw.card.instanceId);
            DestroyFlyingCard(actor);
        }

        private IEnumerator Slide(RectTransform rect, Vector3 from, Vector3 to,
            float seconds, string targetId){
            float elapsed = 0;
            rect.localPosition = from;
            while(elapsed < Mathf.Max(0.01f, seconds)){
                if(!bridge.IsPaused) elapsed += Time.unscaledDeltaTime;
                if(targetId != null){
                    var target = board.GetHandView(targetId);
                    if(!target) yield break;
                    var targetRect = target.GetComponent<RectTransform>();
                    to = CenterInLayer(targetRect);
                    SizeLike(rect, targetRect);
                }
                float t = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, seconds));
                rect.localPosition = Vector3.Lerp(from, to, t * t * (3f - 2f * t));
                yield return null;
            }
            rect.localPosition = to;
        }

        private Vector3 CenterInLayer(RectTransform rect){
            return board.dragLayer.InverseTransformPoint(rect.TransformPoint(rect.rect.center));
        }

        private void SizeLike(RectTransform rect, RectTransform target){
            var corners = new Vector3[4];
            target.GetWorldCorners(corners);
            Vector3 bottomLeft = board.dragLayer.InverseTransformPoint(corners[0]);
            Vector3 topRight = board.dragLayer.InverseTransformPoint(corners[2]);
            rect.sizeDelta = new Vector2(Mathf.Abs(topRight.x - bottomLeft.x),
                Mathf.Abs(topRight.y - bottomLeft.y));
        }

        private Vector3 BelowScreen(float x, RectTransform rect){
            var canvas = board.dragLayer.GetComponentInParent<Canvas>().rootCanvas;
            Camera camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            Vector2 bottom;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(board.dragLayer,
                new Vector2(Screen.width * 0.5f, 0), camera, out bottom);
            return new Vector3(x, bottom.y - rect.rect.height - belowScreenPadding, 0);
        }

        private void DestroyFlyingCard(int actor){
            var flyingCard = flyingCards[actor];
            if(!flyingCard) return;
            flyingCard.gameObject.SetActive(false);
            Destroy(flyingCard.gameObject);
            flyingCards[actor] = null;
        }

        public void Cancel(){
            StopAllCoroutines();
            for(int actor = 0; actor < 2; actor++){
                sequences[actor] = null;
                DestroyFlyingCard(actor);
                draws[actor].Clear();
            }
            foreach(var id in pending.Keys) SetVisible(id, true);
            pending.Clear();
            if(bridge) bridge.SetDrawAnimationPlaying(false);
        }

        private void OnDisable(){ Cancel(); }
    }
}
