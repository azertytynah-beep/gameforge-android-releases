using System;
using GameForge.Runtime;

namespace GameForge.Editor
{
    internal enum ForgeStage { Idea, Questions, Appearance, Gameplay, Complete }
    internal enum ForgeStart { Describe, Explore, Choose }

    [Flags]
    internal enum ForgeLocks
    {
        None = 0,
        Theme = 1,
        Pace = 2,
        Density = 4,
        Obstacle = 8,
        Objective = 16
    }

    [Serializable]
    internal sealed class ForgeProposal
    {
        public string label;
        public string summary;
        public string detail;
        public string assetNote;
        public string enemyStyle;
        public RunnerConfigV1 config;
    }

    [Serializable]
    internal sealed class ForgeProject
    {
        public const int ProposalCount = 4;

        public int schemaVersion = 1;
        public string id;
        public string title;
        public string idea;
        public ForgeStage stage;
        public ForgeStart start;
        public ForgeLocks locks;
        public ForgeBrief brief = new ForgeBrief();
        public int briefChapter;
        public int briefQuestion;
        public RunnerConfigV1 answers = RunnerConfigV1.CreateDefault();
        public ForgeProposal[] looks;
        public ForgeProposal[] playStyles;
        public int lookRound;
        public int playRound;
        public int chosenLook = -1;
        public int chosenPlay = -1;
        public string variantId;
        public string scenePath;

        public string DisplayTitle => string.IsNullOrWhiteSpace(title) ? "Sans titre" : title.Trim();
    }
}
