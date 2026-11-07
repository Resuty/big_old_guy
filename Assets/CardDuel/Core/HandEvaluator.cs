using System;
using System.Collections.Generic;
using System.Linq;

namespace CardDuel
{
    public static class HandEvaluator
    {
        public static IReadOnlyList<HandPattern> Recognize(IEnumerable<int> ranks)
        {
            int[] r = ranks.OrderBy(x => x).ToArray();
            var result = new List<HandPattern>();
            if (r.Length == 0 || r.Any(x => x < 3 || x > 17)) return result;
            var g = r.GroupBy(x => x).ToDictionary(x => x.Key, x => x.Count());
            // COPY may temporarily create more than four of a rank, but no listed shape accepts it.
            if (g.Values.Any(x => x > 4)) return result;
            int n = r.Length;
            Action<HandType, int> add = (type, main) => result.Add(new HandPattern(type, n, main));
            if (n == 1) add(HandType.Single, r[0]);
            if (n == 2 && g.Count == 1) add(HandType.Pair, r[0]);
            if (n == 2 && r[0] == 16 && r[1] == 17) add(HandType.Rocket, 17);
            if (n == 3 && g.Count == 1) add(HandType.Triple, r[0]);
            if (n == 4 && g.Count == 1) add(HandType.Bomb, r[0]);
            if (n == 4 && g.Values.Contains(3)) add(HandType.TripleSingle, g.First(x => x.Value == 3).Key);
            if (n == 5 && g.Count == 2 && g.Values.Contains(3) && g.Values.Contains(2)) add(HandType.TriplePair, g.First(x => x.Value == 3).Key);
            if (n >= 5 && g.Count == n && r[n - 1] <= 14 && r[n - 1] - r[0] == n - 1) add(HandType.Straight, r[n - 1]);
            if (n >= 6 && n % 2 == 0 && g.Values.All(x => x == 2) && g.Keys.Max() <= 14 && g.Keys.Max() - g.Keys.Min() == g.Count - 1) add(HandType.PairRun, r[n - 1]);
            if (n == 6 && g.Values.Contains(4)) add(HandType.FourSingles, g.First(x => x.Value == 4).Key);
            if (n == 8 && g.Count == 3 && g.Values.Count(x => x == 2) == 2 && g.Values.Contains(4)) add(HandType.FourPairs, g.First(x => x.Value == 4).Key);
            return result;
        }
        public static bool Beats(HandPattern play, HandPattern previous)
        {
            if (previous == null) return true;
            if (previous.Type == HandType.Rocket) return false;
            if (play.Type == HandType.Rocket) return true;
            if (play.Type == HandType.Bomb && previous.Type != HandType.Bomb) return true;
            return play.Type == previous.Type && play.Count == previous.Count && play.MainRank > previous.MainRank;
        }
        public static PlaySnapshot Lower(PlaySnapshot previous)
        {
            if (previous == null || previous.Ranks.Any(x => x <= 3 || x >= 16)) return null;
            int[] ranks = previous.Ranks.Select(x => x - 1).ToArray();
            HandPattern pattern = Recognize(ranks).FirstOrDefault(x => x.Type == previous.Pattern.Type && x.Count == previous.Pattern.Count);
            return pattern == null ? null : new PlaySnapshot(previous.Actor, pattern, previous.CardIds, ranks);
        }

        // Generate by rank groups, never enumerate 2^hand. Variants retain distinct effect sources.
        public static IEnumerable<PlayCommand> Generate(IReadOnlyList<CardInstance> hand, int limit = 384)
        {
            var groups = hand.GroupBy(x => x.Rank).OrderBy(x => x.Key).ToDictionary(x => x.Key, x => x.OrderBy(c => c.Id).ToArray());
            var lists = new List<int[]>();
            foreach (var group in groups.Values)
            {
                foreach (var c in group) lists.Add(new[] { c.Id });
                for (int size = 2; size <= Math.Min(4, group.Length); size++)
                    lists.AddRange(Combinations(group.Select(x => x.Id).ToArray(), size));
            }
            if (groups.ContainsKey(16) && groups.ContainsKey(17)) lists.Add(new[] { groups[16][0].Id, groups[17][0].Id });
            foreach (var body in groups.Where(x => x.Value.Length >= 3))
            {
                int[] triple = body.Value.Take(3).Select(x => x.Id).ToArray();
                foreach (var wing in groups.Where(x => x.Key != body.Key))
                {
                    lists.Add(triple.Concat(wing.Value.Take(1).Select(x => x.Id)).ToArray());
                    if (wing.Value.Length >= 2) lists.Add(triple.Concat(wing.Value.Take(2).Select(x => x.Id)).ToArray());
                }
                if (body.Value.Length != 4) continue;
                int[] four = body.Value.Select(x => x.Id).ToArray();
                var others = hand.Where(x => x.Rank != body.Key).OrderBy(x => x.Rank).ToArray();
                // One lowest cost physical representative per wing-rank combination.
                for (int a = 0; a < others.Length; a++) for (int b = a + 1; b < others.Length; b++)
                    if ((a == 0 || others[a - 1].Rank != others[a].Rank) && (b == a + 1 || others[b - 1].Rank != others[b].Rank)) lists.Add(four.Concat(new[] { others[a].Id, others[b].Id }).ToArray());
                var pairs = groups.Where(x => x.Key != body.Key && x.Value.Length >= 2).ToArray();
                for (int a = 0; a < pairs.Length; a++) for (int b = a + 1; b < pairs.Length; b++)
                    lists.Add(four.Concat(pairs[a].Value.Take(2).Select(x => x.Id)).Concat(pairs[b].Value.Take(2).Select(x => x.Id)).ToArray());
            }
            for (int multiplicity = 1; multiplicity <= 2; multiplicity++)
                for (int start = 3; start <= 14; start++)
                {
                    var sequence = new List<int>();
                    for (int rank = start; rank <= 14 && groups.ContainsKey(rank) && groups[rank].Length >= multiplicity; rank++)
                    {
                        sequence.AddRange(groups[rank].Take(multiplicity).Select(x => x.Id));
                        if (sequence.Count >= (multiplicity == 1 ? 5 : 6)) lists.Add(sequence.ToArray());
                    }
                }
            var byId = hand.ToDictionary(x => x.Id);
            return lists.SelectMany(ids => Recognize(ids.Select(id => byId[id].Rank)).Select(p => new PlayCommand { Cards = ids, Type = p.Type }))
                .GroupBy(x => x.Type + ":" + string.Join(",", x.Cards.OrderBy(v => v))).Select(x => x.First())
                .OrderBy(x => x.Type == HandType.Bomb || x.Type == HandType.Rocket ? 1 : 0).ThenByDescending(x => x.Cards.Length).ThenBy(x => x.Cards.Sum(id => byId[id].Rank))
                .Take(limit).ToArray();
        }
        static IEnumerable<int[]> Combinations(int[] ids, int size)
        {
            if (size == 0) { yield return Array.Empty<int>(); yield break; }
            for (int i = 0; i <= ids.Length - size; i++) foreach (var tail in Combinations(ids.Skip(i + 1).ToArray(), size - 1)) yield return new[] { ids[i] }.Concat(tail).ToArray();
        }
    }
}
