using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unity.Collections;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;
using UnityEngine.SceneManagement;
using VocaloidTCG.BoardUI;

namespace VocaloidTCG
{
    public sealed class MultiplayerSession : MonoBehaviour
    {
        public static MultiplayerSession Instance { get; private set; }
        public DeckCatalog Catalog { get; private set; }
        public Lobby Lobby { get; private set; }
        public string Status { get; private set; } = "";
        public string MainScene = "Main", GameScene = "Game", DeckScene = "Edit Deck";
        public string MenuScene { get; private set; }
        public bool Busy { get; private set; }
        public bool InMatch { get; private set; }
        public bool Starting { get; private set; }
        public bool EditingDeck { get; private set; }
        public bool ShowMenu { get; private set; }
        public bool IsHost => Lobby != null && Lobby.HostId == PlayerId;
        public string PlayerId => AuthenticationService.Instance.IsSignedIn ? AuthenticationService.Instance.PlayerId : "";
        public bool Authority => network && network.IsHost;
        public DeckRecord[] MatchDecks { get; private set; }
        public event Action Changed;
        public event Action Sparkle;
        private NetworkManager network;
        private GameplayBoardBridge board;
        private DeckRecord selected;
        private float nextPoll, nextHeartbeat, nextClock, startDeadline;
        private bool polling, suppressDisconnect, remoteLoaded, localLoaded;
        private string consumedTicket = "", matchId = "";
        private int outgoingSequence, incomingSequence;
        private readonly HashSet<ulong> authenticated = new HashSet<ulong>();
        private const string Channel = "VocaloidTCG.Match.v1";

        [Serializable] public sealed class DeckSummary
        {
            public string deckName, coverId;
            public List<CharacterClass> classes;
            public CharacterClass primary;
        }
        [Serializable] public sealed class Message
        {
            public string kind, match, text;
            public int sequence, turn;
            public BoardAction action;
            public DeckRecord deck;
            public DeckRecord[] decks;
            public string[] cards;
            public OnlineBoardPacket board;
        }

        public static MultiplayerSession Open(DeckCatalog catalog, string main, string deckScene, string menuScene = null){
            menuScene = string.IsNullOrEmpty(menuScene) ? "Multiplayer" : menuScene;
            if(!Application.CanStreamedLevelBeLoaded(menuScene))
                throw new InvalidOperationException("Add the " + menuScene + " scene to Build Profiles before opening multiplayer.");
            if(!Instance) new GameObject("Multiplayer session").AddComponent<MultiplayerSession>();
            
            Instance.Catalog = DeckLibrary.Get(catalog).Catalog;
            Instance.MainScene = main; Instance.DeckScene = deckScene;
            Instance.MenuScene = menuScene;
            Instance.ShowMenu = true;
            Instance.Notify();
            
            if(SceneManager.GetActiveScene().name != Instance.MenuScene){ SceneManager.LoadScene(Instance.MenuScene); return Instance; }
            return Instance;
        }

        private void Awake(){
            if(Instance && Instance != this){ Destroy(gameObject); return; }
            Instance = this; DontDestroyOnLoad(gameObject);
            SceneManager.sceneLoaded += SceneLoaded;
            Application.runInBackground = true;
        }
        
        private void OnDestroy(){
            SceneManager.sceneLoaded -= SceneLoaded;
            if(Instance == this) Instance = null;
        }
        
        private void SceneLoaded(Scene scene, LoadSceneMode mode){
            Notify();
        }
        
        public void Notify(){
            Changed?.Invoke();
        }
        
        public void SetStatus(string value){ Status = value; Notify(); }

        private async Task SignIn(){
            if(UnityServices.State != ServicesInitializationState.Initialized){
                var options = new InitializationOptions();
                options.SetProfile("mp_" + System.Diagnostics.Process.GetCurrentProcess().Id);
                await UnityServices.InitializeAsync(options);
            }
            if(!AuthenticationService.Instance.IsSignedIn) await AuthenticationService.Instance.SignInAnonymouslyAsync();
        }

        private DeckRecord ValidDeck(){
            var library = DeckLibrary.Get(Catalog);
            var deck = library.Selected;
            if(!DeckRules.Validate(deck, Catalog, library.Owned, out var error)) throw new InvalidOperationException(error);
            if(deck.cardIds.Count == 0) throw new InvalidOperationException("Add regular cards to your deck before playing online.");
            return deck;
        }

        private string ContentVersion(){
            string content = "tcg-net-2|" + Application.version + "|" + string.Join("|", Catalog.cards.OrderBy(c => c.id).Select(c =>
                c.id + ":" + c.cost + ":" + c.influence + ":" + c.stageInfluenceChange + ":" + c.performer + ":" + c.stageEffect + ":" + c.vip + ":" + c.cardClass.identity));
            using(var hash = System.Security.Cryptography.SHA256.Create())
                return Convert.ToBase64String(hash.ComputeHash(Encoding.UTF8.GetBytes(content)));
        }

        private Dictionary<string, PlayerDataObject> PlayerData(DeckRecord deck){
            var summary = new DeckSummary { deckName = deck.deckName, coverId = Catalog.Cover(deck)?.id,
                classes = deck.classes, primary = DeckPresentation.Classes(deck, Catalog)[0].identity };
            return new Dictionary<string, PlayerDataObject> {
                { "deck", new PlayerDataObject(PlayerDataObject.VisibilityOptions.Member, JsonUtility.ToJson(summary)) },
                { "ready", new PlayerDataObject(PlayerDataObject.VisibilityOptions.Member, "0") }
            };
        }
        
        public static bool Ready(Player player) => player?.Data != null && player.Data.TryGetValue("ready", out var data) && data.Value == "1";
        
        public static DeckSummary Summary(Player player){
            if(player?.Data == null || !player.Data.TryGetValue("deck", out var data)) return null;
            try{
                return JsonUtility.FromJson<DeckSummary>(data.Value);
            }catch{
                return null;
            }
        }

        public async void CreateLobby(){ await Run(async () => {
            if(Lobby != null) return;
            SetStatus("Creating lobby…");
            selected = ValidDeck(); await SignIn();
            Lobby = await LobbyService.Instance.CreateLobbyAsync("Vocaloid TCG", 2, new CreateLobbyOptions {
                IsPrivate = true, Player = new Player(PlayerId, data: PlayerData(selected))
            });
            SetStatus("");
        }); }

        public async void JoinLobby(string code){ await Run(async () => {
            if(Lobby != null) return;
            SetStatus("Joining lobby…");
            if(string.IsNullOrWhiteSpace(code)) throw new InvalidOperationException("Enter a lobby code.");
            selected = ValidDeck(); await SignIn();
            Lobby = await LobbyService.Instance.JoinLobbyByCodeAsync(code.Trim().ToUpperInvariant(), new JoinLobbyByCodeOptions {
                Player = new Player(PlayerId, data: PlayerData(selected))
            });
            consumedTicket = Ticket(); SetStatus("Joined lobby. Press Play when ready.");
        }); }

        public async void Play(){ await Run(async () => {
            if(Lobby == null || Starting || InMatch) return;
            if(!Application.CanStreamedLevelBeLoaded(GameScene)) throw new InvalidOperationException("Add " + GameScene + " to the build scene list.");
            await LobbyService.Instance.UpdatePlayerAsync(Lobby.Id, PlayerId, new UpdatePlayerOptions {
                Data = new Dictionary<string, PlayerDataObject> { { "ready", new PlayerDataObject(PlayerDataObject.VisibilityOptions.Member, "1") } }
            });
            Lobby = await LobbyService.Instance.GetLobbyAsync(Lobby.Id);
            SetStatus("Ready. Waiting for the other player.");
        }); }

        public async void LeaveLobby(){ await Run(async () => {
            if(InMatch || Starting) return;
            if(Lobby == null){ ShowMenu = false; Notify(); return; }
            var latest = await LobbyService.Instance.GetLobbyAsync(Lobby.Id);
            if(latest.Players.Count == 1) await LobbyService.Instance.DeleteLobbyAsync(latest.Id);
            else {
                if(latest.HostId == PlayerId) await LobbyService.Instance.UpdateLobbyAsync(latest.Id, new UpdateLobbyOptions {
                    HostId = latest.Players.First(p => p.Id != PlayerId).Id, IsLocked = false,
                    Data = new Dictionary<string, DataObject> { { "ticket", null }, { "relay", null } }
                });
                await LobbyService.Instance.RemovePlayerAsync(latest.Id, PlayerId);
            }
            StopNetwork(); Lobby = null; consumedTicket = ""; SetStatus("");
        }); }

        public void BackToMain(){
            if(Busy || Lobby != null) return;
            ShowMenu = false; Notify();
            if(SceneManager.GetActiveScene().name != MainScene) SceneManager.LoadScene(MainScene);
        }

        public void EditDeck(){
            if(Lobby != null || Busy) return;
            if(!Application.CanStreamedLevelBeLoaded(DeckScene)){
                SetStatus("Add " + DeckScene + " to the build scene list.");
                return;
            }
            EditingDeck = true; ShowMenu = false; StorySession.Clear(); SceneManager.LoadScene(DeckScene);
        }

        public static bool ReturnFromDeckEditor(){
            if(!Instance || !Instance.EditingDeck) return false;
            Instance.EditingDeck = false; Instance.ShowMenu = true;
            SceneManager.LoadScene(Instance.MenuScene); return true;
        }

        private async Task Run(Func<Task> operation){
            if(Busy) return;
            Busy = true; Notify();
            try{
                await operation();
            }catch(Exception ex){
                SetStatus(FriendlyError(ex));
                Debug.LogException(ex);
            }
            finally{
                Busy = false;
                Notify();
            }
        }

        private static string FriendlyError(Exception ex){
            if(ex is LobbyServiceException lobby){
                if(lobby.Reason == LobbyExceptionReason.LobbyFull) return "This lobby is full.";
                if(lobby.Reason == LobbyExceptionReason.LobbyNotFound || lobby.Reason == LobbyExceptionReason.InvalidJoinCode) return "That lobby does not exist. Check the code.";
                if(lobby.Reason == LobbyExceptionReason.LobbyLocked) return "This lobby is starting or playing a match.";
            }
            return ex.Message;
        }
        
        private string Ticket() => Lobby?.Data != null && Lobby.Data.TryGetValue("ticket", out var value) ? value.Value : "";
        
        private async void Update(){
            if(InMatch && board && Authority && Time.unscaledTime >= nextClock){
                nextClock = Time.unscaledTime + 0.5f;
                Send(new Message { kind = "clock", text = board.RemainingSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture) });
            }

            if(Starting && Time.unscaledTime > startDeadline){ await AbortStart("The other player did not connect. Please try again."); }
            if(Lobby == null || polling || Busy || Time.unscaledTime < nextPoll) return;
            polling = true; nextPoll = Time.unscaledTime + 2;
            string id = Lobby.Id;
            
            try{
                var fresh = await LobbyService.Instance.GetLobbyAsync(id);
                if(Lobby == null || Lobby.Id != id || Busy) return;
                Lobby = fresh;
                if(!Lobby.Players.Any(p => p.Id == PlayerId)){ Lobby = null; SetStatus("You are no longer in this lobby."); return; }
                if(IsHost && Time.unscaledTime >= nextHeartbeat){
                    nextHeartbeat = Time.unscaledTime + 15; await LobbyService.Instance.SendHeartbeatPingAsync(id);
                }
                Notify();
                if(!Starting && !InMatch){
                    if(IsHost && Lobby.Players.Count == 2 && Lobby.Players.All(Ready)) await StartHost();
                    else if(!IsHost && !string.IsNullOrEmpty(Ticket()) && Ticket() != consumedTicket) await StartClient();
                }
            }
            catch(Exception ex){
                if(ex is LobbyServiceException missing && (missing.Reason == LobbyExceptionReason.LobbyNotFound || missing.Reason == LobbyExceptionReason.PlayerNotFound)) Lobby = null;
                SetStatus(FriendlyError(ex));
                if(Starting) await AbortStart(Status);
            }
            finally{
                polling = false;
            }
        }

        private void CreateNetwork(){
            StopNetwork(); suppressDisconnect = false; authenticated.Clear();
            var obj = new GameObject("Match network"); DontDestroyOnLoad(obj);
            var transport = obj.AddComponent<UnityTransport>();
            
            network = obj.AddComponent<NetworkManager>();
            network.NetworkConfig = new NetworkConfig { NetworkTransport = transport, EnableSceneManagement = false,
                ConnectionApproval = true, ConnectionData = Encoding.UTF8.GetBytes(PlayerId + "|" + matchId) };
            network.ConnectionApprovalCallback = (request, response) => {
                string token = Encoding.UTF8.GetString(request.Payload);
                bool local = request.ClientNetworkId == NetworkManager.ServerClientId;
                bool allowed = local || (network.ConnectedClientsIds.Count < 2 && Lobby.Players.Any(p => p.Id != PlayerId && token == p.Id + "|" + matchId));
                response.Approved = allowed; response.CreatePlayerObject = false;
                response.Reason = allowed ? "" : "Lobby membership does not match.";
                if(allowed && !local) authenticated.Add(request.ClientNetworkId);
            };
            
            network.OnClientConnectedCallback += Connected;
            network.OnClientDisconnectCallback += Disconnected;
            remoteLoaded = localLoaded = false; outgoingSequence = incomingSequence = 0;
        }
        private async Task StartHost(){
            Starting = true; startDeadline = Time.unscaledTime + 45; Notify();
            matchId = Guid.NewGuid().ToString("N"); consumedTicket = matchId;
            try{
                var allocation = await RelayService.Instance.CreateAllocationAsync(1);
                var relay = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);
                CreateNetwork(); network.GetComponent<UnityTransport>().SetRelayServerData(allocation.ToRelayServerData("dtls"));
                if(!network.StartHost()) throw new InvalidOperationException("Could not start the host.");
                network.CustomMessagingManager.RegisterNamedMessageHandler(Channel, Receive);
                Lobby = await LobbyService.Instance.UpdateLobbyAsync(Lobby.Id, new UpdateLobbyOptions { IsLocked = true,
                    Data = new Dictionary<string, DataObject> {
                        { "ticket", new DataObject(DataObject.VisibilityOptions.Member, matchId) },
                        { "relay", new DataObject(DataObject.VisibilityOptions.Member, relay) }
                    }
                });
                SetStatus("Connecting opponent…");
            }catch(Exception ex){
                await AbortStart(FriendlyError(ex));
            }
        }
        
        private async Task StartClient(){
            Starting = true; startDeadline = Time.unscaledTime + 45; Notify();
            matchId = Ticket(); consumedTicket = matchId;
            try{
                var allocation = await RelayService.Instance.JoinAllocationAsync(Lobby.Data["relay"].Value);
                CreateNetwork(); network.GetComponent<UnityTransport>().SetRelayServerData(allocation.ToRelayServerData("dtls"));
                if(!network.StartClient()) throw new InvalidOperationException("Could not connect to the host.");
                network.CustomMessagingManager.RegisterNamedMessageHandler(Channel, Receive);
                SetStatus("Connecting…");
            }catch(Exception ex){
                await AbortStart(FriendlyError(ex));
            }
        }

        private void Connected(ulong id){
            if(!Authority && id == network.LocalClientId) Send(new Message { kind = "hello", deck = selected, text = ContentVersion() });
        }

        private void Disconnected(ulong id){
            if(suppressDisconnect) return;
            if(InMatch && board && board.Snapshot != null && board.Snapshot.phase != RoundPhase.Finished)
                board.OnlineForfeit(1 - board.localPlayerId, "Opponent disconnected.");
            if(Starting) _ = AbortStart("The opponent disconnected before the match started.");
        }

        private void StopNetwork(){
            suppressDisconnect = true;
            if(!network) return;
            network.OnClientConnectedCallback -= Connected; network.OnClientDisconnectCallback -= Disconnected;
            network.Shutdown(); Destroy(network.gameObject); network = null;
        }
        
        public void Send(Message message){
            if(!network || !network.IsListening || (!Authority && !network.IsConnectedClient)) return;
            message.match = matchId;
            string json = JsonUtility.ToJson(message);
            using(var writer = new FastBufferWriter(Encoding.UTF8.GetByteCount(json) * 2 + 16, Allocator.Temp)){
                writer.WriteValueSafe(json);
                if(Authority){
                    foreach(var id in network.ConnectedClientsIds.Where(id => id != NetworkManager.ServerClientId).ToArray())
                        network.CustomMessagingManager.SendNamedMessage(Channel, id, writer, NetworkDelivery.ReliableFragmentedSequenced);
                }else network.CustomMessagingManager.SendNamedMessage(Channel, NetworkManager.ServerClientId, writer, NetworkDelivery.ReliableFragmentedSequenced);
            }
        }

        private async void Receive(ulong sender, FastBufferReader reader){
            try{
                if(reader.Length > 131072) return;
                reader.ReadValueSafe(out string json);
                var message = JsonUtility.FromJson<Message>(json);
                
                if(message == null || message.match != matchId) return;
                if(Authority && !authenticated.Contains(sender)) return;
                if(!Authority && sender != NetworkManager.ServerClientId) return;
                if(Authority && message.kind == "hello" && Starting && MatchDecks == null){
                    if(message.text != ContentVersion()) throw new InvalidOperationException("Both players need the same game version and card catalog.");
                    if(message.deck == null || !DeckRules.Validate(message.deck, Catalog, Catalog.cards.Select(c => c.id).ToList(), out var error))
                        throw new InvalidOperationException("Opponent has an invalid deck.");
                    MatchDecks = new[] { selected.Copy(), message.deck };
                    var publicHost = selected.Copy();
                    publicHost.classes = DeckPresentation.Classes(selected, Catalog).Select(c => c.identity).ToList();
                    publicHost.cardIds.Clear();
                    publicHost.vipCardId = "";
                    Send(new Message { kind = "start", decks = new[] { publicHost, message.deck } });
                    await EnterMatch();
                }else if(!Authority && message.kind == "start" && Starting){
                    MatchDecks = message.decks;
                    if(MatchDecks == null || MatchDecks.Length != 2) throw new InvalidOperationException("Invalid match configuration.");
                    await EnterMatch();
                }else if(Authority && message.kind == "loaded") { remoteLoaded = true; TryStartBoard(); }
                else if(!Authority && message.kind == "state" && board) board.ApplyOnlinePacket(message.board);
                else if(!Authority && message.kind == "clock" && board && float.TryParse(message.text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var seconds)) board.SetOnlineClock(seconds);
                else if(Authority && message.kind == "command" && board){
                    if(message.sequence <= incomingSequence) return;
                    incomingSequence = message.sequence;
                    board.ExecuteOnline(1, message.text, message.action, message.cards, message.turn);
                    PublishBoard();
                } else if(message.kind == "forfeit" && board) board.OnlineForfeit(Authority ? 1 : 0, "Opponent left the match.");
                else if(message.kind == "abort" && Starting) await AbortStart(message.text);
            }catch(Exception ex){
                Debug.LogException(ex);
                if(Starting) await AbortStart(FriendlyError(ex)); else SetStatus("Network message rejected: " + ex.Message);
            }
        }
        
        private async Task EnterMatch(){
            Sparkle?.Invoke(); SetStatus("Both players ready!");
            await ResetReady();
            await Task.Delay(1800);
            if(!Starting || !network || !network.IsListening) return;
            if(!Application.CanStreamedLevelBeLoaded(GameScene)) throw new InvalidOperationException("Add " + GameScene + " to the build scene list.");
            InMatch = true; ShowMenu = false;
            PuzzleLaunch.Clear(); StorySession.Clear();
            SceneManager.LoadScene(GameScene);
        }
        
        public void AttachBoard(GameplayBoardBridge game){
            board = game; board.InitializeOnline(this, Authority ? 0 : 1);
            localLoaded = true;
            if(Authority){ board.Changed += PublishBoard; TryStartBoard(); }
            else Send(new Message { kind = "loaded" });
        }
        
        private void TryStartBoard(){
            if(!Authority || !localLoaded || !remoteLoaded || !board) return;
            Starting = false; board.StartMatch(); PublishBoard();
        }
        
        private void PublishBoard(){
            if(Authority && board && board.Snapshot != null) Send(new Message { kind = "state", board = board.ExportOnlinePacket(1) });
        }
        
        public bool Command(string command, BoardAction action, string[] cards = null){
            if(!board || board.Snapshot == null) return false;
            if(Authority) return board.ExecuteOnline(0, command, action, cards, board.TurnNumber);
            Send(new Message { kind = "command", text = command, action = action, cards = cards,
                sequence = ++outgoingSequence, turn = board.TurnNumber });
            return true;
        }
        
        public void BoardReceived(){
            Starting = false;
        }
        
        public async void ReturnToLobby(){
            if(!InMatch || Busy) return;
            await Run(async () => {
                if(board && board.Snapshot != null && board.Snapshot.phase != RoundPhase.Finished){
                    if(Authority) board.OnlineForfeit(0, "Opponent left the match.");
                    else Send(new Message { kind = "forfeit" });
                    await Task.Delay(300);
                }
                
                if(board) board.Changed -= PublishBoard;
                board = null; StopNetwork(); InMatch = Starting = false; MatchDecks = null;
                
                if(Lobby != null){
                    try {
                        await ResetReady();
                        if(IsHost) await UnlockLobby();
                    } catch(Exception ex){ SetStatus(FriendlyError(ex)); }
                }
                ShowMenu = true; SceneManager.LoadScene(MenuScene);
            });
        }

        private async Task ResetReady(){
            if(Lobby == null) return;
            Lobby = await LobbyService.Instance.UpdatePlayerAsync(Lobby.Id, PlayerId, new UpdatePlayerOptions {
                Data = new Dictionary<string, PlayerDataObject> { { "ready", new PlayerDataObject(PlayerDataObject.VisibilityOptions.Member, "0") } }
            });
        }
        
        private async Task UnlockLobby(){
            Lobby = await LobbyService.Instance.UpdateLobbyAsync(Lobby.Id, new UpdateLobbyOptions { IsLocked = false,
                Data = new Dictionary<string, DataObject> { { "ticket", null }, { "relay", null } }
            });
        }
        
        private async Task AbortStart(string reason){
            if(!Starting) return;
            Starting = false;
            Send(new Message { kind = "abort", text = reason });
            StopNetwork(); MatchDecks = null;
            
            try{
                await ResetReady();
                if(IsHost) await UnlockLobby();
            }catch(Exception ex){
                Debug.LogWarning(ex.Message);
            }
            
            SetStatus(reason);
            if(InMatch){ InMatch = false; ShowMenu = true; SceneManager.LoadScene(MenuScene); }
        }
    }
}
