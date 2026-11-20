using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using GameForge.Runtime;
using Newtonsoft.Json;
#if !GAMEFORGE_DESKTOP
using UnityEngine;
#endif

namespace GameForge.Editor
{
    internal sealed class ForgeProjectStore
    {
        internal const int MaxIdeaLength = 600;
        private const int MaxFileBytes = 256 * 1024;
        private const ForgeLocks AllowedLocks = ForgeLocks.Theme | ForgeLocks.Pace | ForgeLocks.Density |
            ForgeLocks.Obstacle | ForgeLocks.Objective;
        private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);
        private readonly string root;

#if !GAMEFORGE_DESKTOP
        public ForgeProjectStore() : this(Path.Combine(Application.dataPath, "..", "..", ".local", "projects")) { }
#endif

        internal ForgeProjectStore(string root)
        {
            this.root = Path.GetFullPath(root);
        }

        public ForgeProject Create()
        {
            var draft = new ForgeProject { id = Guid.NewGuid().ToString("N"), brief = new ForgeBrief { detailed = true } };
            Write(draft, replace: false);
            return draft;
        }

        public ForgeProject Read(string id)
        {
            var path = ProjectPath(id);
            try
            {
                using var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete);
                if (input.Length > MaxFileBytes)
                    throw new InvalidDataException("Le projet GameForge dépasse la taille autorisée.");

                using var text = new StreamReader(input, Utf8, detectEncodingFromByteOrderMarks: false);
                using var json = new JsonTextReader(text) { DateParseHandling = DateParseHandling.None, MaxDepth = 12 };
                var serializer = new JsonSerializer
                {
                    CheckAdditionalContent = true,
                    MetadataPropertyHandling = MetadataPropertyHandling.Ignore,
                    MaxDepth = 12
                };
                var draft = serializer.Deserialize<ForgeProject>(json);
                Validate(draft, id);
                return draft;
            }
            catch (Exception exception) when (exception is JsonException || exception is DecoderFallbackException)
            {
                throw new InvalidDataException("Le projet GameForge est illisible.", exception);
            }
        }

        public IReadOnlyList<ForgeProject> List()
        {
            RequireRegularPath(root);
            if (!Directory.Exists(root))
                return Array.Empty<ForgeProject>();

            var projects = new List<(ForgeProject draft, DateTime modified)>();
            foreach (var file in Directory.EnumerateFiles(root, "*.json"))
            {
                var id = Path.GetFileNameWithoutExtension(file);
                if (!IsId(id) || Path.GetExtension(file) != ".json")
                    continue;

                try { projects.Add((Read(id), File.GetLastWriteTimeUtc(file))); }
                catch (Exception exception) when (exception is IOException || exception is InvalidDataException ||
                    exception is UnauthorizedAccessException)
                {
                    // Le reste de la liste reste disponible si un brouillon est illisible.
                }
            }

            return projects.OrderByDescending(item => item.modified).ThenBy(item => item.draft.id, StringComparer.Ordinal)
                .Select(item => item.draft).ToArray();
        }

        public void Save(ForgeProject draft) => Write(draft, replace: true);

        private void Write(ForgeProject draft, bool replace)
        {
            if (draft == null)
                throw new ArgumentNullException(nameof(draft));

            var destination = ProjectPath(draft.id);
            Validate(draft, draft.id);
#if GAMEFORGE_DESKTOP
            var bytes = Utf8.GetBytes(JsonConvert.SerializeObject(draft, Formatting.Indented));
#else
            var bytes = Utf8.GetBytes(JsonUtility.ToJson(draft, prettyPrint: true));
#endif
            if (bytes.Length > MaxFileBytes)
                throw new InvalidDataException("Le projet GameForge dépasse la taille autorisée.");

            if (!replace)
                Directory.CreateDirectory(root);
            var pending = Path.Combine(root, Guid.NewGuid().ToString("N") + ".pending");
            var output = new FileStream(pending, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            try
            {
                using (output)
                {
                    output.Write(bytes, 0, bytes.Length);
                    output.Flush(flushToDisk: true);
                }

                if (replace)
                    File.Replace(pending, destination, null);
                else
                    File.Move(pending, destination);
            }
            finally
            {
                File.Delete(pending);
            }
        }

        private string ProjectPath(string id)
        {
            if (!IsId(id))
                throw new ArgumentException("Identifiant de projet GameForge invalide.", nameof(id));
            var path = Path.Combine(root, id + ".json");
            RequireRegularPath(path);
            return path;
        }

        private static bool IsId(string id) =>
            Guid.TryParseExact(id, "N", out var value) && value != Guid.Empty && value.ToString("N") == id;

        private static void RequireRegularPath(string path)
        {
            for (; !string.IsNullOrEmpty(path); path = Path.GetDirectoryName(path))
            {
                try
                {
                    if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
                        throw new IOException("Les liens et jonctions sont interdits dans le stockage des projets GameForge.");
                }
                catch (FileNotFoundException) { }
                catch (DirectoryNotFoundException) { }
            }
        }

        private static void Validate(ForgeProject draft, string id)
        {
            if (draft == null || draft.schemaVersion != 1 || draft.id != id ||
                !Enum.IsDefined(typeof(ForgeStage), draft.stage) || !Enum.IsDefined(typeof(ForgeStart), draft.start) ||
                (draft.locks & ~AllowedLocks) != 0 || draft.lookRound < 0 || draft.playRound < 0 ||
                draft.briefChapter < 0 || draft.briefChapter >= ForgeBrief.Chapters.Length ||
                draft.briefQuestion < 0 || draft.briefQuestion >= ForgeBrief.Chapters[draft.briefChapter].Prompts.Count)
                throw new InvalidDataException("Le format du projet GameForge est invalide ou incompatible.");

            ValidateText(draft.title, RunnerConfigValidator.MaxTitleLength);
            ValidateText(draft.idea, MaxIdeaLength, allowMultiline: true);
            draft.brief ??= new ForgeBrief();
            draft.brief.Validate();
            ValidateConfig(draft.answers);
            ValidateProposals(draft.looks, draft.chosenLook);
            ValidateProposals(draft.playStyles, draft.chosenPlay);

            bool hasVariant = !string.IsNullOrEmpty(draft.variantId);
            bool hasScene = !string.IsNullOrEmpty(draft.scenePath);
            if ((draft.stage >= ForgeStage.Appearance && draft.looks?.Length != ForgeProject.ProposalCount) ||
                (draft.stage >= ForgeStage.Gameplay && (draft.chosenLook < 0 || draft.playStyles?.Length != ForgeProject.ProposalCount)))
                throw new InvalidDataException("Il manque une série de propositions dans ce projet GameForge.");
            if ((hasVariant && (!IsId(draft.variantId) || draft.stage < ForgeStage.Gameplay || draft.chosenPlay < 0)) ||
                hasScene != (draft.stage == ForgeStage.Complete) ||
                (hasScene && (!hasVariant ||
                    draft.scenePath != $"Assets/GameForgeGenerated/{draft.variantId}/Runner.unity")))
                throw new InvalidDataException("La variante associée au projet GameForge est invalide.");
        }

        private static void ValidateProposals(ForgeProposal[] proposals, int chosen)
        {
            int count = proposals?.Length ?? 0;
            if ((count != 0 && count != ForgeProject.ProposalCount) || chosen < -1 || chosen >= count)
                throw new InvalidDataException("Les propositions du projet GameForge sont invalides.");

            if (proposals == null)
                return;

            foreach (var proposal in proposals)
            {
                if (proposal == null)
                    throw new InvalidDataException("Une proposition du projet GameForge est manquante.");
                ValidateText(proposal.label, 120);
                ValidateText(proposal.summary, 1000, allowMultiline: true);
                ValidateText(proposal.detail, 1000, allowMultiline: true);
                ValidateText(proposal.assetNote, 400, allowMultiline: true);
                ValidateText(proposal.enemyStyle, 60);
                ValidateConfig(proposal.config);
            }
        }

        private static void ValidateConfig(RunnerConfigV1 config)
        {
            if (config == null || config.schemaVersion != RunnerConfigV1.Version ||
                config.templateId != RunnerConfigV1.RunnerTemplateId ||
                !Enum.IsDefined(typeof(RunnerObjective), config.objective) ||
                !Enum.IsDefined(typeof(RunnerEnemyDesign), config.enemyDesign) ||
                !IsFinite(config.initialSpeed) || !IsFinite(config.maxSpeed) || !IsFinite(config.acceleration) ||
                config.obstacleIds?.Length > RunnerConfigValidator.MaxObstacleTypes)
                throw new InvalidDataException("Les réglages du projet GameForge sont invalides.");

            ValidateText(config.title, RunnerConfigValidator.MaxTitleLength);
            ValidateText(config.themeId, 48);
            ValidateText(config.skyColor, 7);
            ValidateText(config.trackColor, 7);
            ValidateText(config.playerColor, 7);
            ValidateText(config.obstacleColor, 7);
            if (config.obstacleIds != null)
            {
                foreach (var item in config.obstacleIds)
                    ValidateText(item, 48);
            }
        }

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        private static void ValidateText(string value, int maxLength, bool allowMultiline = false)
        {
            if (value != null && (value.Length > maxLength || value.Any(character => char.IsControl(character) &&
                !(allowMultiline && (character == '\r' || character == '\n' || character == '\t')))))
                throw new InvalidDataException("Un texte du projet GameForge est invalide ou trop long.");
        }
    }
}
