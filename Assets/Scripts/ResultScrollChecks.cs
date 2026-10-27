#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace VocaloidTCG.BoardUI
{
    public static class ResultScrollChecks
    {
        private static readonly List<UnityEngine.Object> temporary=new List<UnityEngine.Object>();
        private const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic;
        private static int checks;
        private static TMP_FontAsset font;
        private static void Check(bool ok,string message){ checks++; if(!ok) throw new Exception(message); }
        private static object Call(object obj,string name,params object[] args) => obj.GetType().GetMethod(name,Hidden).Invoke(obj,args);
        private static object Get(object obj,string name) => obj.GetType().GetField(name,Hidden).GetValue(obj);
        private static void Set(object obj,string name,object value) => obj.GetType().GetField(name,Hidden).SetValue(obj,value);
        private static GameObject Obj(string name,params Type[] types){ var obj=new GameObject(name,types){hideFlags=HideFlags.HideAndDontSave}; temporary.Add(obj); return obj; }
        private static RectTransform Rect(Transform parent,string name,Vector2 size,Vector2 position){
            var rect=(RectTransform)Obj(name,typeof(RectTransform)).transform; rect.SetParent(parent,false); rect.sizeDelta=size; rect.anchoredPosition=position; return rect;
        }
        private static TMP_Text Text(Transform parent,string name,Vector2 size,Vector2 position){
            var rect=Rect(parent,name,size,position); var text=rect.gameObject.AddComponent<TextMeshProUGUI>(); text.font=font; text.fontSize=22; return text;
        }
        private static VoicelinesManager Voices(){
            var obj=Obj("Result check voices",typeof(AudioSource)); var voices=obj.AddComponent<VoicelinesManager>(); Call(voices,"Awake");
            var path=File.Exists("Assets/VoiceCheckData/Game.unity.txt") ? "Assets/VoiceCheckData/Game.unity.txt" : "Assets/Scenes/Game.unity";
            string scene=File.ReadAllText(path);
            foreach(string field in new[]{"victory","defeat","multiplayer"}){
                var match=Regex.Match(scene,@"(?m)^  "+field+@":\r?\n((?:  - .*\r?\n)*)");
                var clips=Regex.Matches(match.Groups[1].Value,@"guid: (\w+)").Cast<Match>()
                    .Select(m=>AssetDatabase.LoadAssetAtPath<AudioClip>(AssetDatabase.GUIDToAssetPath(m.Groups[1].Value))).ToArray();
                typeof(VoicelinesManager).GetField(field).SetValue(voices,clips);
            }
            return voices;
        }
        private static SideHUD Hud(CharacterClassData character){
            var root=Obj("Result check HUD",typeof(RectTransform),typeof(Canvas)); var hud=root.AddComponent<SideHUD>();
            hud.avatar=Rect(root.transform,"Avatar",new Vector2(100,100),Vector2.zero).gameObject.AddComponent<Image>();
            hud.avatarAnimator=hud.avatar.gameObject.AddComponent<Animator>(); hud.avatarAnimator.runtimeAnimatorController=character.avatarAnimator;
            hud.avatarAnimator.Rebind(); hud.avatarAnimator.Update(0);
            hud.subtitle=Text(root.transform,"Subtitle",new Vector2(500,60),new Vector2(0,-100)); hud.subtitleRoot=hud.subtitle.gameObject;
            return hud;
        }
        private static void EndSequence(bool localWins,bool departure=false,bool noVoices=false,int localId=0){
            var bridge=Obj("Result check bridge").AddComponent<GameplayBoardBridge>(); bridge.startAutomatically=false;
            var state=new BoardSnapshot {phase=RoundPhase.Finished,localPlayerId=localId,winnerId=localWins ? localId : 1-localId}; Set(bridge,"state",state);
            var ui=Obj("Result check board").AddComponent<BoardUIController>(); ui.game=bridge;
            var setup=ScriptableObject.CreateInstance<BoardSetup>(); temporary.Add(setup);
            setup.playerClass=AssetDatabase.LoadAssetAtPath<CharacterClassData>("Assets/Classes/miku.asset");
            setup.enemyClass=AssetDatabase.LoadAssetAtPath<CharacterClassData>("Assets/Classes/rin.asset"); ui.setup=setup;
            ui.playerHUD=Hud(setup.playerClass); ui.enemyHUD=Hud(setup.enemyClass); ui.voicelines=noVoices ? null : Voices();
            var result=Obj("Result sequence").AddComponent<MatchResultAnimator>(); result.font=font; result.Initialize(ui);
            if(departure){ Call(ui,"BindVoicelineBridge"); bridge.PlayLocalVoiceline(VoicelineEvent.Multiplayer); }
            result.Observe(state);
            if(!noVoices){
                Check(result.IsWaitingForReactions && Get(result,"root")==null,"Result overlay waits while avatars react.");
                if(departure){
                    Check((int)Get(result,"reactionStep")==0 && ui.playerHUD.subtitle.text==setup.playerClass.multiplayerVoiceline,"Departure line completes before the match reactions begin.");
                    Call(result,"UpdateResultReactions",100f);
                }
                var winner=localWins ? ui.playerHUD : ui.enemyHUD;
                var loser=localWins ? ui.enemyHUD : ui.playerHUD;
                Check(winner.avatarAnimator.GetInteger("Reaction")==1 && winner.avatar.gameObject.activeSelf,"Winner's avatar plays Victory on the board.");
                float wait=(float)Get(result,"reactionWait");
                Check(wait>1 && Mathf.Abs(wait-winner.VoicelineRemaining)<0.001f,"Wait uses the winner's actual clip and reaction duration.");
                Call(result,"UpdateResultReactions",0.05f); result.Observe(state);
                Check((int)Get(result,"reactionStep")==1 && Get(result,"root")==null,"Refreshing Finished does not restart or skip the first reaction.");
                Call(result,"UpdateResultReactions",100f);
                Check(result.IsWaitingForReactions && loser.avatarAnimator.GetInteger("Reaction")==2 && loser.avatar.gameObject.activeSelf,"Loser's Defeat plays after Victory finishes.");
                Check(Get(result,"root")==null,"Overlay still waits for the losing avatar.");
                Call(result,"UpdateResultReactions",100f);
            }
            var presentation=(RectTransform)Get(result,"root");
            Check(presentation && !result.IsWaitingForReactions,"Result presentation starts after both reactions, or immediately when no audio is configured.");
            Check(!ui.playerHUD.avatar.gameObject.activeSelf && !ui.enemyHUD.avatar.gameObject.activeSelf,"Neither game avatar appears during the result presentation.");
            Check(!ui.playerHUD.subtitleRoot.activeSelf && !ui.enemyHUD.subtitleRoot.activeSelf,"Neither game subtitle appears during the result presentation.");
            temporary.Add(presentation.gameObject);
            UnityEngine.Object.DestroyImmediate(presentation.gameObject); Set(result,"root",null); result.Cancel();
            Check(ui.playerHUD.avatar.gameObject.activeSelf && ui.enemyHUD.avatar.gameObject.activeSelf,"Cancel restores game avatar visibility for the next match.");
        }
        private static void Scroll(bool nestedContent,bool createScroll){
            var canvas=Obj("Description check canvas",typeof(RectTransform),typeof(Canvas),typeof(GraphicRaycaster)); canvas.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;
            var root=Rect(canvas.transform,"Card info",new Vector2(400,400),Vector2.zero);
            var info=root.gameObject.AddComponent<CardInfoView>();
            info.cardName=Text(root,"Card name",new Vector2(285,55),new Vector2(0,130));
            ScrollRect scroll=null; RectTransform viewport;
            if(createScroll) viewport=root;
            else {
                var scrollRoot=Rect(root,"Scene Scroll View",new Vector2(293.84f,77),Vector2.zero);
                scroll=scrollRoot.gameObject.AddComponent<ScrollRect>(); scrollRoot.gameObject.AddComponent<Image>().color=Color.clear;
                viewport=Rect(scrollRoot,"Viewport",new Vector2(281.84f,77),Vector2.zero);
                viewport.gameObject.AddComponent<Image>(); viewport.gameObject.AddComponent<Mask>().showMaskGraphic=false;
                scroll.viewport=viewport;
            }
            var parent=nestedContent ? Rect(viewport,"Old content wrapper",new Vector2(281.84f,20),Vector2.zero) : viewport;
            info.description=Text(parent,"Description",new Vector2(281.84f,77),Vector2.zero);
            info.description.gameObject.AddComponent<ContentSizeFitter>().verticalFit=ContentSizeFitter.FitMode.PreferredSize;
            if(scroll) scroll.content=parent==viewport ? info.description.rectTransform : parent;
            var data=ScriptableObject.CreateInstance<CardData>(); temporary.Add(data); data.cardName="Scroll regression";
            data.info=string.Join("\n",Enumerable.Range(1,35).Select(i=>"Line "+i+": a long ability description that wraps across the available width."));
            var card=new CardState {data=data,instanceId="scroll-check"};
            var setup=ScriptableObject.CreateInstance<BoardSetup>(); temporary.Add(setup);
            info.Show(card,new SideArt(),setup); Call(info,"LateUpdate"); Canvas.ForceUpdateCanvases();
            scroll=info.description.GetComponentInParent<ScrollRect>(); viewport=scroll.viewport;
            Check(scroll.content==info.description.rectTransform,"Scroll bounds use the actual description, including existing wrapper layouts.");
            Check(scroll.content.rect.height>viewport.rect.height*3,"All long description lines contribute to the scrollable height.");
            Check(!info.description.GetComponent<ContentSizeFitter>().enabled,"Late text measurement has no competing size fitter.");
            var system=EventSystem.current ? EventSystem.current : Obj("Description event system").AddComponent<EventSystem>();
            scroll.OnScroll(new PointerEventData(system){scrollDelta=new Vector2(0,-20)});
            Check(scroll.verticalNormalizedPosition<0.99f,"Mouse wheel can move description content below the mask.");
            scroll.verticalNormalizedPosition=0.35f; float middle=scroll.verticalNormalizedPosition;
            info.cardName.havePropertiesChanged=true;
            Call(info,"LateUpdate"); Check(Mathf.Abs(scroll.verticalNormalizedPosition-middle)<0.001f,"Name mesh updates do not reset the reader's scroll position.");
            info.Show(card,new SideArt(),setup); Call(info,"LateUpdate");
            Check(Mathf.Abs(scroll.verticalNormalizedPosition-middle)<0.001f,"Showing the same card preserves scroll position.");
            scroll.verticalNormalizedPosition=0; info.description.ForceMeshUpdate();
            var last=info.description.textInfo.lineInfo[info.description.textInfo.lineCount-1];
            float bottom=viewport.InverseTransformPoint(info.description.transform.TransformPoint(new Vector3(0,last.descender,0))).y;
            Check(bottom>=viewport.rect.yMin-1 && bottom<=viewport.rect.yMax,"The last line can be scrolled fully inside the mask.");
            float before=scroll.content.rect.height;
            viewport.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal,160); Call(info,"LateUpdate");
            Check(scroll.content.rect.height>before,"Resizing to a narrower viewport recalculates wrapped text height.");
            data.info="Short description."; info.Show(card,new SideArt(),setup); Call(info,"LateUpdate");
            Check(Mathf.Abs(scroll.content.rect.height-viewport.rect.height)<0.1f,"Short descriptions shrink content back to the viewport.");
            Check(Mathf.Abs(scroll.content.anchoredPosition.y)<0.1f,"A changed short description is aligned to the top.");
        }
        private static void RedrawHover(bool usePrefab){
            var canvas=Obj("Redraw hover canvas",typeof(RectTransform),typeof(Canvas)); canvas.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;
            var root=Rect(canvas.transform,"Opening root",new Vector2(1920,1080),Vector2.zero);
            var bridge=Obj("Redraw hover bridge").AddComponent<GameplayBoardBridge>(); bridge.startAutomatically=false;
            var state=new BoardSnapshot {phase=RoundPhase.Mulligan}; Set(bridge,"state",state);
            var ui=Obj("Redraw hover board").AddComponent<BoardUIController>(); ui.game=bridge;
            var setup=ScriptableObject.CreateInstance<BoardSetup>(); temporary.Add(setup); ui.setup=setup;
            var opening=Obj("Redraw hover presentation").AddComponent<MatchOpeningAnimator>(); opening.font=font; opening.Initialize(ui);
            if(usePrefab){
                var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/redraw card info.prefab");
                Check(prefab,"Actual redraw info prefab loads."); opening.redrawInfoPrefab=prefab.GetComponent<CardInfoView>();
            }
            Set(opening,"root",root); Set(opening,"snapshot",state); Set(opening,"selecting",true);
            var longData=ScriptableObject.CreateInstance<CardData>(); temporary.Add(longData); longData.cardName="Long redraw card description";
            longData.info=string.Join("\n",Enumerable.Range(1,7).Select(i=>"Line "+i+": A performer gains influence."));
            var shortData=ScriptableObject.CreateInstance<CardData>(); temporary.Add(shortData); shortData.cardName="Short preview"; shortData.info="Short description.";
            var hand=(List<CardState>[])Get(bridge,"openingCards");
            hand[0].Add(new CardState {data=longData,instanceId="redraw-long"}); hand[0].Add(new CardState {data=shortData,instanceId="redraw-short"});
            var card=Rect(root,"Hovered opening card",new Vector2(160,220),new Vector2(-350,0)).gameObject.AddComponent<OpeningHandCard>(); card.owner=opening;
            float longHeight=0;
            try {
                foreach(int index in new[]{0,0,1,0,1,0}){
                    card.index=index; opening.ShowCardInfo(card);
                    var info=(CardInfoView)Get(opening,"hoverInfo"); Call(info,"LateUpdate"); Canvas.ForceUpdateCanvases(); info.description.ForceMeshUpdate();
                    var panel=(RectTransform)info.transform; var scroll=info.description.GetComponentInParent<ScrollRect>(); var viewport=scroll.viewport;
                    float preferred=info.description.GetPreferredValues(info.description.text,viewport.rect.width,float.PositiveInfinity).y;
                    Check(preferred<=viewport.rect.height+0.1f,"Redraw hover keeps the full description visible after every show (prefab="+usePrefab+", card="+index+", text="+preferred+", viewport="+viewport.rect.height+").");
                    var last=info.description.textInfo.lineInfo[info.description.textInfo.lineCount-1];
                    float bottom=viewport.InverseTransformPoint(info.description.transform.TransformPoint(new Vector3(0,last.descender,0))).y;
                    Check(bottom>=viewport.rect.yMin-1,"Redraw's last line is above the mask without needing to scroll.");
                    if(index==0){
                        if(longHeight==0) longHeight=panel.rect.height;
                        Check(Mathf.Abs(panel.rect.height-longHeight)<1,"Repeated long-card hovers keep the same panel height.");
                    }
                    Check(panel.rect.height<=root.rect.height-32+0.1f,"Preview stays inside the opening screen.");
                    opening.HideCardInfo(card);
                }
            } finally { Set(opening,"root",null); }
        }
        [MenuItem("Tools/QA/Run Result and Info Scroll Checks")]
        public static void Run(){
            checks=0; bool passed=false; string result;
            try {
                font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Thaleah_PixelFont/Materials/ThaleahFat_TTF SDF.asset");
                Check(font,"Actual game font loads.");
                RedrawHover(true); RedrawHover(false);
                EndSequence(true); EndSequence(false); EndSequence(true,false,false,1); EndSequence(true,true); EndSequence(true,false,true);
                Scroll(false,false); Scroll(true,false); Scroll(false,true); passed=true; result="PASS";
            } catch(Exception ex){ result="FAIL\n"+ex; Debug.LogException(ex); }
            finally { for(int i=temporary.Count-1;i>=0;i--) if(temporary[i]) UnityEngine.Object.DestroyImmediate(temporary[i]); temporary.Clear(); }
            result+="\nResult/scroll assertions: "+checks;
            Directory.CreateDirectory("Assets/Scripts/ResultScrollCheckResults~"); File.WriteAllText("Assets/Scripts/ResultScrollCheckResults~/results.txt",result); Debug.Log(result);
            if(Application.isBatchMode) EditorApplication.Exit(passed ? 0 : 1);
        }
    }
}
#endif
