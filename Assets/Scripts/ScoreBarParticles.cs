using UnityEngine;
using UnityEngine.UI;

namespace VocaloidTCG.BoardUI
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class ScoreBarParticles : MaskableGraphic
    {
        public Color accent = Color.white;
        private Image fill;
        private SideHUD owner;
        private float elapsed;

        protected override void Awake(){
            useLegacyMeshGeneration = false;
            base.Awake();
        }

        public override Texture mainTexture => Texture2D.whiteTexture;

        public static ScoreBarParticles Create(Image fill, SideHUD owner){
            var mask = fill.GetComponent<Mask>();
            if(!mask){
                mask = fill.gameObject.AddComponent<Mask>();
                mask.showMaskGraphic = true;
            }
            var obj = new GameObject("Floating hearts and stars", typeof(RectTransform), typeof(CanvasRenderer));
            obj.layer = fill.gameObject.layer;
            var rect = (RectTransform)obj.transform;
            rect.SetParent(fill.transform, false);
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            var effect = obj.AddComponent<ScoreBarParticles>();
            effect.fill = fill; effect.owner = owner;
            effect.raycastTarget = false;
            return effect;
        }

        private void Update(){
            elapsed += Time.unscaledDeltaTime;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper mesh){
            mesh.Clear();
            if(!fill || !fill.isActiveAndEnabled || !owner || !owner.showBarParticles || fill.fillAmount <= 0) return;
            var area = rectTransform.rect;
            if(area.width <= 0 || area.height <= 0) return;
            float height = area.height * fill.fillAmount;
            int count = Mathf.Clamp(owner.barParticleCount, 0, 40);
            for(int i = 0; i < count; i++){
                float seed = Mathf.Repeat(i * 0.618034f + (owner.transform.GetSiblingIndex() + 1) * 0.173f, 1);
                float radius = Mathf.Min(owner.barParticleSize * (0.65f + seed * 0.7f), area.width * 0.22f);
                float travel = Mathf.Max(1, height + radius * 2);
                float life = Mathf.Repeat(i * 0.381966f + elapsed * Mathf.Max(0, owner.barParticleSpeed) * (0.65f + seed * 0.7f) / travel, 1);
                float drift = Mathf.Sin(elapsed * 0.9f + i * 2.4f) * area.width * 0.08f;
                var center = new Vector2(Mathf.Lerp(area.xMin + radius, area.xMax - radius, seed) + drift,
                    area.yMin - radius + life * travel);
                Color tint = Color.Lerp(accent, Color.white, 0.5f + seed * 0.4f);
                tint.a = Mathf.Clamp01(owner.barParticleOpacity) * Mathf.Sin(life * Mathf.PI) * fill.color.a;
                DrawShape(mesh, center, radius, tint, i % 2 == 0, elapsed * 0.2f + i);
            }
        }

        private static void DrawShape(VertexHelper mesh, Vector2 center, float radius, Color tint, bool heart, float angle){
            int start = mesh.currentVertCount;
            mesh.AddVert(center, tint, Vector2.zero);
            int segments = heart ? 32 : 10;
            float rotation = heart ? Mathf.Sin(angle) * 0.18f : angle;
            float cos = Mathf.Cos(rotation), sin = Mathf.Sin(rotation);
            for(int j = 0; j <= segments; j++){
                float t = j * Mathf.PI * 2 / segments;
                Vector2 point;
                if(heart){
                    float x = Mathf.Sin(t);
                    point = new Vector2(x * x * x,
                        (13 * Mathf.Cos(t) - 5 * Mathf.Cos(2 * t) - 2 * Mathf.Cos(3 * t) - Mathf.Cos(4 * t) + 2) / 17f);
                }else{
                    float size = j % 2 == 0 ? 1 : 0.43f;
                    point = new Vector2(Mathf.Sin(t), Mathf.Cos(t)) * size;
                }
                point = new Vector2(point.x * cos - point.y * sin, point.x * sin + point.y * cos);
                mesh.AddVert(center + point * radius, tint, Vector2.zero);
                if(j > 0) mesh.AddTriangle(start, start + j, start + j + 1);
            }
        }
    }
}
