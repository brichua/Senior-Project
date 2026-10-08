using UnityEngine;
using UnityEngine.UI;

namespace VocaloidTCG.BoardUI
{
    public enum ConcertEffectStyle { Opening, Redraw, Victory, Finish, RedrawButton, LobbyStar }

    [System.Serializable]
    public sealed class ConcertEffectSettings
    {
        public bool enabled = true;
        [Range(0, 2)] public float intensity = 1;
        [Range(0.1f, 3)] public float speed = 1;
        [Range(0, 120)] public int particleCount = 60;
    }

    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class ConcertEffects : MaskableGraphic
    {
        public Color left = Color.cyan, right = Color.yellow;
        public bool split = true;
        public bool confirm;
        private float clock;
        private GameplayBoardBridge bridge;
        [SerializeField] private ConcertEffectStyle style;
        [SerializeField] private ConcertEffectSettings settings = new ConcertEffectSettings();
        [Header("Lobby star")]
        [SerializeField, Min(10)] private float lobbyStarRadius = 115;
        [SerializeField, Min(20)] private float lobbyOrbitRadius = 220;
        private float lobbyTransition = -1;
        private struct Burst { public Vector2 position; public Color tint; public float start; }
        private readonly System.Collections.Generic.List<Burst> bursts = new System.Collections.Generic.List<Burst>(8);

        protected override void Awake(){
            useLegacyMeshGeneration = false;
            base.Awake();
        }

        public override Texture mainTexture => Texture2D.whiteTexture;

        public static ConcertEffects Create(Transform parent, BoardUIController board, Color left, Color right,
            ConcertEffectStyle style, ConcertEffectSettings settings){
            var rect = ConcertBackdrop.Rect(parent, "Concert sparkles and ribbons", Vector2.zero, new Vector2(1920, 1080));
            var effect = rect.gameObject.AddComponent<ConcertEffects>();
            effect.raycastTarget = false; effect.left = left; effect.right = right;
            effect.style = style; effect.settings = settings ?? new ConcertEffectSettings();
            effect.split = style == ConcertEffectStyle.Opening || style == ConcertEffectStyle.Redraw;
            effect.bridge = board ? board.game as GameplayBoardBridge : null;
            effect.color = Color.white;
            effect.canvasRenderer.SetColor(Color.white);
            effect.SetAllDirty();
            return effect;
        }

        private void Update(){
            if(bridge && bridge.IsPaused) return;
            clock += Time.unscaledDeltaTime * Mathf.Clamp(settings.speed, 0.1f, 3);
            if(lobbyTransition >= 0) lobbyTransition += Time.unscaledDeltaTime;
            bursts.RemoveAll(b => clock - b.start >= 1.5f);
            SetVerticesDirty();
        }

        public void EmitBurst(Vector2 position, Color tint){
            if(bursts.Count >= 8) bursts.RemoveAt(0);
            bursts.Add(new Burst { position = position, tint = tint, start = clock });
            SetVerticesDirty();
        }

        public void BeginLobbyTransition(){ lobbyTransition = 0; SetVerticesDirty(); }
        public void ResetLobbyTransition(){ lobbyTransition = -1; SetVerticesDirty(); }

        protected override void OnPopulateMesh(VertexHelper mesh){
            mesh.Clear();
            if(settings == null || !settings.enabled || settings.intensity <= 0) return;
            if(style == ConcertEffectStyle.Opening) Opening(mesh);
            else if(style == ConcertEffectStyle.Redraw) Redraw(mesh);
            else if(style == ConcertEffectStyle.RedrawButton) RedrawButton(mesh);
            else if(style == ConcertEffectStyle.LobbyStar) LobbyStar(mesh);
            else Result(mesh);
            DrawBursts(mesh);
        }

        private void LobbyStar(VertexHelper mesh)
        {
            float charge = lobbyTransition < 0 ? 0 : Mathf.Clamp01(lobbyTransition / 0.85f);
            float release = lobbyTransition < 0 ? 0 : Mathf.Clamp01((lobbyTransition - 0.85f) / 0.95f);
            float pulse = 1 + 0.055f * Mathf.Sin(clock * 2);
            float radius = lobbyStarRadius * pulse * (1 - charge * 0.22f + release * release * 3);
            float rotation = Mathf.Sin(clock * 0.45f) * 0.13f + charge * charge * Mathf.PI * 2;
            Color blend = Color.Lerp(left, right, 0.5f);
            Glow(mesh, Vector2.zero, radius * 2.5f, Tint(blend, 0.26f + charge * 0.18f));

            int start = mesh.currentVertCount;
            mesh.AddVert(Vector2.zero, Tint(Color.Lerp(blend, Color.white, 0.65f), 0.8f), Vector2.zero);
            for(int i = 0; i <= 10; i++){
                float angle = Mathf.PI / 2 - i * Mathf.PI / 5 + rotation;
                Vector2 point = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius * (i % 2 == 0 ? 1 : 0.45f);
                Color tint = Color.Lerp(left, right, Mathf.InverseLerp(-radius, radius, point.x));
                mesh.AddVert(point, Tint(tint, 0.4f + charge * 0.3f), Vector2.zero);
                if(i > 0) mesh.AddTriangle(start, start + i, start + i + 1);
            }
            for(int i = 0; i < 10; i++){
                float a = Mathf.PI / 2 - i * Mathf.PI / 5 + rotation;
                float b = a - Mathf.PI / 5;
                Vector2 from = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius * (i % 2 == 0 ? 1 : 0.45f);
                Vector2 to = new Vector2(Mathf.Cos(b), Mathf.Sin(b)) * radius * (i % 2 == 0 ? 0.45f : 1);
                Color tint = Color.Lerp(left, right, Mathf.InverseLerp(-radius, radius, (from.x + to.x) * 0.5f));
                Line(mesh, from, to, 10, Tint(tint, 0.12f));
                Line(mesh, from, to, 2, Tint(Color.Lerp(tint, Color.white, 0.45f), 0.9f));
                if(i % 2 == 0) Flare(mesh, from, 5 + charge * 4, tint, i);
            }

            float orbit = lobbyOrbitRadius * (1 - charge * 0.82f) + release * release * 650;
            for(int band = 0; band < 2; band++){
                Color tint = band == 0 ? left : right;
                float tilt = band == 0 ? 0.55f : -0.55f;
                float head = clock * (band == 0 ? 0.7f : -0.6f) + charge * 5;
                for(int i = 0; i < 90; i++){
                    float angle = head - i * Mathf.PI * 2 / 90;
                    Vector2 from = LobbyOrbit(angle, orbit, tilt);
                    Vector2 to = LobbyOrbit(angle - Mathf.PI * 2 / 90, orbit, tilt);
                    Line(mesh, from, to, 1.4f, Tint(tint, (1 - i / 90f) * 0.55f * (1 - release)));
                }
                Star(mesh, LobbyOrbit(head, orbit, tilt), 9, Tint(tint, 0.9f * (1 - release)));
            }
            for(int i = 0; i < Mathf.Clamp(settings.particleCount, 0, 120); i++){
                float seed = i * 2.399963f;
                float angle = seed + clock * (i % 2 == 0 ? 0.18f : -0.15f) + charge * charge * 4;
                float distance = lobbyOrbitRadius * (0.6f + (i % 13) / 22f) * (1 - charge * 0.9f)
                    + release * release * (300 + i % 7 * 65);
                Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                Vector2 point = direction * distance;
                float twinkle = 0.5f + 0.5f * Mathf.Sin(clock * (1.5f + i % 3 * 0.4f) + seed);
                Color tint = Tint(i % 2 == 0 ? left : right, (0.15f + twinkle * 0.65f) * (1 - release));
                if(i % 3 == 0) Star(mesh, point, 2 + twinkle * 5, tint);
                else Glow(mesh, point, 2 + twinkle * 2, tint);
                if(release > 0) Trail(mesh, point, direction, release * 55, tint);
            }
            if(release > 0){
                Glow(mesh, Vector2.zero, 160 + release * 650, Tint(blend, Mathf.Sin(release * Mathf.PI) * 0.5f));
                Flare(mesh, Vector2.zero, 15 + release * 65, blend, 0);
            }
        }

        private static Vector2 LobbyOrbit(float angle, float radius, float tilt)
        {
            float x = Mathf.Cos(angle) * radius, y = Mathf.Sin(angle) * radius * 0.42f;
            return new Vector2(x * Mathf.Cos(tilt) - y * Mathf.Sin(tilt), x * Mathf.Sin(tilt) + y * Mathf.Cos(tilt));
        }

        private void Rings(VertexHelper mesh){
            for(int side = 0; side < (split ? 2 : 1); side++){
                Color tint = side == 0 ? left : right;
                Vector2 center = new Vector2(split ? (side == 0 ? -460 : 460) : 0, 20);
                for(int ring = 0; ring < 3; ring++){
                    Vector2 previous = Vector2.zero;
                    for(int i = 0; i <= 120; i++){
                        float angle = i / 120f * Mathf.PI * 2 + clock * (ring == 1 ? -0.12f : 0.08f);
                        float radius = split ? 360 + ring * 26 : 560 + ring * 28;
                        var point = center + new Vector2(Mathf.Cos(angle) * radius,
                            Mathf.Sin(angle) * (ring == 2 ? 330 : 150) + Mathf.Cos(angle) * (ring - 1) * 120);
                        if(i > 0 && (ring != 2 || i % 3 != 0)){
                            var color = Tint(tint, 0.12f + 0.22f * Mathf.Pow(Mathf.Sin(angle + clock), 2));
                            Line(mesh, previous, point, ring == 2 ? 1.2f : 2f, color);
                        }
                        previous = point;
                    }
                }
            }
        }

        private void Opening(VertexHelper mesh){
            Rings(mesh);
            Color centerTint = Color.Lerp(left, right, 0.5f);
            Circle(mesh, Vector2.zero, 160, centerTint, false);
            Circle(mesh, Vector2.zero, 172, centerTint, true);
            FadedLine(mesh, new Vector2(0, -350), new Vector2(0, 350), centerTint);
            
            for(int i = 0; i < 8; i++){
                float angle = i * Mathf.PI / 4 + 0.18f;
                Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                FadedLine(mesh, direction * 25, direction * (i % 2 == 0 ? 260 : 210), i < 4 ? right : left);
            }
            
            Flare(mesh, Vector2.zero, 24, centerTint, 0);
            Flare(mesh, new Vector2(0, 185), 12, centerTint, 1);
            Flare(mesh, new Vector2(0, -200), 14, centerTint, 2);
            
            for(int i = 0; i < Mathf.Clamp(settings.particleCount, 0, 120); i++){
                float seed = i * 2.399963f;
                var pos = new Vector2(Mathf.Sin(seed) * 870, Mathf.Cos(seed * 1.73f) * 460);
                pos.y += Mathf.Sin(clock * 0.5f + seed) * 18;
                Color tint = split && pos.x > 0 ? right : left;
                tint = Color.Lerp(tint, Color.white, i % 3 == 0 ? 0.8f : 0.3f);
                float pulse = 0.5f + 0.5f * Mathf.Sin(clock * (1.1f + i % 3 * 0.3f) + seed);
                tint = Tint(tint, 0.15f + pulse * 0.7f);
                float size = (i % 5 == 0 ? 18 : 5) * (0.4f + pulse * 0.6f);
                Star(mesh, pos, size, tint);
            }

            for(int i = 0; i < Mathf.Min(settings.particleCount / 5, 18); i++){
                float t = Mathf.Repeat(clock * 0.25f + i * 0.173f, 1);
                float side = i % 2 == 0 ? -1 : 1;
                Vector2 start = new Vector2(side * (760 + i % 3 * 45), 470 - i % 4 * 80);
                Vector2 end = new Vector2(side * 70, -200 + i % 3 * 100);
                Vector2 position = Vector2.Lerp(start, end, t);
                Color tint = Tint(side < 0 ? left : right, Mathf.Sin(t * Mathf.PI) * 0.7f);
                Trail(mesh, position, (end - start).normalized, 95, tint);
                Star(mesh, position, 10, tint);
            }
        }

        private void Redraw(VertexHelper mesh){
            Color centerTint = Color.Lerp(left, right, 0.5f);
            FadedLine(mesh, new Vector2(0, 155), new Vector2(0, 505), centerTint);
            FadedLine(mesh, new Vector2(0, -355), new Vector2(0, -20), centerTint);
            Ornament(mesh, new Vector2(0, 140), 135, centerTint);
            Ornament(mesh, new Vector2(0, -10), 135, centerTint);
            Flare(mesh, new Vector2(0, 360), 12, left, 1);
            Flare(mesh, new Vector2(0, -240), 12, right, 2);
            Ornament(mesh, new Vector2(0, -510), 290, centerTint);
            
            for(int side = 0; side < 2; side++){
                float x = side == 0 ? -520 : 520;
                Color tint = side == 0 ? left : right;
                Ornament(mesh, new Vector2(x, 245), 155, tint);
                Flare(mesh, new Vector2(x, 380), 18, tint, side);
                RedrawBox(mesh, new Vector2(x, -220), tint);
                int count = Mathf.Clamp(settings.particleCount / 2, 0, 60);
                for(int i = 0; i < count; i++){
                    float angle = clock * (i % 2 == 0 ? 0.35f : -0.25f) + i * 2.399963f;
                    Vector2 position = new Vector2(x + Mathf.Cos(angle) * (335 + i % 3 * 18),
                        65 + Mathf.Sin(angle) * (170 + i % 3 * 15));
                    float pulse = 0.5f + 0.5f * Mathf.Sin(clock * 2 + i);
                    Star(mesh, position, 3 + pulse * (i % 4 == 0 ? 9 : 3), Tint(tint, 0.2f + pulse * 0.5f));
                }
                for(int i = 0; i < Mathf.Min(4, count); i++){
                    float t = Mathf.Repeat(clock * 0.11f + i * 0.25f, 1);
                    Vector2 position = BoxEdge(t) + new Vector2(x, -220);
                    Star(mesh, position, 13, Tint(Color.Lerp(tint, Color.white, 0.4f), 0.8f));
                    Trail(mesh, position, (BoxEdge(t) - BoxEdge(Mathf.Repeat(t - 0.002f, 1))).normalized, 30, Tint(tint, 0.6f));
                }
            }
        }

        private static Vector2 BoxEdge(float t){
            float distance = t * 1980;
            if(distance < 720) return new Vector2(-360 + distance, 135);
            if(distance < 990) return new Vector2(360, 135 - (distance - 720));
            if(distance < 1710) return new Vector2(360 - (distance - 990), -135);
            return new Vector2(-360, -135 + distance - 1710);
        }

        private void RedrawBox(VertexHelper mesh, Vector2 center, Color tint){
            for(int i = 0; i < 132; i++){
                Vector2 from = center + BoxEdge(i / 132f);
                Vector2 to = center + BoxEdge((i + 1) / 132f);
                Line(mesh, from, to, 10, Tint(tint, 0.08f));
                Line(mesh, from, to, 5, Tint(tint, 0.18f));
                Line(mesh, from, to, 1.6f, Tint(Color.Lerp(tint, Color.white, 0.45f), 0.9f));
                if(i % 2 == 0){
                    Vector2 inset = Vector2.Scale(BoxEdge(i / 132f), new Vector2(0.966f, 0.91f));
                    Glow(mesh, center + inset, 1.8f, Tint(tint, 0.9f));
                }
            }
            
            for(int x = -1; x <= 1; x += 2) for(int y = -1; y <= 1; y += 2){
                Vector2 corner = center + new Vector2(x * 350, y * 125);
                Flare(mesh, corner, 8, tint, x + y);
                Line(mesh, corner - new Vector2(x * 24, 0), corner, 2, Tint(tint, 0.85f));
                Line(mesh, corner, corner - new Vector2(0, y * 24), 2, Tint(tint, 0.85f));
            }
            
            Flare(mesh, center, 12, tint, 0);
            Flare(mesh, center + Vector2.up * 135, 10, tint, 1);
            Flare(mesh, center - Vector2.up * 135, 10, tint, 2);
        }

        private void RedrawButton(VertexHelper mesh){
            Glow(mesh, Vector2.zero, 77, Tint(left, 0.18f));
            Circle(mesh, Vector2.zero, 63, left, false);
            Circle(mesh, Vector2.zero, 69, left, true);
            Circle(mesh, Vector2.zero, 55, left, false);
            
            for(int i = 0; i < 96; i++){
                float a = i * Mathf.PI * 2 / 96 + clock * 0.25f;
                float b = (i + 1) * Mathf.PI * 2 / 96 + clock * 0.25f;
                Vector2 from = new Vector2(Mathf.Cos(a) * 90, Mathf.Sin(a) * 23 + Mathf.Cos(a) * 37);
                Vector2 to = new Vector2(Mathf.Cos(b) * 90, Mathf.Sin(b) * 23 + Mathf.Cos(b) * 37);
                Line(mesh, from, to, 1, Tint(left, 0.55f));
            }
            
            for(int i = 0; i < 3; i++){
                float angle = clock * 0.45f + i * Mathf.PI * 2 / 3;
                Flare(mesh, new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 66, 4, left, i);
            }
            
            Color core = Tint(left, 1);
            if(confirm){
                ButtonStroke(mesh, new Vector2(-22, 0), new Vector2(-5, -16), core);
                ButtonStroke(mesh, new Vector2(-5, -16), new Vector2(25, 20), core);
            }
            else{
                ButtonStroke(mesh, new Vector2(-18, -18), new Vector2(18, 18), core);
                ButtonStroke(mesh, new Vector2(-18, 18), new Vector2(18, -18), core);
            }
        }

        private static void ButtonStroke(VertexHelper mesh, Vector2 from, Vector2 to, Color tint){
            Color halo = tint; halo.a = 0.15f;
            Line(mesh, from, to, 14, halo); Line(mesh, from, to, 5, tint);
        }

        private void Result(VertexHelper mesh){
            bool victory = style == ConcertEffectStyle.Victory;
            Rings(mesh);
            
            for(int i = 0; i < Mathf.Clamp(settings.particleCount, 0, 120); i++){
                float seed = i * 0.618034f;
                float t = Mathf.Repeat(clock * 0.06f + seed, 1);
                float angle = seed * Mathf.PI * 2 + clock * (i % 2 == 0 ? 0.12f : -0.09f);
                float x = Mathf.Cos(angle) * (510 + i % 6 * 55);
                float y = 60 + Mathf.Sin(angle) * (220 + i % 5 * 45) + Mathf.Sin(clock * 0.7f + i) * 18;
                var position = new Vector2(x, y);
                Color tint = Tint(i % 3 == 0 ? Color.white : i % 2 == 0 ? left : right,
                    Mathf.Sin(t * Mathf.PI) * (victory ? 0.75f : 0.35f));
                if(i % 6 == 0) Flare(mesh, position, 6 + i % 5, tint, i);
                else if(i % 3 == 0) Glow(mesh, position, 5, tint);
                else Star(mesh, position, 5 + i % 5 * 2, tint);
            }
            
            float sweep = Mathf.Repeat(clock * 0.2f, 1);
            Star(mesh, new Vector2(Mathf.Lerp(-410, 410, sweep), -270), 22,
                Tint(Color.Lerp(left, Color.white, 0.7f), Mathf.Sin(sweep * Mathf.PI) * 0.85f));
            Ornament(mesh, new Vector2(0, -270), 470, left);
            Circle(mesh, new Vector2(0, -270), 34, left, true);
            Flare(mesh, new Vector2(-325, -270), 12, left, 1);
            Flare(mesh, new Vector2(325, -270), 12, right, 2);
            
            for(int i = 0; i < 12; i++){
                float phase = clock * 0.5f + i * 2.39996f;
                Vector2 p = new Vector2(Mathf.Sin(phase) * 430, -270 + Mathf.Cos(phase * 1.7f) * 28);
                Glow(mesh, p, 4, Tint(left, 0.55f));
            }
        }

        private void Ornament(VertexHelper mesh, Vector2 center, float halfWidth, Color tint){
            FadedLine(mesh, center - Vector2.right * halfWidth, center + Vector2.right * halfWidth, tint);
            Flare(mesh, center, 12, tint, center.y * 0.01f);
            for(int side = -1; side <= 1; side += 2){
                Vector2 p = center + Vector2.right * halfWidth * 0.86f * side;
                Quad(mesh, p + Vector2.up * 3, p + Vector2.right * 3, p - Vector2.up * 3, p - Vector2.right * 3, Tint(tint, 0.9f));
            }
        }

        private void Flare(VertexHelper mesh, Vector2 center, float size, Color tint, float phase){
            float pulse = 0.75f + 0.25f * Mathf.Sin(clock * 1.8f + phase);
            Glow(mesh, center, size * 4, Tint(tint, 0.2f * pulse));
            Glow(mesh, center, size * 1.5f, Tint(tint, 0.5f * pulse));
            Star(mesh, center, size * pulse, Tint(Color.Lerp(tint, Color.white, 0.8f), 0.95f));
        }

        private static void Glow(VertexHelper mesh, Vector2 center, float radius, Color tint){
            int start = mesh.currentVertCount;
            mesh.AddVert(center, tint, Vector2.zero);
            Color edge = tint; edge.a = 0;
            for(int i = 0; i <= 32; i++){
                float angle = i * Mathf.PI * 2 / 32;
                mesh.AddVert(center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius, edge, Vector2.zero);
                if(i > 0) mesh.AddTriangle(start, start + i + 1, start + i);
            }
        }

        private void FadedLine(VertexHelper mesh, Vector2 from, Vector2 to, Color tint){
            for(int i = 0; i < 32; i++){
                float t = (i + 0.5f) / 32;
                Color color = Tint(tint, Mathf.Sin(t * Mathf.PI) * 0.8f);
                Line(mesh, Vector2.Lerp(from, to, i / 32f), Vector2.Lerp(from, to, (i + 1) / 32f), 1.2f, color);
            }
        }

        private void Circle(VertexHelper mesh, Vector2 center, float radius, Color tint, bool dotted){
            for(int i = 0; i < 144; i++){
                if(dotted && i % 2 == 0) continue;
                float a = i * Mathf.PI * 2 / 144;
                float b = (i + 1) * Mathf.PI * 2 / 144;
                Line(mesh, center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius,
                    center + new Vector2(Mathf.Cos(b), Mathf.Sin(b)) * radius, 1,
                    Tint(tint, 0.3f + 0.25f * Mathf.Sin(a + clock * 0.4f) * Mathf.Sin(a + clock * 0.4f)));
            }
        }

        private void DrawBursts(VertexHelper mesh){
            foreach(var burst in bursts){
                float t = Mathf.Clamp01((clock - burst.start) / 1.5f);
                float radius = 20 + 190 * (1 - Mathf.Pow(1 - t, 3));
                for(int i = 0; i < 24; i++){
                    float angle = i * Mathf.PI * 2 / 24;
                    Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                    Vector2 p = burst.position + direction * radius * (i % 2 == 0 ? 1 : 0.65f);
                    Color tint = Tint(Color.Lerp(burst.tint, Color.white, i % 3 * 0.25f), (1 - t) * (1 - t));
                    Trail(mesh, p, direction, 25 * (1 - t), tint);
                    Star(mesh, p, (i % 3 == 0 ? 14 : 7) * (1 - t), tint);
                }
            }
        }

        private Color Tint(Color tint, float alpha){
            tint.a = Mathf.Clamp01(alpha * settings.intensity); return tint;
        }

        private static void Trail(VertexHelper mesh, Vector2 head, Vector2 direction, float length, Color tint){
            for(int segment = 0; segment < 6; segment++){
                Color faded = tint; faded.a *= 1 - segment / 6f;
                Line(mesh, head - direction * length * segment / 6,
                    head - direction * length * (segment + 1) / 6, 2 - segment * 0.25f, faded);
            }
        }

        private static void Line(VertexHelper mesh, Vector2 a, Vector2 b, float width, Color tint){
            Vector2 normal = new Vector2(-(b - a).y, (b - a).x).normalized * width * 0.5f;
            Quad(mesh, a + normal, b + normal, b - normal, a - normal, tint);
        }
        private static void Star(VertexHelper mesh, Vector2 p, float size, Color tint){
            size *= 1.5f;
            Color halo = tint; halo.a *= 0.2f;
            Quad(mesh, p + Vector2.up * size * 1.4f, p + Vector2.right * size * 0.45f,
                p - Vector2.up * size * 1.4f, p - Vector2.right * size * 0.45f, halo);
            tint = Color.Lerp(tint, new Color(1, 1, 1, tint.a), 0.45f);
            Quad(mesh, p + Vector2.up * size, p + Vector2.right * size * 0.16f,
                p - Vector2.up * size, p - Vector2.right * size * 0.16f, tint);
            Quad(mesh, p + Vector2.left * size * 0.72f, p + Vector2.up * size * 0.16f,
                p - Vector2.left * size * 0.72f, p - Vector2.up * size * 0.16f, tint);
        }
        private static void Quad(VertexHelper mesh, Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color tint){
            int start = mesh.currentVertCount;
            mesh.AddVert(a, tint, Vector2.zero); mesh.AddVert(b, tint, Vector2.zero);
            mesh.AddVert(c, tint, Vector2.zero); mesh.AddVert(d, tint, Vector2.zero);
            mesh.AddTriangle(start, start + 1, start + 2); mesh.AddTriangle(start, start + 2, start + 3);
        }
    }
}
