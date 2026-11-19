using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace GameForge.Editor
{
    public static class VariantSceneNavigator
    {
        public static bool TryOpen(string scenePath, Func<bool> confirmSceneChanges = null)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Arrêtez la prévisualisation avant de changer de scène.");
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath) == null)
                throw new FileNotFoundException("Scène de variante introuvable.");

            if (SceneManager.sceneCount == 1 && SceneManager.GetActiveScene().path == scenePath)
                return true;

            confirmSceneChanges ??= EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo;
            if (!confirmSceneChanges())
                return false;

            EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            return true;
        }
    }
}
