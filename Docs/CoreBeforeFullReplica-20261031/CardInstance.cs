using UnityEngine;
using System;
namespace  CardDuel
{
    public enum EffectId
    {
        None,
        CopyRank,
        ForceRandomDiscard,
        LowerPreviousPlay,
        ExileJoker,
        DrawTwo,
        ReturnThisCard,
        ReshuffleDiscard,
        PeekOpponentTwo,
        SwapTwoInHand,
        ReorderTopFive
    }
    public sealed  class CardInstance
    {
        static readonly string[] SuitSymbols = { "♠", "♥", "♣", "♦" };
        public int Id { get; }
        public int Rank { get; }
        public int Suit { get; }
        public EffectId Effect { get; }

        public CardInstance(int id, int rank, int suit, EffectId effect)
        {
            if (id < 0)
                throw new ArgumentOutOfRangeException(nameof(id));
            if (rank < 3 || rank > 17)
                throw new ArgumentOutOfRangeException(nameof(rank));
            if (rank <= 15 && (suit < 0 || suit > 3))
                throw new ArgumentOutOfRangeException(nameof(suit));
            if (rank >= 16 && suit != -1)
                throw new ArgumentOutOfRangeException(nameof(suit));

            Id = id;
            Rank = rank;
            Suit = suit;
            Effect = effect;
        }

        public static string RankName(int rank)
        {
            if (rank >= 3 && rank <= 10)
                return rank.ToString();

            switch (rank)
            {
                case 11: return "J";
                case 12: return "Q";
                case 13: return "K";
                case 14: return "A";
                case 15: return "2";
                case 16: return "小王";
                case 17: return "大王";
                default:
                    throw new ArgumentOutOfRangeException(nameof(rank));
            }
        }

        public string Face
        {
            get
            {
                if (Rank >= 16)
                    return RankName(Rank);

                return RankName(Rank) + SuitSymbols[Suit];
            }
        }
    }
}
