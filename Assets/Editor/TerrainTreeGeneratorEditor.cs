using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(TerrainTreeGenerator))]
public class TerrainTreeGeneratorEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        TerrainTreeGenerator generator = (TerrainTreeGenerator)target;

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Tree Generation", EditorStyles.boldLabel);

        if (GUILayout.Button("Generate Trees", GUILayout.Height(30)))
        {
            generator.GenerateTrees();
            EditorUtility.SetDirty(generator);
        }

        if (GUILayout.Button("Clear All Trees", GUILayout.Height(30)))
        {
            if (EditorUtility.DisplayDialog("Clear Trees", 
                "Are you sure you want to delete all generated trees?", 
                "Yes", "Cancel"))
            {
                generator.ClearAllTrees();
                EditorUtility.SetDirty(generator);
            }
        }
    }
}
