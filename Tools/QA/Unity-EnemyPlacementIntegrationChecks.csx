var report = new System.Collections.Generic.List<string>();
var go = new GameObject("Temporary flag integration checks");
go.hideFlags = HideFlags.HideAndDontSave;
try
{
    var bridge = go.AddComponent<VocaloidTCG.BoardUI.GameplayBoardBridge>();
    bridge.startAutomatically = false;
    bridge.enableDebugLogs = false;
    var ai = go.AddComponent<VocaloidTCG.BoardUI.BasicEnemyAITest>();
    ai.game = bridge;
    var scope = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
    var stateField = typeof(VocaloidTCG.BoardUI.GameplayBoardBridge).GetField("state", scope);
    var turnField = typeof(VocaloidTCG.BoardUI.GameplayBoardBridge).GetField("turnNumber", scope);
    var choose = typeof(VocaloidTCG.BoardUI.EnemyAIController).GetMethod("ChooseAction", scope);
    var vampire = UnityEditor.AssetDatabase.LoadAssetAtPath<VocaloidTCG.CardData>("Assets/Card Obj/Miku/vampire.asset");
    var bug = UnityEditor.AssetDatabase.LoadAssetAtPath<VocaloidTCG.CardData>("Assets/Card Obj/Miku/bug.asset");
    var fps = UnityEditor.AssetDatabase.LoadAssetAtPath<VocaloidTCG.CardData>("Assets/Card Obj/Len/FPS.asset");
    foreach (int actor in new[] { 0, 1 })
    foreach (string scenario in new[] { "Vampire play", "Bug play", "FPS play", "FPS losing", "Vampire move", "FPS move" })
    {
        var state = new VocaloidTCG.BoardUI.BoardSnapshot();
        state.localPlayerId = 1 - actor;
        state.activePlayerId = actor;
        state.Side(actor).energy = 8;
        for (int i = 0; i < 25; i++) state.tiles[i] = new VocaloidTCG.BoardUI.TileState();
        int flank = actor == 0 ? 0 : 4;
        var data = scenario.StartsWith("Vampire") ? vampire : scenario.StartsWith("Bug") ? bug : fps;
        var incoming = new VocaloidTCG.BoardUI.CardState { instanceId = "incoming", ownerId = actor, data = data, currentInfluence = data.influence, currentCost = data.cost };
        var buddy = new VocaloidTCG.BoardUI.CardState { instanceId = "buddy", ownerId = actor, data = bug, currentInfluence = 5 };
        if (scenario.StartsWith("Vampire") || scenario.StartsWith("Bug"))
        {
            var tile = state.tiles[flank];
            if (actor == 0) { tile.side0 = buddy; tile.total0 = 5; } else { tile.side1 = buddy; tile.total1 = 5; }
            if (scenario.StartsWith("Bug"))
            {
                var enemy = new VocaloidTCG.BoardUI.CardState { instanceId = "opponent", ownerId = 1 - actor, data = fps, currentInfluence = 5 };
                if (actor == 0) { tile.side1 = enemy; tile.total1 = 5; } else { tile.side0 = enemy; tile.total0 = 5; }
            }
        }
        else
        {
            var tile = state.tiles[12];
            int strength = scenario == "FPS losing" ? 3 : 1;
            var enemy = new VocaloidTCG.BoardUI.CardState { instanceId = "opponent", ownerId = 1 - actor, data = fps, currentInfluence = strength };
            if (actor == 0) { tile.side1 = enemy; tile.total1 = strength; } else { tile.side0 = enemy; tile.total0 = strength; }
        }
        bool move = scenario.EndsWith("move");
        if (move)
        {
            int sourceX = scenario.StartsWith("Vampire") ? (actor == 0 ? 1 : 3) : (actor == 0 ? 3 : 1);
            int sourceY = scenario.StartsWith("Vampire") ? 0 : 2;
            var tile = state.tiles[sourceY * 5 + sourceX];
            if (actor == 0) { tile.side0 = incoming; tile.total0 = incoming.currentInfluence; }
            else { tile.side1 = incoming; tile.total1 = incoming.currentInfluence; }
        }
        else state.Side(actor).hand.Add(incoming);
        stateField.SetValue(bridge, state);
        turnField.SetValue(bridge, 1);
        var arguments = new object[] { actor, default(VocaloidTCG.BoardUI.BoardAction), default(VocaloidTCG.BoardUI.EnemyActionEvaluation) };
        bool found = (bool)choose.Invoke(ai, arguments);
        var action = (VocaloidTCG.BoardUI.BoardAction)arguments[1];
        var evaluation = (VocaloidTCG.BoardUI.EnemyActionEvaluation)arguments[2];
        if (scenario == "Vampire move")
        {
            if (found) throw new System.Exception("Vampire unnecessarily abandoned support: " + actor);
            report.Add(actor + ": Vampire preserves support and passes");
            continue;
        }
        if (!found) throw new System.Exception("No action for " + scenario + ": " + actor);
        string rejected;
        if (!bridge.CanSubmitFor(actor, action, out rejected)) throw new System.Exception("AI selected illegal action: " + rejected);
        if (scenario == "Vampire play" || scenario == "Bug play")
        {
            if (Mathf.Abs(action.toColumn - flank) > 1 || action.toRow > 1 || action.cardInstanceId != incoming.instanceId)
                throw new System.Exception("AI missed the support neighbor: " + scenario + " " + actor);
        }
        if (scenario == "FPS play" && (action.toColumn != 2 || action.toRow != 2))
            throw new System.Exception("FPS missed the entry target");
        if (scenario == "FPS losing" && action.toColumn == 2 && action.toRow == 2)
            throw new System.Exception("FPS relied on an unimplemented debuff");
        if (scenario == "FPS move" && evaluation.reason.Contains("contest_entry"))
            throw new System.Exception("FPS reused an entry hint while moving");
        if (!move && state.Side(actor).hand.Count != 1) throw new System.Exception("Choosing mutated the hand");
        if (actor == 0 && scenario == "Vampire play")
        {
            typeof(VocaloidTCG.BoardUI.EnemyAIController).GetField("observedTurn", scope).SetValue(ai, 1);
            typeof(VocaloidTCG.BoardUI.EnemyAIController).GetField("wait", scope).SetValue(ai, 0f);
            ai.enableDecisionLogs = false;
            typeof(VocaloidTCG.BoardUI.EnemyAIController).GetMethod("Update", scope).Invoke(ai, null);
            if (state.Side(actor).hand.Count != 0 || state.Side(actor).energy != 6 || state.tiles[action.toRow * 5 + action.toColumn].side0 != incoming)
                throw new System.Exception("Inherited BasicEnemyAITest update did not execute the selected action");
            if (incoming.currentInfluence != vampire.influence) throw new System.Exception("Placement hint changed actual influence");
            var placed = (System.Collections.Generic.Dictionary<string, int>)typeof(VocaloidTCG.BoardUI.GameplayBoardBridge).GetField("placedTurns", scope).GetValue(bridge);
            placed.Clear();
            report.Add("BasicEnemyAITest inherited Update executed a legal support play without applying a fake effect");
        }
        report.Add(actor + ": " + scenario + " -> (" + action.toColumn + "," + action.toRow + "), " + evaluation.reason);
    }
    foreach (int actor in new[] { 0, 1 })
    foreach (string path in new[] { "Assets/Card Obj/Miku/world is mine.asset", "Assets/Card Obj/Miku/odds and ends.asset", "Assets/Card Obj/Len/Butterfly on Your Right Shoulder.asset", "Assets/Card Obj/Len/Chilledren.asset" })
    {
        var data = UnityEditor.AssetDatabase.LoadAssetAtPath<VocaloidTCG.CardData>(path);
        var state = new VocaloidTCG.BoardUI.BoardSnapshot { activePlayerId = actor, localPlayerId = 1 - actor };
        state.Side(actor).energy = 8;
        for (int i = 0; i < 25; i++) state.tiles[i] = new VocaloidTCG.BoardUI.TileState();
        var incoming = new VocaloidTCG.BoardUI.CardState { instanceId = "batch", ownerId = actor, data = data, currentInfluence = data.influence, currentCost = data.cost };
        state.Side(actor).hand.Add(incoming);
        System.Action<int, int, bool> put = (x, y, friendly) => {
            var tile = state.tiles[y * 5 + x];
            int owner = friendly ? actor : 1 - actor;
            var card = new VocaloidTCG.BoardUI.CardState { instanceId = x + "-" + y, ownerId = owner, data = fps, currentInfluence = 4 };
            if (owner == 0) { tile.side0 = card; tile.total0 = 4; } else { tile.side1 = card; tile.total1 = 4; }
        };
        int flank = actor == 0 ? 0 : 4;
        if (data.cardName == "World is Mine") { put(flank, 0, true); put(flank, 2, true); }
        if (data.cardName == "Odds&Ends") put(actor == 0 ? 3 : 1, 2, false);
        if (data.cardName == "Butterfly on Your Right Shoulder") { put(2, 3, false); put(4 - flank, 3, false); }
        stateField.SetValue(bridge, state);
        var arguments = new object[] { actor, default(VocaloidTCG.BoardUI.BoardAction), default(VocaloidTCG.BoardUI.EnemyActionEvaluation) };
        if (!(bool)choose.Invoke(ai, arguments)) throw new System.Exception("No batch action for " + data.cardName);
        var action = (VocaloidTCG.BoardUI.BoardAction)arguments[1];
        var evaluation = (VocaloidTCG.BoardUI.EnemyActionEvaluation)arguments[2];
        string rejected;
        if (action.kind != VocaloidTCG.BoardUI.BoardActionKind.PlayCard || !bridge.CanSubmitFor(actor, action, out rejected))
            throw new System.Exception("Batch card did not choose a legal play");
        if (data.cardName == "World is Mine" && (action.toColumn != (actor == 0 ? 1 : 3) || action.toRow != 1))
            throw new System.Exception("Cluster choice missed its two allies");
        if (data.cardName == "Odds&Ends" && (System.Math.Abs(action.toColumn - (actor == 0 ? 3 : 1)) > 1 || System.Math.Abs(action.toRow - 2) > 1))
            throw new System.Exception("Enemy adjacency choice missed its target");
        if (data.cardName == "Butterfly on Your Right Shoulder" && (action.toColumn != (actor == 0 ? 1 : 3) || action.toRow != 3))
            throw new System.Exception("Front-pressure direction was incorrect");
        if (data.cardName == "Chilledren" && action.toColumn != 2) throw new System.Exception("Solo card missed center");
        report.Add(actor + ": " + data.cardName + " -> (" + action.toColumn + "," + action.toRow + "), " + evaluation.reason);
    }
    return report;
}
finally { UnityEngine.Object.DestroyImmediate(go); }
