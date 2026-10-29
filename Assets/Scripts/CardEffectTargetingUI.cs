using TMPro;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace VocaloidTCG.BoardUI
{
    public sealed partial class CardEffectTargetingUI : MonoBehaviour
    {
        private BoardUIController board;
        private GameplayBoardBridge bridge;
        private RectTransform root, choices;
        private CardState selected;
        private Button ability;
        
        public void Initialize(BoardUIController owner){
            board=owner; bridge=owner.game as GameplayBoardBridge;
            revealPrefab=owner.revealedCardsPrefab;
            ability=owner.useAbilityButton;
            if(!ability && owner.playerInfo)
                ability=owner.playerInfo.GetComponentsInChildren<Button>(true).FirstOrDefault(b=>b.name=="use ability");
            if(ability){
                ability.onClick.AddListener(ActivateSelectedAbility);
                ability.gameObject.SetActive(false);
            }
        }
        
        public void Select(CardState card){
            selected=card; Refresh();
        }
        
        public void Refresh(){
            ClearChoices();
            RefreshAbility();
            if(!bridge || board.State==null) return;
            ObserveReveal();
            var p=board.State.pendingEffect;
            if(p==null) return;

            BuildRoot();
            bool mine=p.actor==board.State.localPlayerId;
            var info=mine ? board.playerInfo : board.enemyInfo;
            if(info && p.source!=null){
                info.ShowEffect(p.source,mine ? board.setup.player : board.setup.enemy,board.setup,p.prompt);
                info.RefreshLayout();
            }
            
            if(!mine) return;
            Canvas.ForceUpdateCanvases();
            
            var collection=p.cards.Where(c=>!VisibleCard(c)).ToList();
            if(collection.Count>0){
                var panel=StoryUIElements.Panel(root,"Choose card",new Vector2(740,570));
                var viewport=StoryUIElements.Rect(panel.transform,"Viewport",Vector2.zero,new Vector2(720,550));
                viewport.gameObject.AddComponent<RectMask2D>();
                var scroll=panel.AddComponent<ScrollRect>(); scroll.horizontal=false; scroll.movementType=ScrollRect.MovementType.Clamped; scroll.scrollSensitivity=30;
                choices=StoryUIElements.Rect(viewport,"Cards",Vector2.zero,new Vector2(720,Mathf.Max(550,Mathf.CeilToInt(collection.Count/4f)*270)));
                choices.anchorMin=choices.anchorMax=new Vector2(0.5f,1); choices.pivot=new Vector2(0.5f,1);
                scroll.viewport=viewport; scroll.content=choices;
                
                for(int i=0;i<collection.Count;i++){
                    var c=collection[i]; string id=c.instanceId;
                    bool keyword=p.options.Any(o=>o.instanceId==c.instanceId);
                    var button=StoryUIElements.Button(choices,keyword ? c.tokenKeywords.ToString() : "",new Vector2(-267+(i%4)*178,-135-(i/4)*270),()=>Choose(id,-1));
                    var buttonRect=(RectTransform)button.transform; buttonRect.anchorMin=buttonRect.anchorMax=new Vector2(0.5f,1); buttonRect.sizeDelta=new Vector2(168,250);
                    var label=button.GetComponentInChildren<TMP_Text>(); label.fontSize=21; label.richText=false;
                    label.rectTransform.sizeDelta=new Vector2(154,230);
                    if(!keyword && c.data && c.data.cardImage){
                        button.image.sprite=c.data.cardImage; button.image.color=Color.white; button.image.preserveAspect=true;
                    }else if(!keyword) label.text=c.data ? c.data.cardName : "Card";

                }
                scroll.verticalNormalizedPosition=1;
            }
            if(p.optional) StoryUIElements.Button(root,"DONE",new Vector2(0,-420),()=>bridge.TrySubmit(new BoardAction(BoardActionKind.FinishEffect,null,-1,-1,-1,-1)));
        }

        private CardState SelectedPerformer(){
            if(selected==null || !board || board.State==null) return null;
            return board.State.tiles.Where(t=>t!=null).SelectMany(t=>new[]{t.side0,t.side1,t.assist0,t.assist1})
                .FirstOrDefault(c=>c!=null && c.instanceId==selected.instanceId);
        }

        private void Update(){
            RefreshAbility();
            if(!board || !board.isActiveAndEnabled){ ClearReveals(); return; }
            ObserveReveal();
            if(bridge && !bridge.IsPaused) TickReveal(Time.unscaledDeltaTime);
        }

        private void RefreshAbility(){
            var card=SelectedPerformer();
            bool visible=bridge && card!=null && card.ownerId==board.State.localPlayerId && GameplayBoardBridge.HasActivatedAbility(card);
            if(visible && !ability){
                var parent=board.playerInfo ? board.playerInfo.transform : transform;
                ability=StoryUIElements.Button(parent,"USE ABILITY",new Vector2(0,-190),ActivateSelectedAbility);
                ability.name="use ability";
                board.useAbilityButton=ability;
            }
            if(!ability) return;
            var playerClass=board.setup ? board.setup.PlayerClass : null;
            if(playerClass && playerClass.rewardCardBackground && ability.image){
                ability.image.sprite=playerClass.rewardCardBackground;
                ability.image.color=Color.white;
            }
            ability.interactable=visible && board.CanUseAbilities && bridge.CanActivateEffect(card);
            ability.gameObject.SetActive(visible);
        }

        private void ActivateSelectedAbility(){
            var card=SelectedPerformer();
            if(card==null || !ability || !ability.interactable) return;
            if(card.ownerId==board.State.localPlayerId && board.CanUseAbilities && bridge.CanActivateEffect(card))
                bridge.TrySubmit(new BoardAction(BoardActionKind.ActivateEffect,card.instanceId,-1,-1,-1,-1));
            RefreshAbility();
        }

        private bool VisibleCard(CardState card){
            if(board.GetHandView(card.instanceId)) return true;
            return board.State.tiles.Any(t=>t!=null && new[]{t.side0,t.side1,t.assist0,t.assist1}
                .Any(c=>c!=null && c.instanceId==card.instanceId));
        }

        private void Choose(string id,int tile){
            bridge.TrySubmit(new BoardAction(BoardActionKind.ChooseEffect,id,-1,-1,tile<0 ? -1 : tile%5,tile<0 ? -1 : tile/5));
        }
        
        private void BuildRoot(){
            var obj=new GameObject("Card effect selection",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
            obj.transform.SetParent(transform,false); root=(RectTransform)obj.transform;
            var canvas=obj.GetComponent<Canvas>(); canvas.renderMode=RenderMode.ScreenSpaceOverlay; canvas.sortingOrder=81;
            var scaler=obj.GetComponent<CanvasScaler>(); scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution=new Vector2(1920,1080); scaler.matchWidthOrHeight=0.5f;
        }
        
        private void ClearChoices(){
            if(root){
                root.gameObject.SetActive(false);
                if(Application.isPlaying) Destroy(root.gameObject); else DestroyImmediate(root.gameObject);
            }
            root=null;
        }

        public void Clear(){ ClearChoices(); ClearReveals(); }

        private void OnDisable(){
            Clear();
            if(ability) ability.gameObject.SetActive(false);
        }

        private void OnDestroy(){ if(ability) ability.onClick.RemoveListener(ActivateSelectedAbility); }
    }
    public sealed class TileHoverHandler : MonoBehaviour,
    UnityEngine.EventSystems.IPointerEnterHandler
    {
        public SFXManager sfx;

        public void OnPointerEnter(
            UnityEngine.EventSystems.PointerEventData eventData)
        {
            if (sfx)
                sfx.PlayTileHoverSound();
        }

    }
}
