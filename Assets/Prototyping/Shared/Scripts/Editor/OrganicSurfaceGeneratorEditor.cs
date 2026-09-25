using UnityEditor;
using UnityEngine;

namespace Televised.Prototyping.Shared.EditorTools
{
    [CustomEditor(typeof(OrganicSurfaceGenerator))]
    [CanEditMultipleObjects]
    public class OrganicSurfaceGeneratorEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            EditorGUILayout.Space();
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Randomize Seed"))
                {
                    foreach (Object t in targets)
                    {
                        var gen = (OrganicSurfaceGenerator)t;
                        Undo.RecordObject(gen, "Randomize Seed");
                        gen.RandomizeSeed();
                        PrefabUtility.RecordPrefabInstancePropertyModifications(gen);
                    }
                }
                if (GUILayout.Button("Regenerate"))
                {
                    foreach (Object t in targets) ((OrganicSurfaceGenerator)t).Regenerate();
                }
            }
        }
    }
}
