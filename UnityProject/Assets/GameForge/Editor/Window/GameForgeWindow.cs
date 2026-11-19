using System.Collections.Generic;
using GameForge.Runtime;
using UnityEditor;
using UnityEngine;

namespace GameForge.Editor
{
    public sealed class GameForgeWindow : EditorWindow
    {
        private RunnerConfigurationForm form;

        [MenuItem("Tools/GameForge")]
        public static void Open() => GetWindow<GameForgeWindow>("GameForge");

        public void CreateGUI()
        {
            minSize = new Vector2(360f, 300f);
            form ??= new RunnerConfigurationForm();
            rootVisualElement.Clear();
            rootVisualElement.Add(form);
        }

        public void Bind(IRunnerCatalog catalog, IReadOnlyList<string> themeIds,
            IReadOnlyList<string> obstacleIds, IGameGenerator generator)
        {
            form ??= new RunnerConfigurationForm();
            form.Bind(catalog, themeIds, obstacleIds, generator);
        }
    }
}
