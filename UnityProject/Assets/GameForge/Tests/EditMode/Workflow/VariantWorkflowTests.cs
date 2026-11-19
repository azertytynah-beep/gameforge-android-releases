using System.Collections;
using System.IO;
using System.Linq;
using GameForge.Editor;
using GameForge.Runtime;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace GameForge.Tests.EditMode.Workflow
{
    public sealed class VariantWorkflowTests
    {
        private const string AppearancePreference = "GameForge.WorkspaceAppearance";

        [UnityTest]
        public IEnumerator GameForgeWindowGeneratesOpensSavesAndRestoresAnEditedVariant()
        {
            if (Enumerable.Range(0, SceneManager.sceneCount).Any(index => SceneManager.GetSceneAt(index).isDirty))
                Assert.Ignore("Enregistrez les scènes ouvertes avant ce test de changement de scène.");

            var original = EditorSceneManager.GetSceneManagerSetup();
            var originalWindows = Resources.FindObjectsOfTypeAll<GameForgeWindow>();
            var originalFolders = AssetDatabase.IsValidFolder(GenerationStore.GeneratedRoot)
                ? AssetDatabase.GetSubFolders(GenerationStore.GeneratedRoot)
                : System.Array.Empty<string>();
            var originalSelection = Selection.activeObject;
            var previousBackup = SessionState.GetString("GameForge.LastBackup", string.Empty);
            var previousSelection = SessionState.GetString("GameForge.SelectedVariant", string.Empty);
            StoredVariant generated = null;
            StoredVariant restored = null;
            string backup = null;
            Assert.That(EditorApplication.ExecuteMenuItem("Tools/GameForge"), Is.True);
            yield return null;
            var window = Resources.FindObjectsOfTypeAll<GameForgeWindow>().FirstOrDefault();
            Assert.That(window, Is.Not.Null);
            try
            {
                window.CreateGUI();
                CheckAppearance(window);

                Click(window, "gameforge-step-2");
                var form = window.rootVisualElement.Q<RunnerConfigurationForm>();
                var list = window.rootVisualElement.Q<ListView>("published-variants");
                list.ClearSelection();
                Click(window, "gameforge-step-3");
                Assert.That(window.rootVisualElement.Q<Button>("gameforge-export-apk").enabledInHierarchy, Is.False);

                const string title = "Parcours GameForge";
                Click(window, "gameforge-step-0");
                window.rootVisualElement.Q<TextField>("gameforge-game-name").value = title;
                Click(window, "gameforge-next");
                Assert.That(window.rootVisualElement.Q<Button>("gameforge-step-1")
                    .ClassListContains("gameforge-step--active"), Is.True);
                Assert.That(form.Q<TextField>("title").value, Is.EqualTo(title));
                Click(window, "generate");
                generated = list.selectedItem as StoredVariant;
                Assert.That(generated?.Manifest.title, Is.EqualTo(title));
                Assert.That(window.rootVisualElement.Q<Button>("gameforge-step-2")
                    .ClassListContains("gameforge-step--active"), Is.True);
                Click(window, "gameforge-step-3");
                Assert.That(window.rootVisualElement.Q<TextElement>("gameforge-export-variant").text, Does.Contain(title));
                Assert.That(window.rootVisualElement.Q<Button>("gameforge-export-apk").enabledInHierarchy, Is.True);
                Click(window, "gameforge-step-2");
                Click(window, "open-variant");
                var scene = SceneManager.GetActiveScene();
                Assert.That(scene.path, Is.EqualTo(generated.ScenePath));
                var root = scene.GetRootGameObjects().Single();
                root.name = "Runner édité";
                EditorSceneManager.MarkSceneDirty(scene);

                Click(window, "edit-variant-settings");
                Assert.That(AssetDatabase.GetAssetPath(Selection.activeObject), Is.EqualTo(generated.SettingsPath));
                var editedSettings = new SerializedObject(Selection.activeObject);
                editedSettings.FindProperty("configuration.seed").intValue = 820;
                editedSettings.ApplyModifiedProperties();
                Click(window, "save-variant-edits");
                Assert.That(scene.isDirty, Is.False);

                Click(window, "backup-variant");
                backup = window.rootVisualElement.Q<TextField>("variant-backup-id").value;
                Assert.That(System.Guid.TryParseExact(backup, "N", out _), Is.True);
                Click(window, "restore-variant");
                restored = list.selectedItem as StoredVariant;
                Assert.That(restored.Id, Is.Not.EqualTo(generated.Id));
                Assert.That(restored.Manifest.parentVariantId, Is.EqualTo(generated.Id));

                Click(window, "open-variant");
                var restoredScene = SceneManager.GetActiveScene();
                Assert.That(restoredScene.path, Is.EqualTo(restored.ScenePath));
                var restoredRoot = restoredScene.GetRootGameObjects().Single();
                Assert.That(restoredRoot.name, Is.EqualTo("Runner édité"));
                var bootstrap = new SerializedObject(restoredRoot.GetComponent<RunnerBootstrap>());
                var restoredSettings = AssetDatabase.LoadAssetAtPath<RunnerSettings>(restored.SettingsPath);
                Assert.That(bootstrap.FindProperty("settings").objectReferenceValue, Is.EqualTo(restoredSettings));
                Assert.That(restoredSettings.Seed, Is.EqualTo(820));

                Click(window, "load-variant-settings");
                Assert.That(form.Q<TextField>("title").value, Is.EqualTo(title));
                Assert.That(form.Q<TextField>("seed").value, Is.EqualTo("820"));
                form.Q<TextField>("maxSpeed").value = "100";
                Assert.That(form.TryGenerate(), Is.False);
                Assert.That(form.Q<HelpBox>("maxSpeed-error").text, Does.Contain("18"));
            }
            finally
            {
                if (original.All(scene => !string.IsNullOrEmpty(scene.path)))
                    EditorSceneManager.RestoreSceneManagerSetup(original);
                else
                    EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

                RemoveNewVariant(restored, originalFolders);
                RemoveNewVariant(generated, originalFolders);
                if (backup != previousBackup && System.Guid.TryParseExact(backup, "N", out _))
                {
                    var directory = Path.Combine(Application.dataPath, "..", "Backups", "GameForge", backup);
                    foreach (var file in Directory.EnumerateFiles(directory))
                        File.Delete(file);
                    Directory.Delete(directory);
                }

                SessionState.SetString("GameForge.LastBackup", previousBackup);
                SessionState.SetString("GameForge.SelectedVariant", previousSelection);
                Selection.activeObject = originalSelection;
                if (!originalWindows.Contains(window))
                    window.Close();
            }
        }

        [Test]
        public void CancelledNavigationKeepsAnUnsavedSceneAndItsObjects()
        {
            var original = SceneManager.GetActiveScene();
            var unsaved = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            try
            {
                SceneManager.SetActiveScene(unsaved);
                var marker = new GameObject("Modification non sauvegardée");
                EditorSceneManager.MarkSceneDirty(unsaved);
                var asked = false;
                Assert.That(VariantSceneNavigator.TryOpen(RunnerGenerator.SourceScenePath, () =>
                {
                    asked = true;
                    return false;
                }), Is.False);
                Assert.That(asked, Is.True);
                Assert.That(SceneManager.GetActiveScene(), Is.EqualTo(unsaved));
                Assert.That(unsaved.isDirty, Is.True);
                Assert.That(unsaved.GetRootGameObjects(), Does.Contain(marker));
            }
            finally
            {
                SceneManager.SetActiveScene(original);
                EditorSceneManager.CloseScene(unsaved, true);
            }
        }

        private static void CheckAppearance(GameForgeWindow window)
        {
            var root = window.rootVisualElement;
            var workspace = root.Q("gameforge-workspace");
            var appearance = root.Q<DropdownField>("gameforge-appearance");
            Assert.That(appearance.choices, Is.EqualTo(new[] { "Unity", "Clair", "Sombre" }));
            var initial = appearance.value;
            var hadPreference = EditorPrefs.HasKey(AppearancePreference);
            var originalPreference = EditorPrefs.GetString(AppearancePreference);
            try
            {
                appearance.value = "Clair";
                Assert.That(workspace.ClassListContains("gameforge-workspace--light"), Is.True);
                Assert.That(workspace.ClassListContains("gameforge-workspace--dark"), Is.False);
                appearance.value = "Sombre";
                Assert.That(workspace.ClassListContains("gameforge-workspace--dark"), Is.True);
                Assert.That(workspace.ClassListContains("gameforge-workspace--light"), Is.False);
                Assert.That(EditorPrefs.GetString(AppearancePreference), Is.EqualTo("Sombre"));
            }
            finally
            {
                appearance.value = initial;
                if (hadPreference)
                    EditorPrefs.SetString(AppearancePreference, originalPreference);
                else
                    EditorPrefs.DeleteKey(AppearancePreference);
            }
        }

        private static void Click(GameForgeWindow window, string name)
        {
            var button = window.rootVisualElement.Q<Button>(name);
            Assert.That(button, Is.Not.Null, name);
            Assert.That(button.enabledInHierarchy, Is.True, name);
            for (var element = (VisualElement)button; element != null; element = element.parent)
                Assert.That(element.style.display.value, Is.Not.EqualTo(DisplayStyle.None), name);
            using var submit = NavigationSubmitEvent.GetPooled();
            submit.target = button;
            button.SendEvent(submit);
        }

        private static void RemoveNewVariant(StoredVariant variant, string[] originalFolders)
        {
            if (variant == null || !System.Guid.TryParseExact(variant.Id, "N", out _))
                return;
            var folder = GenerationStore.GeneratedRoot + "/" + variant.Id;
            if (!originalFolders.Contains(folder) && AssetDatabase.IsValidFolder(folder))
                AssetDatabase.DeleteAsset(folder);
        }
    }
}
