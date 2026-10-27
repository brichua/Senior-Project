using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System.Collections.Generic;

namespace VocaloidTCG.BoardUI
{
    public sealed class MatchResultAnimator : MonoBehaviour
    {
        public string defaultReturnScene = "Story", puzzleScene = "Puzzles";
        [Min(0.1f)] public float entranceSeconds = 0.9f;
        public bool showRestartOnDefeat = true;
        public TMP_FontAsset font;
        [Tooltip("Optional override for Prefabs/Resources/Match result panel. Edit that prefab to style the result text and buttons.")]
        public ConcertPanelView resultPanelPrefab;
        [Header("First-clear reward")]
        [Tooltip("Defaults to Resources/reward card. Uses its art and card name children and root background Image.")]
        public GameObject rewardCardPrefab;
        public Vector2 rewardCornerOffset = new Vector2(48, 110);
        [Min(0)] public float rewardDelay = 0.65f;
        [Min(0.1f)] public float rewardEntranceSeconds = 0.7f;
        [Min(0)] public float rewardHoldSeconds = 1f;
        [Min(0.1f)] public float rewardExitSeconds = 0.55f;
        [Header("Screen effects")]
        public ConcertEffectSettings resultEffects = new ConcertEffectSettings { particleCount = 85 };
        public Color victoryColor = new Color(207f / 255f, 211f / 255f, 1f);
        public Color defeatColor = new Color(166f / 255f, 178f / 255f, 242f / 255f);
        [Tooltip("Optional one-shot result audio, using the Effects volume setting.")]
        public AudioClip victoryStinger, defeatStinger;
        [Header("Compact puzzle results")]
        [Tooltip("Editable puzzle text and buttons. Defaults to Prefabs/Resources/Puzzle result panel.")]
        public ConcertPanelView puzzleResultPanelPrefab;
        public ConcertEffectSettings puzzleEffects = new ConcertEffectSettings { particleCount = 45, intensity = 0.7f };
        private BoardUIController board;
        private GameplayBoardBridge bridge;
        private BoardSnapshot shown;
        private RectTransform root, portrait, content;
        private CanvasGroup contentGroup;
        private Image puzzleShade;
        private TMP_Text status;
        private Button back, retry, save;
        private bool won, lost;
        private float time;
        private Color accent, classColor;
        private string navigationError;
        private RectTransform reward;
        private CanvasGroup rewardGroup;
        private Vector2 rewardHome;
        private float rewardTime;
        private sealed class HiddenReactionObject {
            public GameObject obj;
            public bool active;
        }
        private readonly List<HiddenReactionObject> hiddenReactionObjects=new List<HiddenReactionObject>();
        private bool awaitingReactions;
        private int reactionStep;
        private float reactionWait;
        public bool IsWaitingForReactions => awaitingReactions;

        private void BeginResultReactions(){
            awaitingReactions=true; reactionStep=0;
            // A disconnect line may have started immediately before Finished was published.
            reactionWait=bridge.DepartureVoicelinePlayed && board.playerHUD ? board.playerHUD.VoicelineRemaining : 0;
            if(reactionWait<=0) AdvanceResultReactions();
        }
        private void AdvanceResultReactions(){
            while(reactionStep<2){
                bool local=reactionStep++==0 ? won : !won;
                var reaction=reactionStep==1 ? VoicelineEvent.Victory : VoicelineEvent.Defeat;
                if(!board.PlayVoiceline(local,reaction)) continue;
                var hud=local ? board.playerHUD : board.enemyHUD;
                var character=local ? board.setup.PlayerClass : board.setup.EnemyClass;
                var clip=board.voicelines.GetVoiceline(character.identity,reaction);
                reactionWait=hud ? hud.VoicelineRemaining : clip ? clip.length+1 : 0;
                if(reactionWait>0) return;
            }
            awaitingReactions=false;
            HideResultReactions();
            Build(); RefreshActions();
        }
        private void UpdateResultReactions(float elapsed){
            reactionWait-=elapsed;
            if(reactionWait<=0) AdvanceResultReactions();
        }
        private void HideResultReactions(){
            foreach(var hud in new[]{board.playerHUD,board.enemyHUD}){
                if(!hud) continue;
                hud.ClearSubtitle();
                HideReactionObject(hud.avatar ? hud.avatar.gameObject : null);
                HideReactionObject(hud.subtitleRoot ? hud.subtitleRoot : hud.subtitle ? hud.subtitle.gameObject : null);
            }
        }
        private void HideReactionObject(GameObject obj){
            if(!obj || hiddenReactionObjects.Exists(saved=>saved.obj==obj)) return;
            hiddenReactionObjects.Add(new HiddenReactionObject {obj=obj,active=obj.activeSelf});
            obj.SetActive(false);
        }
        private void RestoreResultReactions(){
            foreach(var saved in hiddenReactionObjects) if(saved.obj) saved.obj.SetActive(saved.active);
            hiddenReactionObjects.Clear();
        }

        public bool IsAvailable => isActiveAndEnabled && board && board.isActiveAndEnabled;
        public void Initialize(BoardUIController owner){
            board = owner; bridge = board.game as GameplayBoardBridge;
            if(bridge) bridge.resultPresenter = this;
        }

        public void Observe(BoardSnapshot state){
            if(!IsAvailable || !bridge || state == null || state.phase != RoundPhase.Finished){ Cancel(); return; }
            if(shown == state && (root || awaitingReactions)){ if(root) RefreshActions(); return; }
            Cancel(); shown = state;
            won = bridge.IsPuzzle ? bridge.PuzzleSucceeded : state.winnerId == state.localPlayerId;
            lost = bridge.IsPuzzle ? !bridge.PuzzleSucceeded : state.winnerId >= 0 && !won;
            accent = won ? victoryColor : defeatColor;
            var cls = board.setup.PlayerClass;
            classColor = cls ? cls.color : board.setup.player.textColor;
            if(bridge.IsPuzzle){ BuildPuzzle(); RefreshActions(); }
            else if(won || lost) BeginResultReactions();
            else { HideResultReactions(); Build(); RefreshActions(); }
        }

        private void Update(){
            if(awaitingReactions){ UpdateResultReactions(Time.unscaledDeltaTime); return; }
            if(!root) return;
            time += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(time / Mathf.Max(0.1f, entranceSeconds));
            float ease = 1 - Mathf.Pow(1 - t, 3);
            if(puzzleShade){
                float opacity = board.phaseAnimator ? board.phaseAnimator.overlayOpacity : 0.72f;
                puzzleShade.color = new Color(0.015f, 0.02f, 0.055f, opacity * ease);
            }
            if(portrait){
                portrait.anchoredPosition = new Vector2(0, Mathf.Lerp(20, 100, ease));
                portrait.localScale = Vector3.one * Mathf.Lerp(0.9f, 1, ease);
            }
            if(bridge.IsPuzzle) content.localScale = Vector3.one * (Mathf.Lerp(0.88f, 1, ease) + Mathf.Sin(t * Mathf.PI) * 0.025f);
            content.anchoredPosition = new Vector2(0, Mathf.Lerp(-35, 0, ease));
            contentGroup.alpha = Mathf.SmoothStep(0.1f, 1, t);
            contentGroup.interactable = t >= 1;
            if(reward && reward.gameObject.activeSelf){
                rewardTime += Time.unscaledDeltaTime;
                float enter = Mathf.Max(0.1f, rewardEntranceSeconds);
                float exit = Mathf.Max(0.1f, rewardExitSeconds);
                float elapsed = rewardTime - Mathf.Max(0, rewardDelay);
                float exitStart = enter + Mathf.Max(0, rewardHoldSeconds);
                float rt = Mathf.Clamp01(elapsed / enter);
                float slide = 1 - Mathf.Pow(1 - rt, 3);
                float outT = Mathf.Clamp01((elapsed - exitStart) / exit);
                float slideOut = outT * outT * (3 - 2 * outT);
                reward.anchoredPosition = rewardHome + new Vector2((1 - slide + slideOut) * (reward.rect.width + 100), 0);
                reward.localScale = Vector3.one * (Mathf.Lerp(0.94f, 1, slide) + Mathf.Sin(rt * Mathf.PI) * 0.035f);
                rewardGroup.alpha = Mathf.SmoothStep(0, 1, rt) * (1 - slideOut);
                if(outT >= 1) reward.gameObject.SetActive(false);
            }
        }

        private string SaveError => bridge.IsPuzzle ? bridge.PuzzleSaveError : bridge.StorySaveError;
        private void RefreshActions(){
            bool unsaved = !string.IsNullOrEmpty(SaveError);
            back.interactable = !unsaved; retry.interactable = !unsaved;
            retry.gameObject.SetActive(!bridge.IsOnline && lost && (bridge.IsPuzzle || showRestartOnDefeat) && !unsaved);
            save.gameObject.SetActive(unsaved);
            status.text = unsaved ? "Your reward could not be saved.\n" + SaveError : navigationError ?? (bridge.IsOnline ? bridge.Snapshot.roundSummary : "");
            if(won && !unsaved && bridge.FirstClearReward && !reward) BuildReward();
        }

        private void BuildReward(){
            var prefab = rewardCardPrefab ? rewardCardPrefab : Resources.Load<GameObject>("reward card");
            if(!prefab) return;
            var instance = Instantiate(prefab, root);
            reward = instance.GetComponent<RectTransform>();
            if(!reward){ Destroy(instance); return; }
            var card = bridge.FirstClearReward;
            var cls = card.cardClass;
            var background = instance.GetComponent<Image>();
            if(background && cls && cls.rewardCardBackground){
                background.sprite = cls.rewardCardBackground;
                background.color = Color.white;
            }
            foreach(var image in instance.GetComponentsInChildren<Image>(true)){
                image.raycastTarget = false;
                if(image.name == "art"){
                    image.sprite = card.cardImage;
                    image.color = card.cardImage ? Color.white : Color.clear;
                    image.preserveAspect = true;
                }
            }
            foreach(var label in instance.GetComponentsInChildren<TMP_Text>(true)){
                label.raycastTarget = false;
                if(label.name != "card name") continue;
                label.text = card.cardName; label.richText = false;
                label.color = cls ? cls.color : Color.white;
                label.enableVertexGradient = false;
                label.enableAutoSizing = true; label.fontSizeMin = 12; label.fontSizeMax = label.fontSize;
            }
            reward.anchorMin = reward.anchorMax = Vector2.one;
            reward.pivot = new Vector2(1, 0.5f);
            rewardHome = new Vector2(-rewardCornerOffset.x, -rewardCornerOffset.y);
            reward.anchoredPosition = rewardHome + new Vector2(reward.rect.width + 100, 0);
            reward.localRotation = Quaternion.identity;
            rewardGroup = instance.GetComponent<CanvasGroup>();
            if(!rewardGroup) rewardGroup = instance.AddComponent<CanvasGroup>();
            rewardGroup.alpha = 0; rewardGroup.interactable = false; rewardGroup.blocksRaycasts = false;
            rewardTime = 0;
            instance.SetActive(true);
        }

        private void Restart(){
            if(bridge.IsOnline) return;
            if(!lost || !string.IsNullOrEmpty(SaveError)) return;
            if(board.pausePanel) board.pausePanel.Close();
            bridge.StartMatch();
        }

        private void Return(){
            if(bridge.IsOnline){ MultiplayerSession.Instance.ReturnToLobby(); return; }
            if(!string.IsNullOrEmpty(SaveError)) return;
            if(bridge.IsStory){
                if(!bridge.ReturnToStory()) { navigationError = "Unable to return to the story scene. Check its build settings."; RefreshActions(); }
                return;
            }
            string destination = bridge.IsPuzzle ?
                (!string.IsNullOrEmpty(bridge.PuzzleReturnScene) ? bridge.PuzzleReturnScene : puzzleScene) : defaultReturnScene;
            if(!Application.CanStreamedLevelBeLoaded(destination)){
                navigationError = "Add the return scene to the build scene list: " + destination;
                RefreshActions(); return;
            }
            bridge.SetLocalPause(false); SceneManager.LoadScene(destination);
        }

        private void RetrySave(){
            if(bridge.IsPuzzle) bridge.SavePuzzleReward();
            else bridge.RetryStorySave();
            RefreshActions();
        }

        private void Build(){
            var obj = new GameObject("Concert result", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            obj.transform.SetParent(transform, false); root = (RectTransform)obj.transform;
            var canvas = obj.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 30001;
            var scaler = obj.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = 0.5f;
            ConcertBackdrop.Create(root, board);
            var cls = board.setup.PlayerClass;
            Sprite art = cls ? (cls.fullPlayerImage ? cls.fullPlayerImage : cls.playerArt ? cls.playerArt : cls.classIcon) : board.setup.player.avatar;
            Sprite resultArt = cls ? (won ? cls.victoryImage : lost ? cls.defeatImage : null) : null;
            if(resultArt) art = resultArt;
            portrait = Image(root, "Full player art", new Vector2(0, 20), new Vector2(1250, 970), art, art ? Color.white : Color.clear).rectTransform;
            portrait.GetComponent<Image>().preserveAspect = true;
            var particles = ConcertEffects.Create(root, board, classColor, accent,
                won ? ConcertEffectStyle.Victory : ConcertEffectStyle.Finish, resultEffects);
            if(won){
                particles.EmitBurst(new Vector2(-440, 80), classColor);
                particles.EmitBurst(new Vector2(440, 80), accent);
            }
            var panelPrefab = resultPanelPrefab ? resultPanelPrefab : Resources.Load<ConcertPanelView>("Match result panel");
            if(panelPrefab){
                var panel = Instantiate(panelPrefab, root);
                content = (RectTransform)panel.transform;
                contentGroup = panel.group ? panel.group : panel.gameObject.AddComponent<CanvasGroup>();
                contentGroup.alpha = 0; contentGroup.interactable = false;
                panel.title.text = won ? "VICTORY" : lost ? "DEFEAT" : "TIE";
                back = panel.primary; retry = panel.secondary; save = panel.retrySave; status = panel.message;
                back.onClick.AddListener(Return); retry.onClick.AddListener(Restart); save.onClick.AddListener(RetrySave);
            } else {
            content = Rect(root, "Result and actions", Vector2.zero, new Vector2(1400, 1000));
            contentGroup = content.gameObject.AddComponent<CanvasGroup>(); contentGroup.alpha = 0; contentGroup.interactable = false;
            string title = won ? "VICTORY" : lost ? "DEFEAT" : "TIE";
            var titleText = Text(content, title, new Vector2(0, -175), new Vector2(1360, 200), 150, Color.white);
            titleText.characterSpacing = 8; titleText.enableVertexGradient = true;
            titleText.colorGradient = new VertexGradient(Color.white, Color.white, classColor, accent);
            var glow = titleText.gameObject.AddComponent<Shadow>(); glow.effectColor = new Color(classColor.r, classColor.g, classColor.b, 0.65f); glow.effectDistance = new Vector2(3, -3);
            back = Button(content, "RETURN", new Vector2(0, -345), Return);
            retry = Button(content, bridge.IsPuzzle ? "RETRY" : "RESTART", new Vector2(0, -425), Restart);
            save = Button(content, "RETRY SAVE", new Vector2(0, -425), RetrySave);
            status = Text(content, "", new Vector2(0, 425), new Vector2(1200, 130), 23, Color.white);
            }
            var clip = won ? victoryStinger : defeatStinger;
            if (clip){
                var audio = obj.AddComponent<AudioSource>(); audio.playOnAwake = false; audio.spatialBlend = 0;
                obj.AddComponent<AudioCategorySource>().SetCategory(AudioCategory.Effects);
                audio.PlayOneShot(clip);
            }
        }

        private void BuildPuzzle(){
            var obj = new GameObject("Puzzle result", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            obj.transform.SetParent(transform, false); root = (RectTransform)obj.transform;
            var canvas = obj.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 30001;
            var scaler = obj.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = 0.5f;
            puzzleShade = Image(root, "Dark overlay", Vector2.zero, Vector2.zero, null, Color.clear);
            puzzleShade.rectTransform.anchorMin = Vector2.zero; puzzleShade.rectTransform.anchorMax = Vector2.one;
            puzzleShade.rectTransform.offsetMin = puzzleShade.rectTransform.offsetMax = Vector2.zero;
            content = Rect(root, "Puzzle result and actions", Vector2.zero, new Vector2(1000, 500));
            contentGroup = content.gameObject.AddComponent<CanvasGroup>(); contentGroup.alpha = 0; contentGroup.interactable = false;
            accent = won ? classColor : new Color(1f, 0.56f, 0.64f);
            var prefab = puzzleResultPanelPrefab ? puzzleResultPanelPrefab : Resources.Load<ConcertPanelView>("Puzzle result panel");
            if(prefab){
                var panel = Instantiate(prefab, content);
                panel.title.text = won ? panel.passText : panel.failText;
                if(panel.puzzleNumber) panel.puzzleNumber.text = "PUZZLE " + bridge.puzzle.number;
                if(panel.puzzleName){
                    panel.puzzleName.text = string.IsNullOrWhiteSpace(bridge.puzzle.puzzleName) ? "Puzzle " + bridge.puzzle.number : bridge.puzzle.puzzleName;
                    panel.puzzleName.richText = false;
                }
                back = panel.primary; retry = panel.secondary; save = panel.retrySave; status = panel.message;
                back.onClick.AddListener(Return); retry.onClick.AddListener(Restart); save.onClick.AddListener(RetrySave);
            }else{
            var eyebrow = Text(content, "PUZZLE " + bridge.puzzle.number, new Vector2(0, 150), new Vector2(800, 40), 24, Color.white);
            eyebrow.characterSpacing = 6;
            var title = Text(content, won ? "PASS" : "FAIL", new Vector2(0, 70), new Vector2(800, 125), 92, Color.white);
            title.characterSpacing = 12; title.enableVertexGradient = true;
            title.colorGradient = new VertexGradient(Color.white, Color.white, accent, accent);
            string name = string.IsNullOrWhiteSpace(bridge.puzzle.puzzleName) ? "Puzzle " + bridge.puzzle.number : bridge.puzzle.puzzleName;
            var puzzleName = Text(content, name, new Vector2(0, -20), new Vector2(840, 70), 32, Color.white);
            puzzleName.richText = false; puzzleName.enableAutoSizing = true; puzzleName.fontSizeMin = 20; puzzleName.fontSizeMax = 32;
            foreach(var label in new[] { eyebrow, title, puzzleName }){
                var shadow = label.gameObject.AddComponent<Shadow>(); shadow.effectColor = new Color(0.02f, 0.02f, 0.06f, 0.9f);
                shadow.effectDistance = new Vector2(2, -3);
            }
            back = Button(content, "RETURN", new Vector2(lost ? -180 : 0, -145), Return);
            retry = Button(content, "RESTART", new Vector2(180, -145), Restart);
            save = Button(content, "RETRY SAVE", new Vector2(0, -220), RetrySave);
            foreach(var button in new[] { back, retry, save }) button.GetComponent<RectTransform>().sizeDelta = new Vector2(300, 60);
            status = Text(content, "", new Vector2(0, -310), new Vector2(1000, 110), 23, Color.white);
            }
            var particles = ConcertEffects.Create(content, board, accent, Color.white,
                won ? ConcertEffectStyle.Victory : ConcertEffectStyle.Finish, puzzleEffects);
            particles.transform.SetAsFirstSibling();
            particles.rectTransform.localScale = Vector3.one * 0.65f;
            particles.rectTransform.anchoredPosition = new Vector2(0, 90);
            particles.EmitBurst(new Vector2(-440, 80), accent);
            particles.EmitBurst(new Vector2(440, 80), accent);
            var clip = won ? victoryStinger : defeatStinger;
            if(clip){
                var audio = obj.AddComponent<AudioSource>(); audio.playOnAwake = false; audio.spatialBlend = 0;
                obj.AddComponent<AudioCategorySource>().SetCategory(AudioCategory.Effects); audio.PlayOneShot(clip);
            }
        }

        private Button Button(Transform parent, string label, Vector2 position, UnityEngine.Events.UnityAction action){
            var button = StoryUIElements.Button(parent, label, position, action);
            button.GetComponent<RectTransform>().sizeDelta = new Vector2(420, 65);
            ConcertBackdrop.StyleButton(button, board.setup, classColor);
            var text = button.GetComponentInChildren<TMP_Text>();
            if(font) text.font = font;
            else if(board.phaseAnimator && board.phaseAnimator.font) text.font = board.phaseAnimator.font;
            return button;
        }
        private static RectTransform Rect(Transform parent, string label, Vector2 position, Vector2 size){
            var rect = new GameObject(label, typeof(RectTransform)).GetComponent<RectTransform>(); rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one * 0.5f; rect.anchoredPosition = position; rect.sizeDelta = size; return rect;
        }
        private static Image Image(Transform parent, string label, Vector2 position, Vector2 size, Sprite sprite, Color color){
            var image = Rect(parent, label, position, size).gameObject.AddComponent<Image>();
            image.sprite = sprite; image.color = color; image.raycastTarget = false; return image;
        }
        private TMP_Text Text(Transform parent, string value, Vector2 position, Vector2 size, float fontSize, Color color){
            var text = Rect(parent, value, position, size).gameObject.AddComponent<TextMeshProUGUI>();
            if(font) text.font = font;
            else if(board.phaseAnimator && board.phaseAnimator.font) text.font = board.phaseAnimator.font;
            text.text = value; text.fontSize = fontSize; text.alignment = TextAlignmentOptions.Center; text.color = color; text.raycastTarget = false; return text;
        }
        public void Cancel(){
            awaitingReactions=false; reactionStep=0; reactionWait=0;
            if(shown!=null && board){
                if(board.playerHUD) board.playerHUD.ClearSubtitle();
                if(board.enemyHUD) board.enemyHUD.ClearSubtitle();
            }
            RestoreResultReactions();
            if(root){ root.gameObject.SetActive(false); Destroy(root.gameObject); }
            root = null; portrait = null; puzzleShade = null; shown = null; time = 0; navigationError = null;
            reward = null; rewardGroup = null; rewardTime = 0;
        }
        private void OnDisable(){ Cancel(); }
    }
}
