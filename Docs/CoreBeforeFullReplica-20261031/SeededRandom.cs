using System;
using System.Collections.Generic;
namespace CardDuel
{
    public sealed class SeededRandom
    {
        public uint State{ get; private set ; }
        public SeededRandom(int seed){
            State = unchecked((uint)seed);

            if (State == 0)
                State = 0x9e3779b9u;
        }
        public int Next(int max)
        {
            if (max <= 0)
                throw new ArgumentOutOfRangeException(nameof(max));

            uint bound = (uint)max;
            uint threshold = unchecked(0u - bound) % bound;
            uint value;

            do
            {
                value = State;
                value ^= value << 13;
                value ^= value >> 17;
                value ^= value << 5;
                State = value;
            }
            while (value < threshold);

            return (int)(value % bound);
        }

        public void Shuffle<T>(IList<T> list){
            if(list == null)
            throw new ArgumentNullException(nameof(list));
            for (int i = list.Count - 1; i > 0; i--)
             {
                int j = Next(i + 1);

                T temp = list[i];
                list[i] = list[j];
                list[j] = temp;
             }    
        }
    }
}