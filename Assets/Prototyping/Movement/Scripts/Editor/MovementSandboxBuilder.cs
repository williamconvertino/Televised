using System.Collections.Generic;
using System.IO;
using Televised.Prototyping.Shared;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using static Televised.Prototyping.Shared.EditorTools.PrototypeAssetBuilder;

namespace Televised.Prototyping.Movement.EditorTools
{
    /// <summary>
    /// Regenerates the shared prototype assets (see <see cref="PrototypeAssetBuilder"/>)
    /// and builds the MovementSandbox scene.
    /// Menu: Prototyping/Movement/Rebuild Sandbox (Assets + Scene).
    /// Runs automatically once if the sandbox scene does not exist yet.
    /// </summary>
    public static class MovementSandboxBuilder
    {
        public const string Root = "Assets/Prototyping/Movement";
        public const string ScenePath = Root + "/Scenes/MovementSandbox.unity";

        static readonly Color ArenaFill = new Color(0.20f, 0.22f, 0.28f);
        static readonly Color ArenaOutline = new Color(0.50f, 0.60f, 0.72f);
        static readonly Color ShapeFill = new Color(0.24f, 0.30f, 0.40f);
        static readonly Color ShapeOutline = new Color(0.55f, 0.82f, 0.95f);
        static readonly Color BlobFill = new Color(0.36f, 0.24f, 0.38f);
        static readonly Color BlobOutline = new Color(0.95f, 0.6f, 0.85f);
        static readonly Color TestFill = new Color(0.25f, 0.36f, 0.28f);
        static readonly Color TestOutline = new Color(0.6f, 0.95f, 0.6f);

        // ------------------------------------------------------------------ menu

        [MenuItem("Prototyping/Movement/Rebuild Sandbox (Assets + Scene)")]
        public static void RebuildAll() => Build(true);

        [MenuItem("Prototyping/Movement/Open Sandbox Scene")]
        public static void OpenSandbox()
        {
            if (!File.Exists(ScenePath)) { Build(true); return; }
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                EditorSceneManager.OpenScene(ScenePath);
        }

        // ------------------------------------------------------------------ build

        public static bool Build(bool confirmOverwrite)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[MovementSandbox] Exit Play Mode before rebuilding.");
                return false;
            }
            if (confirmOverwrite && File.Exists(ScenePath) &&
                !EditorUtility.DisplayDialog("Rebuild Movement Sandbox",
                    "This regenerates the prototype prefabs and overwrites MovementSandbox.unity.\n" +
                    "(DefaultMovementTuning is kept.)", "Rebuild", "Cancel"))
                return false;

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                Debug.Log("[MovementSandbox] Build cancelled.");
                return false;
            }

            BuildAssets(out Material mat, out MovementTuning tuning, out Sprite circle);
            BuildScene(mat);
            Debug.Log($"[MovementSandbox] Built {ScenePath}. Press Play; Tab toggles the tuning panel.");
            return true;
        }

        // ------------------------------------------------------------------ scene

        static void BuildScene(Material mat)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Camera.
            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 8f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.07f, 0.06f, 0.09f);
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 100f;
            camGo.transform.position = new Vector3(-27f, -6f, -10f);
            camGo.AddComponent<AudioListener>();
            var sandboxCam = camGo.AddComponent<SandboxCamera>();

            // Player.
            var playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            var player = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab, scene);
            player.name = "Player";
            player.transform.position = new Vector3(-27f, -8f, 0f);
            var motor = player.GetComponent<PlayerMotor2D>();
            Set(sandboxCam, "target", player.transform);

            // Surfaces.
            BuildSurfaces();

            // Spawn points.
            var spawnRoot = new GameObject("SpawnPoints");
            var spawns = new List<Transform>();
            void Spawn(string n, float x, float y)
            {
                var s = new GameObject($"{spawns.Count + 1}_{n}");
                s.transform.SetParent(spawnRoot.transform, false);
                s.transform.position = new Vector3(x, y, 0f);
                spawns.Add(s.transform);
            }
            Spawn("Floor", -27f, -8f);
            Spawn("CompassCenter", -13f, 8f);
            Spawn("LargeBlob", -2f, 9f);
            Spawn("Cluster", 23.5f, 0f);
            Spawn("UnderOverhang", -25f, 5.3f);
            Spawn("ClosePair", 13.9f, 2.5f);
            Spawn("TallNarrow", 6.5f, 2f);
            Spawn("StretchedBlob", 4f, 13.5f);
            Spawn("ConcaveBlob", 24f, 13.5f);

            // Debug / tooling.
            var debugGo = new GameObject("MovementDebug");
            debugGo.AddComponent<MeshFilter>();
            debugGo.AddComponent<MeshRenderer>().sharedMaterial = mat;
            debugGo.AddComponent<DebugLines>();
            var debugRenderer = debugGo.AddComponent<MovementDebugRenderer>();
            var panel = debugGo.AddComponent<MovementDebugPanel>();
            var spawner = debugGo.AddComponent<SandboxSpawner>();

            Set(debugRenderer, "motor", motor);
            Set(panel, "motor", motor);
            Set(panel, "debugRenderer", debugRenderer);
            Set(panel, "legRig", player.GetComponentInChildren<ProceduralLegRig>());
            Set(spawner, "motor", motor);
            var so = new SerializedObject(spawner);
            SerializedProperty list = so.FindProperty("spawnPoints");
            list.arraySize = spawns.Count;
            for (int i = 0; i < spawns.Count; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = spawns[i];
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, ScenePath);
        }

        static void BuildSurfaces()
        {
            var root = new GameObject("Surfaces").transform;

            // 1-3, 13: Arena — floor, walls, ceiling.
            Transform arena = Group(root, "Arena");
            Rect(arena, "Floor", 0f, -10f, 66f, 2f, 0.3f, ArenaFill, ArenaOutline);
            Rect(arena, "Ceiling", 0f, 16f, 66f, 2f, 0.3f, ArenaFill, ArenaOutline);
            Rect(arena, "LeftWall", -32f, 3f, 2f, 28f, 0.3f, ArenaFill, ArenaOutline);
            Rect(arena, "RightWall", 32f, 3f, 2f, 28f, 0.3f, ArenaFill, ArenaOutline);

            // 4: Circle.
            Transform basics = Group(root, "Basics");
            Ellipse(basics, "Circle", -24f, -4.5f, 2f, 2f, ShapeFill, ShapeOutline);

            // 5: Small blob.
            Blob(basics, "SmallBlob", -17.5f, -1.5f, 3, 0.9f, 0.25f, 1f, 1f, 7, 0.4f);

            // 6: Large irregular blob.
            Blob(basics, "LargeIrregularBlob", -2f, 1f, 11, 3.6f, 0.35f, 1.25f, 1f, 11, 0.3f, 0.25f);

            // 7: Tall / narrow object (planted into the floor).
            Rect(basics, "TallNarrow", 6.5f, -4.5f, 1.2f, 9.5f, 0.5f, ShapeFill, ShapeOutline);

            // Extra: stretched platform-like blob, concave-ish blob.
            Blob(basics, "StretchedBlob", 4f, 11f, 21, 2f, 0.2f, 2.2f, 0.5f, 9, 0.5f);
            Blob(basics, "ConcaveBlob", 24f, 9f, 7, 2.2f, 0.5f, 1f, 1f, 7, 0.1f, 0.3f);

            // 8: Two surfaces close enough to jump between (gap 1.1).
            Transform pairs = Group(root, "Pairs");
            Ellipse(pairs, "ClosePair_A", 12f, -1f, 1.4f, 1.4f, TestFill, TestOutline);
            Ellipse(pairs, "ClosePair_B", 15.9f, -1f, 1.4f, 1.4f, TestFill, TestOutline);

            // 9: Two surfaces barely outside attach range of each other (gap 1.7 = diameter + attachDistance + 0.2).
            Ellipse(pairs, "OutOfRange_A", 11.5f, 7f, 1f, 1f, TestFill, TestOutline);
            Ellipse(pairs, "OutOfRange_B", 15.2f, 7f, 1f, 1f, TestFill, TestOutline);

            // 10: Cluster for candidate selection.
            Transform cluster = Group(root, "Cluster");
            Blob(cluster, "Cluster_1", 22f, -3.5f, 31, 0.75f, 0.2f, 1f, 1f, 6, 0.4f);
            Blob(cluster, "Cluster_2", 24f, -2.2f, 32, 0.75f, 0.2f, 1f, 1f, 6, 0.4f);
            Blob(cluster, "Cluster_3", 25.6f, -4f, 33, 0.75f, 0.2f, 1f, 1f, 6, 0.4f);
            Blob(cluster, "Cluster_4", 23.6f, -5.4f, 34, 0.75f, 0.2f, 1f, 1f, 6, 0.4f);
            Blob(cluster, "Cluster_5", 21.5f, -6.3f, 35, 0.75f, 0.2f, 1f, 1f, 6, 0.4f);

            // 11: Overhang from the left wall.
            Transform features = Group(root, "Features");
            Rect(features, "Overhang", -25.5f, 6.5f, 11f, 1.2f, 0.3f, ShapeFill, ShapeOutline);

            // 12: Narrow gap between two pillars (gap 1.3, player diameter 1.0).
            Rect(features, "NarrowGap_Left", -12f, -7.2f, 2f, 4f, 0.25f, ShapeFill, ShapeOutline);
            Rect(features, "NarrowGap_Right", -8.7f, -7.2f, 2f, 4f, 0.25f, ShapeFill, ShapeOutline);

            // 13: Compass — surfaces above, below, left and right of a central point.
            Transform compass = Group(root, "Compass");
            Ellipse(compass, "Compass_Up", -13f, 11.2f, 1f, 1f, TestFill, TestOutline);
            Ellipse(compass, "Compass_Down", -13f, 4.8f, 1f, 1f, TestFill, TestOutline);
            Ellipse(compass, "Compass_Left", -16.2f, 8f, 1f, 1f, TestFill, TestOutline);
            Ellipse(compass, "Compass_Right", -9.8f, 8f, 1f, 1f, TestFill, TestOutline);
        }

        static void Rect(Transform parent, string name, float x, float y, float w, float h, float corner, Color fill, Color outline)
        {
            var go = Instance(RectPrefabPath, parent, name, x, y);
            var shape = go.GetComponent<RectangleSurfaceShape>();
            Configure(shape, so =>
            {
                so.FindProperty("size").vector2Value = new Vector2(w, h);
                so.FindProperty("cornerRadius").floatValue = corner;
                so.FindProperty("fillColor").colorValue = fill;
                so.FindProperty("outlineColor").colorValue = outline;
            });
        }

        static void Ellipse(Transform parent, string name, float x, float y, float rx, float ry, Color fill, Color outline)
        {
            var go = Instance(EllipsePrefabPath, parent, name, x, y);
            var shape = go.GetComponent<EllipseSurfaceShape>();
            Configure(shape, so =>
            {
                so.FindProperty("radii").vector2Value = new Vector2(rx, ry);
                so.FindProperty("segments").intValue = Mathf.Clamp(Mathf.RoundToInt(Mathf.Max(rx, ry) * 36f), 32, 160);
                so.FindProperty("fillColor").colorValue = fill;
                so.FindProperty("outlineColor").colorValue = outline;
            });
        }

        static void Blob(Transform parent, string name, float x, float y, int seed, float radius, float variation,
            float sx, float sy, int points, float smoothing, float asymmetry = 0.15f)
        {
            var go = Instance(BlobPrefabPath, parent, name, x, y);
            var shape = go.GetComponent<OrganicSurfaceGenerator>();
            Configure(shape, so =>
            {
                so.FindProperty("seed").intValue = seed;
                so.FindProperty("radius").floatValue = radius;
                so.FindProperty("radiusVariation").floatValue = variation;
                so.FindProperty("xScale").floatValue = sx;
                so.FindProperty("yScale").floatValue = sy;
                so.FindProperty("pointCount").intValue = points;
                so.FindProperty("smoothing").floatValue = smoothing;
                so.FindProperty("asymmetry").floatValue = asymmetry;
                so.FindProperty("sampleCount").intValue = Mathf.Clamp(Mathf.RoundToInt(radius * Mathf.Max(sx, sy) * 40f), 48, 256);
                so.FindProperty("fillColor").colorValue = BlobFill;
                so.FindProperty("outlineColor").colorValue = BlobOutline;
            });
        }
    }

    /// <summary>Builds the sandbox automatically the first time the scripts compile, if the scene is missing.</summary>
    [InitializeOnLoad]
    static class MovementSandboxAutoBuild
    {
        const string SessionKey = "Televised.MovementSandbox.AutoBuildAttempted";

        static MovementSandboxAutoBuild()
        {
            EditorApplication.delayCall += TryBuild;
        }

        static void TryBuild()
        {
            if (Application.isBatchMode || EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (File.Exists(MovementSandboxBuilder.ScenePath) || SessionState.GetBool(SessionKey, false)) return;
            SessionState.SetBool(SessionKey, true);
            try
            {
                MovementSandboxBuilder.Build(false);
            }
            catch (System.Exception e)
            {
                Debug.LogException(e);
                Debug.LogError("[MovementSandbox] Auto-build failed. Use Prototyping/Movement/Rebuild Sandbox once the error is fixed.");
            }
        }
    }
}
