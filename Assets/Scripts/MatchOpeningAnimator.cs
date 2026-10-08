using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace VocaloidTCG.BoardUI
{
    public sealed class MatchOpeningAnimator : MonoBehaviour
    {
        [Min(0.1f)] public float introductionSeconds = 2.2f, spotlightSeconds = 3.2f, winnerSeconds = 1.3f;
        [Min(0.1f)] public float cardTravelSeconds = 0.4f;
        [Tooltip("Optional presentation font. Use a serif TMP font for the reference artwork's lettering.")]
        public TMP_FontAsset font;
        [Header("Screen effects")]
        public ConcertEffectSettings openingEffects = new ConcertEffectSettings();
        public ConcertEffectSettings redrawEffects = new ConcertEffectSettings { particleCount = 36, intensity = 0.8f };
        private ConcertEffects openingParticles, redrawParticles;
        private ConcertEffects redrawButtonEffects;
        private static readonly Color RedrawAccent = new Color(207f / 255f, 211f / 255f, 1f);
        private BoardUIController board;
        private GameplayBoardBridge bridge;
        private BoardSnapshot snapshot;
        private RectTransform root, introduction, redraw;
        private CanvasGroup group;
        private TMP_Text instruction, submitLabel;
        private Button submit;
        private bool selecting, submitted;
        private readonly List<OpeningHandCard>[] cards = { new List<OpeningHandCard>(), new List<OpeningHandCard>() };
        private Image spotlight;
        private ConcertBackdrop backdrop;
        private readonly RectTransform[] portraits = new RectTransform[2];
        private readonly Color[] colors = new Color[2];
        private Texture2D spotlightTexture;
        private Sprite spotlightSprite;
        private readonly Vector2[] portraitHomes = new Vector2[2];
        private readonly Vector3[] portraitScales = new Vector3[2];
        public RectTransform RedrawZone { get; private set; }
        public bool CanSelect => selecting && !submitted && bridge && bridge.OpeningPending && !bridge.IsPaused;

        public void Initialize(BoardUIController owner){
            board = owner; bridge = board.game as GameplayBoardBridge;
        }

        public void Observe(BoardSnapshot state){
            if(!isActiveAndEnabled || state == null || !bridge || !bridge.OpeningPending){ Cancel(); snapshot = state; return; }
            if(snapshot == state && root) return;
            Cancel(); snapshot = state;
            Build();
            for(int i = 0; i < 2; i++) { portraitHomes[i] = portraits[i].anchoredPosition; portraitScales[i] = portraits[i].localScale; }
            StartCoroutine(Sequence());
        }

        private IEnumerator Sequence(){
            yield return Animate(introductionSeconds, t => {
                for(int side = 0; side < 2; side++){
                    float sign = side == 0 ? -1 : 1;
                    portraits[side].anchoredPosition = portraitHomes[side] + Vector2.right * (sign * 260 * Mathf.Pow(1 - Mathf.Clamp01(t * 2), 3));
                    portraits[side].localScale = portraitScales[side] * Mathf.Lerp(0.88f, 1, Mathf.Clamp01(t * 2));
                }
                AimSpotlight(Mathf.Sin(t * Mathf.PI * 2) * 460);
            });
            
            int starter = snapshot.roundStarterId == snapshot.localPlayerId ? 0 : 1;
            float targetX = starter == 0 ? -460 : 460;
            yield return Animate(spotlightSeconds, t => {
                float sway = Mathf.Sin(t * Mathf.PI * 4) * 560;
                AimSpotlight(Mathf.Lerp(sway, targetX, Smooth(Mathf.Clamp01((t - 0.65f) / 0.35f))));
            });
            
            backdrop.Speed = Mathf.Max(1, board.setup.selectedBackgroundSpeed);
            openingParticles.EmitBurst(portraitHomes[starter], colors[starter]);
            if(board.sfx) board.sfx.PlayClickSound();
            
            yield return Animate(winnerSeconds, t => {
                AimSpotlight(targetX);
                float pop = 1 + 0.16f * Smooth(Mathf.Clamp01(t * 4)) + Mathf.Sin(Mathf.Clamp01(t * 3) * Mathf.PI) * 0.07f;
                portraits[starter].localScale = portraitScales[starter] * pop;
            });
            
            introduction.gameObject.SetActive(false);
            redraw.gameObject.SetActive(true);
            backdrop.ShowRedraw();
            for(int i = 0; i < Math.Max(cards[0].Count, cards[1].Count); i++){
                int slot = i;
                yield return Animate(cardTravelSeconds, t => {
                    for(int side = 0; side < 2; side++) if(slot < cards[side].Count){
                        var card = cards[side][slot]; card.gameObject.SetActive(true);
                        card.Rect.anchoredPosition = Vector2.Lerp(DeckPosition(side), HandPosition(side, slot, false), Smooth(t));
                        card.Rect.localScale = Vector3.one * Mathf.Lerp(0.5f, 1, Smooth(t));
                    }
                });
                if(board.sfx) board.sfx.PlayCardDrawSound();
            }
            
            selecting = true; submit.gameObject.SetActive(true); RefreshSelection();
            while(!submitted) yield return null;
            selecting = false; submit.gameObject.SetActive(false);
            var playerIds = cards[0].Where(c => c.selected).Select(c => bridge.OpeningCards(snapshot.localPlayerId)[c.index].instanceId).ToList();
            if(bridge.IsOnline){
                if(!bridge.SubmitOpeningRedraw(playerIds, Array.Empty<string>())){
                    instruction.text = "Unable to confirm this hand."; yield break;
                }
                instruction.text = "Waiting for your opponent to confirm…";
                while(bridge.OpeningPending && !bridge.OnlineRedrawReady) yield return null;
                if(!bridge.OpeningPending) yield break;
                instruction.text = "Both players confirmed!";
            }
            var enemyIds = bridge.ChooseOpponentRedraw();
            foreach(var card in cards[1]){
                var state = bridge.OpeningCards(1 - snapshot.localPlayerId)[card.index];
                if(bridge.IsOnline ? !bridge.OnlineOpponentRedrawSlots.Contains(card.index) : !enemyIds.Contains(state.instanceId)) continue;
                card.selected = true;
                redrawParticles.EmitBurst(HandPosition(1, card.index, true), colors[1]);
                Vector2 from = card.Rect.anchoredPosition;
                yield return Animate(0.25f, t => card.Rect.anchoredPosition = Vector2.Lerp(from, HandPosition(1, card.index, true), Smooth(t)));
            }
            
            var selectedCards = cards.SelectMany(c => c).Where(c => c.selected).ToList();
            var positions = selectedCards.Select(c => c.Rect.anchoredPosition).ToArray();
            yield return Animate(cardTravelSeconds, t => {
                for(int i = 0; i < selectedCards.Count; i++){
                    var card = selectedCards[i];
                    card.Rect.anchoredPosition = Vector2.Lerp(positions[i], DeckPosition(card.opponent ? 1 : 0), Smooth(t));
                    card.Rect.localScale = Vector3.one * (1 - t);
                }
            });
            
            if(!bridge.IsOnline && !bridge.SubmitOpeningRedraw(playerIds, enemyIds)){
                instruction.text = "Unable to redraw this hand. Restart the match to try again.";
                yield break;
            }
            
            foreach(var card in selectedCards) BindCard(card);
            yield return Animate(cardTravelSeconds, t => {
                foreach(var card in selectedCards){
                    int side = card.opponent ? 1 : 0;
                    card.Rect.anchoredPosition = Vector2.Lerp(DeckPosition(side), HandPosition(side, card.index, false), Smooth(t));
                    card.Rect.localScale = Vector3.one * t;
                }
            });
            
            yield return Animate(0.8f, t => {});
            var allCards = cards.SelectMany(c => c).ToList();
            var starts = allCards.Select(c => c.Rect.anchoredPosition).ToArray();
            yield return Animate(0.65f, t => {
                for(int i = 0; i < allCards.Count; i++){
                    var card = allCards[i];
                    card.Rect.anchoredPosition = Vector2.Lerp(starts[i], BoardDeckPosition(card.opponent ? 1 : 0), Smooth(t));
                    card.Rect.localScale = Vector3.one * Mathf.Lerp(1, 0.22f, t);
                }
            });
            
            foreach(var card in allCards) card.gameObject.SetActive(false);
            yield return Animate(0.45f, t => group.alpha = 1 - Smooth(t));
            bridge.CompleteOpening();
            if(bridge.IsOnline){
                group.alpha = 1;
                instruction.text = "Waiting for your opponent…";
            }
        }

        public void SelectCard(OpeningHandCard card, bool selected){
            if(!CanSelect || card.opponent) return;
            int count = cards[0].Count(c => c.selected && c != card);
            if(selected && count >= snapshot.Side(snapshot.localPlayerId).deckCount) selected = false;
            bool changed = card.selected != selected;
            card.selected = selected;
            card.Rect.anchoredPosition = HandPosition(0, card.index, selected);
            if(changed && redrawParticles) redrawParticles.EmitBurst(card.Rect.anchoredPosition, colors[0]);
            RefreshSelection();
        }

        private void RefreshSelection(){
            int count = cards[0].Count(c => c.selected);
            submitLabel.text = count == 0 ? "KEEP" : "CONFIRM";
            if(redrawButtonEffects) redrawButtonEffects.confirm = count > 0;
        }

        private void BindCard(OpeningHandCard card){
            int actor = card.opponent ? 1 - snapshot.localPlayerId : snapshot.localPlayerId;
            var data = bridge.OpeningCards(actor)[card.index];
            var art = card.opponent ? board.setup.enemy : board.setup.player;
            card.view.Bind(board, card.opponent ? null : data, card.opponent ? art.cardBack : data.data.cardImage, false, false);
            card.view.MakeDragPreview(); card.view.enabled = false;
        }

        private Vector2 HandPosition(int side, int index, bool selected){
            float spacing = Mathf.Min(200, 540f / Mathf.Max(1, cards[side].Count - 1));
            float x = (side == 0 ? -520 : 520) + (index - (cards[side].Count - 1) * 0.5f) * spacing;
            return new Vector2(x, selected ? -220 : 70);
        }

        private static Vector2 DeckPosition(int side) => new Vector2(side == 0 ? -830 : 830, -350);
        
        private Vector2 BoardDeckPosition(int side){
            var hud = side == 0 ? board.playerHUD : board.enemyHUD;
            if(!hud || !hud.deck) return DeckPosition(side);
            var rect = hud.deck.rectTransform;
            var canvas = rect.GetComponentInParent<Canvas>();
            Camera camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            Vector2 result;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(redraw,
                RectTransformUtility.WorldToScreenPoint(camera, rect.TransformPoint(rect.rect.center)), null, out result);
            return result;
        }

        private IEnumerator Animate(float seconds, Action<float> tick){
            float elapsed = 0;
            while(elapsed < Mathf.Max(0.01f, seconds)){
                if(!bridge.IsPaused) elapsed += Time.unscaledDeltaTime;
                tick(Mathf.Clamp01(elapsed / Mathf.Max(0.01f, seconds)));
                yield return null;
            }
        }
        
        private static float Smooth(float t) => t * t * (3 - 2 * t);

        private void AimSpotlight(float x){
            spotlight.rectTransform.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(x, 900) * Mathf.Rad2Deg);
            Color tint = Color.Lerp(colors[0], colors[1], Mathf.InverseLerp(-460, 460, x));
            tint.a = 0.48f; spotlight.color = tint;
        }

        private void Build(){
            var obj = new GameObject("Concert opening", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup));
            obj.transform.SetParent(transform, false); root = (RectTransform)obj.transform;
            var canvas = obj.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 30000;
            var scaler = obj.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = 0.5f;
            group = obj.GetComponent<CanvasGroup>();
            spotlightTexture = new Texture2D(64, 128, TextureFormat.RGBA32, false);
            spotlightTexture.wrapMode = TextureWrapMode.Clamp;
            
            for(int y = 0; y < 128; y++) for(int x = 0; x < 64; x++){
                float height = y / 127f;
                float width = Mathf.Lerp(0.5f, 0.06f, height);
                float distance = Mathf.Abs(x / 63f - 0.5f) / width;
                float alpha = Mathf.SmoothStep(0, 1, 1 - Mathf.Clamp01(distance)) * Mathf.Sin(height * Mathf.PI);
                spotlightTexture.SetPixel(x, y, new Color(1, 1, 1, alpha));
            }
            
            spotlightTexture.Apply();
            spotlightSprite = Sprite.Create(spotlightTexture, new Rect(0, 0, 64, 128), Vector2.one * 0.5f);
            backdrop = ConcertBackdrop.Create(root, board);
            introduction = Rect(root, "Artist introduction", Vector2.zero, new Vector2(1920, 1080));
            redraw = Rect(root, "Opening hand redraw", Vector2.zero, new Vector2(1920, 1080));
            spotlight = Image(introduction, "Swaying spotlight", new Vector2(0, 540), new Vector2(850, 1300), spotlightSprite, Color.clear);
            spotlight.preserveAspect = false; spotlight.rectTransform.pivot = new Vector2(0.5f, 1);
            for(int side = 0; side < 2; side++) BuildSide(side);
            openingParticles = ConcertEffects.Create(introduction, board, colors[0], colors[1], ConcertEffectStyle.Opening, openingEffects);

            var v = Text(introduction, "V", new Vector2(-35, 55), new Vector2(200, 250), 190, colors[0]);
            var s = Text(introduction, "S", new Vector2(38, -25), new Vector2(180, 220), 155, colors[1]);
            v.fontStyle = s.fontStyle = FontStyles.Italic;
            v.enableVertexGradient = s.enableVertexGradient = true;
            v.colorGradient = new VertexGradient(Color.white, colors[0], colors[0], Color.white);
            s.colorGradient = new VertexGradient(colors[1], Color.white, Color.white, colors[1]);
            v.color = s.color = Color.white;
            var slash = Image(introduction, "VS light slash", Vector2.zero, new Vector2(2, 340), null, Color.white);
            slash.rectTransform.localRotation = Quaternion.Euler(0, 0, -32);
            if(board.setup.gameIcon) Image(introduction, "Game icon", new Vector2(0, 415), new Vector2(220, 130), board.setup.gameIcon, Color.white);
            redrawParticles = ConcertEffects.Create(redraw, board, colors[0], colors[1], ConcertEffectStyle.Redraw, redrawEffects);
            Text(redraw, "Redraw", new Vector2(0, 65), new Vector2(320, 105), 64, RedrawAccent);
            instruction = Text(redraw, "", new Vector2(0, -360), new Vector2(1720, 65), 26, Color.white);
            submit = StoryUIElements.Button(redraw, "KEEP", new Vector2(0, -403), SubmitHand);
            submit.GetComponent<RectTransform>().sizeDelta = new Vector2(180, 195);
            submit.image.color = Color.clear;
            redrawButtonEffects = ConcertEffects.Create(submit.transform, board, RedrawAccent, RedrawAccent,
                ConcertEffectStyle.RedrawButton, new ConcertEffectSettings());
            submit.targetGraphic = redrawButtonEffects;
            submitLabel = submit.GetComponentInChildren<TMP_Text>();
            submitLabel.rectTransform.anchorMin = submitLabel.rectTransform.anchorMax = Vector2.one * 0.5f;
            submitLabel.rectTransform.sizeDelta = new Vector2(240, 45);
            submitLabel.rectTransform.anchoredPosition = new Vector2(0, -85);
            submitLabel.fontSize = 25; submitLabel.characterSpacing = 4; submitLabel.color = RedrawAccent;
            submitLabel.transform.SetAsLastSibling();
            if(font) submitLabel.font = font;
            else if(board.phaseAnimator && board.phaseAnimator.font) submitLabel.font = board.phaseAnimator.font;
            submit.gameObject.SetActive(false); redraw.gameObject.SetActive(false);
        }

        private void BuildSide(int side){
            bool enemy = side == 1;
            float x = enemy ? 460 : -460;
            var art = enemy ? board.setup.enemy : board.setup.player;
            var cls = enemy ? board.setup.EnemyClass : board.setup.PlayerClass;
            colors[side] = cls ? cls.color : art.textColor;
            Sprite portrait = cls ? (cls.playerArt ? cls.playerArt : cls.classIcon) : art.avatar;
            portraits[side] = Image(introduction, "Class character", new Vector2(x, 70), new Vector2(750, 760), portrait, portrait ? Color.white : Color.clear).rectTransform;
            Text(introduction, cls ? cls.DisplayName : art.name, new Vector2(x, -320), new Vector2(760, 70), 46, colors[side]);
            
            for(int direction = -1; direction <= 1; direction += 2){
                Image(introduction, "Class ornament line", new Vector2(x + direction * 132, -363), new Vector2(170, 1.5f), null, colors[side]);
                Diamond(introduction, new Vector2(x + direction * 220, -363), 7, colors[side]);
                var jewel = Image(introduction, "Class ornament jewel", new Vector2(x + direction * 196, -363), new Vector2(5, 5), null, colors[side]);
                jewel.rectTransform.localRotation = Quaternion.Euler(0, 0, 45);
            }
            
            float redrawX = enemy ? 520 : -520;
            Text(redraw, enemy ? "ENEMY" : "PLAYER", new Vector2(redrawX, 280), new Vector2(720, 50), 30, colors[side]);
            var classes = new List<CharacterClassData>(enemy ? bridge.enemyClasses : bridge.playerClasses);
            if(classes.Count == 0 && cls) classes.Add(cls);
            classes = classes.Where(c => c).Distinct().ToList();
            for(int i = 0; i < classes.Count; i++){
                var icon = classes[i].gameIcon ? classes[i].gameIcon : classes[i].classIcon;
                Vector2 center = new Vector2(x, -398 - i * Mathf.Min(60, 105f / Mathf.Max(1, classes.Count - 1)));
                var badge = Image(introduction, "Class badge backing", center, new Vector2(64, 64), null, new Color(0.015f, 0.035f, 0.055f, 0.94f));
                badge.rectTransform.localRotation = Quaternion.Euler(0, 0, 45);
                Diamond(introduction, center, 48, classes[i].color);
                Diamond(introduction, center, 41, classes[i].color * new Color(1, 1, 1, 0.5f));
                if(icon) Image(introduction, "Deck class icon", center, new Vector2(42, 42), icon, Color.white);
            }
            
            var zone = Image(redraw, "Redraw area", new Vector2(redrawX, -220), new Vector2(720, 270), board.setup.redrawBoxImage,
                board.setup.redrawBoxImage ? colors[side] : new Color(0.02f, 0.04f, 0.08f, 0.85f));
            zone.preserveAspect = false;
            var outline = zone.gameObject.AddComponent<Outline>(); outline.effectColor = colors[side]; outline.effectDistance = new Vector2(2, -2);
            if(!enemy) RedrawZone = zone.rectTransform;
            SpawnCards(side);
        }

        private void SubmitHand(){ if(CanSelect) submitted = true; }

        private void Diamond(Transform parent, Vector2 center, float radius, Color tint){
            for(int i = 0; i < 4; i++){
                float a = i * Mathf.PI * 0.5f;
                float b = (i + 1) * Mathf.PI * 0.5f;
                Vector2 from = center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius;
                Vector2 to = center + new Vector2(Mathf.Cos(b), Mathf.Sin(b)) * radius;
                var edge = Image(parent, "Diamond edge", (from + to) * 0.5f, new Vector2(Vector2.Distance(from, to), 1.5f), null, tint);
                edge.rectTransform.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(to.y - from.y, to.x - from.x) * Mathf.Rad2Deg);
            }
        }

        private void SpawnCards(int side){
            bool enemy = side == 1;
            Vector2 size = new Vector2(175, 240);
            int actor = enemy ? 1 - snapshot.localPlayerId : snapshot.localPlayerId;
            for(int i = 0; i < bridge.OpeningCards(actor).Count; i++){
                var hit = Image(redraw, "Opening card " + i, Vector2.zero, size, null, Color.clear);
                hit.raycastTarget = !enemy;
                var card = hit.gameObject.AddComponent<OpeningHandCard>(); card.owner = this; card.index = i; card.opponent = enemy;
                card.view = Instantiate(board.handPrefab, hit.transform);
                var rect = (RectTransform)card.view.transform;
                rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one * 0.5f; rect.anchoredPosition = Vector2.zero;
                rect.localScale = Vector3.one * Mathf.Min(size.x / Mathf.Max(1, rect.rect.width), size.y / Mathf.Max(1, rect.rect.height));
                BindCard(card); cards[side].Add(card); card.gameObject.SetActive(false);
            }
        }

        private static RectTransform Rect(Transform parent, string label, Vector2 position, Vector2 size){
            var rect = new GameObject(label, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false); rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one * 0.5f;
            rect.anchoredPosition = position; rect.sizeDelta = size; return rect;
        }
        private static Image Image(Transform parent, string label, Vector2 position, Vector2 size, Sprite sprite, Color color){
            var image = Rect(parent, label, position, size).gameObject.AddComponent<Image>();
            image.sprite = sprite; image.color = color; image.preserveAspect = true; image.raycastTarget = false; return image;
        }
        private TMP_Text Text(Transform parent, string value, Vector2 position, Vector2 size, float fontSize, Color color){
            var text = Rect(parent, value, position, size).gameObject.AddComponent<TextMeshProUGUI>();
            if(font) text.font = font;
            else if(board.phaseAnimator && board.phaseAnimator.font) text.font = board.phaseAnimator.font;
            text.text = value; text.fontSize = fontSize; text.alignment = TextAlignmentOptions.Center; text.color = color; text.raycastTarget = false; return text;
        }

        public void Cancel(){
            StopAllCoroutines(); selecting = submitted = false;
            if(root){ root.gameObject.SetActive(false); Destroy(root.gameObject); }
            root = null; RedrawZone = null; backdrop = null; cards[0].Clear(); cards[1].Clear();
            openingParticles = redrawParticles = null;
            redrawButtonEffects = null;
            if(spotlightSprite) Destroy(spotlightSprite);
            if(spotlightTexture) Destroy(spotlightTexture);
            spotlightSprite = null; spotlightTexture = null;
        }
        private void OnDisable(){ Cancel(); }
    }
}
