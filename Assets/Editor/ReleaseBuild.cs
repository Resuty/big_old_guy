using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;

namespace CardDuel.Editor
{
    public static class ReleaseBuild
    {
        [MenuItem("Card Duel/Build Windows Release")]
        public static void Windows()
        {
            const string root = "Builds/v0.1.0";
            Directory.CreateDirectory(root);
            var checks = RuleChecks.Run().ToList();
            foreach (int seed in new[] { 20261022, 20261023, 20261024, 20261025, 20261026 })
            {
                var game = RuleChecks.Simulate(seed, false);
                game.CheckInvariant();
                if (game.State.Phase != TurnPhase.GameOver)
                    throw new Exception("Simulation did not finish: " + seed);
                checks.Add("Completed simulation: " + seed);
            }
            File.WriteAllLines(root + "/verification.txt", checks);
            PlayerSettings.bundleVersion = "0.1.0";
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { "Assets/Scenes/Game.unity" },
                locationPathName = root + "/Windows/BigOldGuy.exe",
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            });
            if (report.summary.result != BuildResult.Succeeded)
                throw new Exception("Windows build failed: " + report.summary.result);
            File.AppendAllText(root + "/verification.txt",
                "\nWindows build succeeded; errors=" + report.summary.totalErrors +
                "; warnings=" + report.summary.totalWarnings + "\n");
        }
    }
}
