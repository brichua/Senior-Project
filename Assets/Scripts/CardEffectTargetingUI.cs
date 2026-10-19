using TMPro;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace VocaloidTCG.BoardUI
{
    public sealed class CardEffectTargetingUI : MonoBehaviour
    {
        private BoardUIController board;
        private GameplayBoardBridge bridge;
        private RectTransform root, choices;
        private Canvas infoCanvas;
        private bool addedInfoCanvas, previousOverride;
        private int previousOrder;
        private CardState selected;
        private Button ability;
        private bool revealExpanded;
        
        public void Initialize(BoardUIController owner){
            board=owner; bridge=owner.game as GameplayBoardBridge;
        }
        
        public void Select(CardState card){
            selected=card; Refresh();
        }
        
        public void Refresh(){
            Clear();
            if(!bridge || board.State==null) return;
            var p=board.State.pendingEffect;
            if(p==null){
                if(board.State.revealedCards.Count>0){
                    BuildRoot(false);
                    StoryUIElements.Button(root,"REVEALED CARDS",new Vector2(0,460),()=>{ revealExpanded=!revealExpanded; Refresh(); });
                    if(revealExpanded){
                        var panel=StoryUIElements.Panel(root,"Last reveal",new Vector2(900,380));
                        var details=board.State.revealMessage+"\n\n"+string.Join("\n\n",board.State.revealedCards.Select(c=>
                            c.data ? c.data.cardName+" | Cost "+c.currentCost+" | INF "+c.currentInfluence+"\n"+c.data.info : ""));
                        var label=StoryUIElements.Text(panel.transform,"Revealed cards",details,Vector2.zero,new Vector2(860,340));
                        label.fontSize=20; label.richText=false;
                    }
                }
                if(selected!=null && selected.ownerId==board.State.localPlayerId && bridge.CanActivateEffect(selected)){
                    if(!root) BuildRoot(false);
                    ability=StoryUIElements.Button(root,"USE ABILITY",new Vector2(0,-410),()=>bridge.TrySubmit(new BoardAction(BoardActionKind.ActivateEffect,selected.instanceId,-1,-1,-1,-1)));
                }
                return;
            }

            BuildRoot(true);
            bool mine=p.actor==board.State.localPlayerId;
            var info=mine ? board.playerInfo : board.enemyInfo;
            if(info && p.source!=null){
                info.ShowEffect(p.source,mine ? board.setup.player : board.setup.enemy,board.setup,p.prompt);
                infoCanvas=info.GetComponent<Canvas>(); addedInfoCanvas=!infoCanvas;
                if(addedInfoCanvas) infoCanvas=info.gameObject.AddComponent<Canvas>();
                previousOrder=infoCanvas.sortingOrder; previousOverride=infoCanvas.overrideSorting;
                infoCanvas.overrideSorting=true; infoCanvas.sortingOrder=82;
            }
            
            if(!mine) return;
            Canvas.ForceUpdateCanvases();
            
            foreach(int index in p.tiles){
                int targetIndex=index;
                var source=board.GetEffectTileView(index); if(!source) continue;
                var copy=Instantiate(source,root,false);
                copy.name="Effect option " + index;
                copy.enabled=false;
                
                foreach(var pointer in copy.GetComponentsInChildren<CardPointer>(true)) pointer.enabled=false;
                foreach(var graphic in copy.GetComponentsInChildren<Graphic>(true)) graphic.raycastTarget=false;
                foreach(var childCanvas in copy.GetComponentsInChildren<Canvas>(true)) childCanvas.overrideSorting=false;
                AlignTileCopy((RectTransform)source.transform,(RectTransform)copy.transform);
                
                if(copy.tile){
                    copy.tile.overrideSprite=null;
                    if(board.setup.movingTile) copy.tile.sprite=board.setup.movingTile;
                    copy.tile.enabled=copy.tile.sprite!=null;
                }
                
                var rect=source.tile ? source.tile.rectTransform : (RectTransform)source.transform;
                var corners=new Vector3[4]; rect.GetWorldCorners(corners);
                var canvas=rect.GetComponentInParent<Canvas>().rootCanvas;
                Camera camera=canvas.renderMode==RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(root,RectTransformUtility.WorldToScreenPoint(camera,corners[0]),null,out var bottom);
                RectTransformUtility.ScreenPointToLocalPointInRectangle(root,RectTransformUtility.WorldToScreenPoint(camera,corners[2]),null,out var top);
                var hit=StoryUIElements.Button(root,"",(top+bottom)*0.5f,()=>Choose(null,targetIndex));
                ((RectTransform)hit.transform).sizeDelta=top-bottom;
                hit.image.color=Color.clear;
                hit.targetGraphic=copy.tile;
                hit.transition=Selectable.Transition.SpriteSwap;
                
                var hover=board.setup.player.hoverTile ? board.setup.player.hoverTile : board.setup.hoverTile;
                var sprites=hit.spriteState;
                sprites.highlightedSprite=hover;
                sprites.pressedSprite=hover;
                sprites.selectedSprite=board.setup.movingTile;
                hit.spriteState=sprites;
            }
            
            if(p.cards.Count>0){
                var panel=StoryUIElements.Panel(root,"Choose card",new Vector2(740,570));
                var viewport=StoryUIElements.Rect(panel.transform,"Viewport",Vector2.zero,new Vector2(720,550));
                viewport.gameObject.AddComponent<RectMask2D>();
                var scroll=panel.AddComponent<ScrollRect>(); scroll.horizontal=false; scroll.movementType=ScrollRect.MovementType.Clamped; scroll.scrollSensitivity=30;
                choices=StoryUIElements.Rect(viewport,"Cards",Vector2.zero,new Vector2(720,Mathf.Max(550,p.cards.Count*70)));
                choices.anchorMin=choices.anchorMax=new Vector2(0.5f,1); choices.pivot=new Vector2(0.5f,1);
                scroll.viewport=viewport; scroll.content=choices;
                
                for(int i=0;i<p.cards.Count;i++){
                    var c=p.cards[i]; string id=c.instanceId;
                    var button=StoryUIElements.Button(choices,c.data.cardName+"  |  Cost "+c.currentCost+"  |  INF "+c.currentInfluence,new Vector2(0,-35-i*70),()=>Choose(id,-1));
                    var buttonRect=(RectTransform)button.transform; buttonRect.anchorMin=buttonRect.anchorMax=new Vector2(0.5f,1); buttonRect.sizeDelta=new Vector2(690,60);
                    var label=button.GetComponentInChildren<TMP_Text>(); label.rectTransform.sizeDelta=new Vector2(670,55); label.fontSize=21; label.richText=false;
                }
                scroll.verticalNormalizedPosition=1;
            }
            if(p.optional) StoryUIElements.Button(root,"DONE",new Vector2(0,-420),()=>bridge.TrySubmit(new BoardAction(BoardActionKind.FinishEffect,null,-1,-1,-1,-1)));
        }

        private void AlignTileCopy(RectTransform source,RectTransform copy){
            var canvas=source.GetComponentInParent<Canvas>().rootCanvas;
            Camera camera=canvas.renderMode==RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            Vector2 origin=OverlayPoint(source.TransformPoint(Vector3.zero),camera);
            Vector2 right=OverlayPoint(source.TransformPoint(Vector3.right),camera)-origin;
            Vector2 up=OverlayPoint(source.TransformPoint(Vector3.up),camera)-origin;
            copy.anchorMin=copy.anchorMax=root.pivot;
            copy.pivot=source.pivot;
            copy.sizeDelta=source.rect.size;
            copy.anchoredPosition=origin;
            copy.localRotation=Quaternion.Euler(0,0,Mathf.Atan2(right.y,right.x)*Mathf.Rad2Deg);
            copy.localScale=new Vector3(right.magnitude,up.magnitude,1);
        }

        private Vector2 OverlayPoint(Vector3 world,Camera camera){
            RectTransformUtility.ScreenPointToLocalPointInRectangle(root,
                RectTransformUtility.WorldToScreenPoint(camera,world),null,out var point);
            return point;
        }

        private void Choose(string id,int tile){
            bridge.TrySubmit(new BoardAction(BoardActionKind.ChooseEffect,id,-1,-1,tile<0 ? -1 : tile%5,tile<0 ? -1 : tile/5));
        }
        
        private void BuildRoot(bool dim){
            var obj=new GameObject("Card effect selection",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
            obj.transform.SetParent(transform,false); root=(RectTransform)obj.transform;
            var canvas=obj.GetComponent<Canvas>(); canvas.renderMode=RenderMode.ScreenSpaceOverlay; canvas.sortingOrder=81;
            var scaler=obj.GetComponent<CanvasScaler>(); scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution=new Vector2(1920,1080); scaler.matchWidthOrHeight=0.5f;
            if(dim){
                var image=StoryUIElements.Rect(root,"Waiting for input",Vector2.zero,Vector2.zero).gameObject.AddComponent<Image>();
                image.rectTransform.anchorMin=Vector2.zero; image.rectTransform.anchorMax=Vector2.one; image.rectTransform.offsetMin=image.rectTransform.offsetMax=Vector2.zero;
                image.color=new Color(0.015f,0.02f,0.055f,0.65f); image.raycastTarget=true;
            }
        }
        
        public void Clear(){
            if(infoCanvas){ infoCanvas.sortingOrder=previousOrder; infoCanvas.overrideSorting=previousOverride; }
            infoCanvas=null; addedInfoCanvas=false;
            if(root){ root.gameObject.SetActive(false); Destroy(root.gameObject); }
            root=null;
        }

        private void OnDisable(){
            Clear();
        }
    }
}
