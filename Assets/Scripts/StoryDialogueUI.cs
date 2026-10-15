using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.Serialization;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace VocaloidTCG
{
    public sealed class StoryDialogueUI : MonoBehaviour
    {
        public GameObject panel, logPanel;
        [Tooltip("Optional glow or selected indicator shown while automatic dialogue advancement is enabled. Leave empty if unused.")]
        public GameObject autoHighlight;
        public TMP_Text speaker, line;
        [Tooltip("Dialogue backgrounds with and without the speaker nameplate.")]
        public GameObject characterBackground, narrationBackground;
        public Image[] avatars = new Image[6];
        public Button skip, auto, log, closeLog;
        
        [SerializeField, HideInInspector, FormerlySerializedAs("next")]
        private Button legacyNext;
        public Transform logRoot;
        public StoryLogView logPrefab;
        public ScrollRect logScroll;
        [Min(1)] public float charactersPerSecond = 35;
        [Min(0)] public float autoDelay = 1;
        private readonly List<StoryLogView> rows = new List<StoryLogView>();
        private List<StoryLine> lines;
        private Action finished;
        private int index;
        private bool automatic, typing;
        private Coroutine playback;
        private readonly List<RaycastResult> pointerHits = new List<RaycastResult>();
        private int lineShownFrame;
        private Transform raisedLogControl;
        private int logControlSiblingIndex;
        private bool setupChecked;

        [ContextMenu("Validate Dialogue UI Setup")]
        public void ValidateSetup(){
            setupChecked = true;
            var issues = new List<string>();
            StoryDiagnostics.Require(issues, (nameof(panel), panel), (nameof(logPanel), logPanel),
                (nameof(speaker), speaker), (nameof(line), line), (nameof(skip), skip), (nameof(auto), auto),
                (nameof(log), log), (nameof(logRoot), logRoot), (nameof(logPrefab), logPrefab));
            if(panel && transform.IsChildOf(panel.transform))
                issues.Add("StoryDialogueUI is inside panel, which it hides. Move the component to an always-active Canvas or manager.");
            if(logPanel && log && log.transform.IsChildOf(logPanel.transform))
                issues.Add("log: the Log button is inside logPanel and will disappear when the log closes. Place it outside logPanel.");
            if(logPanel && logRoot && !logRoot.IsChildOf(logPanel.transform))
                issues.Add("logRoot: place the log rows inside logPanel so they hide when the log closes.");
            
            if(logScroll && !logScroll.content) issues.Add("logScroll.content: assign the log content RectTransform.");
            if(avatars != null && avatars.Length > 6) issues.Add("avatars: use at most six image slots (0–5).");
            if(charactersPerSecond <= 0) issues.Add("charactersPerSecond: must be greater than zero.");
            if(autoDelay < 0) issues.Add("autoDelay: cannot be negative.");
            StoryDiagnostics.Report(this, issues);
            if(logPrefab) logPrefab.ValidateSetup();
        }

        private void Awake(){
            if(legacyNext) legacyNext.gameObject.SetActive(false);
            if(skip) skip.onClick.AddListener(Finish);
            if(auto) auto.onClick.AddListener(ToggleAuto);
            if(log) log.onClick.AddListener(ToggleLog);
            if(closeLog) closeLog.onClick.AddListener(CloseLog);
        }

        private void LateUpdate(){
            if(!panel || !panel.activeInHierarchy || lines == null || Time.frameCount == lineShownFrame ||
                (logPanel && logPanel.activeSelf)) return;
            Vector2 position;
#if ENABLE_INPUT_SYSTEM
            if(Mouse.current == null || !Mouse.current.leftButton.wasPressedThisFrame) return;
            position = Mouse.current.position.ReadValue();
#elif ENABLE_LEGACY_INPUT_MANAGER
            if(!Input.GetMouseButtonDown(0)) return;
            position = Input.mousePosition;
#else
            return;
#endif
#if ENABLE_INPUT_SYSTEM || ENABLE_LEGACY_INPUT_MANAGER
            if(EventSystem.current){
                pointerHits.Clear();
                EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = position }, pointerHits);
                if(pointerHits.Count > 0){
                    var target = pointerHits[0].gameObject.transform;
                    if(target.GetComponentInParent<Selectable>() || !target.IsChildOf(panel.transform)) return;
                }
            }
            Next();
#endif
        }

        public void Play(StoryDialogue dialogue, Action onFinished){
            if(!setupChecked) ValidateSetup();
            if(!panel || !line){
                StoryDiagnostics.Report(this, new[] { "panel/line: assign the dialogue panel and line text before playing dialogue." });
                onFinished?.Invoke(); return;
            }

            var issues = new List<string>();
            StoryDiagnostics.Dialogue(issues, dialogue, "Playing dialogue");
            StoryDiagnostics.Report(this, issues);
            StopPlayback(); ClearLog();
            finished = onFinished; lines = dialogue?.lines; index = 0; automatic = false;
            if(autoHighlight) autoHighlight.SetActive(false);
            CloseLog();
            if(panel) panel.SetActive(true);
            ShowLine();
        }

        private void ShowLine(){
            if(lines == null || index >= lines.Count){
                Finish();
                return;
            }

            lineShownFrame = Time.frameCount;
            var entry = lines[index] ?? new StoryLine();
            DeckUI.Text(speaker, entry.speaker); DeckUI.Text(line, entry.text);
            bool narrated = string.IsNullOrWhiteSpace(entry.speaker) ||
                string.Equals(entry.speaker.Trim(), "Narrator", StringComparison.OrdinalIgnoreCase);
            if(speaker) speaker.gameObject.SetActive(!narrated);
            if(characterBackground) characterBackground.SetActive(!narrated);
            if(narrationBackground) narrationBackground.SetActive(narrated);

            foreach(var avatar in avatars ?? new Image[0]) if(avatar) avatar.gameObject.SetActive(false);
            foreach(var avatar in entry.avatars ?? new List<StoryAvatar>())
                if(avatar != null && avatar.slot >= 0 && avatar.slot <= 5){
                    if(avatars == null || avatar.slot >= avatars.Length || !avatars[avatar.slot])
                        StoryDiagnostics.Report(this, new[] { "avatars[" + avatar.slot + "]: missing Image required by dialogue line [" + index + "] (" + entry.speaker + ")." });
                    else{
                        DeckUI.Image(avatars[avatar.slot], avatar.sprite);
                        avatars[avatar.slot].gameObject.SetActive(avatar.sprite);
                    }
                }

            if(logRoot && logPrefab){
                var row = Instantiate(logPrefab, logRoot); row.gameObject.SetActive(true); row.Bind(entry); rows.Add(row);
            }

            if(line){
                line.maxVisibleCharacters = 0;
                line.ForceMeshUpdate();
            }

            typing = true; playback = StartCoroutine(Reveal());
        }

        private IEnumerator Reveal(){
            float visible = 0;
            int length = line ? line.textInfo.characterCount : 0;

            while(typing && visible < length){
                if(!logPanel || !logPanel.activeSelf){
                    visible += Time.unscaledDeltaTime * Mathf.Max(1, charactersPerSecond);
                    if(line) line.maxVisibleCharacters = Mathf.FloorToInt(visible);
                }

                yield return null;
            }

            typing = false;
            if(line) line.maxVisibleCharacters = int.MaxValue;
            float elapsed = 0;

            while(true){
                yield return null;
                if(automatic && (!logPanel || !logPanel.activeSelf)) elapsed += Time.unscaledDeltaTime;
                else elapsed = 0;
                if(automatic && elapsed >= autoDelay){
                    playback = null;
                    index++;
                    ShowLine();
                    yield break;
                }
            }
        }

        public void Next(){
            if(!panel || !panel.activeInHierarchy || lines == null) return;
            if(logPanel && logPanel.activeSelf) return;
            if(typing && line && line.maxVisibleCharacters < line.textInfo.characterCount){
                typing = false;
                line.maxVisibleCharacters = int.MaxValue;
                return;
            }

            StopPlayback(); index++; ShowLine();
        }

        public void ToggleAuto(){
            automatic = !automatic;
            if(autoHighlight) autoHighlight.SetActive(automatic);
        }

        public void ToggleLog(){
            if(!logPanel){
                StoryDiagnostics.Report(this, new[] { "logPanel: assign the log panel before opening the log." });
                return;
            }

            if(logPanel.activeSelf){
                CloseLog();
                return;
            }

            logPanel.SetActive(true);
            if(log){
                var branch = log.transform;

                while(branch.parent && !logPanel.transform.IsChildOf(branch.parent)) branch = branch.parent;

                if(branch.parent && !logPanel.transform.IsChildOf(branch)){
                    raisedLogControl = branch; logControlSiblingIndex = branch.GetSiblingIndex(); branch.SetAsLastSibling();
                }
            }

            Canvas.ForceUpdateCanvases(); if(logScroll) logScroll.verticalNormalizedPosition = 0;
        }

        public void CloseLog(){
            if(logPanel) logPanel.SetActive(false);
            if(raisedLogControl) raisedLogControl.SetSiblingIndex(logControlSiblingIndex);
            raisedLogControl = null;
        }

        public void Finish(){
            StopPlayback(); var callback = finished; finished = null;
            CloseLog(); lines = null; automatic = false;
            if(autoHighlight) autoHighlight.SetActive(false);
            if(panel) panel.SetActive(false);
            callback?.Invoke();
        }

        private void ClearLog(){
            foreach(var row in rows) if(row) Destroy(row.gameObject);

            rows.Clear();
        }

        private void StopPlayback(){
            if(playback != null) StopCoroutine(playback);
            playback = null;
            typing = false;
        }

        private void OnDisable(){
            StopPlayback();
            CloseLog();
        }
    }
}
