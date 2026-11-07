using System;
using System.Collections.Generic;
using System.Linq;

namespace CardDuel
{
    public sealed class PlaySnapshot
    {
        public int Actor { get; }
        public HandPattern Pattern { get; }
        public IReadOnlyList<int> CardIds { get; }
        public IReadOnlyList<int> Ranks { get; }

        public PlaySnapshot(
            int actor,
            HandPattern pattern,
            IEnumerable<int> cardIds,
            IEnumerable<int> ranks)
        {
            Actor = actor;
            Pattern = pattern;
            CardIds = Array.AsReadOnly(cardIds.ToArray());
            Ranks = Array.AsReadOnly(ranks.ToArray());
        }
    }
    public sealed class PlayCommand
    {
        public int[] Cards = Array.Empty<int>();
        public HandType Type = HandType.Single;
        public int PreSource = -1;
        public int CopyTarget = -1;
    }

    public sealed class PreviewResult
    {
        public bool Legal { get; internal set; }
        public string Reason { get; internal set; } = "";
        public IReadOnlyList<HandPattern> Patterns { get; internal set; }
            = Array.Empty<HandPattern>();
        public HandPattern Selected { get; internal set; }
        internal int[] LogicalRanks = Array.Empty<int>();
        public PlaySnapshot Previous { get; internal set; }
    }
}