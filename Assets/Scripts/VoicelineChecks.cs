#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using TMPro;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.UI;

namespace VocaloidTCG.BoardUI
{
    public static class VoicelineChecks
    {
        private static readonly List<UnityEngine.Object> temporary=new List<UnityEngine.Object>();
        private static readonly BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic;
        private static readonly string[] Names={"kaito","len","luka","meiko","miku","rin"};
        private static readonly CharacterClass[] Identities={CharacterClass.KAITO,CharacterClass.KagamineLen,CharacterClass.MegurineLuka,CharacterClass.MEIKO,CharacterClass.HatsuneMiku,CharacterClass.KagamineRin};
        private static int checks;
        private static void Check(bool ok,string message){ checks++; if(!ok) throw new Exception(message); }
        private static object Call(object obj,string method,params object[] args) => obj.GetType().GetMethod(method,Hidden).Invoke(obj,args);
        private static void Field(object obj,string name,object value) => obj.GetType().GetField(name,Hidden).SetValue(obj,value);
        private static GameObject Obj(string name,params Type[] types){ var obj=new GameObject(name,types){hideFlags=HideFlags.HideAndDontSave}; temporary.Add(obj); return obj; }
        private static AudioClip[] ReadClips(string text,string field){
            var match=Regex.Match(text,@"(?m)^  "+field+@":\r?\n((?:  - .*\r?\n)*)");
            Check(match.Success,"Scene audio array exists: "+field);
            return Regex.Matches(match.Groups[1].Value,@"guid: (\w+)").Cast<Match>()
                .Select(m=>AssetDatabase.LoadAssetAtPath<AudioClip>(AssetDatabase.GUIDToAssetPath(m.Groups[1].Value))).ToArray();
        }
        private static VoicelinesManager Voices(){
            var obj=Obj("Voiceline check source",typeof(AudioSource));
            var voices=obj.AddComponent<VoicelinesManager>(); Call(voices,"Awake");
            string scene=File.ReadAllText(File.Exists("Assets/VoiceCheckData/Game.unity.txt") ? "Assets/VoiceCheckData/Game.unity.txt" : "Assets/Scenes/Game.unity");
            foreach(string field in new[]{"victory","defeat","excited","concerned","multiplayer"}.Concat(Names.SelectMany(n=>new[]{n+"_idle",n+"_win_tile",n+"_lose_tile"})))
                typeof(VoicelinesManager).GetField(field).SetValue(voices,ReadClips(scene,field));
            return voices;
        }
        private static SideHUD HUD(CharacterClassData character){
            var obj=Obj("Voiceline check HUD",typeof(RectTransform)); obj.SetActive(false);
            var hud=obj.AddComponent<SideHUD>();
            var caption=Obj("Voiceline check subtitle",typeof(RectTransform)); caption.transform.SetParent(obj.transform,false);
            hud.subtitle=caption.AddComponent<TextMeshProUGUI>(); hud.subtitleRoot=caption;
            var avatar=Obj("Voiceline check avatar",typeof(RectTransform),typeof(Image),typeof(Animator),typeof(Canvas));
            hud.avatar=avatar.GetComponent<Image>(); hud.avatarAnimator=avatar.GetComponent<Animator>();
            hud.avatarAnimator.runtimeAnimatorController=character.avatarAnimator;
            hud.avatarAnimator.Rebind(); hud.avatarAnimator.Update(0);
            return hud;
        }
        private static void IndexedAudio(VoicelinesManager voices){
            string[][] expected={new[]{"Ah...","Heh...","Hmm..."},new[]{"Heh...","Oops!","No..."},new[]{"Hmm...","Oh...","Ah..."},
                new[]{"Oh...","Hmm...","Ah..."},new[]{"Hmm...","Ah...","Ehehe..."},new[]{"Ehehe...","Hmm...","Fufufu..."}};
            for(int i=0;i<Names.Length;i++){
                var character=AssetDatabase.LoadAssetAtPath<CharacterClassData>("Assets/Classes/"+Names[i]+".asset");
                Check(character && character.avatarAnimator,Names[i]+" has its avatar controller.");
                Check(character.idleVoicelines.SequenceEqual(expected[i]),Names[i]+" idle subtitles follow the scene's audio order.");
                var controller=(AnimatorController)character.avatarAnimator;
                foreach(var reaction in new[]{VoicelineEvent.Victory,VoicelineEvent.Defeat,VoicelineEvent.Concerned,VoicelineEvent.Excited,VoicelineEvent.Idle,VoicelineEvent.Win_Tile,VoicelineEvent.Lose_Tile}){
                    var state=controller.layers[0].stateMachine.states.Single(s=>s.state.name==reaction.ToString()).state;
                    Check(state.motion,Names[i]+" has animation motion for "+reaction);
                    Check(controller.layers[0].stateMachine.anyStateTransitions.Any(t=>t.destinationState==state && t.conditions.Any(c=>c.parameter=="Reaction" && c.threshold==(int)reaction)),"Reaction parameter maps to the correct animation.");
                }
                foreach(var reaction in new[]{VoicelineEvent.Idle,VoicelineEvent.Win_Tile,VoicelineEvent.Lose_Tile}){
                    var clips=(AudioClip[])typeof(VoicelinesManager).GetField(Names[i]+(reaction==VoicelineEvent.Idle ? "_idle" : reaction==VoicelineEvent.Win_Tile ? "_win_tile" : "_lose_tile")).GetValue(voices);
                    var seen=new HashSet<int>();
                    for(int sample=0;sample<60;sample++){
                        Check(voices.TryGetVoiceline(Identities[i],reaction,out var clip,out int index),"Select populated variant.");
                        Check(index>=0 && index<clips.Length && clip==clips[index],"Selected index identifies the exact audio clip.");
                        Check(!string.IsNullOrWhiteSpace(character.VoicelineText(reaction,index)),"Selected clip has an indexed subtitle."); seen.Add(index);
                    }
                    Check(seen.Count==clips.Length,"Every configured variant can play.");
                }
                var hud=HUD(character);
                foreach(var reaction in new[]{VoicelineEvent.Victory,VoicelineEvent.Defeat,VoicelineEvent.Concerned,VoicelineEvent.Excited,VoicelineEvent.Multiplayer}){
                    Check(hud.PlayVoiceline(character,reaction,voices),"Play "+Names[i]+" "+reaction);
                    Check(hud.subtitle.text==character.VoicelineText(reaction),"Scalar reaction uses the matching subtitle.");
                    Check(hud.avatarAnimator.GetInteger("Reaction")== (int)SideHUD.AnimationReaction(reaction),"Scalar reaction drives the correct animation.");
                    Check(hud.avatarAnimator.updateMode==AnimatorUpdateMode.UnscaledTime,"Voice animation uses real time.");
                    var expectedClip=voices.GetVoiceline(Identities[i],reaction);
                    Check(Mathf.Abs((float)typeof(SideHUD).GetField("subtitleRemaining",Hidden).GetValue(hud)-(expectedClip.length+1))<0.01f,"Subtitle lifetime follows the played clip.");
                }
            }
        }
        private static void Routing(VoicelinesManager voices){
            var character=AssetDatabase.LoadAssetAtPath<CharacterClassData>("Assets/Classes/miku.asset");
            var setup=ScriptableObject.CreateInstance<BoardSetup>(); temporary.Add(setup); setup.playerClass=setup.enemyClass=character;
            var bridge=Obj("Voiceline bridge").AddComponent<GameplayBoardBridge>(); bridge.startAutomatically=false;
            var ui=Obj("Voiceline board UI").AddComponent<BoardUIController>();
            ui.game=bridge; ui.setup=setup; ui.voicelines=voices; ui.playerHUD=HUD(character); ui.enemyHUD=HUD(character);
            Call(ui,"BindVoicelineBridge");
            Check(ui.PlayVoiceline(false,VoicelineEvent.Idle),"Enemy idle plays through the HUD.");
            Check(ui.enemyHUD.avatarAnimator.GetInteger("Reaction")==7 && ui.playerHUD.avatarAnimator.GetInteger("Reaction")==0,"Same-class opponents route to the requested side.");
            ui.enemyHUD.SetTimerSubtitle("5");
            bridge.PlayLocalVoiceline(VoicelineEvent.Concerned);
            Check(ui.playerHUD.subtitle.text==character.concernedVoiceline && ui.playerHUD.avatarAnimator.GetInteger("Reaction")==4,"Bridge concerned request reaches the local HUD.");
            Check(ui.enemyHUD.subtitle.text=="5" && ui.enemyHUD.avatarAnimator.GetInteger("Reaction")==0,"Interrupting a voice clears the old reaction and preserves countdown text.");
            bridge.PlayLocalVoiceline(VoicelineEvent.Multiplayer);
            Check(bridge.DepartureVoicelinePlayed && ui.playerHUD.subtitle.text==character.multiplayerVoiceline,"Departure line is remembered and uses its own subtitle.");
            Check(ui.playerHUD.avatarAnimator.GetInteger("Reaction")==2,"Departure plays the Defeat animation.");
            // The game HUD is hidden, rather than lifted above the result presentation.
            var avatarCanvas=ui.playerHUD.avatar.GetComponent<Canvas>(); avatarCanvas.sortingOrder=17; avatarCanvas.overrideSorting=false;
            var captionCanvas=ui.playerHUD.subtitleRoot.AddComponent<Canvas>(); captionCanvas.sortingOrder=18; captionCanvas.overrideSorting=false;
            var result=Obj("Voiceline result").AddComponent<MatchResultAnimator>(); Field(result,"board",ui); Field(result,"bridge",bridge);
            Call(result,"HideResultReactions");
            Check(!ui.playerHUD.avatar.gameObject.activeSelf && !ui.enemyHUD.avatar.gameObject.activeSelf,"Both avatars are hidden for the result presentation.");
            Check(!ui.playerHUD.subtitleRoot.activeSelf && !ui.enemyHUD.subtitleRoot.activeSelf,"Both subtitles are hidden for the result presentation.");
            Call(result,"RestoreResultReactions");
            Check(ui.playerHUD.avatar.gameObject.activeSelf && ui.enemyHUD.avatar.gameObject.activeSelf && avatarCanvas.sortingOrder==17 && captionCanvas.sortingOrder==18,"Avatar visibility restores without changing canvas sorting.");
            var snapshot=new BoardSnapshot(); Field(bridge,"state",snapshot); Field(bridge,"approachingEndVoicelinePlayed",false);
            int requests=0; bridge.LocalVoicelineRequested+=r=>requests++;
            Call(bridge,"PlayApproachingEndVoiceline"); Check(requests==0,"No near-end line before the score threshold.");
            snapshot.side0.score=16; snapshot.side1.score=1; Call(bridge,"PlayApproachingEndVoiceline");
            Check(requests==1 && ui.playerHUD.avatarAnimator.GetInteger("Reaction")==3,"Near victory triggers Excited.");
            Call(bridge,"PlayApproachingEndVoiceline"); Check(requests==1,"Near-end line plays only once per match.");
            Field(bridge,"approachingEndVoicelinePlayed",false); snapshot.side0.score=1; snapshot.side1.score=16; Call(bridge,"PlayApproachingEndVoiceline");
            Check(requests==2 && ui.playerHUD.avatarAnimator.GetInteger("Reaction")==4,"Near defeat triggers Concerned.");
            Call(ui,"OnDisable"); bridge.PlayLocalVoiceline(VoicelineEvent.Excited);
            Check(ui.playerHUD.avatarAnimator.GetInteger("Reaction")==0,"Disabled board releases its bridge listener.");
        }
        private static void MissingClips(VoicelinesManager voices){
            var original=voices.miku_idle;
            voices.miku_idle=new[]{null,original[0],null,original[2]};
            for(int i=0;i<20;i++){
                Check(voices.TryGetVoiceline(CharacterClass.HatsuneMiku,VoicelineEvent.Idle,out _,out int index) && (index==1 || index==3),"Sparse arrays preserve their original subtitle indexes.");
            }
            voices.miku_idle=new[]{original[0]};
            Check(voices.TryGetVoiceline(CharacterClass.HatsuneMiku,VoicelineEvent.Idle,out _,out int only) && only==0,"Single-clip arrays play safely.");
            voices.miku_idle=new AudioClip[0];
            Check(!voices.TryGetVoiceline(CharacterClass.HatsuneMiku,VoicelineEvent.Idle,out _,out _),"Empty arrays do not throw.");
            voices.miku_idle=new AudioClip[]{null,null};
            Check(!voices.TryGetVoiceline(CharacterClass.HatsuneMiku,VoicelineEvent.Idle,out _,out _),"Missing clips do not throw.");
            voices.miku_idle=original;
        }
        [MenuItem("Tools/Voicelines/Run Animation and Subtitle Checks")]
        public static void Run(){
            bool passed=false; string result; checks=0; var random=UnityEngine.Random.state;
            try { UnityEngine.Random.InitState(20261024); var voices=Voices(); IndexedAudio(voices); Routing(voices); MissingClips(voices); passed=true; result="PASS"; }
            catch(Exception ex){ result="FAIL\n"+ex; Debug.LogException(ex); }
            finally { UnityEngine.Random.state=random; foreach(var obj in temporary) if(obj) UnityEngine.Object.DestroyImmediate(obj); temporary.Clear(); }
            result+="\nVoiceline assertions: "+checks; Directory.CreateDirectory("Assets/Scripts/VoicelineCheckResults~");
            File.WriteAllText("Assets/Scripts/VoicelineCheckResults~/results.txt",result); Debug.Log(result);
            if(Application.isBatchMode) EditorApplication.Exit(passed ? 0 : 1);
        }
    }
}
#endif
