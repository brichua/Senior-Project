using System;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VocaloidTCG.BoardUI;

namespace VocaloidTCG
{
    public sealed class MultiplayerLobbyUI : MonoBehaviour
    {
        [Header("Used when opening this scene directly")]
        public DeckCatalog catalog;
        public string mainScene = "Main", gameScene = "Game", deckScene = "Edit Deck";
        [Serializable] public sealed class DeckDisplay
        {
            public Image avatar, art, firstClass, secondClass, hostIcon;
            public TMP_Text className, deckName;
            public void Render(MultiplayerSession.DeckSummary deck, DeckCatalog catalog, bool host)
            {
                var cls = deck == null ? null : catalog.Class(deck.primary);
                DeckUI.Image(avatar, cls ? (cls.avatar ? cls.avatar : cls.playerArt) : null);
                DeckUI.Image(art, deck == null ? null : catalog.Card(deck.coverId)?.cardImage);
                DeckUI.Text(deckName, deck?.deckName ?? "Waiting for opponent…");
                if(deckName) deckName.richText = false;
                if(className){
                    className.text = cls ? cls.DisplayName : "";
                    className.color = cls ? cls.color : Color.white;
                }
                if(hostIcon) hostIcon.gameObject.SetActive(host);
                ClassIcon(firstClass, deck?.classes != null && deck.classes.Count > 0 ? catalog.Class(deck.classes[0])?.classIcon : null);
                ClassIcon(secondClass, deck?.classes != null && deck.classes.Count > 1 ? catalog.Class(deck.classes[1])?.classIcon : null);
            }
            private static void ClassIcon(Image image, Sprite sprite)
            {
                if(!image) return;
                image.gameObject.SetActive(sprite != null);
                if(sprite) DeckUI.Image(image, sprite);
            }
        }
        public GameObject root, entryPanel, roomPanel, joinPanel;
        public GameObject playerReadyImage, opponentReadyImage;
        [SerializeField, HideInInspector] private GameObject readyImage;
        public Button create, join, submitCode, back, editDeck, leave, copy, play;
        public Button pasteCode, clearCode;
        public TMP_InputField codeInput;
        public TMP_Text status, lobbyCode;
        public TMP_Text playerHeading, opponentHeading;
        public DeckDisplay selectedDeck, playerDeck, opponentDeck;
        private MultiplayerSession session;
        [SerializeField] private ConcertEffects sparkles;
        private bool sparkling;
        private static readonly Color Accent = new Color(0.8f, 0.72f, 1, 1);

        private void Start(){
            session = MultiplayerSession.Instance;
            if(!session){
                try {
                    session = MultiplayerSession.Open(catalog, mainScene, deckScene, UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
                    session.GameScene = gameScene;
                } catch(Exception ex){ Debug.LogException(ex); enabled = false; return; }
            }

            if(!root){ Debug.LogError("Save the multiplayer layout into the scene using Tools > Vocaloid TCG > Save Multiplayer UI to Scene.", this); enabled = false; return; }
            if(joinPanel) joinPanel.SetActive(true);
            if(codeInput) codeInput.gameObject.SetActive(true);
            if(readyImage) readyImage.SetActive(false);
            if(create) create.onClick.AddListener(session.CreateLobby);
            if(join) join.onClick.AddListener(Join);
            if(submitCode && submitCode != join) submitCode.onClick.AddListener(Join);
            if(back) back.onClick.AddListener(session.BackToMain);
            if(editDeck) editDeck.onClick.AddListener(session.EditDeck);
            if(leave) leave.onClick.AddListener(session.LeaveLobby);
            if(copy) copy.onClick.AddListener(Copy);
            if(pasteCode) pasteCode.onClick.AddListener(PasteCode);
            if(clearCode) clearCode.onClick.AddListener(ClearCode);
            if(play) play.onClick.AddListener(session.Play);

            session.Changed += Refresh; session.Sparkle += Sparkle;
            Refresh();
        }

        private void Join(){
            session.JoinLobby(codeInput ? codeInput.text : "");
        }
        
        private void Copy(){
            if(session.Lobby != null){
                GUIUtility.systemCopyBuffer = session.Lobby.LobbyCode;
                session.SetStatus("Lobby code copied.");
            }
        }
        
        private void PasteCode(){
            SetCode(GUIUtility.systemCopyBuffer.Trim().ToUpperInvariant());
        }
        
        private void ClearCode(){
            SetCode("");
        }
        
        private void SetCode(string value){
            if(!codeInput) return;
            codeInput.text = value;
            codeInput.ActivateInputField();
            codeInput.caretPosition = codeInput.text.Length;
        }

        private void Sparkle(){
            sparkling = true;
            if(!sparkles) return;
            
            RefreshEffectColors();
            sparkles.gameObject.SetActive(true);
            sparkles.BeginLobbyTransition();
        }

        private void RefreshEffectColors(){
            if(!sparkles || session.Lobby == null) return;
            var me = MultiplayerSession.Summary(session.Lobby.Players.FirstOrDefault(p => p.Id == session.PlayerId));
            var other = MultiplayerSession.Summary(session.Lobby.Players.FirstOrDefault(p => p.Id != session.PlayerId));
            var mine = me == null ? null : session.Catalog.Class(me.primary);
            var theirs = other == null ? null : session.Catalog.Class(other.primary);
            
            sparkles.left = mine ? mine.color : Accent;
            sparkles.right = theirs ? theirs.color : sparkles.left;
        }
        
        private void Refresh()
        {
            if(!root || !session) return;
            root.SetActive(session.ShowMenu && !session.EditingDeck && !session.InMatch);
            bool room = session.Lobby != null;
            if(entryPanel) entryPanel.SetActive(!room);
            if(roomPanel) roomPanel.SetActive(room);
            if(joinPanel) joinPanel.SetActive(!room);
            
            DeckUI.Text(status, session.Status);
            bool available = !session.Busy && !session.Starting;
            foreach(var button in new[] { create, join, submitCode, back, editDeck, leave, pasteCode, clearCode }) if(button) button.interactable = available;
            
            if(!session.Starting && sparkling){ sparkling = false; if(sparkles) sparkles.ResetLobbyTransition(); }
            if(sparkles) sparkles.gameObject.SetActive(room);
            
            if(room){
                var me = session.Lobby.Players.FirstOrDefault(p => p.Id == session.PlayerId);
                var other = session.Lobby.Players.FirstOrDefault(p => p.Id != session.PlayerId);
                playerDeck?.Render(MultiplayerSession.Summary(me), session.Catalog, session.Lobby.HostId == me?.Id);
                opponentDeck?.Render(MultiplayerSession.Summary(other), session.Catalog, session.Lobby.HostId == other?.Id);
                DeckUI.Text(playerHeading, session.Lobby.HostId == me?.Id ? "you (host)" : "you");
                DeckUI.Text(opponentHeading, session.Lobby.HostId == other?.Id ? "opponent (host)" : "opponent");
                RefreshEffectColors();
                DeckUI.Text(lobbyCode, session.Lobby.LobbyCode);
                if(playerReadyImage) playerReadyImage.SetActive(sparkling || MultiplayerSession.Ready(me));
                if(opponentReadyImage) opponentReadyImage.SetActive(other != null && (sparkling || MultiplayerSession.Ready(other)));
                if(play) play.interactable = available && !MultiplayerSession.Ready(me);
            }else{
                var deck = DeckLibrary.Get(session.Catalog).Selected;
                var primary = DeckPresentation.Classes(deck, session.Catalog).FirstOrDefault();
                selectedDeck?.Render(deck == null || !primary ? null : new MultiplayerSession.DeckSummary {
                    deckName = deck.deckName, coverId = session.Catalog.Cover(deck)?.id, classes = deck.classes, primary = primary.identity
                }, session.Catalog, false);
            }
        }

        private void OnDestroy(){
            if(!session) return;
            session.Changed -= Refresh; session.Sparkle -= Sparkle;
            if(create) create.onClick.RemoveListener(session.CreateLobby);
            if(join) join.onClick.RemoveListener(Join);
            if(submitCode && submitCode != join) submitCode.onClick.RemoveListener(Join);
            if(back) back.onClick.RemoveListener(session.BackToMain);
            if(editDeck) editDeck.onClick.RemoveListener(session.EditDeck);
            if(leave) leave.onClick.RemoveListener(session.LeaveLobby);
            if(copy) copy.onClick.RemoveListener(Copy);
            if(pasteCode) pasteCode.onClick.RemoveListener(PasteCode);
            if(clearCode) clearCode.onClick.RemoveListener(ClearCode);
            if(play) play.onClick.RemoveListener(session.Play);
        }
    }
}
