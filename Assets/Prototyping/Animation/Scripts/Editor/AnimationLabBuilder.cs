using System.IO;
using Televised.Prototyping.Shared;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Televised.Prototyping.AnimationLab.EditorTools
{
    /// <summary>
    /// Builds Scenes/AnimationLab.unity: the single reusable scene for character rig, expression and shader
    /// experiments. It starts as an empty stage (neutral background, zoomable camera, a Stage root at the origin);
    /// characters and lab tooling are added to it as experiments need them.
    /// Menu: Prototyping/Animation/Open Animation Lab, Prototyping/Animation/Rebuild Animation Lab.
    /// </summary>
    public static class AnimationLabBuilder
    {
        public const string Root = "Assets/Prototyping/Animation";
        public const string ScenePath = Root + "/Scenes/AnimationLab.unity";

        // ------------------------------------------------------------------ menu

        [MenuItem("Prototyping/Animation/Rebuild Animation Lab")]
        public static void RebuildMenu() => Build(true);

        [MenuItem("Prototyping/Animation/Open Animation Lab")]
        public static void OpenMenu()
        {
            if (!File.Exists(ScenePath)) { Build(false); return; }
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                EditorSceneManager.OpenScene(ScenePath);
        }

        // ------------------------------------------------------------------ build

        public static bool Build(bool confirmOverwrite)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[AnimationLab] Exit Play Mode before rebuilding.");
                return false;
            }
            if (confirmOverwrite && File.Exists(ScenePath) &&
                !EditorUtility.DisplayDialog("Rebuild Animation Lab",
                    "This overwrites AnimationLab.unity, including anything added to it by hand.", "Rebuild", "Cancel"))
                return false;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                Debug.Log("[AnimationLab] Build cancelled.");
                return false;
            }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 5f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.36f, 0.36f, 0.38f); // neutral mid-grey: light and dark art both read
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 100f;
            camGo.transform.position = new Vector3(0f, 0f, -10f);
            camGo.AddComponent<AudioListener>();

            // No target: the camera stays on overviewCenter and only zooms (scroll).
            var labCam = camGo.AddComponent<SandboxCamera>();
            var camSo = new SerializedObject(labCam);
            camSo.FindProperty("size").floatValue = 5f;
            camSo.FindProperty("sizeRange").vector2Value = new Vector2(0.5f, 20f);
            camSo.FindProperty("overviewCenter").vector2Value = Vector2.zero;
            camSo.ApplyModifiedPropertiesWithoutUndo();

            var stage = new GameObject("Stage");
            HungryRigBuilder.Build(stage.transform);

            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.Refresh();
            Debug.Log($"[AnimationLab] Built {ScenePath}. Put characters under Stage.");
            return true;
        }
    }
}
