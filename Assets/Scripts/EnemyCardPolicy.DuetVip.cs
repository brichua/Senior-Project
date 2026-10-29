using System;
using System.Collections.Generic;
using System.Linq;

namespace VocaloidTCG.BoardUI
{
    public static partial class EnemyCardPolicy
    {
        private static bool ActiveEffect(CardState c, CardEffect effect) => !c.token && !c.silenced && c.data.effect == effect;

        private static int DuetEntryInfluence(BoardSnapshot state, int actor, CardState card, int tile)
        {
            int value = Units(state, actor).Any(c => ActiveEffect(c, CardEffect.DyingBreath)) ? 1 : 0;
            value += Units(state, actor).Count(c => ActiveEffect(c, CardEffect.RabbitHole) &&
                c.triggerRound != state.roundNumber && Adjacent(tile, Tile(state, c)));
            if (card.data.effect == CardEffect.ColorfulSexy && tile % 5 == 2 && PerformerKeywords.Of(card).Count > 0) value++;
            if (card.data.effect == CardEffect.Projection && (actor == 0 ? tile % 5 > 2 : tile % 5 < 2)) value++;
            return value;
        }

        private static int DuetPerformerBenefit(BoardSnapshot state, int actor, CardState card, int tile)
        {
            var allies = Units(state, actor).ToArray();
            var enemies = Units(state, 1 - actor).ToArray();
            var nearby = allies.Where(c => Adjacent(tile, Tile(state, c))).ToArray();
            switch (card.data.effect)
            {
                case CardEffect.RuinToken: return Contested(state, tile) ? Weight(tile) : 2;
                case CardEffect.ColorfulSexy: return tile % 5 == 2 ? KeywordValue : 0;
                case CardEffect.Doubleganger: return nearby.Sum(c => Gain(state, c, 1)) + (nearby.Length > 0 ? 4 : 0);
                case CardEffect.FruitKiller: return enemies.Select(c => Damage(state, c, 1) +
                    SingleKeywordValue(card, PerformerKeywords.Of(c))).DefaultIfEmpty(0).Max();
                case CardEffect.Leona: return HandRoom(state, actor) > 0 ? ExpectedCreatedCardValue : 0;
                case CardEffect.LoveHate: return allies.Length > 0 ? enemies.Sum(c => Damage(state, c, 2)) / 2 : 0;
                case CardEffect.Luoyang: case CardEffect.WintryWinds: return 3;
                case CardEffect.Makeup: return Enemy(state, actor, tile) == null ? 0 : nearby.Sum(c => Gain(state, c, 1));
                case CardEffect.Ohedo: return nearby.Select(c => Gain(state, c, 1)).DefaultIfEmpty(0).Max() + 2;
                case CardEffect.Projection: return enemies.Any(c => Tile(state, c) % 5 != tile % 5) ? 3 : 0;
                case CardEffect.Pygmalion: return enemies.Where(c => Tile(state, c) == tile).Select(c => Damage(state, c, 1)).DefaultIfEmpty(0).Max() +
                    (Enemy(state, actor, tile) == null ? 0 : 6);
                case CardEffect.Taoyuan: return nearby.Select(c => SingleKeywordValue(card, PerformerKeywords.Of(c))).DefaultIfEmpty(0).Max();
                case CardEffect.UnbalanceHero: return nearby.Select(c => Gain(state, c, 1)).DefaultIfEmpty(0).Max() +
                    enemies.Where(c => Tile(state, c) == tile).Select(c => Damage(state, c, 1)).DefaultIfEmpty(0).Max();
                case CardEffect.Void: return allies.Any(c => c.token && c.currentInfluence == 1) ? 3 : 0;
                case CardEffect.DyingBreath: return allies.Length * 4 + DuetAbilityBenefit(state, actor, card);
                case CardEffect.EgoRenegade: return DuetAbilityBenefit(state, actor, card);
                case CardEffect.LucidDreamscape: return tile % 5 == 2 && Enemy(state, actor, tile) != null ? 8 : 3;
                case CardEffect.YoungYouOut: return allies.Where(c => PerformerKeywords.Of(c).Count >= 2).Sum(c => Gain(state, c, 1)) +
                    (state.Side(actor).hand.Any(c => c != card && PerformerKeywords.Of(c).Count > 0) ? KeywordValue : 0);
                case CardEffect.RabbitHole: return nearby.Length > 0 || state.Side(actor).hand.Any(c => c != card && c.data.performer) ? 6 : 0;
                case CardEffect.Shinjinrui: return allies.Length > 0 ? TokenSpaceValue(state, actor, 1, i => Adjacent(tile, i)) + 3 : 0;
                default: return 0;
            }
        }

        private static int SingleKeywordValue(CardState target, PerformerKeywords offered) =>
            GameplayBoardBridgeKeywordOptions(offered).Select(k => KeywordGain(target, k)).DefaultIfEmpty(0).Max();

        private static IEnumerable<PerformerKeywords> GameplayBoardBridgeKeywordOptions(PerformerKeywords k)
        {
            if (k.stageShare) yield return new PerformerKeywords { stageShare = true };
            if (k.encore) yield return new PerformerKeywords { encore = true };
            if (k.nimble) yield return new PerformerKeywords { nimble = true };
            if (k.regen) yield return new PerformerKeywords { regen = true };
            if (k.harmonize > 0) yield return new PerformerKeywords { harmonize = k.harmonize };
            if (k.soloist > 0) yield return new PerformerKeywords { soloist = k.soloist };
            if (k.protection > 0) yield return new PerformerKeywords { protection = k.protection };
        }

        private static int DuetTargetValue(BoardSnapshot state, int actor, CardEffect effect, CardState card)
        {
            int tile = Tile(state, card);
            var allies = Units(state, actor).ToArray();
            var enemies = Units(state, 1 - actor).ToArray();
            switch (effect)
            {
                case CardEffect.EgoRock: return Damage(state, card, 3) + (card.silenced ? 0 : 4) +
                    (card.currentInfluence <= 3 ? DrawValue(state, actor, 1) : 0);
                case CardEffect.Fragile: return BestRelocation(state, actor, card) + Damage(state, card, 2) + 2;
                case CardEffect.StealKeyword: return GameplayBoardBridgeKeywordOptions(PerformerKeywords.Of(card))
                    .Select(k => KeywordValue + allies.Select(c => KeywordGain(c, k) + ProtectionValue(state, c, 1)).DefaultIfEmpty(0).Max()).DefaultIfEmpty(0).Max();
                case CardEffect.Blender: return ShareValue(state, actor, card);
                case CardEffect.Cendrillon: return BestRelocation(state, actor, card) + Gain(state, card, 1) +
                    allies.Where(c => c != card).Select(c => Gain(state, c, 1)).DefaultIfEmpty(0).Max();
                case CardEffect.Division: return AdjacentMoves(state, card).Where(i => Enemy(state, actor, i) != null)
                    .Select(i => RelocationValue(state, actor, card, i) + 2 * Weight(i)).DefaultIfEmpty(0).Max();
                case CardEffect.OnTheRocks: return AdjacentMoves(state, card).Select(i => RelocationValue(state, actor, card, i) + KeywordValue +
                    (i % 5 != tile % 5 ? DrawValue(state, actor, 1) : 0)).DefaultIfEmpty(0).Max();
                case CardEffect.Quarter: return BestRelocation(state, actor, card) + Gain(state, card, 1);
                case CardEffect.ColorfulMelody: return Enumerable.Range(0, 25).Where(i => Adjacent(tile, i) &&
                    allies.Concat(enemies).Any(c => c != card && Tile(state, c) == i))
                    .Select(i => card.currentInfluence * Weight(i) - SacrificeCost(state, actor, card) + DrawValue(state, actor, 1)).DefaultIfEmpty(0).Max();
                case CardEffect.Hibikase: return Enumerable.Range(0, 25).Where(i => Adjacent(tile, i) &&
                    allies.Concat(enemies).Any(c => Tile(state, c) == i)).Select(i => card.currentInfluence / 2 * Weight(i)).DefaultIfEmpty(0).Max();
                case CardEffect.DropPopCandy: return 6 + 2 * Weight(tile) - SacrificeCost(state, actor, card);
                case CardEffect.HappyHalloween: return DrawValue(state, actor, 2) +
                    (state.Side(actor).discard.Count(c => c.data.performer && !c.token) + (card.token ? 0 : 1) >= 4 ? 3 * Weight(tile) : 0) - SacrificeCost(state, actor, card);
                case CardEffect.Gemini: return enemies.Where(c => c.currentInfluence <= (card.token ? card.tokenInfluence : card.data.influence))
                    .Select(c => Damage(state, c, 3)).DefaultIfEmpty(0).Max() - SacrificeCost(state, actor, card);
                case CardEffect.Pallucination: return enemies.Where(c => Adjacent(tile, Tile(state, c)))
                    .Select(c => BestRelocation(state, actor, c) + Damage(state, c, 1)).DefaultIfEmpty(0).Max() - SacrificeCost(state, actor, card);
                case CardEffect.Futairo: return KeywordValue + DrawValue(state, actor, 1);
                case CardEffect.FairyTaled: return HandRoom(state, actor) > 0 ? RevivalValue(card) + 2 * KeywordValue : 0;
                case CardEffect.Magnet: return Gain(state, card, 2) + allies.Where(c => Adjacent(tile, Tile(state, c))).Sum(c => Gain(state, c, 1)) + (tile % 5 == 2 ? 6 : 0);
                case CardEffect.Wonderland: return Gain(state, card, 2) + enemies.Where(c => Adjacent(tile, Tile(state, c)))
                    .Select(c => Damage(state, c, 2)).DefaultIfEmpty(0).Max();
                default: return 0;
            }
        }

        private static IEnumerable<int> AdjacentMoves(BoardSnapshot state, CardState card) =>
            Enumerable.Range(0, 25).Where(i => Adjacent(Tile(state, card), i) && CanEstimateRelocation(state, card, i));

        private static int DuetSpellBenefit(BoardSnapshot state, int actor, CardState spell, int tile, CardState target)
        {
            var allies = Units(state, actor).ToArray();
            var enemies = Units(state, 1 - actor).ToArray();
            switch (spell.data.effect)
            {
                case CardEffect.Attakaito: return allies.GroupBy(c => Tile(state, c) % 5).Sum(g => g.Select(c => Gain(state, c, 2)).DefaultIfEmpty(0).Max());
                case CardEffect.LastOfMe: return allies.Where(c => Contested(state, Tile(state, c))).Sum(c => Gain(state, c, 2)) + 6;
                case CardEffect.PerfectCrime: return enemies.Where(c => Tile(state, c) == tile).Sum(c => Damage(state, c, 2)) + 6;
                case CardEffect.Hibikase: return allies.Sum(c => Gain(state, c, 1)) + allies.Select(c => DuetTargetValue(state, actor, spell.data.effect, c)).DefaultIfEmpty(0).Max();
                case CardEffect.SelfishPrincess: return state.Side(actor).hand.Where(c => c != spell && (c.token || c.data.performer))
                    .Select(c => ExpectedCreatedCardValue + TokenSpaceValue(state, actor, 1, i => true) - HandValue(c)).DefaultIfEmpty(0).Max();
                case CardEffect.Quarter: return allies.Select(c => DuetTargetValue(state, actor, spell.data.effect, c)).OrderByDescending(v => v).Take(3).Sum();
                default: return (target == null ? allies.Concat(enemies).Where(c => Tile(state, c) == tile) : new[] { target })
                    .Where(c => (spell.data.effect == CardEffect.EgoRock || spell.data.effect == CardEffect.Fragile || spell.data.effect == CardEffect.StealKeyword) ? c.ownerId != actor : c.ownerId == actor)
                    .Select(c => DuetTargetValue(state, actor, spell.data.effect, c)).DefaultIfEmpty(0).Max();
            }
        }

        public static int DyingBreathMoveBenefit(BoardSnapshot state, int actor, CardState card, IEnumerable<int> visited = null)
        {
            int from = Tile(state, card);
            return Enumerable.Range(0, 25).Where(i => (i / 5 == from / 5 || i % 5 == from % 5) &&
                CanEstimateRelocation(state, card, i) && (visited == null || !visited.Contains(i)))
                .Select(i => RelocationValue(state, actor, card, i) + DuetMovementBenefit(state, actor, card, from, i)).DefaultIfEmpty(0).Max();
        }

        private static int DuetAbilityBenefit(BoardSnapshot state, int actor, CardState card)
        {
            if (card.data.effect == CardEffect.EgoRenegade)
                return Units(state, 1 - actor).Select(c => Damage(state, c, 2) +
                    (c.currentInfluence <= 2 && state.Side(actor).energy < 8 ? 2 : 0)).DefaultIfEmpty(0).Max();
            if (card.data.effect == CardEffect.DyingBreath)
                return Units(state, actor).Where(c => c != card).Select(c => DyingBreathMoveBenefit(state, actor, c)).DefaultIfEmpty(0).Max();
            return 0;
        }

        public static int DuetMovementBenefit(BoardSnapshot state, int actor, CardState card, int from, int to)
        {
            int value = from % 5 != to % 5 && !card.visitedColumns.Contains(to % 5) &&
                Units(state, actor).Any(c => ActiveEffect(c, CardEffect.DyingBreath)) ? Weight(to) : 0;
            if (card.token || card.silenced) return value;
            switch (card.data.effect)
            {
                case CardEffect.ColorfulSexy: if (from % 5 != 2 && to % 5 == 2) value += KeywordValue + (PerformerKeywords.Of(card).Count > 0 ? Weight(to) : 0); break;
                case CardEffect.Luoyang: if (from % 5 != 2 && to % 5 == 2) value += Weight(to); break;
                case CardEffect.WintryWinds: if (card.triggerRound != state.roundNumber) value += KeywordValue; break;
                case CardEffect.Ohedo: value += Units(state, actor).Where(c => c != card && (Adjacent(to, Tile(state, c)) || to == Tile(state, c)))
                    .Select(c => Gain(state, c, 1)).DefaultIfEmpty(0).Max(); break;
                case CardEffect.Projection:
                    if (from % 5 != to % 5) value += Units(state, 1 - actor).Where(c => Tile(state, c) % 5 == to % 5).Select(c => Damage(state, c, 1)).DefaultIfEmpty(0).Max(); break;
                case CardEffect.Void: value += Units(state, actor).Where(c => c.token && c.currentInfluence == 1 && CanEstimateRelocation(state, c, from))
                    .Select(c => RelocationValue(state, actor, c, from)).DefaultIfEmpty(0).Max(); break;
            }
            return value;
        }

        private static int DuetChoiceValue(BoardSnapshot state, PendingCardEffect p, CardState card, BoardAction action)
        {
            int actor = p.actor;
            if (p.options.Count > 0 && card != null)
            {
                if (p.effect == CardEffect.StealKeyword) return KeywordValue + Units(state, actor)
                    .Select(c => KeywordGain(c, card.tokenKeywords)).DefaultIfEmpty(0).Max();
                return KeywordGain(p.source, card.tokenKeywords);
            }
            if (card != null)
            {
                if (p.stage >= 90) return p.stage == 90 ? Gain(state, card, 2) : Damage(state, card, Math.Abs(p.amount));
                switch (p.effect)
                {
                    case CardEffect.FruitKiller: return Damage(state, card, 1) + SingleKeywordValue(p.source, PerformerKeywords.Of(card));
                    case CardEffect.EgoRenegade: return Damage(state, card, 2) + (card.currentInfluence <= 2 && state.Side(actor).energy < 8 ? 2 : 0);
                    case CardEffect.DyingBreath: return DyingBreathMoveBenefit(state, actor, card);
                    case CardEffect.Void: return RelocationValue(state, actor, card, p.sourceTile);
                    case CardEffect.Doubleganger: return Gain(state, card, 2);
                    case CardEffect.UnbalanceHero: case CardEffect.Ohedo: return Gain(state, card, 1);
                    case CardEffect.Projection: return Damage(state, card, 1);
                    case CardEffect.SelfishPrincess: return -HandValue(card);
                    case CardEffect.StealKeyword: if (p.stage == 2) return KeywordGain(card, p.keyword) + ProtectionValue(state, card, 1); break;
                    case CardEffect.Gemini: if (p.stage == 1) return Damage(state, card, 3); break;
                    case CardEffect.Wonderland: if (p.stage == 1) return Damage(state, card, 2); break;
                    case CardEffect.Pallucination: if (p.stage == 1) return BestRelocation(state, actor, card) + Damage(state, card, 1); break;
                    case CardEffect.Blender: case CardEffect.Cendrillon:
                        if (p.selected != null) return p.effect == CardEffect.Cendrillon ? Gain(state, card, 1) : Gain(state, card, 1) +
                            Gain(state, p.selected, 1) + KeywordGain(card, PerformerKeywords.Of(p.selected)) + KeywordGain(p.selected, PerformerKeywords.Of(card));
                        break;
                }
                return DuetTargetValue(state, actor, p.effect, card);
            }
            int tile = action.toRow * 5 + action.toColumn;
            if (tile < 0 || tile >= 25) return 0;
            if (p.effect == CardEffect.Shinjinrui || p.effect == CardEffect.SelfishPrincess)
                return Weight(tile) - (Enemy(state, actor, tile) == null ? 0 : 12);
            if (p.effect == CardEffect.ColorfulMelody || p.effect == CardEffect.Hibikase) return p.amount * Weight(tile);
            if (p.selected == null) return 0;
            int value = RelocationValue(state, actor, p.selected, tile) + DuetMovementBenefit(state, p.selected.ownerId, p.selected, Tile(state, p.selected), tile) * (p.selected.ownerId == actor ? 1 : -1);
            if (p.effect == CardEffect.Fragile) value += Damage(state, p.selected, 2);
            if (p.effect == CardEffect.Pallucination) value += Damage(state, p.selected, 1);
            if (p.effect == CardEffect.Cendrillon) value += Weight(tile);
            if (p.effect == CardEffect.Division) value += 2 * Weight(tile);
            if (p.effect == CardEffect.OnTheRocks) value += KeywordValue + (tile % 5 != Tile(state, p.selected) % 5 ? DrawValue(state, actor, 1) : 0);
            if (p.effect == CardEffect.Quarter) value += Weight(tile);
            return value;
        }
    }
}
