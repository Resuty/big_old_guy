using System;
using System.Collections.Generic;

namespace CardDuel
{
    public enum TurnPhase { Rps, Draw, Select, Post, Discard, GameOver }

    
    public sealed class GameState
    {
        internal CardInstance[] cards = Array.Empty<CardInstance>();
        internal readonly List<int> draw = new List<int>();
        internal readonly List<int>[] hands = {
            new List<int>(),
            new List<int>()
        };
        public PlaySnapshot LastPlay { get; internal set; }
        public int Seed { get; internal set; }
        public int Actor { get; internal set; }
        public TurnPhase Phase { get; internal set; } = TurnPhase.Rps;
        public int Turn { get; internal set; }
        public int LastAiGesture { get; internal set; } = -1;
        internal bool firstTurn = true;
        internal int freeDraw;
        internal bool freeDrawDone, purchased;
        internal readonly int[] hp = new int[2];
        public int FreeDraw => freeDraw;
        public int Hp(int actor) => hp[actor];
        public IReadOnlyList<CardInstance> Cards => Array.AsReadOnly(cards);
        public IReadOnlyList<int> DrawPile => draw.AsReadOnly();
        public IReadOnlyList<int> Hand (int actor) => hands[actor].AsReadOnly();
    }
}