using System;
using System.Collections.Generic;
namespace CardDuel{
    public static class DeckFactory{
        public const int CardCount = 54 ;
        public static CardInstance[]  CreateCards(EffectId[] effectsByCardId){
            if (effectsByCardId == null)
                throw new ArgumentNullException(nameof(effectsByCardId));

            if (effectsByCardId.Length != CardCount)
                throw new ArgumentException("需要为54张牌分别指定效果。", nameof(effectsByCardId));

            CardInstance[] cards = new CardInstance[CardCount];
            int id = 0;

            for (int rank = 3; rank <= 15; rank++)
            {
                for (int suit = 0; suit < 4; suit++)
                {
                    cards[id] = new CardInstance(id, rank, suit, effectsByCardId[id]);
                    id++;
                }
            }

            for (int rank = 16; rank <= 17; rank++)
            {
                cards[id] = new CardInstance(id, rank, -1, effectsByCardId[id]);
                id++;
            }

            return cards;
        }

        public static List<int> CreateDrawPile(SeededRandom random)
        {
            if (random == null)
             throw new ArgumentNullException(nameof(random));

            List<int> drawPile = new List<int>(CardCount);

            for (int id = 0; id < CardCount; id++)
            drawPile.Add(id);

            random.Shuffle(drawPile);
            return drawPile;
        }

        public static List<int> SelectEffectSlots(
                 SeededRandom random, int lowBlanks, int highBlanks)
        {       
            if (random == null)
                throw new ArgumentNullException(nameof(random));
            if (lowBlanks < 0 || lowBlanks > 32)
                throw new ArgumentOutOfRangeException(nameof(lowBlanks));
            if (highBlanks < 0 || highBlanks > 20)
                throw new ArgumentOutOfRangeException(nameof(highBlanks));

            List<int> low = new List<int>(32);
            List<int> high = new List<int>(20);

            for (int id = 0; id < 32; id++)
                low.Add(id);
            for (int id = 32; id < 52; id++)
                high.Add(id);

            random.Shuffle(low);
            random.Shuffle(high);

            List<int> slots = new List<int>(52 - lowBlanks - highBlanks);

            for (int i = lowBlanks; i < low.Count; i++)
                slots.Add(low[i]);
            for (int i = highBlanks; i < high.Count; i++)
                slots.Add(high[i]);

            random.Shuffle(slots);
            return slots;
        }
    
    public static EffectId[] CreateEffectAssignments(
        SeededRandom random,
        int lowBlanks,
        int highBlanks,
        List<EffectId> effectBag)
        {
            if (random == null)
                throw new ArgumentNullException(nameof(random));
            if (effectBag == null)
                throw new ArgumentNullException(nameof(effectBag));

            List<int> slots = SelectEffectSlots(random, lowBlanks, highBlanks);

            if (effectBag.Count != slots.Count)
                throw new ArgumentException("牌效数量必须等于可分配的位置数。", nameof(effectBag));

            List<EffectId> shuffledBag = new List<EffectId>(effectBag);
            random.Shuffle(shuffledBag);

            EffectId[] effectsByCardId = new EffectId[CardCount];

            for (int i = 0; i < slots.Count; i++)
                effectsByCardId[slots[i]] = shuffledBag[i];

            effectsByCardId[52 + random.Next(2)] = EffectId.ExileJoker;
            return effectsByCardId;
        }
    public static List<EffectId> CreateDefaultEffectBag()
        {
            List<EffectId> bag = new List<EffectId>(33);

            AddQuota(bag, EffectId.CopyRank, 3);
            AddQuota(bag, EffectId.ForceRandomDiscard, 2);
            AddQuota(bag, EffectId.LowerPreviousPlay, 2);
            AddQuota(bag, EffectId.DrawTwo, 4);
            AddQuota(bag, EffectId.ReturnThisCard, 3);
            AddQuota(bag, EffectId.ReshuffleDiscard, 4);
            AddQuota(bag, EffectId.PeekOpponentTwo, 4);
            AddQuota(bag, EffectId.SwapTwoInHand, 5);
            AddQuota(bag, EffectId.ReorderTopFive, 6);

            return bag;
        }

        private static void AddQuota(List<EffectId> bag, EffectId effect, int count)
        {
            for (int i = 0; i < count; i++)
                bag.Add(effect);
        }
    }
}

