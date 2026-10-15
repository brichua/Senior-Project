using UnityEngine;
using UnityEngine.UI;

namespace VocaloidTCG
{
    public sealed class MainMenuAtmosphere : MaskableGraphic
    {
        [Range(0, 1)] public float intensity = 0.65f;
        [Min(1)] public float cycleSeconds = 18f;
        [Min(4)] public float centerBurstInterval = 12f;
        [Range(0, 2)] public float centerBurstIntensity = 1f;
        private Image background;
        private Color originalColor;
        private float elapsed;
        private static readonly Color Ice = new Color(0.73f, 0.91f, 1f);
        private static readonly Color Lilac = new Color(0.92f, 0.76f, 1f);

        public static MainMenuAtmosphere Create(Image image){
            var obj = new GameObject("Concert atmosphere", typeof(RectTransform), typeof(CanvasRenderer));
            obj.transform.SetParent(image.transform, false);
            var rect = (RectTransform)obj.transform;
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            var effect = obj.AddComponent<MainMenuAtmosphere>();
            effect.background = image; effect.originalColor = image.color;
            effect.raycastTarget = false;
            return effect;
        }

        private void Update(){
            elapsed += Time.unscaledDeltaTime;
            if(background){
                float brightness = Mathf.Lerp(200f / 255f, 1f,
                    0.5f + 0.5f * Mathf.Cos(elapsed * Mathf.PI * 2 / Mathf.Max(1, cycleSeconds)));
                background.color = new Color(brightness, brightness, brightness, originalColor.a);
            }
            SetVerticesDirty();
        }

        protected override void OnDisable(){
            if(background) background.color = originalColor;
            base.OnDisable();
        }

        protected override void OnPopulateMesh(VertexHelper mesh){
            mesh.Clear();
            var bounds = rectTransform.rect;
            if(bounds.width <= 0 || bounds.height <= 0) return;
            
            for(int i = 0; i < 5; i++){
                float phase = elapsed * 0.24f + i * 1.8f;
                var center = Point(bounds, 0.1f + i * 0.2f + Mathf.Sin(phase) * 0.035f,
                    0.48f + Mathf.Sin(phase * 0.63f) * 0.12f);
                Glow(mesh, center, new Vector2(bounds.width * 0.18f, bounds.height * 0.34f),
                    i % 2 == 0 ? Ice : Lilac, (0.035f + 0.025f * Mathf.Sin(phase)) * intensity);
            }
            
            for(int i = 0; i < 5; i++){
                float phase = Mathf.Repeat(elapsed / (7f + i * 0.73f) + i * 0.219f, 1f);
                if(phase > 0.34f) continue;
                float t = phase / 0.34f;
                Vector2 head = Point(bounds, Mathf.Lerp(0.28f + i * 0.13f, 0.02f + i * 0.12f, t),
                    Mathf.Lerp(0.96f, 0.48f - i * 0.035f, t));
                Vector2 tail = new Vector2(bounds.width * 0.07f, bounds.height * 0.13f);
                float alpha = Mathf.Sin(t * Mathf.PI) * 0.5f * intensity;
                for(int segment = 0; segment < 8; segment++){
                    float a = segment / 8f, b = (segment + 1) / 8f;
                    Line(mesh, head + tail * a, head + tail * b, bounds.height * 0.0013f,
                        Color.white, alpha * (1 - a) * (1 - a));
                }
                Glow(mesh, head, Vector2.one * bounds.height * 0.008f, Ice, alpha);
            }
            
            for(int i = 0; i < 24; i++){
                float pulse = Mathf.Pow(Mathf.Max(0, Mathf.Sin(elapsed * (0.45f + i % 4 * 0.09f) + i * 2.39996f)), 5);
                Vector2 center = Point(bounds, 0.04f + Mathf.Repeat(i * 0.618034f, 1) * 0.92f,
                    0.38f + Mathf.Repeat(i * 0.381966f, 1) * 0.56f);
                float size = bounds.height * (0.003f + pulse * 0.004f);
                Line(mesh, center - Vector2.up * size, center + Vector2.up * size, size * 0.22f, Color.white, pulse * intensity * 0.55f);
                Line(mesh, center - Vector2.right * size, center + Vector2.right * size, size * 0.22f, Color.white, pulse * intensity * 0.55f);
            }
            
            CenterBurst(mesh, bounds);
            for(int side = 0; side < 2; side++) for(int i = 0; i < 20; i++){
                float x = 0.012f + Mathf.Repeat(i * 0.618034f, 1) * 0.235f;
                
                if(side == 1) x = 1 - x;
                float y = 0.025f + Mathf.Repeat(i * 0.381966f, 1) * 0.17f;
                float phase = elapsed * 1.1f + i * 1.7f + side;
                Vector2 center = Point(bounds, x + Mathf.Sin(phase) * 0.002f, y);
                float alpha = (0.08f + 0.09f * (0.5f + 0.5f * Mathf.Sin(phase))) * intensity;
                Glow(mesh, center, new Vector2(bounds.width * 0.008f, bounds.height * 0.027f),
                    i % 3 == 0 ? Lilac : Ice, alpha);
            }
        }

        private void CenterBurst(VertexHelper mesh, Rect bounds){
            float age = Mathf.Repeat(elapsed + Mathf.Max(4, centerBurstInterval) - 4,
                Mathf.Max(4, centerBurstInterval));
            if(age >= 3) return;
            float t = age / 3f;
            float strength = Mathf.Sin(t * Mathf.PI) * intensity * centerBurstIntensity;
            Vector2 center = Point(bounds, 0.5f, 0.21f);
            Glow(mesh, center, new Vector2(bounds.width * 0.2f, bounds.height * 0.16f), Lilac, strength * 0.24f);
            Glow(mesh, center, Vector2.one * bounds.height * 0.065f, Color.white, strength * 0.5f);

            for(int ring = 0; ring < 3; ring++){
                float progress = (age - ring * 0.3f) / 2.1f;
                if(progress <= 0 || progress >= 1) continue;
                float radius = 0.025f + progress * 0.25f;
                float alpha = Mathf.Sin(progress * Mathf.PI) * (1 - progress) * strength;
                for(int segment = 0; segment < 64; segment++){
                    float a = segment * Mathf.PI * 2 / 64;
                    float b = (segment + 1) * Mathf.PI * 2 / 64;
                    var from = center + new Vector2(Mathf.Cos(a) * bounds.width * radius, Mathf.Sin(a) * bounds.height * radius * 0.23f);
                    var to = center + new Vector2(Mathf.Cos(b) * bounds.width * radius, Mathf.Sin(b) * bounds.height * radius * 0.23f);
                    Line(mesh, from, to, bounds.height * 0.002f, Ice, alpha * 0.7f);
                }
            }

            for(int i = 0; i < 18; i++){
                float angle = i * 2.39996f;
                Vector2 spark = center + new Vector2(Mathf.Cos(angle) * bounds.width * 0.16f * t,
                    bounds.height * (0.08f + 0.2f * Mathf.Repeat(i * 0.618f, 1)) * Mathf.Sin(t * Mathf.PI * 0.65f));
                float size = bounds.height * 0.006f * (1 - t);
                Line(mesh, spark - Vector2.up * size, spark + Vector2.up * size, size * 0.3f, Color.white, strength * 0.75f);
                Line(mesh, spark - Vector2.right * size, spark + Vector2.right * size, size * 0.3f, Ice, strength * 0.75f);
            }
        }

        private static Vector2 Point(Rect rect, float x, float y)
            => new Vector2(rect.xMin + rect.width * x, rect.yMin + rect.height * y);

        private static void Line(VertexHelper mesh, Vector2 from, Vector2 to, float width, Color tint, float alpha){
            Vector2 direction = (to - from).normalized;
            Vector2 normal = new Vector2(-direction.y, direction.x) * width * 0.5f;
            int start = mesh.currentVertCount;
            tint.a = alpha;
            mesh.AddVert(from - normal, tint, Vector2.zero); mesh.AddVert(from + normal, tint, Vector2.zero);
            mesh.AddVert(to + normal, tint, Vector2.zero); mesh.AddVert(to - normal, tint, Vector2.zero);
            mesh.AddTriangle(start, start + 1, start + 2); mesh.AddTriangle(start, start + 2, start + 3);
        }

        private static void Glow(VertexHelper mesh, Vector2 center, Vector2 radius, Color tint, float alpha){
            const int segments = 24;
            int start = mesh.currentVertCount;
            tint.a = alpha; mesh.AddVert(center, tint, Vector2.zero);
            tint.a = 0;
            for(int i = 0; i < segments; i++){
                float angle = i * Mathf.PI * 2 / segments;
                mesh.AddVert(center + new Vector2(Mathf.Cos(angle) * radius.x, Mathf.Sin(angle) * radius.y), tint, Vector2.zero);
            }
            for(int i = 0; i < segments; i++) mesh.AddTriangle(start, start + 1 + i, start + 1 + (i + 1) % segments);
        }
    }
}
