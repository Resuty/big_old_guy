using System.Linq;
using NUnit.Framework;

namespace CardDuel
{
    public class RulesTests
    {
        [Test] public void RuleMatrix() { Assert.That(RuleChecks.Run().Last(),Does.StartWith("TOTAL 42 groups passed")); }
        [TestCase(20261022)][TestCase(20261023)][TestCase(20261024)][TestCase(20261025)][TestCase(20261026)]
        [TestCase(20261027)][TestCase(20261028)][TestCase(20261029)][TestCase(20261030)][TestCase(20261031)]
        [TestCase(20261032)][TestCase(20261033)][TestCase(20261034)][TestCase(20261035)][TestCase(20261036)]
        [TestCase(20261037)][TestCase(20261038)][TestCase(20261039)][TestCase(20261040)][TestCase(20261041)]
        public void SimulationRemainsLegalAndConserves(int seed)
        {var g=RuleChecks.Simulate(seed,false);g.CheckInvariant();Assert.That(g.State.Phase==TurnPhase.GameOver||(g.State.Hp(0)>=0&&g.State.Hp(1)>=0),Is.True);}
    }
}
