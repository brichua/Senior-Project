using System;
using System.Collections.Generic;
using System.Linq;

namespace VocaloidTCG.BoardUI
{
    public static partial class EnemyCardPolicy
    {
        private const int ExpectedCreatedCardValue = 6;
        private const int KeywordValue = 3;

        private static int RemovalValue(BoardSnapshot state, CardState card) =>
            card == null ? 0 : Gain(state, card, card.currentInfluence) + (card.token ? 0 : 6);

        private static int SacrificeCost(BoardSnapshot state, int actor, CardState card)
        {
            int cost = RemovalValue(state, card);
            if (state.Side(actor).leaveDiscardScoreVenue) cost -= 6;
            if (!card.token && !card.silenced)
            {
                switch (card.data.effect)
                {
                    case CardEffect.LeaveDelayedToken: cost -= 4; break;
                    case CardEffect.LeaveNextPerformerBonus: cost -= 8; break;
                    case CardEffect.LeaveSearchPerformer:
                        if (state.Side(actor).discard.Count(c => c.data.performer && !c.token) >= 3) cost -= 6;
                        break;
                }
            }
            return cost;
        }

        private static int CheapestSacrifice(BoardSnapshot state, int actor, CardState source) =>
            Units(state, actor).Where(c => c != source).Select(c => SacrificeCost(state, actor, c)).DefaultIfEmpty(100).Min();

        private static int HandRoom(BoardSnapshot state, int actor) =>
            Math.Max(0, GameplayBoardBridge.MaxHandSize - state.Side(actor).hand.Count);

        private static int KeywordGain(CardState target, PerformerKeywords offered)
        {
            var current = PerformerKeywords.Of(target);
            int before = current.Count;
            int levels = current.harmonize + current.soloist + current.protection;
            current.Include(offered);
            return (current.Count - before) * KeywordValue +
                (current.harmonize + current.soloist + current.protection - levels) * 2;
        }

        private static int ProtectionValue(BoardSnapshot state, CardState card, int amount)
        {
            if (card == null || card.silenced || card.shieldActive && card.currentInfluence >= amount) return 0;
            // Protection replaces current influence with a shield; it is not a
            // straight buff to an already strong performer.
            return (amount - card.currentInfluence) * Weight(Tile(state, card)) + 6;
        }

        private static bool CanEstimateRelocation(BoardSnapshot state, CardState card, int to)
        {
            if (card == null || to < 0 || to >= 25 || to == Tile(state, card) || card.rootedRound == state.roundNumber) return false;
            var tile = state.tiles[to];
            var primary = card.ownerId == 0 ? tile.side0 : tile.side1;
            var assistant = card.ownerId == 0 ? tile.assist0 : tile.assist1;
            return primary == null || PerformerKeywords.Of(card).stageShare && assistant == null;
        }

        private static int PositionValue(BoardSnapshot state, CardState card, int tile)
        {
            if (tile < 0) return 0;
            int owner = card.ownerId;
            int friends = Units(state, owner).Where(c => c != card && Tile(state, c) == tile).Sum(c => c.currentInfluence);
            int enemies = Units(state, 1 - owner).Where(c => Tile(state, c) == tile).Sum(c => c.currentInfluence);
            int influence = Math.Max(0, card.currentInfluence);
            int contest = enemies == 0 ? 0 : influence + friends > enemies ? 12 : influence + friends == enemies ? 0 : -12;
            return influence * Weight(tile) + contest;
        }

        private static int RelocationValue(BoardSnapshot state, int actor, CardState card, int to)
        {
            int value = PositionValue(state, card, to) - PositionValue(state, card, Tile(state, card));
            return card.ownerId == actor ? value : -value;
        }

        private static int BestRelocation(BoardSnapshot state, int actor, CardState card, bool sameColumn = false)
        {
            int from = Tile(state, card);
            if (from < 0) return 0;
            return Enumerable.Range(0, 25).Where(to => CanEstimateRelocation(state, card, to) &&
                (sameColumn ? to % 5 == from % 5 : Adjacent(from, to)))
                .Select(to => RelocationValue(state, actor, card, to)).DefaultIfEmpty(0).Max();
        }

        public static int ColumnMoveBenefit(BoardSnapshot state, int actor, CardState card, IEnumerable<int> visited)
        {
            int from = Tile(state, card);
            if (from < 0) return 0;
            return Enumerable.Range(0, 25).Where(to => to % 5 == from % 5 &&
                CanEstimateRelocation(state, card, to) && (visited == null || !visited.Contains(to)))
                .Select(to => RelocationValue(state, actor, card, to)).DefaultIfEmpty(0).Max();
        }

        private static int TokenSpaceValue(BoardSnapshot state, int actor, int influence, Func<int, bool> allowed, int count = 1) =>
            Enumerable.Range(0, 25).Where(tile => Ally(state, actor, tile) == null && allowed(tile))
                .Select(tile => Math.Max(0, influence * Weight(tile) - (Enemy(state, actor, tile) == null ? 0 : 12)))
                .OrderByDescending(value => value).Take(count).Sum();

        private static int ExtendedPerformerBenefit(BoardSnapshot state, int actor, CardState card, int tile)
        {
            var side = state.Side(actor);
            var friends = Units(state, actor).ToArray();
            var enemies = Units(state, 1 - actor).ToArray();
            var hand = side.hand.Where(c => c != card).ToArray();
            switch (card.data.effect)
            {
                case CardEffect.LeaveSearchPerformer:
                    return side.discard.Count(c => c.data.performer && !c.token) >= 3 ? 6 : 2;
                case CardEffect.BackColumnTokens:
                    return Enumerable.Range(0, 25).Count(i => i % 5 == (actor == 0 ? 0 : 4) && Ally(state, actor, i) == null) >= 2 ? 6 : 2;
                case CardEffect.LeaveDelayedToken: return 4;
                case CardEffect.AdjacentLeaveBonus: return friends.Count(c => Adjacent(tile, Tile(state, c))) * 3;
                case CardEffect.DiscardThenDraw:
                    return hand.Length == 0 ? 0 : DrawValue(state, actor, 1) - hand.Min(HandValue);
                case CardEffect.LeaveNextPerformerBonus: return hand.Any(c => c.data.performer) ? 8 : 3;
                case CardEffect.ContestedWinToken: return Enemy(state, actor, tile) == null ? 2 : 6;
                case CardEffect.PreviousRoundLeaveBonus: return 0; // Included in EntryInfluence.
                case CardEffect.SacrificeDrawDiscard:
                    return DrawValue(state, actor, 2) - 4 - CheapestSacrifice(state, actor, card);
                case CardEffect.SacrificeRemoveEnemy:
                    return enemies.Select(c => RemovalValue(state, c)).DefaultIfEmpty(0).Max() - CheapestSacrifice(state, actor, card);
                case CardEffect.RevealDeckKeywords: return 4;
                case CardEffect.CreatedCardDiscount: return 3 + hand.Count(c => c.createdCard && c.currentCost > 0) * 2;
                case CardEffect.CreateFirstClasses: case CardEffect.CreateLastClasses:
                    return HandRoom(state, actor) > 0 ? ExpectedCreatedCardValue : 0;
                case CardEffect.CreateKeywordToken: return HandRoom(state, actor) > 0 ? 8 : 0;
                case CardEffect.RevealHandKeywords:
                    return hand.Where(c => c.data.performer || c.token).Select(c => KeywordGain(card, PerformerKeywords.Of(c))).DefaultIfEmpty(0).Max();
                case CardEffect.KeywordPlayBonus: return hand.Count(c => c.data.performer && PerformerKeywords.Of(c).Count > 0) * Weight(tile);
                case CardEffect.KeywordInfluence:
                    return friends.Sum(c => PerformerKeywords.Of(c).Count * Weight(Tile(state, c))) -
                        enemies.Sum(c => PerformerKeywords.Of(c).Count * Weight(Tile(state, c))) + PerformerKeywords.Of(card).Count * Weight(tile);
                case CardEffect.WanderAndToken: return 5;
                case CardEffect.ShareAllKeywords:
                    var union = PerformerKeywords.Of(card);
                    foreach (var friend in friends) union.Include(PerformerKeywords.Of(friend));
                    return friends.Sum(c => KeywordGain(c, union)) + KeywordGain(card, union);
                case CardEffect.NextColumnBonus:
                    int next = tile % 5 + (actor == 0 ? 1 : -1);
                    return next >= 0 && next < 5 && hand.Any(c => c.data.performer) ? Weight(next) : 0;
                case CardEffect.OtherColumnToken:
                    return TokenSpaceValue(state, actor, 1, i => i % 5 != tile % 5 && (actor == 0 ? i % 5 < 2 : i % 5 > 2)) + 3;
                case CardEffect.LoneCenterProtected:
                    return tile % 5 == 2 && !friends.Concat(enemies).Any(c => Tile(state, c) % 5 == 2) ?
                        (2 - card.currentInfluence) * Weight(tile) + 6 : 0;
                case CardEffect.OpponentSideBonus: return 0; // Included in EntryInfluence.
                case CardEffect.ThreeColumnsDraw:
                    return friends.Select(c => Tile(state, c) % 5).Append(tile % 5).Distinct().Count() >= 3 ? DrawValue(state, actor, 1) : 0;
                case CardEffect.DisplaceContested:
                    return Enemy(state, actor, tile) != null && Enumerable.Range(0, 25).Any(i =>
                        i % 5 == tile % 5 && Math.Abs(i / 5 - tile / 5) == 1 && !Units(state, 0).Concat(Units(state, 1)).Any(c => Tile(state, c) == i)) ? 4 : 0;
                case CardEffect.CrossColumnGreeting:
                    return friends.Concat(enemies).Where(c => Adjacent(tile, Tile(state, c)) && Tile(state, c) % 5 != tile % 5)
                        .Sum(c => Weight(tile) + (c.ownerId == actor ? Weight(Tile(state, c)) : -Weight(Tile(state, c))));
                case CardEffect.AllyGreeting:
                    return friends.Where(c => Adjacent(tile, Tile(state, c))).Sum(c => Weight(tile) + Weight(Tile(state, c)));
                case CardEffect.PerformanceColumnsAura:
                    int columns = friends.Select(c => Tile(state, c) % 5).Append(tile % 5).Distinct().Count();
                    return friends.Where(c => Tile(state, c) % 5 == 2 || (actor == 0 ? Tile(state, c) % 5 > 2 : Tile(state, c) % 5 < 2))
                        .Sum(c => columns * Weight(Tile(state, c)));
                case CardEffect.ColumnMoveAbility: return AbilityBenefit(state, actor, card);
                default: return 0;
            }
        }

        private static int RowValue(BoardSnapshot state, int actor, int row, bool friendly) =>
            Units(state, friendly ? actor : 1 - actor).Where(c => Tile(state, c) / 5 == row)
                .Sum(c => friendly ? Gain(state, c, 2) : Damage(state, c, 2));

        private static int ExtendedSpellBenefit(BoardSnapshot state, int actor, CardState spell, int tile, CardState target)
        {
            var friends = Units(state, actor).ToArray();
            var enemies = Units(state, 1 - actor).ToArray();
            var side = state.Side(actor);
            switch (spell.data.effect)
            {
                case CardEffect.WeakenAfterAllyLeft:
                    return (target == null ? enemies.Where(c => Tile(state, c) == tile) : new[] { target })
                        .Select(c => Damage(state, c, side.alliesLeftLastRound > 0 ? 3 : 2)).DefaultIfEmpty(0).Max();
                case CardEffect.DrawTwoDiscardDrawn: return Math.Max(0, DrawValue(state, actor, 2) - 4);
                case CardEffect.SacrificeWeakenAdjacent:
                    return friends.Where(c => target == null ? Tile(state, c) == tile : c == target)
                        .Select(c => enemies.Where(e => Adjacent(Tile(state, c), Tile(state, e))).Sum(e => Damage(state, e, 2)) - SacrificeCost(state, actor, c)).DefaultIfEmpty(0).Max();
                case CardEffect.OutsideCenterTokens: return TokenSpaceValue(state, actor, 1, i => i % 5 != 2, 2);
                case CardEffect.LeaveDiscardScoreVenue: return side.leaveDiscardScoreVenue ? 0 : Math.Min(4, friends.Length + side.hand.Count(c => c.data.performer)) * 6;
                case CardEffect.SacrificeDrawWeaken:
                    int extra = side.discard.Count(c => c.data.performer && !c.token) >= 4 ? enemies.Select(c => Damage(state, c, 3)).DefaultIfEmpty(0).Max() : 0;
                    return friends.Where(c => target == null ? Tile(state, c) == tile : c == target)
                        .Select(c => DrawValue(state, actor, 2) + extra - SacrificeCost(state, actor, c)).DefaultIfEmpty(0).Max();
                case CardEffect.GrantProtected:
                    return friends.Where(c => Tile(state, c) == tile).Select(c => ProtectionValue(state, c, 2)).DefaultIfEmpty(0).Max();
                case CardEffect.PositionKeywordVenue:
                    return side.positionKeywordVenue ? 0 : friends.Sum(c => Tile(state, c) % 5 == 2 ? 4 : Tile(state, c) % 5 == 0 || Tile(state, c) % 5 == 4 ? 3 : 0) + 3;
                case CardEffect.ReturnAndCopy:
                    return friends.Concat(enemies).Where(c => Tile(state, c) == tile && c.currentInfluence <= 3)
                        .Select(c => ReturnCopyValue(state, actor, c)).DefaultIfEmpty(0).Max();
                case CardEffect.DiscardAndCombine:
                    return side.hand.Where(c => c != spell && (c.data.performer || c.token))
                        .Select(c => TokenSpaceValue(state, actor, 3, i => actor == 0 ? i % 5 <= 2 : i % 5 >= 2) + ExpectedCreatedCardValue - HandValue(c)).DefaultIfEmpty(0).Max();
                case CardEffect.ShareAdjacentKeywords:
                    return friends.Where(c => Tile(state, c) == tile).Select(c => ShareValue(state, actor, c)).DefaultIfEmpty(0).Max();
                case CardEffect.MoveAndCreate: case CardEffect.MoveAndAdjust:
                    return friends.Concat(enemies).Where(c => Tile(state, c) == tile && (c.ownerId == actor || spell.data.effect == CardEffect.MoveAndAdjust))
                        .Select(c => BestRelocation(state, actor, c) + (spell.data.effect == CardEffect.MoveAndCreate ?
                            HandRoom(state, actor) > 0 ? ExpectedCreatedCardValue : 0 : c.ownerId == actor ? Gain(state, c, 1) : Damage(state, c, 1))).DefaultIfEmpty(0).Max();
                case CardEffect.RowInfluence: return Math.Max(RowValue(state, actor, tile / 5, true), RowValue(state, actor, tile / 5, false));
                case CardEffect.SwapAllies:
                    return friends.Where(c => Tile(state, c) == tile).Select(c => SwapValue(state, actor, c)).DefaultIfEmpty(0).Max();
                case CardEffect.FormationBonus:
                    int rowBonus = friends.Any(a => friends.Any(b => a != b && Tile(state, a) / 5 == Tile(state, b) / 5)) ? 8 + Math.Min(1, 8 - side.energy) * 2 : 0;
                    int columnBonus = friends.Any(a => friends.Any(b => a != b && Tile(state, a) % 5 == Tile(state, b) % 5)) ? 12 + DrawValue(state, actor, 1) : 0;
                    return rowBonus + columnBonus;
                case CardEffect.StrongestColumns:
                    return friends.GroupBy(c => Tile(state, c) % 5).Sum(group => group.Max(c => Gain(state, c, 2) + KeywordValue));
                default: return 0;
            }
        }

        private static int ReturnCopyValue(BoardSnapshot state, int actor, CardState card) =>
            (card.ownerId == actor ? -RemovalValue(state, card) : RemovalValue(state, card) - 6) +
            (HandRoom(state, actor) > 0 ? ExpectedCreatedCardValue * (card.ownerId == actor ? 2 : 1) : 0);

        private static int ShareValue(BoardSnapshot state, int actor, CardState card) =>
            Units(state, actor).Where(other => Adjacent(Tile(state, card), Tile(state, other)))
                .Select(other => Gain(state, card, 1) + Gain(state, other, 1) +
                    KeywordGain(card, PerformerKeywords.Of(other)) + KeywordGain(other, PerformerKeywords.Of(card))).DefaultIfEmpty(0).Max();

        private static int SwapValue(BoardSnapshot state, int actor, CardState first, CardState second = null)
        {
            if (first.rootedRound == state.roundNumber) return 0;
            return Units(state, actor).Where(other => other != first && (second == null || other == second) &&
                other.rootedRound != state.roundNumber && Tile(state, first) != Tile(state, other))
                .Select(other => RelocationValue(state, actor, first, Tile(state, other)) +
                    RelocationValue(state, actor, other, Tile(state, first))).DefaultIfEmpty(0).Max();
        }

        private static int ExtendedChoiceValue(BoardSnapshot state, PendingCardEffect pending, CardState card, BoardAction action)
        {
            int actor = pending.actor;
            int tile = action.toRow * 5 + action.toColumn;
            if (card != null)
            {
                switch (pending.effect)
                {
                    case CardEffect.RevealHandKeywords: return KeywordGain(pending.source, PerformerKeywords.Of(card));
                    case CardEffect.DiscardAndCombine: return KeywordValue * PerformerKeywords.Of(card).Count - HandValue(card);
                    case CardEffect.GrantProtected: return ProtectionValue(state, card, 2);
                    case CardEffect.ReturnAndCopy: return ReturnCopyValue(state, actor, card);
                    case CardEffect.RowInfluence: return RowValue(state, actor, pending.sourceTile / 5, card.ownerId == actor);
                    case CardEffect.FormationBonus:
                        return pending.stage < 2 ? ProtectionValue(state, card, 1) : Gain(state, card, 1);
                    case CardEffect.ShareAdjacentKeywords:
                        return pending.selected == null ? ShareValue(state, actor, card) : Gain(state, card, 1) +
                            KeywordGain(card, PerformerKeywords.Of(pending.selected)) + KeywordGain(pending.selected, PerformerKeywords.Of(card));
                    case CardEffect.SwapAllies: return pending.selected == null ? SwapValue(state, actor, card) : SwapValue(state, actor, pending.selected, card);
                    case CardEffect.ColumnMoveAbility: return BestRelocation(state, actor, card, true);
                    case CardEffect.MoveAndCreate: return BestRelocation(state, actor, card) + (HandRoom(state, actor) > 0 ? ExpectedCreatedCardValue : 0);
                    case CardEffect.MoveAndAdjust: return BestRelocation(state, actor, card) + (card.ownerId == actor ? Gain(state, card, 1) : Damage(state, card, 1));
                    default: return 0;
                }
            }
            if (tile < 0 || tile >= 25) return 0;
            switch (pending.effect)
            {
                case CardEffect.OtherColumnToken: return Weight(tile) - (Enemy(state, actor, tile) == null ? 0 : 12);
                case CardEffect.DiscardAndCombine: return 3 * Weight(tile) - (Enemy(state, actor, tile) == null ? 0 : 12);
                case CardEffect.DisplaceContested:
                    var enemy = Enemy(state, actor, pending.sourceTile);
                    return enemy == null ? 0 : RelocationValue(state, actor, enemy, tile);
                case CardEffect.ColumnMoveAbility: case CardEffect.MoveAndCreate: case CardEffect.MoveAndAdjust:
                    return pending.selected == null ? 0 : RelocationValue(state, actor, pending.selected, tile);
                default: return 0;
            }
        }
    }
}
