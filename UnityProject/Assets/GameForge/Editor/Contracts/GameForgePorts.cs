using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using GameForge.Runtime;

namespace GameForge.Editor
{
    public interface IGameGenerator
    {
        GenerationResult Generate(RunnerConfigV1 configuration);
    }

    public interface ISettingsAdvisor
    {
        Task<SettingsSuggestion> SuggestAsync(string description, CancellationToken cancellation);
    }

    public sealed class GenerationResult
    {
        public GenerationResult(string variantId, string scenePath, IReadOnlyList<RunnerDiagnostic> diagnostics)
        {
            VariantId = variantId;
            ScenePath = scenePath;
            Diagnostics = diagnostics;
        }

        public string VariantId { get; }
        public string ScenePath { get; }
        public IReadOnlyList<RunnerDiagnostic> Diagnostics { get; }
        public bool Succeeded => !string.IsNullOrEmpty(VariantId) && !string.IsNullOrEmpty(ScenePath);
    }

    public sealed class SettingsSuggestion
    {
        public SettingsSuggestion(RunnerConfigV1 draft, IReadOnlyList<RunnerDiagnostic> diagnostics)
        {
            Draft = draft;
            Diagnostics = diagnostics;
        }

        public RunnerConfigV1 Draft { get; }
        public IReadOnlyList<RunnerDiagnostic> Diagnostics { get; }
    }
}
