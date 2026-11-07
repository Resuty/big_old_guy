using System;
using System.Collections.Generic;
using System.Linq;

namespace CardDuel
{
    public static class AiController
    {
        public static IEnumerable<PlayCommand> LegalPlays(GameObservation o, int limit)
        {
            var candidates = new List<PlayCommand>();
            var baseline = HandEvaluator.Generate(o.Hand, limit).ToArray();
            candidates.AddRange(baseline);
            foreach (var source in o.Hand.Where(c => EffectCatalog.Phase(c.Effect) == EffectPhase.Pre))
            {
                if (source.Effect == EffectId.CopyRank)
                {
                    foreach (var target in o.Hand.Where(c => c.Id != source.Id))
                    {
                        var modified = o.Hand.Select(c => c.Id == source.Id ? new CardInstance(c.Id, target.Rank, c.Suit, c.Effect) : c).ToArray();
                        foreach (var play in HandEvaluator.Generate(modified, Math.Min(limit, 96)).Where(p => p.Cards.Contains(source.Id) && p.Cards.Contains(target.Id)))
                            candidates.Add(new PlayCommand { Cards = play.Cards, Type = play.Type, PreSource = source.Id, CopyTarget = target.Id });
                    }
                }
                else if ((source.Effect != EffectId.LowerPreviousPlay || HandEvaluator.Lower(o.Previous) != null) && (source.Effect != EffectId.ForceRandomDiscard || o.OpponentCount > 0))
                    foreach (var play in baseline.Where(p => p.Cards.Contains(source.Id))) candidates.Add(new PlayCommand { Cards = play.Cards, Type = play.Type, PreSource = source.Id });
            }
            var byId = o.Hand.ToDictionary(x => x.Id);
            return candidates.Where(p => IsLegal(o, p)).OrderBy(p => p.Type == HandType.Bomb ? 1 : p.Type == HandType.Rocket ? 2 : 0)
                .ThenBy(p => Pattern(o, p).MainRank * 2 - p.Cards.Length)
                .ThenByDescending(p => p.PreSource >= 0 && byId[p.PreSource].Effect == EffectId.ForceRandomDiscard ? 1 : 0)
                .ThenBy(p => p.Cards.Sum(id => byId[id].Rank)).Take(limit).ToArray();
        }
        static HandPattern Pattern(GameObservation o, PlayCommand p)
        {
            var byId = o.Hand.ToDictionary(x => x.Id);
            int[] ranks = p.Cards.Select(id => id == p.PreSource && byId[id].Effect == EffectId.CopyRank ? byId[p.CopyTarget].Rank : byId[id].Rank).ToArray();
            return HandEvaluator.Recognize(ranks).FirstOrDefault(x => x.Type == p.Type);
        }
        static bool IsLegal(GameObservation o, PlayCommand p)
        {
            HandPattern pattern = Pattern(o, p);
            PlaySnapshot previous = p.PreSource >= 0 && o.Hand.First(x => x.Id == p.PreSource).Effect == EffectId.LowerPreviousPlay ? HandEvaluator.Lower(o.Previous) : o.Previous;
            return pattern != null && HandEvaluator.Beats(pattern, previous?.Pattern);
        }
        public static EffectChoice ChooseEffect(GameObservation o)
        {
            foreach (var source in o.Candidates.OrderBy(c => c.Effect == EffectId.ReturnThisCard ? 0 : c.Effect == EffectId.DrawTwo ? 1 : 2).ThenBy(c => c.Id))
            {
                var choice = new EffectChoice { Source = source.Id };
                switch (source.Effect)
                {
                    case EffectId.PeekOpponentTwo: if (o.Actor != o.HumanPlayer || o.OpponentCount < 2) continue; choice.Targets = new[] { 0, 1 }; break;
                    case EffectId.SwapTwoInHand: if (o.Hand.Count < 2) continue; choice.Targets = o.Hand.OrderBy(c => c.Rank).Take(2).Select(c => c.Id).ToArray(); break;
                    case EffectId.ReorderTopFive: choice.Top = o.VisibleTop.OrderBy(c => c.Rank).Select(c => c.Id).ToArray(); break;
                }
                return choice;
            }
            return new EffectChoice();
        }
    }
}
