using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace CardDuel
{
    public enum EffectId { None, CopyRank, ForceRandomDiscard, LowerPreviousPlay, ExileJoker, DrawTwo, ReturnThisCard, ReshuffleDiscard, PeekOpponentTwo, SwapTwoInHand, ReorderTopFive }
    public enum EffectPhase { None, Pre, Post, Discard }
    public enum TurnPhase { Rps, Draw, Select, Post, Discard, GameOver }
    public enum HandType { Single, Pair, Triple, TripleSingle, TriplePair, Straight, PairRun, FourSingles, FourPairs, Bomb, Rocket }
    public enum Difficulty { Simple, Normal }

    public sealed class CardInstance
    {
        public int Id { get; }
        public int Rank { get; }
        public int Suit { get; }
        public EffectId Effect { get; }
        public CardInstance(int id, int rank, int suit, EffectId effect) { Id = id; Rank = rank; Suit = suit; Effect = effect; }
        public static string RankName(int rank) => rank <= 10 ? rank.ToString() : new[] { "J", "Q", "K", "A", "2", "小王", "大王" }[rank - 11];
        public string Face => RankName(Rank) + (Suit < 0 ? "" : new[] { "♠", "♥", "♣", "♦" }[Suit]);
    }

    // xorshift32 is identical on .NET and Unity; every random decision uses this instance.
    public sealed class SeededRandom
    {
        public uint State { get; private set; }
        public SeededRandom(int seed) { State = unchecked((uint)seed); if (State == 0) State = 0x9e3779b9; }
        public int Next(int max)
        {
            if (max <= 0) throw new ArgumentOutOfRangeException(nameof(max));
            uint bound = (uint)max, threshold = unchecked(0u - bound) % bound, value;
            do { value = State; value ^= value << 13; value ^= value >> 17; value ^= value << 5; State = value; } while (value < threshold);
            return (int)(value % bound);
        }
        public void Shuffle<T>(IList<T> list) { for (int i = list.Count - 1; i > 0; --i) { int j = Next(i + 1); T v = list[i]; list[i] = list[j]; list[j] = v; } }
    }

    [Serializable]
    public sealed class GameBalanceConfig
    {
        public int InitialHp = 3, InitialHand = 8, NormalDraw = 1, ExtraDrawCost = 1, ExtraDrawCount = 4, PassDamage = 1;
        public int LowThreshold = 2, LowRefill = 4, ReturnLimit = 3, LowBlanks = 7, HighBlanks = 12;
        public int CompensationFirst = 1, CompensationSecond = 3, CompensationThird = 3;
        public int AiBuyMinHp = 2, AiBuyMaxHand = 3, AiCandidateLimit = 384;
        public int CopyQuota = 3, ForceQuota = 2, LowerQuota = 2, DrawQuota = 4, ReturnQuota = 3, ReshuffleQuota = 4, PeekQuota = 4, SwapQuota = 5, ReorderQuota = 6;
        public void Validate()
        {
            if (LowBlanks < 0 || LowBlanks > 32 || HighBlanks < 0 || HighBlanks > 20 ||
                CopyQuota < 0 || ForceQuota < 0 || LowerQuota < 0 || DrawQuota < 0 || ReturnQuota < 0 || ReshuffleQuota < 0 || PeekQuota < 0 || SwapQuota < 0 || ReorderQuota < 0 ||
                LowBlanks + HighBlanks + CopyQuota + ForceQuota + LowerQuota + DrawQuota + ReturnQuota + ReshuffleQuota + PeekQuota + SwapQuota + ReorderQuota != 52)
                throw new ArgumentException("效果配额与普通牌总数不符；两张王固定1白板+1除外。");
            if (InitialHp < 0 || InitialHand < 1 || InitialHand > 27 || NormalDraw < 0 || ExtraDrawCost < 1 || ExtraDrawCount < 0 || PassDamage < 1 ||
                LowThreshold < 0 || LowRefill < 0 || ReturnLimit < 0 || CompensationFirst < 0 || CompensationSecond < 0 || CompensationThird < 0 || AiCandidateLimit < 32)
                throw new ArgumentException("配置数值超出安全范围。");
        }
        internal GameBalanceConfig Copy() => (GameBalanceConfig)MemberwiseClone();
    }

    public static class EffectCatalog
    {
        public static EffectPhase Phase(EffectId id)
        {
            if (id >= EffectId.CopyRank && id <= EffectId.LowerPreviousPlay) return EffectPhase.Pre;
            if (id == EffectId.ExileJoker || id == EffectId.DrawTwo) return EffectPhase.Post;
            return id == EffectId.None ? EffectPhase.None : EffectPhase.Discard;
        }
        public static string Name(EffectId id) => new[] { "白板", "复制点数", "随机弃敌牌", "降低上手", "王除外", "抽两张", "本牌回手", "洗回弃牌", "看敌两张", "换手牌两张", "牌顶排序" }[(int)id];
        public static string Badge(EffectId id) => new[] { "—", "复制", "弃敌1", "降上手", "除外", "抽2", "回手", "洗回", "看2", "换2", "顶底" }[(int)id];
        public static string Description(EffectId id) => new[] {
            "无牌效", "PRE：本牌临时复制另一张所选牌的印刷点数", "PRE：合法出牌后，敌方随机弃1张；不触发其牌效", "PRE：上一手每个逻辑点数降一级；含3或王不可用",
            "POST：仅该效果的王移出本局", "POST：抽最多2张，牌库不足先回收弃牌", "DISCARD：本牌回手；每位玩家全局合计最多3次", "DISCARD：全部当前弃牌洗回牌库",
            "DISCARD：人类选2个敌方背面位置查看；AI不可用", "DISCARD：选2张手牌洗入牌库再抽2张", "DISCARD：看牌顶最多5张，按点击顺序分配顶/底；不额外回收弃牌" }[(int)id];
    }

    public sealed class HandPattern : IEquatable<HandPattern>
    {
        public HandType Type { get; }
        public int Count { get; }
        public int MainRank { get; }
        public HandPattern(HandType type, int count, int mainRank) { Type = type; Count = count; MainRank = mainRank; }
        public bool Equals(HandPattern other) => other != null && Type == other.Type && Count == other.Count && MainRank == other.MainRank;
        public override bool Equals(object obj) => Equals(obj as HandPattern);
        public override int GetHashCode() => ((int)Type * 64 + Count) * 32 + MainRank;
        public override string ToString() => new[] { "单张", "对子", "三张", "三带一", "三带一对", "顺子", "连对", "四带二单", "四带两对", "炸弹", "王炸" }[(int)Type] + " · " + Count + "张 · " + CardInstance.RankName(MainRank);
    }

    public sealed class PlaySnapshot
    {
        public int Actor { get; }
        public HandPattern Pattern { get; }
        public IReadOnlyList<int> CardIds { get; }
        public IReadOnlyList<int> Ranks { get; }
        public PlaySnapshot(int actor, HandPattern pattern, IEnumerable<int> ids, IEnumerable<int> ranks) { Actor = actor; Pattern = pattern; CardIds = Array.AsReadOnly(ids.ToArray()); Ranks = Array.AsReadOnly(ranks.ToArray()); }
    }
    public sealed class PlayCommand
    {
        public int[] Cards = Array.Empty<int>();
        public HandType Type;
        public int PreSource = -1, CopyTarget = -1;
    }
    public sealed class EffectChoice
    {
        public int Source = -1;
        public int[] Targets = Array.Empty<int>(), Top = Array.Empty<int>(), Bottom = Array.Empty<int>();
    }
    public sealed class PreviewResult
    {
        public bool Legal { get; internal set; }
        public string Reason { get; internal set; } = "";
        public IReadOnlyList<HandPattern> Patterns { get; internal set; } = Array.Empty<HandPattern>();
        public HandPattern Selected { get; internal set; }
        internal int[] LogicalRanks;
        public PlaySnapshot Previous { get; internal set; }
    }
    internal sealed class DrawGrant { public int Actor, Count; public DrawGrant(int actor, int count) { Actor = actor; Count = count; } }

    public enum BattleEventKind { Note, Effect, Draw, Recycle }
    public sealed class BattleLogEntry
    {
        public int Turn { get; }
        public int Actor { get; }
        public string Text { get; }
        public BattleEventKind Kind { get; }
        public EffectId Effect { get; }
        public int SourceCardId { get; }
        public int Count { get; }
        internal BattleLogEntry(int turn, int actor, string text, BattleEventKind kind=BattleEventKind.Note, EffectId effect=EffectId.None, int sourceCardId=-1, int count=0)
        { Turn=turn;Actor=actor;Text=text;Kind=kind;Effect=effect;SourceCardId=sourceCardId;Count=count; }
        public override string ToString() => "[T" + Turn + "] " + (Actor < 0 ? "系统" : Actor == 0 ? "玩家" : "AI") + "：" + Text;
    }

    public sealed class GameState
    {
        public const int CardsPerHeal = 10;
        internal readonly List<int> draw = new List<int>(), discard = new List<int>(), exile = new List<int>();
        internal readonly List<int>[] hands = { new List<int>(), new List<int>() };
        internal readonly int[] hp = new int[2], returns = new int[2], played = new int[2], effects = new int[11];
        internal readonly List<string> log = new List<string>();
        internal readonly List<BattleLogEntry> battleLog = new List<BattleLogEntry>();
        internal readonly Queue<DrawGrant> grants = new Queue<DrawGrant>();
        internal readonly List<int> pending = new List<int>(), enteredDiscard = new List<int>(), peeked = new List<int>();
        internal CardInstance[] cards;
        internal bool firstTurn = true, lowLocked, purchased, freeDrawDone;
        internal int freeDraw;
        internal string drawSource = "常规";
        public int Seed { get; internal set; }
        public int Actor { get; internal set; }
        public TurnPhase Phase { get; internal set; } = TurnPhase.Rps;
        public PlaySnapshot LastPlay { get; internal set; }
        public int Turn { get; internal set; }
        public int Trick { get; internal set; } = 1;
        public int Winner { get; internal set; } = -1;
        public int LowEvents { get; internal set; }
        public int Purchases { get; internal set; }
        public int Passes { get; internal set; }
        public int HandPeak { get; internal set; }
        public IReadOnlyList<CardInstance> Cards => Array.AsReadOnly(cards);
        public IReadOnlyList<int> DrawPile => draw.AsReadOnly();
        public IReadOnlyList<int> DiscardPile => discard.AsReadOnly();
        public IReadOnlyList<int> ExileZone => exile.AsReadOnly();
        public IReadOnlyList<int> Hand(int actor) => hands[actor].AsReadOnly();
        public int Hp(int actor) => hp[actor];
        public int ReturnUsed(int actor) => returns[actor];
        public int PlayedCount(int actor) => played[actor];
        public int CardsUntilHeal(int actor) => CardsPerHeal - played[actor] % CardsPerHeal;
        public IReadOnlyList<int> EffectUses => Array.AsReadOnly(effects);
        public IReadOnlyList<string> Log => log.AsReadOnly();
        public IReadOnlyList<BattleLogEntry> BattleLog => battleLog.AsReadOnly();
        public int FreeDraw => freeDraw;
        public string DrawSource => drawSource;
        public bool FirstTurnSkipped => firstTurn;
    }

    // A detached information boundary: no State, opponent identities/ranks or unrevealed deck order.
    public sealed class GameObservation
    {
        public int Actor { get; internal set; }
        public int HumanPlayer { get; internal set; }
        public int Hp { get; internal set; }
        public int OpponentHp { get; internal set; }
        public int OpponentCount { get; internal set; }
        public int DeckCount { get; internal set; }
        public int AvailableToDraw { get; internal set; }
        public int PendingFreeDraw { get; internal set; }
        public int ReturnUsed { get; internal set; }
        public bool CanBuy { get; internal set; }
        public TurnPhase Phase { get; internal set; }
        public PlaySnapshot Previous { get; internal set; }
        public IReadOnlyList<CardInstance> Hand { get; internal set; }
        public IReadOnlyList<CardInstance> PublicDiscard { get; internal set; }
        public IReadOnlyList<CardInstance> PublicExile { get; internal set; }
        public IReadOnlyList<CardInstance> Candidates { get; internal set; }
        public IReadOnlyList<CardInstance> VisibleTop { get; internal set; }
    }
}
