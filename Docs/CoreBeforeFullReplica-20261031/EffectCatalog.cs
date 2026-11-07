using System;

namespace CardDuel
{
    public enum EffectPhase
    {
        None,
        Pre,
        Post,
        Discard
    }

    public static class EffectCatalog
    {
        public static EffectPhase Phase(EffectId effect)
        {
            switch (effect)
            {
                case EffectId.None:
                    return EffectPhase.None;

                case EffectId.CopyRank:
                case EffectId.ForceRandomDiscard:
                case EffectId.LowerPreviousPlay:
                    return EffectPhase.Pre;

                case EffectId.ExileJoker:
                case EffectId.DrawTwo:
                    return EffectPhase.Post;

                case EffectId.ReturnThisCard:
                case EffectId.ReshuffleDiscard:
                case EffectId.PeekOpponentTwo:
                case EffectId.SwapTwoInHand:
                case EffectId.ReorderTopFive:
                    return EffectPhase.Discard;

                default:
                    throw new ArgumentOutOfRangeException(nameof(effect));
            }
        }

        public static string Badge(EffectId effect){
            switch(effect)
            {case EffectId.None: return "—";
            case EffectId.CopyRank: return "复制";
            case EffectId.ForceRandomDiscard: return "弃敌1";
            case EffectId.LowerPreviousPlay: return "降上手";
            case EffectId.ExileJoker: return "除外";
            case EffectId.DrawTwo: return "抽2";
            case EffectId.ReturnThisCard: return "回手";
            case EffectId.ReshuffleDiscard: return "洗回";
            case EffectId.PeekOpponentTwo: return "看2";
            case EffectId.SwapTwoInHand: return "换2";
            case EffectId.ReorderTopFive: return "顶底";
            default: throw new ArgumentOutOfRangeException(nameof(effect));
            }
        }
    }
}