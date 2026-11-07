using System;
using System.Collections.Generic;
using System.Linq;

namespace CardDuel
{
    // Shared by the dependency-free .NET runner and the Unity editor verification command.
    public static class RuleChecks
    {
        static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
        static void Ok(GameEngine g, bool condition) { Check(condition, g.Error); g.CheckInvariant(); }
        public static IReadOnlyList<string> Run()
        {
            var report = new List<string>();
            Action<string, Action> test = (name, body) => { body(); report.Add("PASS " + name); };
            test("01 Initial zones, immutable IDs, 8+8, quotas over 40 seeds", () => {
                for (int seed = 0; seed < 40; seed++)
                {
                    var g = new GameEngine(seed); var s = g.State; g.CheckInvariant();
                    Check(s.Hand(0).Count == 8 && s.Hand(1).Count == 8 && s.DrawPile.Count == 38, "deal");
                    Check(s.Cards.Count(x => x.Effect == EffectId.None) == 20, "blank total");
                    Check(s.Cards.Count(x => x.Rank <= 10 && x.Effect == EffectId.None) == 7, "low blank");
                    Check(s.Cards.Count(x => x.Rank >= 11 && x.Rank <= 15 && x.Effect == EffectId.None) == 12, "high blank");
                    Check(s.Cards.Count(x => x.Rank >= 16 && x.Effect == EffectId.ExileJoker) == 1, "joker exile");
                    Check(s.Cards.Count(x => x.Rank >= 16 && x.Effect == EffectId.None) == 1, "joker blank");
                    Check(s.Cards.Count(x => EffectCatalog.Phase(x.Effect) == EffectPhase.Pre) == 7 && s.Cards.Count(x => EffectCatalog.Phase(x.Effect) == EffectPhase.Post) == 5 && s.Cards.Count(x => EffectCatalog.Phase(x.Effect) == EffectPhase.Discard) == 22, "phase quotas");
                }
            });
            test("02 All 11 allowed shapes and main rank", () => {
                Shape(HandType.Single, 3, 3); Shape(HandType.Pair, 7, 7, 7); Shape(HandType.Triple, 9, 9, 9, 9);
                Shape(HandType.TripleSingle, 5, 5, 5, 5, 15); Shape(HandType.TriplePair, 6, 6, 6, 6, 4, 4);
                Shape(HandType.Straight, 8, 4, 5, 6, 7, 8); Shape(HandType.PairRun, 5, 3, 3, 4, 4, 5, 5);
                Shape(HandType.FourSingles, 7, 7, 7, 7, 7, 8, 8); Shape(HandType.FourSingles, 7, 7, 7, 7, 7, 16, 17);
                Shape(HandType.FourPairs, 6, 6, 6, 6, 6, 8, 8, 9, 9); Shape(HandType.Bomb, 15, 15, 15, 15, 15); Shape(HandType.Rocket, 17, 16, 17);
            });
            test("03 Airplanes and invalid sequences rejected", () => {
                foreach (int[] ranks in new[] { new[] {3,3,3,4,4,4}, new[] {3,3,3,4,4,4,5,6}, new[] {3,3,3,4,4,4,5,5,6,6}, new[] {11,12,13,14,15}, new[] {3,4,5,6}, new[] {3,3,4,4}, new[] {3,3,3,3,4,4,4,4}, new[] {3,3,3,3,3} })
                    Check(HandEvaluator.Recognize(ranks).Count == 0, "invalid ranks accepted");
            });
            test("04 Structured comparison, lengths, bombs, wings, rocket", () => {
                Check(HandEvaluator.Beats(P(HandType.Pair, 2, 5), P(HandType.Pair, 2, 4)), "pair");
                Check(!HandEvaluator.Beats(P(HandType.Single, 1, 17), P(HandType.Pair, 2, 3)), "different shape");
                Check(!HandEvaluator.Beats(P(HandType.Straight, 6, 10), P(HandType.Straight, 5, 8)), "length");
                Check(HandEvaluator.Beats(P(HandType.Bomb, 4, 3), P(HandType.FourSingles, 6, 15)), "bomb");
                Check(!HandEvaluator.Beats(P(HandType.FourSingles, 6, 15), P(HandType.Bomb, 4, 3)), "four wings not bomb");
                Check(HandEvaluator.Beats(P(HandType.Rocket, 2, 17), P(HandType.Bomb, 4, 15)), "rocket");
                Check(!HandEvaluator.Beats(P(HandType.Rocket, 2, 17), P(HandType.Rocket, 2, 17)), "rocket immune");
                Check(!HandEvaluator.Beats(P(HandType.Bomb, 4, 4), P(HandType.Bomb, 4, 5)), "bomb strength");
            });
            test("05 RPS ties, fair draws, first-turn skip, ordinary draw and buy guard", () => {
                var g = new GameEngine(123); Check(!g.Guess(4), "gesture guard"); int tries = 0;
                while (g.State.Phase == TurnPhase.Rps) { Ok(g, g.Guess(0)); Check(++tries < 100, "rps loop"); }
                Check(g.State.FreeDraw == 0 && g.State.Hand(g.State.Actor).Count == 8 && !g.CanBuy(g.State.Actor), "first skip");
                int actor = g.State.Actor; Ok(g, g.FinishDraw(actor)); Check(!g.Pass(actor), "free lead pass");
                PlaySingle(g, actor, g.State.Hand(actor)[0]); Check(g.State.Actor != actor && g.State.FreeDraw == 1, "normal draw");
                int next = g.State.Actor; int before = g.State.Hand(next).Count;
                Ok(g, g.BuyExtra(next)); Check(g.State.Hp(next) == 2 && g.State.Hand(next).Count == before + 5 && !g.BuyExtra(next), "purchase applies free1 plus extra4 once");
            });
            test("06 Injured player leads next; injured1 / winner3 / injured3 / normal1", () => {
                var g = Fixture(); SetHands(g, new[] { C(10, 0), C(3,0), C(4,0), C(5,0), C(6,0), C(7,0), C(15,1) }, new[] { C(3,1), C(11,0), C(12,0), C(13,0), C(14,0), C(15,0) });
                PlaySingle(g, 0, C(10,0)); Ok(g, g.FinishDraw(1)); Ok(g, g.Pass(1));
                Check(g.State.Actor == 1 && g.State.FreeDraw == 1 && g.State.LastPlay == null, "injured free lead draw1");
                Ok(g, g.FinishDraw(1)); PlaySingle(g, 1, C(3,1)); Check(g.State.FreeDraw == 3 && g.State.Actor == 0, "winner draw3");
                Ok(g, g.FinishDraw(0)); PlaySingle(g, 0, C(4,0)); Check(g.State.FreeDraw == 3 && g.State.Actor == 1, "injured draw3");
                Ok(g, g.FinishDraw(1)); PlaySingle(g, 1, C(11,0)); Check(g.State.FreeDraw == 1 && g.State.Actor == 0, "default1");
            });
            test("07 New Pass replaces unused compensation queue", () => {
                var g = Fixture(); SetHands(g, new[] {C(5,0),C(6,0),C(7,0),C(8,0)}, new[] {C(9,0),C(10,0),C(11,0),C(12,0)});
                PlaySingle(g,0,C(5,0)); Ok(g,g.FinishDraw(1)); Ok(g,g.Pass(1)); Ok(g,g.FinishDraw(1)); PlaySingle(g,1,C(9,0)); Ok(g,g.FinishDraw(0)); Ok(g,g.Pass(0));
                Check(g.State.Actor == 0 && g.State.FreeDraw == 1 && g.State.grants.Count == 2 && g.State.grants.Peek().Actor==1 && g.State.grants.Peek().Count == 3, "queue replace");
            });
            test("08 HP0 alive, no HP purchase; HP-1 ends before refill", () => {
                var g = Fixture(); g.State.hp[1] = 1;
                for (int i = 0; i < 2; i++)
                {
                    FixturePlay(g,0,new[]{C(3,0)},HandType.Single);Ok(g,g.ResolveEffect(0,new EffectChoice()));Ok(g,g.ResolveEffect(0,new EffectChoice()));Ok(g,g.FinishDraw(1));Ok(g,g.Pass(1));
                    if (i == 0) { Check(g.State.Hp(1) == 0 && g.State.Phase != TurnPhase.GameOver, "zero survives"); Check(!g.CanBuy(1) && !g.BuyExtra(1), "zero buy blocked"); }
                }
                Check(g.State.Hp(1) == -1 && g.State.Winner == 0 && g.State.Phase == TurnPhase.GameOver, "negative ends");
            });
            test("09 PRE COPY forms pair, printed rank unchanged, preview is pure", () => {
                var g = Fixture(); int source = C(3,0), target = C(8,0); SetEffect(g,source,EffectId.CopyRank); SetHands(g,new[] {source,target,C(4,0),C(5,0)},new[] {C(6,0),C(7,0),C(9,0)});
                var p = new PlayCommand {Cards=new[] {source,target},Type=HandType.Pair,PreSource=source,CopyTarget=target};
                string before = Fingerprint(g); Check(g.Preview(0,p).Legal && Fingerprint(g) == before, "pure preview"); Ok(g,g.Commit(0,p));
                Check(g.State.LastPlay.Pattern.MainRank == 8 && g.State.Cards[source].Rank == 3, "temporary only");
            });
            test("10 PRE LOWER pair/straight, rank3 and jokers immune", () => {
                var pair = new PlaySnapshot(1,P(HandType.Pair,2,4),new[] {0,1},new[] {4,4}); Check(HandEvaluator.Lower(pair).Pattern.MainRank == 3, "pair lower");
                var straight = new PlaySnapshot(1,P(HandType.Straight,5,8),new[] {0,1,2,3,4},new[] {4,5,6,7,8}); Check(HandEvaluator.Lower(straight).Ranks.SequenceEqual(new[] {3,4,5,6,7}), "straight lower");
                Check(HandEvaluator.Lower(new PlaySnapshot(1,P(HandType.Pair,2,3),new[] {0,1},new[] {3,3})) == null, "3 immune");
                Check(HandEvaluator.Lower(new PlaySnapshot(1,P(HandType.Rocket,2,17),new[] {52,53},new[] {16,17})) == null, "rocket immune");
                var g = Fixture(); int source=C(4,0); SetEffect(g,source,EffectId.LowerPreviousPlay); SetHands(g,new[] {source,C(4,1),C(9,0)},new[] {C(7,0),C(8,0),C(10,0)}); g.State.LastPlay=pair;
                Ok(g,g.Commit(0,new PlayCommand {Cards=new[] {source,C(4,1)},Type=HandType.Pair,PreSource=source}));
                Check(g.State.LastPlay.Pattern.MainRank == 4 && g.State.Cards[source].Rank == 4,"lower commit");
            });
            test("11 Invalid PRE + play is atomic, including PRNG and usage counters", () => {
                var g = Fixture(); int source=g.State.Hand(0)[0]; SetEffect(g,source,EffectId.ForceRandomDiscard); g.State.LastPlay=new PlaySnapshot(1,P(HandType.Single,1,17),new[] {53},new[] {17});
                string before=Fingerprint(g); var command=new PlayCommand {Cards=new[] {source},Type=HandType.Single,PreSource=source};
                Check(!g.Preview(0,command).Legal && !g.Commit(0,command) && Fingerprint(g)==before,"atomic rejection");
                Check(!g.Commit(0,new PlayCommand {Cards=new[] {source,source},Type=HandType.Pair}) && Fingerprint(g)==before,"duplicate guard");
            });
            test("12 FORCE random discard never activates forced card effect", () => {
                var g=Fixture(); int source=C(3,0), victim=C(4,0); SetEffect(g,source,EffectId.ForceRandomDiscard); SetEffect(g,victim,EffectId.ReturnThisCard); SetHands(g,new[] {source,C(5,0),C(6,0)},new[] {victim});
                Ok(g,g.Commit(0,new PlayCommand {Cards=new[] {source},Type=HandType.Single,PreSource=source}));
                Check(g.State.DiscardPile.Contains(victim) && g.State.ReturnUsed(1)==0,"forced no effect");
                Ok(g,g.ResolveEffect(0,new EffectChoice())); Check(!g.Candidates(0).Any(x=>x.Id==victim),"forced victim not discard candidate");
            });
            test("13 POST exile only effect joker, rocket snapshot persists", () => {
                var g=Fixture(); SetEffect(g,52,EffectId.ExileJoker); SetEffect(g,53,EffectId.None); SetHands(g,new[] {52,53,C(3,0),C(4,0)},new[] {C(5,0),C(6,0),C(7,0)});
                Ok(g,g.Commit(0,new PlayCommand {Cards=new[] {52,53},Type=HandType.Rocket})); Ok(g,g.ResolveEffect(0,new EffectChoice {Source=52}));
                Check(g.State.ExileZone.Contains(52) && g.State.DiscardPile.Contains(53) && !g.State.DiscardPile.Contains(52) && g.State.LastPlay.Pattern.Type==HandType.Rocket,"one joker exile");
                Ok(g,g.ResolveEffect(0,new EffectChoice())); Check(g.State.LastPlay.CardIds.Contains(52),"snapshot independent");
            });
            test("14 POST draw2 only once; skip leaves joker in discard", () => {
                var g=Fixture(); int source=g.State.Hand(0)[0]; SetEffect(g,source,EffectId.DrawTwo); int before=g.State.Hand(0).Count;
                Ok(g,g.Commit(0,new PlayCommand {Cards=new[] {source},Type=HandType.Single})); Ok(g,g.ResolveEffect(0,new EffectChoice {Source=source}));
                Check(g.State.Hand(0).Count==before+1 && !g.ResolveEffect(0,new EffectChoice {Source=source}),"post once");
                var j=Fixture(); SetEffect(j,52,EffectId.ExileJoker); SetHands(j,new[] {52,C(3,0),C(4,0)},new[] {C(5,0),C(6,0),C(7,0)}); Ok(j,j.Commit(0,new PlayCommand {Cards=new[] {52},Type=HandType.Single})); Ok(j,j.ResolveEffect(0,new EffectChoice())); Check(j.State.DiscardPile.Contains(52),"exile optional");
            });
            test("15 DISCARD return quota belongs independently to each player", () => {
                var g=Fixture(); int source=C(8,0); SetEffect(g,source,EffectId.ReturnThisCard);
                for(int actor=0;actor<2;actor++) for(int attempt=0;attempt<4;attempt++)
                {
                    PrepareDiscard(g,actor,source); string before=Fingerprint(g); bool success=g.ResolveEffect(actor,new EffectChoice {Source=source});
                    if(attempt<3) {Ok(g,success);Check(g.State.ReturnUsed(actor)==attempt+1,"return increment");}
                    else Check(!success && Fingerprint(g)==before,"return cap atomic");
                }
            });
            test("16 DISCARD reshuffle moves all and invalidates old sources", () => {
                var g=Fixture(); int source=C(3,0); SetEffect(g,source,EffectId.ReshuffleDiscard); PrepareDiscard(g,0,source); int[] old=g.State.discard.ToArray();
                Ok(g,g.ResolveEffect(0,new EffectChoice {Source=source})); Check(old.All(id=>!g.State.DiscardPile.Contains(id)),"reshuffle all"); Check(!g.ResolveEffect(0,new EffectChoice {Source=source}),"same effect twice");
            });
            test("17 DISCARD peek exact slots, human-only, private temporary reveal", () => {
                var g=Fixture(); int source=C(3,0); SetEffect(g,source,EffectId.PeekOpponentTwo); PrepareDiscard(g,0,source); int[] expected=g.State.Hand(1).Take(2).ToArray();
                string before=Fingerprint(g); Check(!g.ResolveEffect(0,new EffectChoice {Source=source,Targets=new[] {0,0}}) && Fingerprint(g)==before,"peek guard");
                Ok(g,g.ResolveEffect(0,new EffectChoice {Source=source,Targets=new[] {0,1}})); Check(g.RevealedToHuman.Select(c=>c.Id).SequenceEqual(expected),"peek content"); g.DismissPeek();Check(g.RevealedToHuman.Count==0,"peek dismiss");
                PrepareDiscard(g,1,source); Check(g.EffectBlockedReason(1,source)!="" && !g.Observe(1).Candidates.Any(c=>c.Id==source),"ai peek disabled");
            });
            test("18 DISCARD swap is validated then shuffled/drawn, no duplicates", () => {
                var g=Fixture(); int source=C(3,0);SetEffect(g,source,EffectId.SwapTwoInHand);PrepareDiscard(g,0,source);int count=g.State.Hand(0).Count;int[] targets=g.State.Hand(0).Take(2).ToArray();
                string before=Fingerprint(g);Check(!g.ResolveEffect(0,new EffectChoice {Source=source,Targets=new[] {targets[0],targets[0]}}) && Fingerprint(g)==before,"swap atomic");
                Ok(g,g.ResolveEffect(0,new EffectChoice {Source=source,Targets=targets}));Check(g.State.Hand(0).Count==count,"swap count");
            });
            test("19 DISCARD reorder arbitrary top+bottom, exact permutation, no recycling", () => {
                var g=Fixture();int source=C(3,0);SetEffect(g,source,EffectId.ReorderTopFive);PrepareDiscard(g,0,source);
                int[] preview=g.ReorderPreview(0,source).Select(c=>c.Id).ToArray(); int[] original=g.State.DrawPile.ToArray(); string before=Fingerprint(g);
                Check(!g.ResolveEffect(0,new EffectChoice {Source=source,Top=new[] {preview[0],preview[0]}}) && Fingerprint(g)==before,"order guard");
                int[] top=new[] {preview[3],preview[1]},bottom=new[] {preview[4],preview[0],preview[2]};
                Ok(g,g.ResolveEffect(0,new EffectChoice {Source=source,Top=top,Bottom=bottom}));
                Ok(g,g.ChooseDraw(1,false));
                Check(g.State.DrawPile.SequenceEqual(top.Skip(1).Concat(original.Skip(5)).Concat(bottom)),"top consumed next free draw, remaining exact");
                g=Fixture();SetEffect(g,source,EffectId.ReorderTopFive);PrepareDiscard(g,0,source);g.State.discard.AddRange(g.State.draw);g.State.draw.Clear();Check(g.ReorderPreview(0,source).Count==0,"preview no auto recycle");
            });
            test("20 Low hand <=2 refills alternately and merges nonempty deck", () => {
                var g=Fixture();SetHands(g,new[] {C(3,0),C(4,0)},new[] {C(5,0),C(6,0),C(7,0)});int extra=g.State.draw[0];g.State.draw.RemoveAt(0);g.State.discard.Add(extra);
                g.ResolveLowHands();g.CheckInvariant();Check(g.State.Hand(0).Count==6 && g.State.Hand(1).Count==7 && g.State.LowEvents==1 && g.State.DiscardPile.Count==0,"low refill");
            });
            test("21 Low hand lock prevents recursion under starvation and resets", () => {
                var g=Fixture();SetHands(g,new[] {C(3,0),C(4,0)},new[] {C(5,0),C(6,0)});g.State.exile.AddRange(g.State.draw);g.State.draw.Clear();
                g.ResolveLowHands();g.ResolveLowHands();g.CheckInvariant();Check(g.State.LowEvents==1,"lock");
                foreach(int actor in new[] {0,1}) {int id=g.State.exile[0];g.State.exile.RemoveAt(0);g.State.hands[actor].Add(id);}g.ResolveLowHands();Check(!g.State.lowLocked,"unlock");
                int remove=g.State.hands[0][0];g.State.hands[0].Remove(remove);g.State.exile.Add(remove);g.ResolveLowHands();Check(g.State.LowEvents==2,"retrigger");
            });
            test("22 Deck exhaustion recycles actual discard, zero drawable returns zero", () => {
                var g=Fixture();g.State.discard.AddRange(g.State.draw);g.State.draw.Clear();int available=g.AvailableToDraw;
                Check(g.Draw(0,100,"test")==available && g.Draw(1,4,"test")==0,"actual draw count");g.CheckInvariant();Check(g.State.DrawPile.Count==0 && g.State.DiscardPile.Count==0,"empty");
            });
            test("23 Every phase rejects wrong actor and stale effect source", () => {
                var g=Fixture();string before=Fingerprint(g);int source=g.State.Hand(0)[0];
                Check(!g.FinishDraw(1) && !g.BuyExtra(1) && !g.Pass(1) && !g.Commit(1,new PlayCommand {Cards=new[] {source},Type=HandType.Single}) && !g.ResolveEffect(0,new EffectChoice {Source=source}) && Fingerprint(g)==before,"guards atomic");
            });
            test("24 AI observation detached and hidden information invariant", () => {
                var g=Fixture();var first=AiController.LegalPlays(g.Observe(0),384).Select(CommandKey).ToArray();var o=g.Observe(0);
                int a=g.State.hands[1][0],b=g.State.draw[0];g.State.hands[1][0]=b;g.State.draw[0]=a;
                Check(first.SequenceEqual(AiController.LegalPlays(g.Observe(0),384).Select(CommandKey)),"AI changed with hidden card");
                Check(!typeof(GameObservation).GetProperties().Any(p=>p.PropertyType==typeof(GameState)),"state leak");
                Check(o.VisibleTop.Count==0 && o.Hand.Count==g.State.Hand(0).Count,"observation contents");g.CheckInvariant();
            });
            test("25 Pattern generator covers 11 types, bounded large-hand generation", () => {
                var g=new GameEngine(4);var all=HandEvaluator.Generate(g.State.Cards,4096).ToArray();
                Check(Enum.GetValues(typeof(HandType)).Cast<HandType>().All(t=>all.Any(p=>p.Type==t)),"generation missing shape");
                Check(HandEvaluator.Generate(g.State.Cards,384).Count()<=384,"bounded candidates");
            });
            test("26 Config validation and defensive copy", () => {
                bool failed=false;try{new GameEngine(1,new GameBalanceConfig {CopyQuota=4});}catch(ArgumentException){failed=true;}Check(failed,"config reject");
                var config=new GameBalanceConfig();var g=new GameEngine(1,config);config.InitialHp=99;g.Config.InitialHp=100;Check(g.Config.InitialHp==3,"config copy");
            });
            test("27 Fixed-seed 1000-command replay, all actions legal and conserved", () => {var a=Simulate(100,false,1000);var b=Simulate(100,false,1000);Check(a.State.Log.SequenceEqual(b.State.Log) && a.State.BattleLog.Select(x=>x.ToString()).SequenceEqual(b.State.BattleLog.Select(x=>x.ToString())) && a.RandomState==b.RandomState,"replay");});
            test("28 Draw yes/no is atomic, delayed until choice; compensation stays 3", () => {
                var g=Fixture();PlaySingle(g,0,g.State.Hand(0)[0]);int before=g.State.Hand(1).Count;
                Check(g.State.Phase==TurnPhase.Draw&&g.State.Hand(1).Count==before,"draw pending");
                string unchanged=Fingerprint(g);Check(!g.ChooseDraw(0,false)&&Fingerprint(g)==unchanged,"wrong actor draw atomic");
                Ok(g,g.ChooseDraw(1,false));Check(g.State.Hand(1).Count==before+1&&g.State.Phase==TurnPhase.Select,"no draws exactly1");
                Ok(g,g.Pass(1));Ok(g,g.ChooseDraw(1,false));PlaySingle(g,1,g.State.Hand(1)[0]);before=g.State.Hand(0).Count;
                Ok(g,g.ChooseDraw(0,false));Check(g.State.Hand(0).Count==before+3,"winner compensation draws3");
                g=Fixture();PlaySingle(g,0,g.State.Hand(0)[0]);before=g.State.Hand(1).Count;Ok(g,g.ChooseDraw(1,true));Check(g.State.Hand(1).Count==before+5&&g.State.Hp(1)==2,"yes draws1plus4");
            });
            test("29 Automatic stage choice skips zero, activates single, asks for multiple/parameters", () => {
                var g=Fixture();int a=C(3,0),b=C(3,1);SetHands(g,new[]{a,b,C(8,0),C(9,0)},new[]{C(4,0),C(5,0),C(6,0)});
                Ok(g,g.Commit(0,new PlayCommand{Cards=new[]{a,b},Type=HandType.Pair}));Check(g.AutomaticEffect(0).Source<0,"zero skips");
                SetEffect(g,a,EffectId.DrawTwo);Check(g.AutomaticEffect(0).Source==a,"one auto");SetEffect(g,b,EffectId.DrawTwo);Check(g.AutomaticEffect(0)==null,"multiple asks");
                PrepareDiscard(g,0,a);SetEffect(g,a,EffectId.SwapTwoInHand);Check(g.AutomaticEffect(0)==null,"parameters need panel");
            });
            test("30 Exile auto activates, leaves every recyclable pile and never returns", () => {
                var g=Fixture();SetEffect(g,52,EffectId.ExileJoker);SetHands(g,new[]{52,C(3,0),C(4,0),C(5,0)},new[]{C(6,0),C(7,0),C(8,0)});
                Ok(g,g.Commit(0,new PlayCommand{Cards=new[]{52},Type=HandType.Single}));Check(g.AutomaticEffect(0).Source==52,"exile selected automatically");
                Ok(g,g.ResolveEffect(0,g.AutomaticEffect(0)));Check(g.State.ExileZone.Contains(52)&&!g.State.Hand(0).Contains(52)&&!g.State.DrawPile.Contains(52)&&!g.State.DiscardPile.Contains(52),"exile exclusive");
                Ok(g,g.ResolveEffect(0,g.AutomaticEffect(0)));g.Draw(0,100,"exile regression");g.Draw(1,100,"exile regression");g.CheckInvariant();
                Check(g.State.ExileZone.SequenceEqual(new[]{52})&&!g.State.Hand(0).Contains(52)&&!g.State.Hand(1).Contains(52),"exile cannot recycle");
            });
            test("31 Selected-set play options are pure and auto COPY retains printed faces", () => {
                var g=Fixture();int source=C(3,0),target=C(8,0);SetEffect(g,source,EffectId.CopyRank);SetHands(g,new[]{source,target,C(4,0)},new[]{C(5,0),C(6,0),C(7,0)});
                string before=Fingerprint(g);var options=g.PlayOptions(0,new[]{source,target});Check(Fingerprint(g)==before&&options.Any(p=>p.PreSource==source&&p.CopyTarget==target&&p.Type==HandType.Pair),"copy options pure");
                Ok(g,g.Commit(0,options.First(p=>p.PreSource==source)));Check(g.State.Cards[source].Rank==3&&g.State.LastPlay.Pattern.MainRank==8,"auto copy commit");
            });
            test("32 Ten played cards heal across turns; passes/draws do not count", () => {
                var g=Fixture();g.State.hp[0]=1;
                int[] straight=Enumerable.Range(3,9).Select(r=>C(r,0)).ToArray();
                SetHands(g,straight.Concat(new[]{C(12,0),C(13,0),C(14,0)}).ToArray(),new[]{C(3,1),C(4,1),C(5,1),C(6,1)});
                Ok(g,g.Commit(0,new PlayCommand{Cards=straight,Type=HandType.Straight}));
                Check(g.State.PlayedCount(0)==9&&g.State.CardsUntilHeal(0)==1&&g.State.Hp(0)==1,"nine is not ten");
                Ok(g,g.ResolveEffect(0,new EffectChoice()));Ok(g,g.ResolveEffect(0,new EffectChoice()));Ok(g,g.ChooseDraw(1,false));Ok(g,g.Pass(1));
                Check(g.State.PlayedCount(1)==0,"pass and draw do not count");Ok(g,g.ChooseDraw(1,false));PlaySingle(g,1,C(3,1));Ok(g,g.ChooseDraw(0,false));
                PlaySingle(g,0,g.State.Hand(0)[0]);
                Check(g.State.PlayedCount(0)==10&&g.State.CardsUntilHeal(0)==10&&g.State.Hp(0)==2&&g.State.PlayedCount(1)==1,"cross-turn recovery");
            });
            test("33 Multiple thresholds, remainder and full-HP cap consume progress", () => {
                var g=Fixture();g.State.hp[0]=0;
                int[] pairs=Enumerable.Range(3,10).SelectMany(r=>new[]{C(r,0),C(r,1)}).ToArray();
                FixturePlay(g,0,pairs,HandType.PairRun);
                Check(g.State.Hp(0)==2&&g.State.PlayedCount(0)==20&&g.State.CardsUntilHeal(0)==10,"two thresholds in one play");
                FixturePlay(g,0,pairs,HandType.PairRun);Check(g.State.Hp(0)==3&&g.State.PlayedCount(0)==40,"cap at maximum");
                FixturePlay(g,0,Enumerable.Range(3,11).Select(r=>C(r,0)).ToArray(),HandType.Straight);
                Check(g.State.Hp(0)==3&&g.State.CardsUntilHeal(0)==9,"full HP consumes threshold, carries remainder");
                g.State.hp[0]=2;FixturePlay(g,0,new[]{C(3,0)},HandType.Single);
                Check(g.State.Hp(0)==2&&g.State.CardsUntilHeal(0)==8,"no banked recovery");
                g.State.Phase=TurnPhase.Select;string before=Fingerprint(g);
                Check(!g.Commit(0,new PlayCommand{Cards=new[]{C(3,0),C(3,0)},Type=HandType.Pair})&&Fingerprint(g)==before,"invalid play cannot count/heal/log");
            });
            test("34 Independent players; exile and returned cards count only on play", () => {
                var g=Fixture();g.State.played[0]=9;g.State.hp[0]=0;SetEffect(g,52,EffectId.ExileJoker);
                FixturePlay(g,0,new[]{52},HandType.Single);Ok(g,g.ResolveEffect(0,new EffectChoice{Source=52}));Ok(g,g.ResolveEffect(0,new EffectChoice()));
                Check(g.State.PlayedCount(0)==10&&g.State.Hp(0)==1&&g.State.CardsUntilHeal(1)==10,"exile counts once, zero HP can heal");
                g.State.played[1]=9;g.State.hp[1]=1;FixturePlay(g,1,new[]{C(4,0)},HandType.Single);
                Check(g.State.PlayedCount(1)==10&&g.State.Hp(1)==2&&g.State.PlayedCount(0)==10,"independent players");
                g=Fixture();int source=C(3,0);SetEffect(g,source,EffectId.ReturnThisCard);g.State.played[0]=9;g.State.hp[0]=1;
                FixturePlay(g,0,new[]{source},HandType.Single);Ok(g,g.ResolveEffect(0,new EffectChoice()));Ok(g,g.ResolveEffect(0,new EffectChoice{Source=source}));
                Check(g.State.Hand(0).Contains(source)&&g.State.PlayedCount(0)==10&&g.State.Hp(0)==2,"return is not another play");
                var fresh=new GameEngine(7);Check(fresh.State.PlayedCount(0)==0&&fresh.State.CardsUntilHeal(0)==10&&fresh.State.CardsUntilHeal(1)==10,"restart progress");
            });
            test("35 Battle history records exact play, activation and real destinations", () => {
                var g=Fixture();SetEffect(g,52,EffectId.ExileJoker);SetEffect(g,53,EffectId.ReturnThisCard);
                FixturePlay(g,0,new[]{52,53},HandType.Rocket);var first=g.State.BattleLog.Last();string preserved=first.ToString();
                Check(first.Actor==0&&first.Text.Contains("小王")&&first.Text.Contains("大王")&&first.Text.Contains("王炸"),"played faces and shape");
                Ok(g,g.ResolveEffect(0,new EffectChoice{Source=52}));
                Check(g.State.BattleLog.Any(x=>x.Text.Contains("小王 发动【王除外】"))&&g.State.BattleLog.Any(x=>x.Text.Contains("小王 → 除外")),"exile history");
                Check(g.State.BattleLog.Last().Text=="大王 → 弃牌堆","only actual discard listed");
                Ok(g,g.ResolveEffect(0,new EffectChoice{Source=53}));
                Check(g.State.BattleLog.Any(x=>x.Text=="大王：弃牌堆 → 手牌")&&first.ToString()==preserved,"return history immutable");
                int mark=g.State.BattleLog.Count;g.Draw(0,100,"history recycle");
                Check(g.State.BattleLog.Skip(mark).All(x=>!x.Text.Contains("小王")),"exile never reenters movements");
            });
            test("36 Hidden draws, swaps, peeks and deck orders do not leak identities", () => {
                var a=Fixture();var b=Fixture();int source=C(3,0);
                foreach(var g in new[]{a,b}){SetEffect(g,source,EffectId.SwapTwoInHand);PrepareDiscard(g,1,source);}
                int mark=a.State.BattleLog.Count;
                Ok(a,a.ResolveEffect(1,new EffectChoice{Source=source,Targets=a.State.Hand(1).Take(2).ToArray()}));
                Ok(b,b.ResolveEffect(1,new EffectChoice{Source=source,Targets=b.State.Hand(1).Skip(2).Take(2).ToArray()}));
                Check(a.State.BattleLog.Skip(mark).Select(x=>x.ToString()).SequenceEqual(b.State.BattleLog.Skip(mark).Select(x=>x.ToString())),"hidden swaps change public history");
                var peek=Fixture();SetEffect(peek,source,EffectId.PeekOpponentTwo);PrepareDiscard(peek,0,source);mark=peek.State.BattleLog.Count;
                Ok(peek,peek.ResolveEffect(0,new EffectChoice{Source=source,Targets=new[]{0,1}}));
                Check(peek.State.BattleLog.Skip(mark).Last().Text=="查看对手 2 张手牌","peek identities stay temporary");
                var order=Fixture();SetEffect(order,source,EffectId.ReorderTopFive);PrepareDiscard(order,1,source);mark=order.State.BattleLog.Count;
                int[] ids=order.ReorderPreview(1,source).Select(x=>x.Id).ToArray();Ok(order,order.ResolveEffect(1,new EffectChoice{Top=ids.Reverse().ToArray(),Source=source}));
                Check(order.State.BattleLog.Skip(mark).Last().Text=="牌库 → 牌顶 5 张 / 牌底 0 张","deck identities hidden");
            });
            test("37 Explicit 5667 COPY to 6 before recognizing and comparing triple-single", () => {
                foreach(int previousRank in new[]{5,6,7})
                {
                    var g=Fixture();int source=C(5,0),target=C(6,0);int[] ids={source,target,C(6,1),C(7,0)};
                    SetEffect(g,source,EffectId.CopyRank);SetHands(g,ids,new[]{C(3,0),C(4,0),C(8,0),C(9,0)});
                    g.State.LastPlay=new PlaySnapshot(1,P(HandType.TripleSingle,4,previousRank),new[]{1,2,3,4},new[]{previousRank,previousRank,previousRank,3});
                    string before=Fingerprint(g);
                    Check(!g.PreviewSelection(0,ids).Legal,"5667 must not silently copy");
                    Check(!g.PreviewSelection(0,ids,source).Legal,"missing target rejected");
                    Check(!g.PreviewSelection(0,ids,source,C(7,0)).Legal,"chosen 7 is not a valid triple-single");
                    var preview=g.PreviewSelection(0,ids,source,target);
                    Check(preview.Selected.Type==HandType.TripleSingle&&preview.Selected.MainRank==6&&preview.Legal==(previousRank<6)&&Fingerprint(g)==before,"shape then comparison, pure preview");
                    var command=new PlayCommand{Cards=ids,Type=HandType.TripleSingle,PreSource=source,CopyTarget=target};
                    if(previousRank<6){Ok(g,g.Commit(0,command));Check(g.State.LastPlay.Ranks.SequenceEqual(new[]{6,6,6,7})&&g.State.Cards[source].Rank==5,"logical snapshot only");}
                    else Check(!g.Commit(0,command)&&Fingerprint(g)==before,"cannot beat equal/higher triple");
                }
            });
            test("38 Actual commit rejects invalid shape/type/length/rank with no mutation", () => {
                Action<int[],HandType,HandPattern> reject=(ids,type,previous)=>{
                    var g=Fixture();int[] other=Enumerable.Range(0,54).Except(ids).Take(4).ToArray();SetHands(g,ids,other);
                    if(previous!=null)g.State.LastPlay=new PlaySnapshot(1,previous,other.Take(previous.Count),Enumerable.Repeat(previous.MainRank,previous.Count));
                    string before=Fingerprint(g);Check(!g.Commit(0,new PlayCommand{Cards=ids,Type=type})&&Fingerprint(g)==before,"invalid command mutated state");
                };
                reject(new[]{C(5,0),C(6,0),C(6,1),C(7,0)},HandType.TripleSingle,null);
                reject(new[]{C(14,0)},HandType.Single,P(HandType.Pair,2,3));
                reject(new[]{C(6,0),C(6,1)},HandType.Pair,P(HandType.Pair,2,6));
                reject(new[]{C(6,0),C(6,1)},HandType.Pair,P(HandType.Pair,2,7));
                reject(Enumerable.Range(4,6).Select(r=>C(r,0)).ToArray(),HandType.Straight,P(HandType.Straight,5,8));
                reject(new[]{C(6,0),C(6,1)},HandType.Single,null);
                reject(Enumerable.Range(0,4).Select(s=>C(15,s)).Concat(new[]{C(3,0),C(4,0)}).ToArray(),HandType.FourSingles,P(HandType.Bomb,4,3));
                reject(Enumerable.Range(0,4).Select(s=>C(15,s)).ToArray(),HandType.Bomb,P(HandType.Rocket,2,17));
            });
            test("39 New damage schedule is symmetric and clears the comparison snapshot", () => {
                foreach(int loser in new[]{0,1})
                {
                    var g=Fixture();int winner=1-loser;FixturePlay(g,winner,new[]{C(10,0)},HandType.Single);
                    Ok(g,g.ResolveEffect(winner,new EffectChoice()));Ok(g,g.ResolveEffect(winner,new EffectChoice()));Ok(g,g.ChooseDraw(loser,false));Ok(g,g.Pass(loser));
                    Check(g.State.Actor==loser&&g.State.Phase==TurnPhase.Draw&&g.State.FreeDraw==1&&g.State.LastPlay==null&&g.State.Hp(loser)==2,"loser is new free leader");
                    var grants=g.State.grants.ToArray();Check(grants.Length==2&&grants[0].Actor==winner&&grants[0].Count==3&&grants[1].Actor==loser&&grants[1].Count==3,"future actual actors receive 3/3");
                }
            });
            string audit="";test("40 Independent Dou Dizhu templates, exhaustive small hands, all shape comparisons and full-deck generation",()=>audit=DouDizhuAudit.Run());
            report[report.Count-1]+=" ("+audit+")";
            test("41 Real turn 10 vs 10, both actors, unrelated effects, AI/hint and explicit LOWER exception",()=>{
                foreach(int leader in new[]{0,1})foreach(var effect in new[]{EffectId.None,EffectId.CopyRank,EffectId.ForceRandomDiscard,EffectId.DrawTwo,EffectId.ReturnThisCard,EffectId.LowerPreviousPlay})
                {
                    var g=Fixture();int responder=1-leader,ten=C(10,1);
                    int[] leading={C(10,0),C(3,0),C(4,0),C(5,0)},responding={ten,C(9,0),C(11,0),C(6,0)};
                    SetEffect(g,ten,effect);SetHands(g,leader==0?leading:responding,leader==0?responding:leading);g.State.Actor=leader;
                    PlaySingle(g,leader,C(10,0));Ok(g,g.ChooseDraw(responder,false));
                    string before=Fingerprint(g);
                    var equal=new PlayCommand{Cards=new[]{ten},Type=HandType.Single};
                    Check(!g.PreviewSelection(responder,equal.Cards).Legal&&!g.Commit(responder,equal)&&Fingerprint(g)==before,"plain 10 cannot beat 10, regardless of suit or bound effect");
                    Check(!g.Commit(responder,new PlayCommand{Cards=new[]{C(9,0)},Type=HandType.Single})&&Fingerprint(g)==before,"9 cannot beat 10");
                    Check(g.PreviewSelection(responder,new[]{C(11,0)}).Legal,"J must beat 10");
                    var legal=AiController.LegalPlays(g.Observe(responder),384).ToArray();
                    Check(legal.Length>0&&legal.All(p=>g.Preview(responder,p).Legal),"every AI/hint candidate must pass authoritative preview");
                    var equalOptions=legal.Where(p=>p.Cards.Length==1&&p.Cards[0]==ten).ToArray();
                    Check(equalOptions.Any()==(effect==EffectId.LowerPreviousPlay)&&equalOptions.All(p=>p.PreSource==ten),"AI may only play equal printed 10 through explicit LOWER");
                    equal.PreSource=ten;
                    if(effect==EffectId.LowerPreviousPlay)
                    {
                        var preview=g.Preview(responder,equal);
                        Check(preview.Legal&&preview.Previous.Pattern.MainRank==9&&g.State.LastPlay.Pattern.MainRank==10&&g.State.Cards[C(10,0)].Rank==10,"LOWER is pure 10 vs logical 9, not 10 vs 10");
                        Ok(g,g.Commit(responder,equal));Check(g.State.LastPlay.Pattern.MainRank==10&&g.State.BattleLog.Any(x=>x.Text=="降低上手：10 → 9"),"log explains exact lowered ranks");
                    }
                    else
                    {
                        Check(!g.Commit(responder,equal)&&Fingerprint(g)==before,"unrelated effects cannot bypass strict strength");
                        Ok(g,g.StepAi(responder));Check(g.State.LastPlay.Actor==responder&&g.State.LastPlay.Pattern.MainRank>10,"actual AI commits a strictly larger response");
                    }
                }
            });
            test("42 Typed presentation metadata is public, exact and absent on rejected actions",()=>{
                var g=Fixture();int source=C(3,0),target=C(8,0);SetEffect(g,source,EffectId.CopyRank);
                SetHands(g,new[]{source,target,C(9,0),C(10,0)},new[]{C(4,0),C(5,0),C(6,0),C(7,0)});
                int mark=g.State.BattleLog.Count;string before=Fingerprint(g);
                Check(!g.Commit(0,new PlayCommand{Cards=new[]{source,target},Type=HandType.Pair,PreSource=source})&&Fingerprint(g)==before&&g.State.BattleLog.Count==mark,"rejection emitted a visual event");
                Ok(g,g.Commit(0,new PlayCommand{Cards=new[]{source,target},Type=HandType.Pair,PreSource=source,CopyTarget=target}));
                var activated=g.State.BattleLog.Skip(mark).Single(x=>x.Kind==BattleEventKind.Effect);
                Check(activated.Actor==0&&activated.Effect==EffectId.CopyRank&&activated.SourceCardId==source&&g.State.LastPlay.CardIds.Contains(activated.SourceCardId),"effect source must be a public played card");
                Ok(g,g.ResolveEffect(0,new EffectChoice()));Ok(g,g.ResolveEffect(0,new EffectChoice()));mark=g.State.BattleLog.Count;
                int hand=g.State.Hand(1).Count;Ok(g,g.ChooseDraw(1,false));
                var draws=g.State.BattleLog.Skip(mark).Where(x=>x.Kind==BattleEventKind.Draw).ToArray();
                Check(draws.Sum(x=>x.Count)==g.State.Hand(1).Count-hand&&draws.All(x=>x.SourceCardId<0&&x.Effect==EffectId.None),"draw metadata must contain actual count without hidden identity");
                g=Fixture();SetEffect(g,source,EffectId.PeekOpponentTwo);PrepareDiscard(g,1,source);mark=g.State.BattleLog.Count;
                Check(!g.ResolveEffect(1,new EffectChoice{Source=source,Targets=new[]{0,1}})&&g.State.BattleLog.Count==mark,"AI cannot activate or animate a blocked peek");
            });
            report.Add("TOTAL " + report.Count + " groups passed (including parameterized shapes, 40-seed quotas and command invariants).");
            return report;
        }
        static string CommandKey(PlayCommand p)=>p.Type+":"+string.Join(",",p.Cards)+":"+p.PreSource+":"+p.CopyTarget;
        static int C(int rank,int suit)=>(rank-3)*4+suit;
        static HandPattern P(HandType type,int count,int rank)=>new HandPattern(type,count,rank);
        static void Shape(HandType type,int main,params int[] ranks){Check(HandEvaluator.Recognize(ranks).Any(x=>x.Type==type&&x.MainRank==main),"missing "+type);}
        static GameEngine Fixture()
        {
            var g=new GameEngine(7);g.State.Phase=TurnPhase.Select;g.State.Actor=0;g.State.firstTurn=false;
            // Scenario fixtures remove random bound effects; explicit effect tests install their own.
            for(int id=0;id<54;id++) SetEffect(g,id,EffectId.None);
            return g;
        }
        static void SetEffect(GameEngine g,int id,EffectId effect){var c=g.State.cards[id];g.State.cards[id]=new CardInstance(id,c.Rank,c.Suit,effect);}
        static void SetHands(GameEngine g,int[] left,int[] right)
        {
            g.State.hands[0].Clear();g.State.hands[1].Clear();g.State.draw.Clear();g.State.discard.Clear();g.State.exile.Clear();
            g.State.hands[0].AddRange(left);g.State.hands[1].AddRange(right);g.State.draw.AddRange(Enumerable.Range(0,54).Except(left.Concat(right)));g.CheckInvariant();
        }
        static void PrepareDiscard(GameEngine g,int actor,int source)
        {
            foreach(var hand in g.State.hands)hand.Remove(source);g.State.draw.Remove(source);g.State.discard.Remove(source);g.State.exile.Remove(source);g.State.discard.Add(source);
            g.State.Phase=TurnPhase.Discard;g.State.Actor=actor;g.State.enteredDiscard.Clear();g.State.enteredDiscard.Add(source);g.State.pending.Clear();g.State.LastPlay=null;
            // Keep scenario completion away from low-hand refills.
            for(int p=0;p<2;p++)while(g.State.hands[p].Count<4&&g.State.draw.Count>0){int id=g.State.draw[0];g.State.draw.RemoveAt(0);g.State.hands[p].Add(id);}g.CheckInvariant();
        }
        static void PlaySingle(GameEngine g,int actor,int id)
        {
            Ok(g,g.Commit(actor,new PlayCommand {Cards=new[] {id},Type=HandType.Single}));Ok(g,g.ResolveEffect(actor,new EffectChoice()));Ok(g,g.ResolveEffect(actor,new EffectChoice()));
        }
        static void FixturePlay(GameEngine g,int actor,int[] cards,HandType type)
        {
            int[] own=cards.Concat(Enumerable.Range(0,54).Except(cards).Take(4)).ToArray();
            int[] other=Enumerable.Range(0,54).Except(own).Take(4).ToArray();
            SetHands(g,actor==0?own:other,actor==0?other:own);
            g.State.Actor=actor;g.State.Phase=TurnPhase.Select;g.State.LastPlay=null;g.State.pending.Clear();g.State.enteredDiscard.Clear();g.State.grants.Clear();
            Ok(g,g.Commit(actor,new PlayCommand{Cards=cards,Type=type}));
        }
        static string Fingerprint(GameEngine g)
        {
            var s=g.State;return string.Join("/",s.DrawPile)+"|"+string.Join("/",s.Hand(0))+"|"+string.Join("/",s.Hand(1))+"|"+string.Join("/",s.DiscardPile)+"|"+string.Join("/",s.ExileZone)+"|"+s.Hp(0)+","+s.Hp(1)+"|"+s.Phase+"|"+s.Actor+"|"+s.ReturnUsed(0)+","+s.ReturnUsed(1)+"|"+string.Join(",",s.EffectUses)+"|"+s.Log.Count+"|"+s.PlayedCount(0)+","+s.PlayedCount(1)+"|"+s.BattleLog.Count+"|"+g.RandomState;
        }
        public static GameEngine Simulate(int seed, bool requireEnd = true, int commandLimit = 20000)
        {
            var g=new GameEngine(seed);int commands=0;
            while(g.State.Phase!=TurnPhase.GameOver&&commands<commandLimit)
            {
                commands++;
                if(g.State.Phase==TurnPhase.Rps)Ok(g,g.Guess(0));else Ok(g,g.StepAi(g.State.Actor,Difficulty.Normal,true));
            }
            if(requireEnd&&g.State.Phase!=TurnPhase.GameOver)throw new Exception("simulation command bound exceeded Seed="+seed+" Turn="+g.State.Turn+" HP="+g.State.Hp(0)+"/"+g.State.Hp(1)+" Played="+g.State.PlayedCount(0)+"/"+g.State.PlayedCount(1));
            g.CheckInvariant();return g;
        }
    }
}
