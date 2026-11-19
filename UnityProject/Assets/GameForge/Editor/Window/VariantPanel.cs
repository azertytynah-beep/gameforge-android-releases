using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GameForge.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace GameForge.Editor
{
    internal sealed class VariantPanel : ScrollView
    {
        private const string BackupSessionKey = "GameForge.LastBackup";
        private const string SelectionSessionKey = "GameForge.SelectedVariant";
        private readonly GenerationStore store;
        private readonly RunnerCatalog catalog;
        private readonly List<StoredVariant> variants = new List<StoredVariant>();
        private readonly ListView list = new ListView { name = "published-variants", fixedItemHeight = 22 };
        private readonly VisualElement actions = new VisualElement();
        private readonly HelpBox status = new HelpBox(string.Empty, HelpBoxMessageType.Info) { name = "variant-status" };
        private readonly TextField displayTitle = new TextField("Titre affiché") { name = "variant-title" };
        private readonly TextField backupId = new TextField("Sauvegarde à restaurer") { name = "variant-backup-id" };

        public VariantPanel(GenerationStore store, RunnerCatalog catalog, RunnerGenerator generator,
            Action<RunnerConfigV1> load)
        {
            this.store = store;
            this.catalog = catalog;
            name = "variant-panel";
            style.flexGrow = 1f;
            contentContainer.style.paddingLeft = 8f;
            contentContainer.style.paddingRight = 8f;
            contentContainer.style.paddingBottom = 8f;

            Add(ActionButton("refresh-variants", "Actualiser les variantes", () => Refresh()));
            list.style.height = 94f;
            list.itemsSource = variants;
            list.makeItem = () => new Label { style = { overflow = Overflow.Hidden, textOverflow = TextOverflow.Ellipsis } };
            list.bindItem = (element, index) =>
            {
                var variant = variants[index];
                var label = (Label)element;
                label.text = $"{variant.Manifest.title} · {variant.Id.Substring(0, 8)}";
                label.tooltip = variant.Id;
            };
            list.selectionChanged += _ => ShowSelection();
            Add(list);

            var sceneActions = Row(actions);
            sceneActions.Add(ActionButton("open-variant", "Ouvrir", () => Open(preview: false)));
            sceneActions.Add(ActionButton("preview-variant", "Prévisualiser", () => Open(preview: true)));
            sceneActions.Add(ActionButton("edit-variant-settings", "Inspecter les réglages", () =>
                Selection.activeObject = Settings(ReadSelected())));
            sceneActions.Add(ActionButton("save-variant-edits", "Enregistrer les modifications", Save));
            sceneActions.Add(ActionButton("load-variant-settings", "Reprendre la configuration", () =>
                load(Settings(ReadSelected()).ToConfig())));

            var archiveActions = Row(actions);
            var duplicate = ActionButton("duplicate-variant", "Dupliquer", () =>
            {
                var result = generator.Duplicate(ReadSelected());
                if (!result.Succeeded)
                {
                    Report(string.Join("\n", result.Diagnostics.Select(item => item.Message)), HelpBoxMessageType.Error);
                    return;
                }
                Refresh(result.VariantId);
                Report("Variante dupliquée.");
            });
            duplicate.SetEnabled(generator != null);
            archiveActions.Add(duplicate);
            archiveActions.Add(ActionButton("backup-variant", "Créer une sauvegarde", Backup));

            actions.Add(displayTitle);
            actions.Add(ActionButton("rename-variant", "Renommer", () =>
            {
                var renamed = store.Rename(ReadSelected().Id, displayTitle.value);
                Refresh(renamed.Id);
                Report("Titre de la variante enregistré.");
            }));
            Add(actions);

            backupId.value = SessionState.GetString(BackupSessionKey, string.Empty);
            Add(backupId);
            Add(ActionButton("restore-variant", "Restaurer en nouvelle variante", () =>
            {
                var restored = store.Restore(backupId.value.Trim());
                Refresh(restored.Id);
                Report("Sauvegarde restaurée dans une nouvelle variante.");
            }));
            Add(status);
            Refresh();
        }

        public void Refresh(string selectedId = null)
        {
            selectedId ??= (list.selectedItem as StoredVariant)?.Id ?? SessionState.GetString(SelectionSessionKey, string.Empty);
            variants.Clear();
            var skipped = 0;
            var folders = AssetDatabase.IsValidFolder(GenerationStore.GeneratedRoot)
                ? AssetDatabase.GetSubFolders(GenerationStore.GeneratedRoot)
                : Array.Empty<string>();
            foreach (var path in folders)
            {
                var id = Path.GetFileName(path);
                if (!Guid.TryParseExact(id, "N", out _))
                    continue;
                try
                {
                    variants.Add(store.ReadVariant(id));
                }
                catch (Exception exception) when (exception is IOException || exception is InvalidDataException ||
                    exception is UnauthorizedAccessException || exception is ArgumentException)
                {
                    skipped++;
                }
            }
            variants.Sort((left, right) => StringComparer.CurrentCulture.Compare(left.Manifest.title, right.Manifest.title));
            list.Rebuild();
            var selected = variants.FindIndex(variant => variant.Id == selectedId);
            list.selectedIndex = selected >= 0 || variants.Count == 0 ? selected : 0;
            ShowSelection();
            if (skipped > 0)
                Report($"Variantes illisibles : {skipped}.", HelpBoxMessageType.Warning);
            else
                Report(variants.Count == 0 ? "Générez une variante pour la retrouver ici." : string.Empty);
        }

        private void ShowSelection()
        {
            var current = list.selectedItem as StoredVariant;
            actions.SetEnabled(current != null);
            displayTitle.SetValueWithoutNotify(current?.Manifest.title ?? string.Empty);
            if (current != null)
                SessionState.SetString(SelectionSessionKey, current.Id);
        }

        private void Open(bool preview)
        {
            var variant = ReadSelected();
            if (preview)
                ValidateSettings(Settings(variant));
            if (!VariantSceneNavigator.TryOpen(variant.ScenePath))
                return;
            if (preview)
                EditorApplication.EnterPlaymode();
        }

        private void Save()
        {
            var variant = ReadSelected();
            var settings = Settings(variant);
            ValidateSettings(settings);
            var scene = SceneManager.GetSceneByPath(variant.ScenePath);
            if (scene.IsValid() && scene.isLoaded && scene.isDirty && !EditorSceneManager.SaveScene(scene))
                throw new IOException("La scène n’a pas pu être enregistrée.");
            AssetDatabase.SaveAssetIfDirty(settings);
            Report("Modifications enregistrées.");
        }

        private void Backup()
        {
            var variant = ReadSelected();
            ValidateSettings(Settings(variant));
            backupId.value = store.Backup(variant.Id);
            SessionState.SetString(BackupSessionKey, backupId.value);
            Report("Sauvegarde créée. Conservez son identifiant pour la retrouver.");
        }

        private void ValidateSettings(RunnerSettings settings)
        {
            var diagnostics = RunnerConfigValidator.Validate(settings.ToConfig(), catalog);
            if (diagnostics.Count > 0)
                throw new InvalidDataException(string.Join("\n", diagnostics.Select(item => item.Message)));
        }

        private StoredVariant ReadSelected() => store.ReadVariant(((StoredVariant)list.selectedItem).Id);

        private static RunnerSettings Settings(StoredVariant variant)
        {
            var settings = AssetDatabase.LoadAssetAtPath<RunnerSettings>(variant.SettingsPath);
            return settings != null ? settings : throw new FileNotFoundException("Réglages de la variante introuvables.");
        }

        private Button ActionButton(string name, string label, Action action) => new Button(() =>
        {
            try
            {
                action();
            }
            catch (Exception exception) when (exception is IOException || exception is InvalidDataException ||
                exception is UnauthorizedAccessException || exception is ArgumentException || exception is InvalidOperationException)
            {
                Report(exception.Message, HelpBoxMessageType.Error);
            }
        }) { name = name, text = label };

        private void Report(string message, HelpBoxMessageType type = HelpBoxMessageType.Info)
        {
            status.text = message;
            status.messageType = type;
            status.style.display = string.IsNullOrEmpty(message) ? DisplayStyle.None : DisplayStyle.Flex;
        }

        private static VisualElement Row(VisualElement parent)
        {
            var row = new VisualElement { style = { flexDirection = FlexDirection.Row, flexWrap = Wrap.Wrap } };
            parent.Add(row);
            return row;
        }
    }
}
