using System;
using System.Collections.Generic;
using System.Linq;

namespace VocaloidTCG.BoardUI
{
    /// <summary>
    /// Scores card effects using the visible board and the AI's legal choices.
    /// Does not change match state or read the opponent's hidden cards.
    /// </summary>
    public static partial class EnemyCardPolicy
    {
        private static CardState Ally(BoardSnapshot state, int actor, int index) => actor == 0 ? state.tiles[index].side0 : state.tiles[index].side1;
        private static CardState Enemy(BoardSnapshot state, int actor, int index) => Ally(state, 1 - actor, index);
        private static IEnumerable<CardState> Units(BoardSnapshot state, int actor) => state.tiles.SelectMany(t =>
            actor == 0 ? new[] { t.side0, t.assist0 } : new[] { t.side1, t.assist1 }).Where(c => c != null);
        private static int Tile(BoardSnapshot state, CardState c) => c == null ? -1 : Array.FindIndex(state.tiles,
            t => t.side0 == c || t.side1 == c || t.assist0 == c || t.assist1 == c);
        private static int Weight(int index) => index >= 0 && index % 5 == 2 ? 8 : 4;
        private static bool Adjacent(int first, int second) => first >= 0 && second >= 0 && first != second &&
            Math.Abs(first % 5 - second % 5) <= 1 && Math.Abs(first / 5 - second / 5) <= 1;
        private static int HandValue(CardState c) => c.currentInfluence * 4 +
            (c.data.stageEffect && !c.token ? 8 : 4) + (!c.token && c.data.effect != CardEffect.None ? 4 : 0) +
            PerformerKeywords.Of(c).Count * KeywordValue - c.currentCost;
        private static int RevivalValue(CardState c) => Math.Max(c.data.influence, c.data.protectedInfluence) * 4 + 4 +
            (c.data.effect != CardEffect.None ? 4 : 0);
        private static int Gain(BoardSnapshot state, CardState c, int n) => c == null ? 0 : n * Weight(Tile(state, c));
        private static int Damage(BoardSnapshot state, CardState c, int n)
        {
            if (c == null) return 0;
            int after = c.shieldActive && n >= c.currentInfluence ? c.data.influence : Math.Max(0, c.currentInfluence - n);
            return (c.currentInfluence - after) * Weight(Tile(state, c));
        }
        private static bool Contested(BoardSnapshot state, int index) => state.tiles[index].side0 != null && state.tiles[index].side1 != null;

        public static int EntryInfluence(BoardSnapshot state, int actor, CardState card, int tile)
        {
            var d = card.data;
            int neighbors = Units(state, actor).Count(c => Adjacent(tile, Tile(state, c)));
            var keywords = PerformerKeywords.Of(card);
            int harmonize = keywords.harmonize;
            if (state.Side(actor).harmonizeVenue && (actor == 0 ? tile % 5 >= 2 : tile % 5 <= 2)) harmonize = Math.Max(1, harmonize);
            int bonus = neighbors >= 2 ? harmonize : 0;
            if (tile % 5 == 2 && !Units(state, actor).Any(c => Tile(state, c) % 5 == 2)) bonus += keywords.soloist;
            bonus += state.Side(actor).nextPerformerColumns.Count(column => column == tile % 5);
            if (card.token || card.silenced) return bonus;
            bonus += state.Side(actor).nextPerformerInfluence;
            if (d.effect == CardEffect.PreviousRoundLeaveBonus && state.Side(actor).alliesLeftLastRound > 0) bonus++;
            if (d.effect == CardEffect.OpponentSideBonus && (actor == 0 ? tile % 5 > 2 : tile % 5 < 2)) bonus += 2;
            switch (d.effect)
            {
                case CardEffect.FirstPerformerBonus: if (state.Side(actor).performersPlayed == 0) bonus++; break;
                case CardEffect.AdjacentSelfBonus: if (neighbors > 0) bonus += 2; break;
                case CardEffect.TwoAdjacentBonus: if (neighbors >= 2) bonus += 3; break;
                case CardEffect.HighEnergyBonus: if (state.Side(actor).energy - card.currentCost >= 6) bonus += 2; break;
                case CardEffect.SoloCenterBonus:
                    if (tile % 5 == 2 && !Units(state, 0).Concat(Units(state, 1)).Any(c => Tile(state, c) % 5 == 2)) bonus += 3; break;
                case CardEffect.ContestedPenalty: if (Enemy(state, actor, tile) != null) bonus--; break;
            }
            bonus += DuetEntryInfluence(state, actor, card, tile);
            return bonus;
        }

        public static int PerformerBenefit(BoardSnapshot state, int actor, CardState card, int tile)
        {
            var friends = Units(state, actor).ToArray();
            var nearby = friends.Where(c => Adjacent(tile, Tile(state, c))).ToArray();
            var enemies = Units(state, 1 - actor).ToArray();
            var d = card.data;
            var keywords = PerformerKeywords.Of(card);
            int value = (keywords.encore ? 4 : 0) + (keywords.nimble ? 3 : 0) + (keywords.regen ? 2 : 0);
            if (card.token || card.silenced) return value;
            value += ExtendedPerformerBenefit(state, actor, card, tile);
            value += DuetPerformerBenefit(state, actor, card, tile);
            switch (d.effect)
            {
                case CardEffect.ShareInfluence: case CardEffect.ShareHalfInfluence:
                    value += nearby.Length == 0 ? 0 : nearby.Max(c => Weight(Tile(state, c))) *
                        Math.Max(0, card.currentInfluence) / (d.effect == CardEffect.ShareHalfInfluence ? 2 : 1); break;
                case CardEffect.AdjacentAura: value += nearby.Sum(c => Gain(state, c, 1)); break;
                case CardEffect.ContestedAdjacentAura: value += nearby.Where(c => Contested(state, Tile(state, c))).Sum(c => Gain(state, c, 1)); break;
                case CardEffect.GlobalAura: value += friends.Sum(c => Gain(state, c, 2)) + 2 * Weight(tile); break;
                case CardEffect.ContestedAlliesAura: value += friends.Where(c => Contested(state, Tile(state, c))).Sum(c => Gain(state, c, 1)); break;
                case CardEffect.BuffAlly: value += friends.Length == 0 ? 0 : friends.Max(c => Gain(state, c, 1)); break;
                case CardEffect.CreateAdjacentToken: value += Enumerable.Range(0, 25).Any(index => Adjacent(tile, index) && Ally(state, actor, index) == null) ? 3 : 0; break;
                case CardEffect.CreateTwoTokens: value += 10; break;
                case CardEffect.DiscardToPlay:
                    value -= state.Side(actor).hand.Where(c => c != card).Select(HandValue).DefaultIfEmpty(20).Min(); break;
                case CardEffect.WeakenOpposingTile: value += Damage(state, Enemy(state, actor, tile), 2); break;
                case CardEffect.WeakenFront:
                    int front = tile % 5 + (actor == 0 ? 1 : -1);
                    if (front >= 0 && front < 5) value += Damage(state, Enemy(state, actor, tile / 5 * 5 + front), 1); break;
                case CardEffect.WeakenEnemyCenter: value += enemies.Where(c => Tile(state, c) % 5 == 2).Sum(c => Damage(state, c, 1)); break;
                case CardEffect.DiscountSpell: value += state.Side(actor).hand.Any(c => c != card && c.data.stageEffect && c.currentCost > 0) ? 6 : 1; break;
                case CardEffect.TaxSpell: case CardEffect.DelayedEnergy: case CardEffect.DelayedSpell: case CardEffect.NewAllyBonus: value += 4; break;
                case CardEffect.DrawAndInherit: value += DrawValue(state, actor, 2); break;
                case CardEffect.DrawAndRevive: value += state.Side(actor).discard.Where(c => c.data.performer && c.data.influence <= 4).Select(HandValue).DefaultIfEmpty(0).Max(); break;
                case CardEffect.WeakenAndRoot: case CardEffect.ReviveAbility: value += AbilityBenefit(state, actor, card); break;
                case CardEffect.GlobalEncore: value += friends.Sum(c => Gain(state, c, Contested(state, Tile(state, c)) ? 2 : 1) + 3); break;
                case CardEffect.DrawWithCenterAllies: if (friends.Count(c => Tile(state, c) % 5 == 2) + (tile % 5 == 2 ? 1 : 0) >= 3) value += DrawValue(state, actor, 1); break;
            }
            return value;
        }

        private static int DrawValue(BoardSnapshot state, int actor, int count) =>
            6 * Math.Max(0, Math.Min(count, Math.Min(state.Side(actor).deckCount, GameplayBoardBridge.MaxHandSize - state.Side(actor).hand.Count)));

        public static int AbilityBenefit(BoardSnapshot state, int actor, CardState c)
        {
            if (c.data.effect >= CardEffect.RuinToken) return DuetAbilityBenefit(state, actor, c);
            if (c.data.effect == CardEffect.ColumnMoveAbility)
                return Units(state, actor).Select(unit => BestRelocation(state, actor, unit, true)).DefaultIfEmpty(0).Max();
            if (c.data.effect == CardEffect.WeakenAndRoot)
                // This ability's legal Enemy choices include primary performers,
                // not their assistants. Valuing an untargetable assistant could
                // otherwise trigger the same empty ability every turn forever.
                return Enumerable.Range(0, 25).Select(index => Enemy(state, actor, index)).Where(e => e != null)
                    .Select(e => Damage(state, e, 2) + (e.rootedRound == state.roundNumber ? 0 : 2)).DefaultIfEmpty(0).Max();
            if (c.data.effect == CardEffect.ReviveAbility)
            {
                bool free = c.data.scoreThreshold > 0 && state.Side(actor).score >= c.data.scoreThreshold;
                return state.Side(actor).discard.Where(d => d.data.performer && !d.token &&
                    (d.data.effect != CardEffect.DiscardToPlay || state.Side(actor).hand.Count > 0) && (free || d.data.cost <= state.Side(actor).energy))
                    .Select(d => Math.Max(0, RevivalValue(d) - (free ? 0 : 2 * d.data.cost))).DefaultIfEmpty(0).Max();
            }
            return 0;
        }

        public static int SpellBenefit(BoardSnapshot state, int actor, CardState spell, int tile, CardState target = null)
        {
            var d = spell.data;
            if (d.effect >= CardEffect.RuinToken)
                return DuetSpellBenefit(state, actor, spell, tile, target);
            if (d.effect >= CardEffect.LeaveSearchPerformer)
                return ExtendedSpellBenefit(state, actor, spell, tile, target);
            var own = Units(state, actor).ToArray();
            var foes = Units(state, 1 - actor).ToArray();
            bool hostile = d.effect == CardEffect.WeakenContested || d.effect == CardEffect.RemoveSmall ||
                d.effect == CardEffect.Remove || d.effect == CardEffect.Silence;
            var t = state.tiles[tile];
            var choices = hostile ? (actor == 0 ? new[] { t.side1, t.assist1 } : new[] { t.side0, t.assist0 }) :
                (actor == 0 ? new[] { t.side0, t.assist0 } : new[] { t.side1, t.assist1 });
            if (target == null && choices.Count(c => c != null) > 1 && IsDirect(d.effect))
                return choices.Where(c => c != null && (d.effect != CardEffect.RemoveSmall || c.currentInfluence <= 2))
                    .Select(c => SpellBenefit(state, actor, spell, tile, c)).DefaultIfEmpty(0).Max();
            target = target ?? choices.FirstOrDefault(c => c != null);
            switch (d.effect)
            {
                case CardEffect.None: return Gain(state, target, d.stageInfluenceChange);
                case CardEffect.BuffRound: return Gain(state, target, 2);
                case CardEffect.BuffContested: return Gain(state, target, 3);
                case CardEffect.BuffAndDraw: return Gain(state, target, 1) + DrawValue(state, actor, 1);
                case CardEffect.DoubleInfluence: return target == null ? 0 : Gain(state, target, target.currentInfluence);
                case CardEffect.ProtectTurn: return target != null && Enemy(state, actor, tile) != null ? 5 : 0;
                case CardEffect.WeakenContested: return Damage(state, target, 2);
                case CardEffect.Remove: case CardEffect.RemoveSmall: return target == null ? 0 : Gain(state, target, target.currentInfluence) + (target.token ? 0 : 6);
                case CardEffect.Silence: return target == null ? 0 : (target.noScoreRound == state.roundNumber ? 0 : Gain(state, target, target.currentInfluence)) + (target.silenced ? 0 : 4);
                case CardEffect.WeakenCenter: return foes.Where(c => Tile(state, c) % 5 == 2).Sum(c => Damage(state, c, 1)) - own.Where(c => Tile(state, c) % 5 == 2).Sum(c => Damage(state, c, 1));
                case CardEffect.WeakenCenterAndScore: return foes.Where(c => Tile(state, c) % 5 == 2).Sum(c => Damage(state, c, 3));
                case CardEffect.BuffCenter: return own.Where(c => Tile(state, c) % 5 == 2).Sum(c => Gain(state, c, d.scoreThreshold > 0 && state.Side(actor).score >= d.scoreThreshold ? 2 : 1));
                case CardEffect.BuffAllAndScore: return own.Sum(c => Gain(state, c, 1) + (Tile(state, c) % 5 == 2 ? 6 : 0));
                case CardEffect.HarmonizeVenue: return state.Side(actor).harmonizeVenue ? 0 : own.Length * 3;
                case CardEffect.HighestInfluenceTile: return own.Select(c => c.currentInfluence).DefaultIfEmpty(0).Max() * Weight(tile) + DrawValue(state, actor, 1);
                case CardEffect.FreePlayAndSearch: return state.Side(actor).hand.Where(c => c != spell && c.data.performer && c.currentCost <= 2).Select(HandValue).DefaultIfEmpty(0).Max() + DrawValue(state, actor, 1);
                case CardEffect.MoveAllToCenter: return own.Select(c => BestMove(state, actor, c, CardEffect.MoveAllToCenter)).Where(v => v > 0).Sum() + foes.Where(c => Tile(state, c) % 5 == 2).Sum(c => Damage(state, c, 1));
                case CardEffect.MoveHome: case CardEffect.MoveToEnemy: case CardEffect.ReturnToCenter:
                    return target == null ? 0 : BestMove(state, actor, target, d.effect);
                case CardEffect.ReturnAndShare:
                    return target == null ? 0 : Math.Max(0, Enumerable.Range(0, 25).Where(index => Ally(state, actor, index) != null || Enemy(state, actor, index) != null)
                        .Select(index => target.currentInfluence * Weight(index)).DefaultIfEmpty(0).Max() - target.currentInfluence * Weight(tile));
                default: return 0;
            }
        }

        private static bool IsDirect(CardEffect e) => e == CardEffect.None || e == CardEffect.BuffRound || e == CardEffect.BuffContested ||
            e == CardEffect.BuffAndDraw || e == CardEffect.DoubleInfluence || e == CardEffect.ProtectTurn || e == CardEffect.WeakenContested ||
            e == CardEffect.RemoveSmall || e == CardEffect.Remove || e == CardEffect.Silence;

        private static int BestMove(BoardSnapshot state, int actor, CardState c, CardEffect effect)
        {
            if (c.rootedRound == state.roundNumber) return 0;
            return Enumerable.Range(0, 25).Where(index => Ally(state, actor, index) == null &&
                (effect == CardEffect.MoveHome ? (actor == 0 ? index % 5 <= 2 : index % 5 >= 2) :
                 effect == CardEffect.MoveToEnemy ? Enemy(state, actor, index) != null : index % 5 == 2))
                .Select(index => MoveValue(state, actor, c, index, effect)).DefaultIfEmpty(0).Max();
        }

        private static int MoveValue(BoardSnapshot state, int actor, CardState c, int to, CardEffect effect)
        {
            int from = Tile(state, c);
            int value = (Weight(to) - (from < 0 ? 0 : Weight(from))) * c.currentInfluence;
            var enemy = Enemy(state, actor, to);
            if (enemy != null) value += c.currentInfluence > enemy.currentInfluence ? 12 : -20;
            if (effect == CardEffect.ReturnToCenter) value += 2 * Weight(to);
            return value;
        }

        public static int ChoiceValue(BoardSnapshot state, PendingCardEffect p, BoardAction action)
        {
            if (action.kind == BoardActionKind.FinishEffect) return 0;
            var card = p.cards.FirstOrDefault(c => c.instanceId == action.cardInstanceId);
            if (p.choice == EffectChoice.Duet) return DuetChoiceValue(state, p, card, action);
            if (p.choice == EffectChoice.Extended) return ExtendedChoiceValue(state, p, card, action);
            if (p.stage == 2) return SpellBenefit(state, p.actor, p.source, p.sourceTile, card);
            if (card != null)
            {
                if (p.choice == EffectChoice.SacrificeAlly) return -SacrificeCost(state, p.actor, card);
                if (p.choice == EffectChoice.RinEnemy)
                    return p.effect == CardEffect.SacrificeRemoveEnemy ? RemovalValue(state, card) : Damage(state, card, 3);
                if (p.choice == EffectChoice.HandDiscard) return -HandValue(card);
                int cost = p.effect == CardEffect.ReviveAbility && !(p.source.data.scoreThreshold > 0 && state.Side(p.actor).score >= p.source.data.scoreThreshold) ? card.data.cost * 2 : 0;
                return (p.choice == EffectChoice.DiscardPerformer ? RevivalValue(card) : HandValue(card)) - cost;
            }
            int tile = action.toRow * 5 + action.toColumn;
            var ally = Ally(state, p.actor, tile);
            switch (p.choice)
            {
                case EffectChoice.AdjacentAlly: return p.source.currentInfluence * Weight(tile);
                case EffectChoice.Ally: return Gain(state, ally, 1);
                case EffectChoice.Enemy: return Damage(state, Enemy(state, p.actor, tile), 2) + (Enemy(state, p.actor, tile).rootedRound == state.roundNumber ? 0 : 2);
                case EffectChoice.OccupiedTile: return p.amount * Weight(tile);
                case EffectChoice.TokenTile: return Weight(tile) * Math.Max(1, p.amount) - (Enemy(state, p.actor, tile) == null ? 0 : 12);
                case EffectChoice.MoveAlly: return BestMove(state, p.actor, ally, CardEffect.MoveAllToCenter);
                case EffectChoice.MoveTile:
                    var selected = Units(state, p.actor).Concat(state.Side(p.actor).hand).Concat(state.Side(p.actor).discard).FirstOrDefault(c => c.instanceId == p.selectedId);
                    return selected == null ? 0 : MoveValue(state, p.actor, selected, tile, p.effect);
                default: return 0;
            }
        }
    }
}
