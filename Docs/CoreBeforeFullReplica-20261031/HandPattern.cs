namespace CardDuel
{
    public enum HandType
    {
        Single,
        Pair,
        Triple,
        TripleSingle,
        TriplePair,
        Straight,
        PairRun,
        FourSingles,
        FourPairs,
        Bomb,
        Rocket
    }

    public sealed class HandPattern
    {
        public HandType Type { get; }
        public int Count { get; }
        public int MainRank { get; }

        public HandPattern(HandType type, int count, int mainRank)
        {
            Type = type;
            Count = count;
            MainRank = mainRank;
        }
    }
}