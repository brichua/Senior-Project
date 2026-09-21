using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using System.Collections;

namespace VocaloidTCG.BoardUI
{
    public sealed class BoardUIController : MonoBehaviour
    {
        public BoardSetup setup;
        public BoardGameBridge game;
        public Image background, topBar, cd;
        public Button endTurn, pause;
        public SideHUD playerHUD, enemyHUD;
        public CardInfoView playerInfo, enemyInfo;
        public PausePanel pausePanel;
        [SerializeField, HideInInspector] private TMP_Text phaseText;
        public bool subtitleTimerOnlyDuringCountdown;
        public TileView tilePrefab;
        public Transform gridRoot;
        public CardPointer handPrefab;
        public Transform playerHandRoot, enemyHandRoot;
        public RectTransform dragLayer;
        public bool mirrorColumnsForSide1 = true;
        public Vector2 ghostSize = new Vector2(120, 160);
        public SFXManager sfx;
        public CardDrawAnimator drawAnimator;

        public BoardSnapshot State { get { return game ? game.Snapshot : null; } }
        private readonly List<TileView> tiles = new List<TileView>();
        private readonly List<CardPointer> hands = new List<CardPointer>();
        private readonly Dictionary<string, CardPointer> handViews = new Dictionary<string, CardPointer>();
        public CardPointer GetHandView(string instanceId){
            CardPointer view;
            return handViews.TryGetValue(instanceId, out view) ? view : null;
        }
        private CardPointer dragSource;
        private CardState dragged;
        private BoardActionKind dragKind;
        private int sourceColumn, sourceRow;
        private CardPointer ghost;
        private RectTransform ghostRect;
        private string selectedPlayer, selectedEnemy;
        private bool ready;
        public bool CanHoverHand => isActiveAndEnabled && dragged == null &&
            (!drawAnimator || !drawAnimator.IsPlaying) &&
            (!pausePanel || !pausePanel.IsOpen);

        public void ClearHandHover(){
            foreach(var hand in hands) if(hand) hand.ResetHandHover();
        }

        private void Start()
        {
            if (!setup || !game || !tilePrefab || !gridRoot || !handPrefab || !playerHandRoot || !enemyHandRoot || !dragLayer)
            {
                Debug.LogError("BoardUI: assign setup, bridge, prefabs, grid/hand roots and drag layer.", this);
                enabled = false; return;
            }
            SetImage(background, setup.background); SetImage(topBar, setup.topBar);

            if (endTurn)
            {
                SetImage(endTurn.image, setup.endTurn); endTurn.onClick.AddListener(Pass);
            }
            if (pause)
            {
                SetImage(pause.image, setup.pause); pause.onClick.AddListener(OpenPause);
            }
            if (playerHUD) playerHUD.Apply(setup.player);
            if (enemyHUD) enemyHUD.Apply(setup.enemy);

            for (int i = 0; i < 25; i++) tiles.Add(Instantiate(tilePrefab, gridRoot));
            ghost = Instantiate(handPrefab, dragLayer);
            ghost.name = "Drag card";
            ghostRect = ghost.GetComponent<RectTransform>();
            ghostRect.anchorMin = ghostRect.anchorMax = ghostRect.pivot = new Vector2(0.5f, 0.5f);
            float ghostScale = Mathf.Min(ghostSize.x / Mathf.Max(1, ghostRect.rect.width),
                ghostSize.y / Mathf.Max(1, ghostRect.rect.height));
            ghostRect.localScale = Vector3.one * ghostScale;
            var ghostLayout = ghost.GetComponent<LayoutElement>();
            if (ghostLayout) ghostLayout.ignoreLayout = true;
            ghost.gameObject.SetActive(false);

            if(pausePanel) pausePanel.Initialize(this, game);
            if(!drawAnimator) drawAnimator = GetComponent<CardDrawAnimator>();
            if(!drawAnimator) drawAnimator = gameObject.AddComponent<CardDrawAnimator>();
            drawAnimator.Initialize(this);
            ready = true;
            game.Changed += Refresh;
            Refresh();
        }

        private void OnEnable()
        {
            if (ready)
            {
                game.Changed += Refresh;
                Refresh();
            }
        }
        private void OnDisable(){
            if(drawAnimator) drawAnimator.Cancel();
            ClearHandHover();
            if(game) game.Changed -= Refresh;
            CancelDrag();
            if (pausePanel && pausePanel.IsOpen) pausePanel.Close();
            if (playerHUD && setup) playerHUD.Countdown(false, setup.countdownParameter);
            if (enemyHUD && setup) enemyHUD.Countdown(false, setup.countdownParameter);
            if (playerHUD) playerHUD.ClearSubtitle();
            if (enemyHUD) enemyHUD.ClearSubtitle();
        }
        private void OnDestroy()
        {
            if (endTurn) endTurn.onClick.RemoveListener(Pass);
            if (pause) pause.onClick.RemoveListener(OpenPause);
            foreach (var t in tiles) if (t) Destroy(t.gameObject);
            foreach (var h in hands) if (h) Destroy(h.gameObject);
            if (ghost) Destroy(ghost.gameObject);
        }

        private bool CanInteract()
        {
            var s = State;
            return isActiveAndEnabled && s != null && s.inputAllowed && s.activePlayerId == s.localPlayerId &&
                (!drawAnimator || !drawAnimator.IsPlaying) &&
                (s.phase == RoundPhase.Preparation || s.phase == RoundPhase.Performance) &&
                (!pausePanel || !pausePanel.IsOpen);
        }

        public void Refresh(){
            if(!ready || State == null) return;
            if(drawAnimator) drawAnimator.CollectDraws();
            ClearHandHover();
            CancelDrag();
            var s = State;
            if (playerHUD) playerHUD.Render(s.Side(s.localPlayerId), s.winScore, setup.player);
            if (enemyHUD) enemyHUD.Render(s.Side(1 - s.localPlayerId), s.winScore, setup.enemy);

            for (int visualRow = 0; visualRow < 5; visualRow++)
                for (int visualColumn = 0; visualColumn < 5; visualColumn++)
                {
                    int x = mirrorColumnsForSide1 && s.localPlayerId == 1 ? 4 - visualColumn : visualColumn;
                    int index = visualRow * 5 + x;
                    tiles[visualRow * 5 + visualColumn].Bind(this, x, visualRow,
                        s.tiles != null && index < s.tiles.Length ? s.tiles[index] : null);
                }
            foreach (var h in hands)
            {
                h.gameObject.SetActive(false); Destroy(h.gameObject);
            }
            hands.Clear();
            handViews.Clear();
            var localHand = s.Side(s.localPlayerId).hand;
            if (localHand != null) foreach (var card in localHand)
            {
                if (card == null || !card.data) continue;
                var h = Instantiate(handPrefab, playerHandRoot);
                h.Bind(this, card, card.data.cardImage, true, true); hands.Add(h);
                handViews[card.instanceId] = h;
            }
            for (int i = 0; i < s.Side(1 - s.localPlayerId).hiddenHandCount; i++)
            {
                var h = Instantiate(handPrefab, enemyHandRoot);
                h.Bind(this, null, setup.enemy.cardBack, true, false); hands.Add(h);
                var enemyHand = s.Side(1 - s.localPlayerId).hand;
                if(enemyHand != null && i < enemyHand.Count && enemyHand[i] != null)
                    handViews[enemyHand[i].instanceId] = h;
            }
            if(drawAnimator) drawAnimator.PresentDraws();
            RenderSelection(playerInfo, selectedPlayer, setup.player);
            RenderSelection(enemyInfo, selectedEnemy, setup.enemy);
            UpdateTurnDisplay();
        }

        private void Update()
        {
            if (!ready) return;
            if (State != null) UpdateTurnDisplay();
            else if (enemyHUD) enemyHUD.SetTimerSubtitle("");
        }
        private void UpdateTurnDisplay()
        {
            var s = State;
            bool localTurn = s.activePlayerId == s.localPlayerId;
            bool playing = s.phase == RoundPhase.Preparation || s.phase == RoundPhase.Performance;
            if (endTurn)
            {
                endTurn.gameObject.SetActive(localTurn && playing);
                endTurn.interactable = CanInteract();
            }
            SetImage(cd, localTurn ? setup.player.cd : setup.enemy.cd);
            bool showTimer = playing && game.RemainingSeconds > 0 &&
                (!subtitleTimerOnlyDuringCountdown || game.RemainingSeconds <= 10);
            if (enemyHUD) enemyHUD.SetTimerSubtitle(showTimer ?
                "Time left: " + Mathf.CeilToInt(game.RemainingSeconds) + "s" : "");
            if(playerHUD) playerHUD.RenderPhase(s.phase, playing && localTurn);
            if(enemyHUD) enemyHUD.RenderPhase(s.phase, playing && s.activePlayerId == 1 - s.localPlayerId);
            if(phaseText && (!playerHUD || phaseText != playerHUD.phaseText) &&
                (!enemyHUD || phaseText != enemyHUD.phaseText))
                CardInfoView.Put(phaseText, "");
            bool countdown = playing && game.RemainingSeconds > 0 && game.RemainingSeconds <= 10 &&
                !(pausePanel && pausePanel.IsOpen && !s.multiplayer);
            if (playerHUD) playerHUD.Countdown(countdown && !localTurn, setup.countdownParameter);
            if (enemyHUD) enemyHUD.Countdown(countdown && localTurn, setup.countdownParameter);
            if (dragged != null && !CanInteract()) CancelDrag();
        }

        private void Pass()
        {
            if (CanInteract())
            {
                CancelDrag(); game.RequestPass();
            }
        }

        private void OpenPause(){
            ClearHandHover();
            CancelDrag(); if(pausePanel) pausePanel.Open();
        }

        public void Select(CardState card)
        {
            if (card == null || State == null || (pausePanel && pausePanel.IsOpen)) return;
            bool local = card.ownerId == State.localPlayerId;
            if (local) selectedPlayer = card.instanceId; else selectedEnemy = card.instanceId;
            var view = local ? playerInfo : enemyInfo;
            if (view) view.Show(card, local ? setup.player : setup.enemy, setup);
        }

        private void RenderSelection(CardInfoView view, string id, SideArt side)
        {
            if (view) view.Show(FindCard(id), side, setup);
        }

        private CardState FindCard(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            var hand = State.Side(State.localPlayerId).hand;
            if (hand != null) foreach (var c in hand) if (c != null && c.instanceId == id) return c;
            if (State.tiles != null) foreach (var t in State.tiles)
            {
                if (t == null) continue;
                foreach (var c in new[] { t.side0, t.side1, t.assist0, t.assist1 })
                    if (c != null && c.instanceId == id) return c;
            }
            return null;
        }

        public bool BeginDrag(CardPointer source, CardState card, bool hand, int x, int y, PointerEventData e){
            if(!CanInteract() || card == null || !card.data || card.ownerId != State.localPlayerId) return false;
            if(!hand && !HasLegalMove(card, x, y)) return false;
            ClearHandHover();
            CancelDrag(); dragSource = source; dragged = card;
            dragKind = hand ? BoardActionKind.PlayCard : BoardActionKind.MovePerformer;
            sourceColumn = x; sourceRow = y;
            ghost.Bind(this, card, card.data.cardImage ? card.data.cardImage : card.data.characterImage, false, false);
            ghost.MakeDragPreview(); MoveGhost(e);
            foreach (var t in tiles) t.RefreshHover();
            return true;
        }

        private bool HasLegalMove(CardState card, int fromX, int fromY)
        {
            for (int y = 0; y < 5; y++) for (int x = 0; x < 5; x++)
            {
                if (x == fromX && y == fromY) continue;
                string reason;
                if (game.CanSubmit(new BoardAction(BoardActionKind.MovePerformer, card.instanceId,
                    fromX, fromY, x, y), out reason)) return true;
            }
            return false;
        }
        public bool IsMovingFrom(int x, int y)
        {
            return dragged != null && dragKind == BoardActionKind.MovePerformer &&
                sourceColumn == x && sourceRow == y;
        }
        public bool TryGetContestPreview(int x, int y, out int localInfluence, out int enemyInfluence)
        {
            localInfluence = enemyInfluence = 0;
            if (!CanDrop(x, y) || !dragged.data || !dragged.data.performer) return false;
            return game.TryPreviewContest(ActionAt(x, y), dragged, out localInfluence, out enemyInfluence);
        }
        public void MoveGhost(PointerEventData e)
        {
            if (!ghost || !dragLayer) return;
            var canvas = dragLayer.GetComponentInParent<Canvas>();
            Camera camera = canvas && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            Vector2 point;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(dragLayer, e.position, camera, out point))
                ghostRect.localPosition = new Vector3(point.x, point.y, 0);
        }
        private BoardAction ActionAt(int x, int y)
        {
            return new BoardAction(dragKind, dragged.instanceId, sourceColumn, sourceRow, x, y);
        }

        public bool CanDrop(int x, int y)
        {
            string reason;
            return dragged != null && CanInteract() && x >= 0 && x < 5 && y >= 0 && y < 5 &&
                game.CanSubmit(ActionAt(x, y), out reason);
        }
        public void Drop(int x, int y)
        {
            if (!CanDrop(x, y))
            {
                CancelDrag(); return;
            }

            StartCoroutine(DropCoroutine(x, y));
        }

        // wait for ticket tear sound to complete before placing card
        private IEnumerator DropCoroutine(int x, int y)
        {
            sfx.PlayTicketTearSound();
            var action = ActionAt(x, y);
            if (dragSource)
                dragSource.gameObject.SetActive(false);

            yield return new WaitForSeconds(sfx.GetTicketTearSoundLength()+0.50f);
            
            sfx.PlayDropSound();
            CancelDrag();
            if (game.TrySubmit(action)) Refresh();

        }

        public void CancelDrag()
        {
            var source = dragSource;
            dragSource = null; dragged = null;
            if (source) source.Restore();
            if (ghost) ghost.gameObject.SetActive(false);
            if (ready && State != null) foreach (var t in tiles) t.RefreshHover();
        }

        public static void SetImage(Image image, Sprite sprite)
        {
            if (image)
            {
                image.sprite = sprite; image.enabled = sprite != null;
            }
        }
    }
}
