using System;
using System.Collections.Generic;
using System.Linq;

namespace CardDuel
{
    public sealed class GameEngine
    {
        readonly GameBalanceConfig config;
        readonly SeededRandom random;
        readonly int humanPlayer;
        public GameState State { get; }
        public GameBalanceConfig Config => config.Copy();
        public string Error { get; private set; } = "";
        public uint RandomState => random.State;
        public event Action Changed;

        public GameEngine(int seed, GameBalanceConfig balance = null, int humanPlayer = 0)
        {
            config = (balance ?? new GameBalanceConfig()).Copy(); config.Validate();
            if (humanPlayer < -1 || humanPlayer > 1) throw new ArgumentOutOfRangeException(nameof(humanPlayer));
            this.humanPlayer = humanPlayer; random = new SeededRandom(seed);
            State = new GameState { Seed = seed };
            CreateDeck();
            State.hp[0] = State.hp[1] = config.InitialHp;
            Draw(0, config.InitialHand, "初始发牌"); Draw(1, config.InitialHand, "初始发牌");
            Log("新局 Seed=" + seed + "；HP=" + config.InitialHp + "；请选择猜拳（石头0/剪刀1/布2）");
            CheckInvariant();
        }
        void CreateDeck()
        {
            var ranks = new List<int>(); var suits = new List<int>();
            for (int r = 3; r <= 15; r++) for (int s = 0; s < 4; s++) { ranks.Add(r); suits.Add(s); }
            ranks.Add(16); ranks.Add(17); suits.Add(-1); suits.Add(-1);
            var effects = Enumerable.Repeat(EffectId.None, 54).ToArray();
            var low = Enumerable.Range(0, 32).ToList(); var high = Enumerable.Range(32, 20).ToList();
            random.Shuffle(low); random.Shuffle(high);
            var slots = low.Skip(config.LowBlanks).Concat(high.Skip(config.HighBlanks)).ToList(); random.Shuffle(slots);
            var bag = new List<EffectId>();
            AddQuota(bag, EffectId.CopyRank, config.CopyQuota); AddQuota(bag, EffectId.ForceRandomDiscard, config.ForceQuota);
            AddQuota(bag, EffectId.LowerPreviousPlay, config.LowerQuota); AddQuota(bag, EffectId.DrawTwo, config.DrawQuota);
            AddQuota(bag, EffectId.ReturnThisCard, config.ReturnQuota); AddQuota(bag, EffectId.ReshuffleDiscard, config.ReshuffleQuota);
            AddQuota(bag, EffectId.PeekOpponentTwo, config.PeekQuota); AddQuota(bag, EffectId.SwapTwoInHand, config.SwapQuota); AddQuota(bag, EffectId.ReorderTopFive, config.ReorderQuota);
            random.Shuffle(bag);
            for (int i = 0; i < slots.Count; i++) effects[slots[i]] = bag[i];
            effects[52 + random.Next(2)] = EffectId.ExileJoker;
            State.cards = Enumerable.Range(0, 54).Select(i => new CardInstance(i, ranks[i], suits[i], effects[i])).ToArray();
            State.draw.AddRange(Enumerable.Range(0, 54)); random.Shuffle(State.draw);
        }
        static void AddQuota(List<EffectId> bag, EffectId id, int count) { for (int i = 0; i < count; i++) bag.Add(id); }
        void Log(string message) => State.log.Add("[T" + State.Turn + "/R" + State.Trick + "] " + message);
        void Record(int actor, string message, BattleEventKind kind=BattleEventKind.Note, EffectId effect=EffectId.None, int sourceCardId=-1, int count=0)
            => State.battleLog.Add(new BattleLogEntry(State.Turn,actor,message,kind,effect,sourceCardId,count));
        string Faces(IEnumerable<int> ids) => string.Join("  ", ids.Select(id => State.cards[id].Face));
        bool Reject(string message) { Error = message; return false; }
        bool Done() { Error = ""; CheckInvariant(); Changed?.Invoke(); return true; }
        bool Guard(int actor, TurnPhase phase) => actor >= 0 && actor <= 1 && State.Actor == actor && State.Phase == phase;
        static string Player(int p) => p == 0 ? "玩家" : "AI";
        public int AvailableToDraw => State.draw.Count + State.discard.Count;
        public bool CanBuy(int actor) => Guard(actor, TurnPhase.Draw) && !State.firstTurn && !State.purchased && State.hp[actor] >= config.ExtraDrawCost;

        public bool Guess(int gesture)
        {
            if (State.Phase != TurnPhase.Rps || gesture < 0 || gesture > 2) return Reject("当前不能猜拳。");
            int ai = random.Next(3);
            string[] names = { "石头", "剪刀", "布" };
            Log("猜拳：玩家 " + names[gesture] + " / AI " + names[ai]);
            if (gesture == ai) { Log("同拳，请重猜。"); return Done(); }
            int winner = (gesture + 1) % 3 == ai ? 0 : 1;
            Log(Player(winner) + "获得先手（首次免费抽0，不能购买）。");
            Record(winner, "猜拳获胜，先手。");
            BeginTurn(winner); return Done();
        }
        void BeginTurn(int actor)
        {
            State.Actor = actor; State.Turn++; State.Phase = TurnPhase.Draw; State.purchased = false; State.freeDrawDone = false;
            State.pending.Clear(); State.enteredDiscard.Clear();
            State.freeDraw = State.firstTurn ? 0 : config.NormalDraw; State.drawSource = State.firstTurn ? "首次先手跳过" : "常规";
            if (!State.firstTurn && State.grants.Count > 0)
            {
                DrawGrant grant = State.grants.Dequeue();
                if (grant.Actor != actor) throw new InvalidOperationException("补偿行动队列与实际行动方不符。");
                State.freeDraw = grant.Count; State.drawSource = "伤害补偿";
            }
            Log(Player(actor) + "行动开始；免费" + State.freeDraw + "（" + State.drawSource + "）");
        }
        void ApplyFreeDraw(int actor)
        {
            if (State.freeDrawDone) return;
            Draw(actor, State.freeDraw, State.drawSource); State.freeDrawDone = true;
        }
        void ApplyPurchase(int actor)
        {
            ApplyFreeDraw(actor);
            State.hp[actor] -= config.ExtraDrawCost; State.purchased = true; State.Purchases++;
            Log(Player(actor) + "支付" + config.ExtraDrawCost + "HP，现HP=" + State.hp[actor]);
            Record(actor, "支付 " + config.ExtraDrawCost + " HP（" + State.hp[actor] + "/" + config.InitialHp + "）");
            Draw(actor, config.ExtraDrawCount, "HP购买");
        }
        public bool BuyExtra(int actor)
        {
            if (!CanBuy(actor)) return Reject("仅非跳过的抽牌阶段、HP足够且本行动未购买时可用。");
            ApplyPurchase(actor); return Done();
        }
        public bool FinishDraw(int actor)
        {
            if (!Guard(actor, TurnPhase.Draw)) return Reject("不在你的抽牌阶段。");
            CompleteDraw(actor); return Done();
        }
        public bool ChooseDraw(int actor, bool buyExtra)
        {
            if (!Guard(actor, TurnPhase.Draw)) return Reject("不在你的抽牌阶段。");
            if (buyExtra && !CanBuy(actor)) return Reject("生命不足或当前不能购买。");
            if (buyExtra) ApplyPurchase(actor);
            CompleteDraw(actor); return Done();
        }
        void CompleteDraw(int actor)
        {
            ApplyFreeDraw(actor);
            State.firstTurn = false; State.Phase = TurnPhase.Select;
            if (State.hands[actor].Count == 0 && State.LastPlay == null) { Log("空先手无牌可打，自动受伤并让出先手。"); ResolvePass(actor); }
        }

        // Legal alternatives for one selected set; previewing this list never consumes randomness.
        public PreviewResult PreviewSelection(int actor, int[] selected, int preSource = -1, int copyTarget = -1, HandType preferred = HandType.Single)
        {
            var command = new PlayCommand { Cards = selected, Type = preferred, PreSource = preSource, CopyTarget = copyTarget };
            var preview = Preview(actor, command);
            if (preview.Patterns.Count > 0 && !preview.Patterns.Any(p => p.Type == preferred))
            {
                command.Type = preview.Patterns[0].Type;
                return Preview(actor, command);
            }
            return preview;
        }
        public IReadOnlyList<PlayCommand> PlayOptions(int actor, int[] selected)
        {
            var result = new List<PlayCommand>();
            if (!Guard(actor, TurnPhase.Select) || selected == null || selected.Length == 0 || selected.Distinct().Count() != selected.Length || selected.Any(id => !State.hands[actor].Contains(id))) return result;
            Action<int,int> add = (source,target) => {
                int[] ranks = selected.Select(id => id == source && State.cards[id].Effect == EffectId.CopyRank ? State.cards[target].Rank : State.cards[id].Rank).ToArray();
                foreach (var pattern in HandEvaluator.Recognize(ranks))
                {
                    var command = new PlayCommand {Cards=selected.ToArray(),Type=pattern.Type,PreSource=source,CopyTarget=target};
                    if (Preview(actor,command).Legal) result.Add(command);
                }
            };
            add(-1,-1);
            foreach (int id in selected.Where(id => EffectCatalog.Phase(State.cards[id].Effect) == EffectPhase.Pre))
            {
                if (State.cards[id].Effect == EffectId.CopyRank)
                    foreach (int target in selected.Where(target => target != id).GroupBy(target => State.cards[target].Rank).Select(g => g.First())) add(id,target);
                else add(id,-1);
            }
            return result;
        }

        public PreviewResult Preview(int actor, PlayCommand command)
        {
            var result = new PreviewResult();
            Action<string> fail = message => { result.Legal = false; result.Reason = message; };
            if (!Guard(actor, TurnPhase.Select)) { fail("不在你的选牌阶段。"); return result; }
            if (command == null || command.Cards == null || command.Cards.Length == 0) { fail("请先选择手牌。"); return result; }
            int[] ids = command.Cards;
            if (ids.Distinct().Count() != ids.Length || ids.Any(id => !State.hands[actor].Contains(id))) { fail("选牌含重复或不属于你的实体牌。"); return result; }
            int[] ranks = ids.Select(id => State.cards[id].Rank).ToArray();
            PlaySnapshot previous = State.LastPlay;
            if (command.PreSource >= 0)
            {
                if (!ids.Contains(command.PreSource)) { fail("PRE效果必须来自本次选牌。"); return result; }
                CardInstance source = State.cards[command.PreSource];
                if (EffectCatalog.Phase(source.Effect) != EffectPhase.Pre) { fail("所选牌没有PRE效果。"); return result; }
                if (source.Effect == EffectId.CopyRank)
                {
                    if (ids.Length < 2 || command.CopyTarget == source.Id || !ids.Contains(command.CopyTarget)) { fail("复制目标必须是另一张已选牌。"); return result; }
                    ranks[Array.IndexOf(ids, source.Id)] = State.cards[command.CopyTarget].Rank;
                }
                if (source.Effect == EffectId.LowerPreviousPlay)
                {
                    previous = HandEvaluator.Lower(previous);
                    if (previous == null) { fail("上一手含3/王、没有上一手或无法维持牌型，不能降低。"); return result; }
                }
                if (source.Effect == EffectId.ForceRandomDiscard && State.hands[1 - actor].Count == 0) { fail("对手没有可随机弃掉的手牌。"); return result; }
            }
            var patterns = HandEvaluator.Recognize(ranks);
            result.Patterns = patterns;
            HandPattern selected = patterns.FirstOrDefault(x => x.Type == command.Type);
            if (selected == null) { fail(patterns.Count == 0 ? "牌型不成立" : "所选牌不是指定牌型"); return result; }
            result.Selected = selected; result.LogicalRanks = ranks; result.Previous = previous;
            if (!HandEvaluator.Beats(selected, previous?.Pattern))
            {
                fail(previous.Pattern.Type == HandType.Rocket ? "王炸不能被压过" : selected.Type != previous.Pattern.Type ? "牌型不同，不能压制" : selected.Count != previous.Pattern.Count ? "长度不同，不能压制" : "点数必须比上一手更大");
                return result;
            }
            result.Legal = true; result.Reason = previous == null ? "可出牌" : "可压过"; return result;
        }
        public bool Commit(int actor, PlayCommand command)
        {
            PreviewResult preview = Preview(actor, command);
            if (!preview.Legal) return Reject(preview.Reason);
            // All rejection paths finish above. No random numbers or zone mutations occur during preview.
            Record(actor, "打出 " + Faces(command.Cards) + "\n" + preview.Selected);
            if (command.PreSource >= 0)
            {
                EffectId effect = State.cards[command.PreSource].Effect;
                Use(effect, actor, command.PreSource);
                if (effect == EffectId.ForceRandomDiscard)
                {
                    var hand = State.hands[1 - actor]; int id = hand[random.Next(hand.Count)]; hand.Remove(id); State.discard.Add(id);
                    Log(Player(1 - actor) + "被迫弃1张（不触发牌效）：" + State.cards[id].Face);
                    Record(1 - actor, State.cards[id].Face + "：手牌 → 弃牌堆（被迫弃牌，不发动效果）");
                }
                if (effect == EffectId.CopyRank) Record(actor, State.cards[command.PreSource].Face + " 本次视为 " + CardInstance.RankName(State.cards[command.CopyTarget].Rank));
                if (effect == EffectId.LowerPreviousPlay) Record(actor, "降低上手：" + string.Join(" ", State.LastPlay.Ranks.Select(CardInstance.RankName)) + " → " + string.Join(" ", preview.Previous.Ranks.Select(CardInstance.RankName)));
            }
            State.LastPlay = new PlaySnapshot(actor, preview.Selected, command.Cards, preview.LogicalRanks);
            State.pending.AddRange(command.Cards);
            Log(Player(actor) + "出牌 " + preview.Selected + "：" + string.Join(" ", command.Cards.Select(id => State.cards[id].Face)));
            int earned = (State.played[actor] + command.Cards.Length) / GameState.CardsPerHeal - State.played[actor] / GameState.CardsPerHeal;
            State.played[actor] += command.Cards.Length;
            if (earned > 0)
            {
                int before = State.hp[actor]; State.hp[actor] = Math.Min(config.InitialHp, before + earned);
                string recovery = State.hp[actor] == before ? "出牌回复：生命已满" : "出牌回复 +" + (State.hp[actor] - before) + " HP（" + State.hp[actor] + "/" + config.InitialHp + "）";
                Log(Player(actor) + recovery); Record(actor, recovery);
            }
            // Pending cards remain physically in the actor's hand, reserved until POST finishes.
            State.Phase = TurnPhase.Post; return Done();
        }
        public bool Pass(int actor)
        {
            if (!Guard(actor, TurnPhase.Select) || State.LastPlay == null || State.LastPlay.Actor == actor) return Reject("只有回应上一手时可以Pass。");
            ResolvePass(actor); return Done();
        }
        void ResolvePass(int actor)
        {
            int leader = State.LastPlay == null ? 1 - actor : State.LastPlay.Actor;
            State.hp[actor] -= config.PassDamage; State.Passes++;
            Log(Player(actor) + " Pass/无法行动，-" + config.PassDamage + "HP，现HP=" + State.hp[actor]);
            Record(actor, "要不起，−" + config.PassDamage + " HP（" + State.hp[actor] + "/" + config.InitialHp + "）");
            State.LastPlay = null; State.Trick++;
            State.grants.Clear();
            State.grants.Enqueue(new DrawGrant(actor, config.CompensationFirst));
            State.grants.Enqueue(new DrawGrant(leader, config.CompensationSecond));
            State.grants.Enqueue(new DrawGrant(actor, config.CompensationThird));
            if (CheckEnd()) return;
            ResolveLowHands(); BeginTurn(actor);
        }
        public IReadOnlyList<CardInstance> Candidates(int actor)
        {
            if (State.Actor != actor) return Array.Empty<CardInstance>();
            IEnumerable<int> ids;
            EffectPhase phase;
            if (State.Phase == TurnPhase.Post) { ids = State.pending.Where(id => State.hands[actor].Contains(id)); phase = EffectPhase.Post; }
            else if (State.Phase == TurnPhase.Discard) { ids = State.enteredDiscard.Where(id => State.discard.Contains(id)); phase = EffectPhase.Discard; }
            else return Array.Empty<CardInstance>();
            return ids.Select(id => State.cards[id]).Where(c => EffectCatalog.Phase(c.Effect) == phase).ToArray();
        }
        public string EffectBlockedReason(int actor, int source)
        {
            if (!Candidates(actor).Any(x => x.Id == source)) return "效果来源不在本阶段、本次候选中。";
            switch (State.cards[source].Effect)
            {
                case EffectId.ExileJoker: return State.cards[source].Rank < 16 ? "只能除外王。" : "";
                case EffectId.ReturnThisCard: return State.returns[actor] >= config.ReturnLimit ? "本局回手次数已用完。" : "";
                case EffectId.PeekOpponentTwo: return actor != humanPlayer ? "仅人类玩家可以发动。" : State.hands[1 - actor].Count < 2 ? "对手不足2张。" : "";
                case EffectId.SwapTwoInHand: return State.hands[actor].Count < 2 ? "本方手牌不足2张。" : "";
                default: return "";
            }
        }
        public IReadOnlyList<CardInstance> UsableEffects(int actor) => Candidates(actor).Where(c => EffectBlockedReason(actor,c.Id) == "").ToArray();
        public EffectChoice AutomaticEffect(int actor)
        {
            if (State.Actor != actor || (State.Phase != TurnPhase.Post && State.Phase != TurnPhase.Discard)) return null;
            var candidates = UsableEffects(actor);
            if (candidates.Count == 0) return new EffectChoice();
            if (candidates.Count != 1) return null;
            var card = candidates[0];
            if (card.Effect == EffectId.PeekOpponentTwo || card.Effect == EffectId.SwapTwoInHand || card.Effect == EffectId.ReorderTopFive) return null;
            return new EffectChoice {Source=card.Id};
        }
        public IReadOnlyList<CardInstance> ReorderPreview(int actor, int source)
        {
            if (EffectBlockedReason(actor, source) != "" || State.cards[source].Effect != EffectId.ReorderTopFive) return Array.Empty<CardInstance>();
            return State.draw.Take(5).Select(id => State.cards[id]).ToArray();
        }
        public IReadOnlyList<CardInstance> RevealedToHuman => State.peeked.Select(id => State.cards[id]).ToArray();
        public void DismissPeek() { State.peeked.Clear(); Changed?.Invoke(); }
        public bool ResolveEffect(int actor, EffectChoice choice)
        {
            if (actor != State.Actor || (State.Phase != TurnPhase.Post && State.Phase != TurnPhase.Discard)) return Reject("不在可选择牌效的阶段。");
            if (choice == null) return Reject("缺少效果选择。");
            if (choice.Source < 0) { Log(Player(actor) + "跳过" + State.Phase + "效果。"); AdvanceEffectStage(); return Done(); }
            string blocked = EffectBlockedReason(actor, choice.Source);
            if (blocked != "") return Reject(blocked);
            EffectId effect = State.cards[choice.Source].Effect;
            int[] targets = choice.Targets ?? Array.Empty<int>(), top = choice.Top ?? Array.Empty<int>(), bottom = choice.Bottom ?? Array.Empty<int>();
            if (effect == EffectId.SwapTwoInHand && (targets.Length != 2 || targets.Distinct().Count() != 2 || targets.Any(id => !State.hands[actor].Contains(id)))) return Reject("换牌必须选择本方2张不同手牌。");
            // Peek targets are back-of-hand slot indices, not hidden physical IDs.
            if (effect == EffectId.PeekOpponentTwo && (targets.Length != 2 || targets.Distinct().Count() != 2 || targets.Any(i => i < 0 || i >= State.hands[1 - actor].Count))) return Reject("请选择2个不同敌方背面位置。");
            if (effect == EffectId.ReorderTopFive)
            {
                int[] expected = State.draw.Take(5).ToArray(), actual = top.Concat(bottom).ToArray();
                if (actual.Length != expected.Length || actual.Distinct().Count() != actual.Length || !expected.OrderBy(x => x).SequenceEqual(actual.OrderBy(x => x))) return Reject("排序必须完整且仅包含当前牌顶预览中的每张牌一次。");
            }
            Use(effect, actor, choice.Source);
            switch (effect)
            {
                case EffectId.ExileJoker:
                    State.hands[actor].Remove(choice.Source); State.exile.Add(choice.Source);
                    Record(actor, State.cards[choice.Source].Face + " → 除外（移出本局）"); break;
                case EffectId.DrawTwo: Draw(actor, 2, "卡牌效果",choice.Source); break;
                case EffectId.ReturnThisCard:
                    State.discard.Remove(choice.Source); State.hands[actor].Add(choice.Source); State.returns[actor]++; Log("回手限额 " + State.returns[actor] + "/" + config.ReturnLimit);
                    Record(actor, State.cards[choice.Source].Face + "：弃牌堆 → 手牌"); break;
                case EffectId.ReshuffleDiscard: Recycle("弃牌效果",choice.Source); break;
                case EffectId.PeekOpponentTwo:
                    State.peeked.Clear(); State.peeked.AddRange(targets.Select(i => State.hands[1 - actor][i])); Log("已向玩家暂时展示指定2张牌（本地查看，不写入公开日志）。");
                    Record(actor, "查看对手 2 张手牌"); break;
                case EffectId.SwapTwoInHand:
                    Record(actor, (actor == humanPlayer ? Faces(targets) : "2 张牌") + "：手牌 → 牌库（洗切）");
                    foreach (int id in targets) { State.hands[actor].Remove(id); State.draw.Add(id); }
                    random.Shuffle(State.draw); Draw(actor, 2, "卡牌效果换牌",choice.Source); break;
                case EffectId.ReorderTopFive:
                    State.draw.RemoveRange(0, top.Length + bottom.Length); State.draw.InsertRange(0, top); State.draw.AddRange(bottom); Log("牌顶/底排序完成：顶" + top.Length + " / 底" + bottom.Length);
                    Record(actor, "牌库 → 牌顶 " + top.Length + " 张 / 牌底 " + bottom.Length + " 张"); break;
            }
            AdvanceEffectStage(); return Done();
        }
        void Use(EffectId effect, int actor, int source)
        {
            State.effects[(int)effect]++; Log(Player(actor) + "发动 " + EffectCatalog.Name(effect) + "（实体#" + source + "）");
            Record(actor, State.cards[source].Face + " 发动【" + EffectCatalog.Name(effect) + "】",BattleEventKind.Effect,effect,source);
        }
        void AdvanceEffectStage()
        {
            if (State.Phase == TurnPhase.Post)
            {
                State.enteredDiscard.Clear();
                foreach (int id in State.pending) if (State.hands[State.Actor].Remove(id)) { State.discard.Add(id); State.enteredDiscard.Add(id); }
                if (State.enteredDiscard.Count > 0) Record(State.Actor, Faces(State.enteredDiscard) + " → 弃牌堆");
                State.Phase = TurnPhase.Discard;
            }
            else
            {
                int next = 1 - State.Actor;
                State.pending.Clear(); State.enteredDiscard.Clear();
                if (CheckEnd()) return;
                ResolveLowHands(); BeginTurn(next);
            }
        }
        internal int Draw(int actor, int count, string source, int effectSource=-1)
        {
            int actual = 0;
            for (int i = 0; i < count; i++)
            {
                if (State.draw.Count == 0) Recycle("抽牌不足自动回收",effectSource);
                if (State.draw.Count == 0) break;
                int id = State.draw[0]; State.draw.RemoveAt(0); State.hands[actor].Add(id); actual++;
            }
            State.HandPeak = Math.Max(State.HandPeak, State.hands[actor].Count);
            Log(Player(actor) + "抽牌 " + actual + "/" + count + "（" + source + "）；手牌=" + State.hands[actor].Count);
            if (actual > 0) Record(actor, "牌库 → 手牌 " + actual + " 张（" + source + "）",BattleEventKind.Draw,sourceCardId:effectSource,count:actual);
            return actual;
        }
        void Recycle(string source, int effectSource=-1)
        {
            if (State.discard.Count == 0) return;
            Record(-1, Faces(State.discard) + "\n弃牌堆 → 牌库（" + source + "，洗切）",BattleEventKind.Recycle,sourceCardId:effectSource,count:State.discard.Count);
            int count = State.discard.Count; State.draw.AddRange(State.discard); State.discard.Clear(); random.Shuffle(State.draw);
            Log("洗回弃牌 " + count + "张（" + source + "）");
        }
        internal void ResolveLowHands()
        {
            if (State.hands.All(x => x.Count > config.LowThreshold)) { State.lowLocked = false; return; }
            if (State.lowLocked) return;
            State.lowLocked = true; State.LowEvents++;
            Log("低手牌事件：先合并弃牌洗牌，双方交替补最多" + config.LowRefill);
            // Even an empty discard requires reshuffling the existing draw pile for this event.
            Record(-1, (State.discard.Count > 0 ? Faces(State.discard) + "\n弃牌堆 → 牌库；" : "") + "低手牌补牌，洗切牌库",BattleEventKind.Recycle,count:State.discard.Count);
            State.draw.AddRange(State.discard); State.discard.Clear(); random.Shuffle(State.draw);
            for (int i = 0; i < config.LowRefill; i++) { Draw(0, 1, "低手牌事件"); Draw(1, 1, "低手牌事件"); }
            if (State.hands.All(x => x.Count > config.LowThreshold)) State.lowLocked = false;
        }
        bool CheckEnd()
        {
            if (State.hp.All(x => x >= 0)) return false;
            State.Winner = State.hp[0] < 0 ? 1 : 0; State.Phase = TurnPhase.GameOver;
            Record(State.Winner, "获胜。");
            Log("终局：" + Player(State.Winner) + "胜；行动=" + State.Turn + "；HP=" + State.hp[0] + "/" + State.hp[1] + "；总实体数=54"); return true;
        }
        public GameObservation Observe(int actor)
        {
            if (actor < 0 || actor > 1) throw new ArgumentOutOfRangeException(nameof(actor));
            var candidates = UsableEffects(actor).ToArray();
            bool canReorder = candidates.Any(x => x.Effect == EffectId.ReorderTopFive);
            return new GameObservation {
                Actor = actor, HumanPlayer = humanPlayer, Hp = State.hp[actor], OpponentHp = State.hp[1 - actor], OpponentCount = State.hands[1 - actor].Count,
                DeckCount = State.draw.Count, AvailableToDraw = AvailableToDraw, ReturnUsed = State.returns[actor], CanBuy = CanBuy(actor), Phase = State.Phase, Previous = State.LastPlay,
                PendingFreeDraw = State.Phase == TurnPhase.Draw && !State.freeDrawDone ? State.freeDraw : 0,
                Hand = Array.AsReadOnly(State.hands[actor].Select(id => State.cards[id]).ToArray()),
                PublicDiscard = Array.AsReadOnly(State.discard.Select(id => State.cards[id]).ToArray()), PublicExile = Array.AsReadOnly(State.exile.Select(id => State.cards[id]).ToArray()),
                Candidates = Array.AsReadOnly(candidates), VisibleTop = Array.AsReadOnly((canReorder ? State.draw.Take(5) : Enumerable.Empty<int>()).Select(id => State.cards[id]).ToArray())
            };
        }
        // AI receives only Observe(); the engine owns randomness used for its tie-breaking.
        public bool StepAi(int actor, Difficulty difficulty = Difficulty.Normal, bool explain = false)
        {
            if (actor != State.Actor || State.Phase == TurnPhase.GameOver || State.Phase == TurnPhase.Rps) return Reject("AI当前不能行动。");
            GameObservation observation = Observe(actor);
            switch (State.Phase)
            {
                case TurnPhase.Draw:
                    if (observation.CanBuy && observation.Hp >= config.AiBuyMinHp && observation.Hand.Count + Math.Min(observation.PendingFreeDraw,observation.AvailableToDraw) <= config.AiBuyMaxHand) return BuyExtra(actor);
                    return FinishDraw(actor);
                case TurnPhase.Select:
                    var plays = AiController.LegalPlays(observation, config.AiCandidateLimit).ToArray();
                    if (plays.Length == 0)
                    {
                        if (explain) Log(Player(actor) + "决策：未找到合法压制，Pass。");
                        return Pass(actor);
                    }
                    int pick = difficulty == Difficulty.Simple ? random.Next(Math.Min(plays.Length, 8)) : (random.Next(10) == 0 ? random.Next(Math.Min(plays.Length, 3)) : 0);
                    if (explain) Log(Player(actor) + "决策：候选" + plays.Length + "，优先低主体点数/节省炸弹。");
                    return Commit(actor, plays[pick]);
                default: return ResolveEffect(actor, AiController.ChooseEffect(observation));
            }
        }
        public void CheckInvariant()
        {
            var all = State.draw.Concat(State.discard).Concat(State.exile).Concat(State.hands[0]).Concat(State.hands[1]).ToArray();
            if (State.cards.Length != 54 || all.Length != 54 || all.Distinct().Count() != 54 || all.Any(id => id < 0 || id >= 54)) throw new InvalidOperationException("54张实体牌守恒失败。");
            if (State.returns.Any(x => x < 0 || x > config.ReturnLimit)) throw new InvalidOperationException("回手限额错误。");
            if (State.played.Any(x => x < 0) || State.hp.Any(x => x > config.InitialHp)) throw new InvalidOperationException("出牌进度或生命上限错误。");
        }
    }
}
