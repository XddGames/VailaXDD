using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor.SceneManagement;

/// <summary>
/// Removes TerrainTreeGenerator components before building to prevent "missing prefab" errors.
/// The tree generator is an editor-only tool - trees are baked into the scene, not spawned at runtime.
/// </summary>
public class TerrainTreeGeneratorBuildProcessor : IPreprocessBuildWithReport, IPostprocessBuildWithReport
{
    public int callbackOrder => -100; // Run early

    public void OnPreprocessBuild(BuildReport report)
    {
        // Process all scenes in the build
        for (int i = 0; i < SceneManager.sceneCountInBuildSettings; i++)
        {
            string scenePath = SceneUtility.GetScenePathByBuildIndex(i);
            if (string.IsNullOrEmpty(scenePath)) continue;
            
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            bool modified = false;
            
            // Find and destroy all TerrainTreeGenerator components in this scene
            var generators = Object.FindObjectsByType<TerrainTreeGenerator>(FindObjectsSortMode.None);
            foreach (var generator in generators)
            {
                Debug.Log($"[Build] Removing TerrainTreeGenerator from '{generator.gameObject.name}' in scene '{scenePath}'");
                Object.DestroyImmediate(generator);
                modified = true;
            }
            
            if (modified)
            {
                EditorSceneManager.SaveScene(scene);
            }
        }
        
        Debug.Log("[Build] TerrainTreeGenerator cleanup complete.");
    }

    public void OnPostprocessBuild(BuildReport report)
    {
        // Note: The components are removed from the scene files
        // You may need to revert your scenes after building if you want the components back
        Debug.Log("[Build] Build complete. Run 'git checkout -- Assets/Scenes/' to restore TerrainTreeGenerator components if needed.");
    }
}
