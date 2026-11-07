using System;
using System.Collections.Generic;
using System.Linq;

namespace CardDuel
{
    public static class HandEvaluator
    {
        public static IReadOnlyList<HandPattern> Recognize(
            IEnumerable<int> ranks)
        {
            if (ranks == null)
                throw new ArgumentNullException(nameof(ranks));

            int[] sorted = ranks.OrderBy(rank => rank).ToArray();
            List<HandPattern> result = new List<HandPattern>();

            if (sorted.Length == 0 ||
                sorted.Any(rank => rank < 3 || rank > 17))
                return result;

            int count = sorted.Length;
            Dictionary<int, int> groups = sorted
                .GroupBy(rank => rank)
                .ToDictionary(group => group.Key, group => group.Count());

            if (groups.Values.Any(amount => amount > 4))
                return result;

            if (count == 1)
                result.Add(new HandPattern(
                    HandType.Single, count, sorted[0]));

            if (count == 2 && sorted[0] == sorted[1])
                result.Add(new HandPattern(
                    HandType.Pair, count, sorted[0]));

            if (count == 2 && sorted[0] == 16 && sorted[1] == 17)
                result.Add(new HandPattern(
                    HandType.Rocket, count, 17));

            if (count == 3 && sorted[0] == sorted[2])
                result.Add(new HandPattern(
                    HandType.Triple, count, sorted[0]));

            if (count == 4 && sorted[0] == sorted[3])
                result.Add(new HandPattern(
                    HandType.Bomb, count, sorted[0]));
            if (count == 4 && groups.Values.Contains(3))
            {
                int mainRank = groups.First(group => group.Value == 3).Key;
                result.Add(new HandPattern(
                    HandType.TripleSingle, count, mainRank));
            }

            if (count == 5 && groups.Count == 2 &&
                groups.Values.Contains(3) && groups.Values.Contains(2))
            {
                int mainRank = groups.First(group => group.Value == 3).Key;
                result.Add(new HandPattern(
                    HandType.TriplePair, count, mainRank));
            }
            if (count >= 5 &&
                groups.Count == count &&
                sorted[count - 1] <= 14 &&
                sorted[count - 1] - sorted[0] == count - 1)
            {
                result.Add(new HandPattern(
                    HandType.Straight, count, sorted[count - 1]));
            }

            if (count >= 6 &&
                count % 2 == 0 &&
                groups.Values.All(amount => amount == 2) &&
                sorted[count - 1] <= 14 &&
                sorted[count - 1] - sorted[0] == groups.Count - 1)
            {
                result.Add(new HandPattern(
                    HandType.PairRun, count, sorted[count - 1]));
            }
            if (count == 6 && groups.Values.Contains(4))
            {
                int mainRank = groups.First(group => group.Value == 4).Key;
                result.Add(new HandPattern(
                    HandType.FourSingles, count, mainRank));
            }

            if (count == 8 &&
                groups.Count == 3 &&
                groups.Values.Count(amount => amount == 2) == 2 &&
                groups.Values.Contains(4))
            {
                int mainRank = groups.First(group => group.Value == 4).Key;
                result.Add(new HandPattern(
                    HandType.FourPairs, count, mainRank));
            }

            return result;
        }
        public static bool Beats(HandPattern play, HandPattern previous)
        {
            if (play == null)
                return false;

            if (previous == null)
                return true;

            if (previous.Type == HandType.Rocket)
                return false;

            if (play.Type == HandType.Rocket)
                return true;

            if (play.Type == HandType.Bomb &&
                previous.Type != HandType.Bomb)
                return true;

            return play.Type == previous.Type &&
                play.Count == previous.Count &&
                play.MainRank > previous.MainRank;
        }
        public static PlaySnapshot Lower(PlaySnapshot previous)
        {
            if (previous == null ||
                previous.Ranks.Any(rank => rank <= 3 || rank >= 16))
                return null;

            int[] loweredRanks = previous.Ranks
                .Select(rank => rank - 1)
                .ToArray();

            HandPattern pattern = Recognize(loweredRanks)
                .FirstOrDefault(item =>
                    item.Type == previous.Pattern.Type &&
                    item.Count == previous.Pattern.Count);

            if (pattern == null)
                return null;

            return new PlaySnapshot(
                previous.Actor, pattern, previous.CardIds, loweredRanks);
        }
    }
}