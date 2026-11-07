using System;
using System.Collections.Generic;
using System.Linq;

namespace CardDuel
{
    // Independent templates from GDD section 3; never use the production recognizer to build the oracle.
    public static class DouDizhuAudit
    {
        public static string Run()
        {
            var catalogue=new Dictionary<string,HandPattern>();
            Action<HandType,int,IEnumerable<int>> add=(type,main,cards)=>{
                int[] ranks=cards.OrderBy(x=>x).ToArray();catalogue.Add(Key(ranks),new HandPattern(type,ranks.Length,main));
            };
            for(int rank=3;rank<=17;rank++)add(HandType.Single,rank,new[]{rank});
            for(int rank=3;rank<=15;rank++)
            {
                add(HandType.Pair,rank,Enumerable.Repeat(rank,2));
                add(HandType.Triple,rank,Enumerable.Repeat(rank,3));
                add(HandType.Bomb,rank,Enumerable.Repeat(rank,4));
                for(int wing=3;wing<=17;wing++)if(wing!=rank)
                {
                    add(HandType.TripleSingle,rank,Enumerable.Repeat(rank,3).Concat(new[]{wing}));
                    if(wing<=15)add(HandType.TriplePair,rank,Enumerable.Repeat(rank,3).Concat(new[]{wing,wing}));
                    for(int second=wing;second<=17;second++)if(second!=rank&&(second!=wing||wing<=15))
                        add(HandType.FourSingles,rank,Enumerable.Repeat(rank,4).Concat(new[]{wing,second}));
                    if(wing<=15)for(int second=wing+1;second<=15;second++)if(second!=rank)
                        add(HandType.FourPairs,rank,Enumerable.Repeat(rank,4).Concat(new[]{wing,wing,second,second}));
                }
            }
            for(int start=3;start<=14;start++)for(int end=start;end<=14;end++)
            {
                if(end-start+1>=5)add(HandType.Straight,end,Enumerable.Range(start,end-start+1));
                if(end-start+1>=3)add(HandType.PairRun,end,Enumerable.Range(start,end-start+1).SelectMany(x=>new[]{x,x}));
            }
            add(HandType.Rocket,17,new[]{16,17});
            Action<int[]> verify=ranks=>{
                string key=Key(ranks);catalogue.TryGetValue(key,out var expected);
                var actual=HandEvaluator.Recognize(ranks);
                if(expected==null?actual.Count!=0:actual.Count!=1||!expected.Equals(actual[0]))
                    throw new Exception("Rank multiset mismatch: "+key+"; expected="+expected+"; actual="+string.Join("/",actual));
            };
            foreach(string key in catalogue.Keys){int[] ranks=key.Split(',').Select(int.Parse).ToArray();verify(ranks);verify(ranks.Reverse().ToArray());}
            int multisets=0;var selected=new List<int>();
            Action<int> visit=null;
            visit=rank=>{
                if(rank==18){if(selected.Count>0){verify(selected.ToArray());multisets++;}return;}
                int count=selected.Count,max=Math.Min(rank>=16?1:4,8-count);
                for(int n=0;n<=max;n++)
                {
                    visit(rank+1);if(n<max)selected.Add(rank);
                }
                selected.RemoveRange(count,selected.Count-count);
            };
            visit(3);
            var cards=new GameEngine(0).State.Cards;
            var generated=HandEvaluator.Generate(cards,100000).ToArray();
            var generatedKeys=new HashSet<string>();
            foreach(var play in generated)
            {
                string key=Key(play.Cards.Select(id=>cards[id].Rank));
                if(!catalogue.TryGetValue(key,out var expected)||expected.Type!=play.Type)throw new Exception("Generator invented shape: "+key);
                generatedKeys.Add(key);
            }
            if(!generatedKeys.SetEquals(catalogue.Keys))throw new Exception("Generator missed legal rank sets: "+string.Join(" / ",catalogue.Keys.Except(generatedKeys).Take(8)));
            var patterns=catalogue.Values.Distinct().ToArray();int comparisons=0;
            foreach(var play in patterns)foreach(var previous in patterns)
            {
                bool expected;
                if(previous.Type==HandType.Rocket)expected=false;
                else if(play.Type==HandType.Rocket)expected=true;
                else if(play.Type==HandType.Bomb&&previous.Type!=HandType.Bomb)expected=true;
                else expected=play.Type==previous.Type&&play.Count==previous.Count&&play.MainRank>previous.MainRank;
                if(HandEvaluator.Beats(play,previous)!=expected)throw new Exception("Comparison mismatch: "+play+" against "+previous);
                comparisons++;
            }
            return "templates="+catalogue.Count+", exhaustive 1..8-card rank multisets="+multisets+", comparison pairs="+comparisons;
        }
        static string Key(IEnumerable<int> ranks)=>string.Join(",",ranks.OrderBy(x=>x));
    }
}
