using System.Collections.Generic;
using System.Linq;
using System;
namespace CardDuel
{
    public sealed class GameEngine
    {
        private const int InitialHp = 3;
        private const int NormalDraw = 1;
        private const int ExtraDrawCost = 1;
        private const int ExtraDrawCount = 4;

        private readonly SeededRandom random;

        public GameState State { get; }
        public string Error { get; private set; } = "";

        public GameEngine(int seed)
        {
            random = new SeededRandom(seed);

            State = new GameState();
            State.Seed = seed;
            State.hp[0] = State.hp[1] = InitialHp;

            List<EffectId> effectBag = DeckFactory.CreateDefaultEffectBag();
            EffectId[] effects = DeckFactory.CreateEffectAssignments(
                random, 7, 12, effectBag);

            State.cards = DeckFactory.CreateCards(effects);
            State.draw.AddRange(DeckFactory.CreateDrawPile(random));
            Draw(0, 8);
            Draw(1, 8);
        }

        // 0 = 石头，1 = 剪刀，2 = 布；平局保持在猜拳阶段。
        public bool Guess(int gesture)
        {
            if (State.Phase != TurnPhase.Rps || gesture < 0 || gesture > 2)
            {
                Error = "当前不能猜拳";
                return false;
            }

            int ai = random.Next(3);
            State.LastAiGesture = ai;
            Error = "";
            if (gesture == ai)
                return true;

            int winner = (gesture + 1) % 3 == ai ? 0 : 1;
            BeginTurn(winner);
            return true;
        }

        private void BeginTurn(int actor)
        {
            State.Actor = actor;
            State.Turn++;
            State.Phase = TurnPhase.Draw;
            State.freeDraw = State.firstTurn ? 0 : NormalDraw;
            State.freeDrawDone = false;
            State.purchased = false;
        }

        public bool CanBuy(int actor)
        {
            return actor >= 0 && actor <= 1 &&
                   actor == State.Actor && State.Phase == TurnPhase.Draw &&
                   !State.firstTurn && !State.purchased &&
                   State.hp[actor] >= ExtraDrawCost;
        }

        private void ApplyFreeDraw(int actor)
        {
            if (State.freeDrawDone)
                return;

            Draw(actor, State.freeDraw);
            State.freeDrawDone = true;
        }

        private void ApplyPurchase(int actor)
        {
            ApplyFreeDraw(actor);
            State.hp[actor] -= ExtraDrawCost;
            State.purchased = true;
            Draw(actor, ExtraDrawCount);
        }

        public bool BuyExtra(int actor)
        {
            if (!CanBuy(actor))
            {
                Error = "当前不能购买额外手牌";
                return false;
            }

            ApplyPurchase(actor);
            Error = "";
            return true;
        }

        public bool ChooseDraw(int actor, bool buyExtra)
        {
            if (actor != State.Actor || State.Phase != TurnPhase.Draw ||
                (buyExtra && !CanBuy(actor)))
            {
                Error = "当前不能执行该抽牌选择";
                return false;
            }

            if (buyExtra)
                ApplyPurchase(actor);
            return FinishDraw(actor);
        }

        public bool FinishDraw(int actor)
        {
            if (actor != State.Actor || State.Phase != TurnPhase.Draw)
            {
                Error = "不在该玩家的抽牌阶段";
                return false;
            }

            ApplyFreeDraw(actor);
            State.firstTurn = false;
            State.Phase = TurnPhase.Select;
            Error = "";
            return true;
        }

        private int Draw(int actor, int count)
        {
            int actual = 0;

            for (int i = 0; i < count; i++)
            {
                if (State.draw.Count == 0)
                    break;

                int id = State.draw[0];
                State.draw.RemoveAt(0);
                State.hands[actor].Add(id);
                actual++;
            }

            return actual;
        }
        public PreviewResult Preview(int actor, PlayCommand command)
        {
            PreviewResult result = new PreviewResult();

            if (actor != State.Actor || State.Phase != TurnPhase.Select)
            {
                result.Reason = "不在该玩家的选牌阶段";
                return result;
            }

            if (actor < 0 || actor > 1)
            {
                result.Reason = "玩家编号无效";
                return result;
            }

            if (command == null || command.Cards == null ||
                command.Cards.Length == 0)
            {
                result.Reason = "请先选择手牌";
                return result;
            }

            

            int[] ids = command.Cards;

            if (ids.Distinct().Count() != ids.Length ||
                ids.Any(id => !State.hands[actor].Contains(id)))
            {
                result.Reason = "选牌重复或不属于该玩家";
                return result;
            }

            int[] ranks = ids.Select(id => State.cards[id].Rank).ToArray();
            PlaySnapshot previous = State.LastPlay;
            if (command.PreSource >= 0)
            {
                if (!ids.Contains(command.PreSource))
                {
                    result.Reason = "效果来源必须是本次选中的牌";
                    return result;
                }

                CardInstance source = State.cards[command.PreSource];

                if (EffectCatalog.Phase(source.Effect) != EffectPhase.Pre)
                {
                    result.Reason = "这张牌没有出牌前效果";
                    return result;
                }

                if (source.Effect == EffectId.CopyRank)
                {
                    if (command.CopyTarget < 0 ||
                        command.CopyTarget == source.Id ||
                        !ids.Contains(command.CopyTarget))
                    {
                        result.Reason = "复制目标必须是另一张已选牌";
                        return result;
                    }

                    int sourceIndex = Array.IndexOf(ids, source.Id);
                    ranks[sourceIndex] = State.cards[command.CopyTarget].Rank;
                }
                else
                {
                    if (command.CopyTarget >= 0)
                    {
                        result.Reason = "该效果不需要复制目标";
                        return result;
                    }

                    if (source.Effect == EffectId.LowerPreviousPlay)
                    {
                        previous = HandEvaluator.Lower(previous);
                        if (previous == null)
                        {
                            result.Reason = "上一手不能降低";
                            return result;
                        }
                    }
                    else if (source.Effect == EffectId.ForceRandomDiscard &&
                             State.hands[1 - actor].Count == 0)
                    {
                        result.Reason = "对手没有可弃的手牌";
                        return result;
                    }
                }
            }
            else if (command.CopyTarget >= 0)
            {
                result.Reason = "没有复制效果来源";
                return result;
            }
            result.Patterns = HandEvaluator.Recognize(ranks);
            result.Selected = result.Patterns
                .FirstOrDefault(pattern => pattern.Type == command.Type);

            if (result.Selected == null)
            {
                result.Reason = result.Patterns.Count == 0
                    ? "牌型不成立"
                    : "所选牌不是指定牌型";
                return result;
            }

            result.LogicalRanks = ranks;
            result.Previous = previous;

            if (!HandEvaluator.Beats(result.Selected, previous?.Pattern))
            {
                result.Reason = "不能压过上一手";
                return result;
            }

            result.Legal = true;
            result.Reason = previous == null ? "可出牌" : "可压过";
            return result;
        }
    }
}