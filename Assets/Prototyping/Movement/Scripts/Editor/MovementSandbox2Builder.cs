using System.Collections.Generic;
using System.IO;
using Televised.Prototyping.Movement.Playtest;
using Televised.Prototyping.Shared;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using static Televised.Prototyping.Shared.EditorTools.PrototypeAssetBuilder;
using B = Televised.Prototyping.Movement.EditorTools.MovementSandboxBuilder;

namespace Televised.Prototyping.Movement.EditorTools
{
    /// <summary>
    /// Builds Scenes/MovementSandbox2.unity: a second laboratory that mixes static surfaces with moving, spinning
    /// and swinging ones (elevator, ferry, windmill, orbit, pendulum, bobbing stones, spinners, a sliding slab).
    /// Moving surfaces are orange, static ones blue. Motion comes from <see cref="PlaytestMover"/>.
    /// Movers are placed so they never sweep into static geometry the player could be standing on:
    /// the motor doesn't resolve a surface pushing into an attached player, so nothing here can crush you.
    /// Menu: Prototyping/Movement/Rebuild Sandbox 2 (Moving Objects).
    /// </summary>
    public static class MovementSandbox2Builder
    {
        public const string ScenePath = B.Root + "/Scenes/MovementSandbox2.unity";

        static readonly Color ArenaFill = new Color(0.20f, 0.22f, 0.28f);
        static readonly Color ArenaOutline = new Color(0.50f, 0.60f, 0.72f);
        static readonly Color StaticFill = new Color(0.24f, 0.30f, 0.40f);
        static readonly Color StaticOutline = new Color(0.55f, 0.82f, 0.95f);
        static readonly Color MovingFill = new Color(0.42f, 0.27f, 0.14f);
        static readonly Color MovingOutline = new Color(1.0f, 0.72f, 0.32f);

        // ------------------------------------------------------------------ menu

        [MenuItem("Prototyping/Movement/Rebuild Sandbox 2 (Moving Objects)")]
        public static void RebuildMenu() => Build(true);

        [MenuItem("Prototyping/Movement/Open Sandbox 2 (Moving Objects)")]
        public static void OpenMenu()
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
                Debug.LogWarning("[MovementSandbox2] Exit Play Mode before rebuilding.");
                return false;
            }
            if (confirmOverwrite && File.Exists(ScenePath) &&
                !EditorUtility.DisplayDialog("Rebuild Movement Sandbox 2",
                    "This overwrites MovementSandbox2.unity.", "Rebuild", "Cancel"))
                return false;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                Debug.Log("[MovementSandbox2] Build cancelled.");
                return false;
            }

            EnsureAssets(out Material mat, out _);
            BuildScene(mat);
            Debug.Log($"[MovementSandbox2] Built {ScenePath}. Press Play; Tab toggles the tuning panel.");
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
            camGo.transform.position = new Vector3(-37f, -6f, -10f);
            camGo.AddComponent<AudioListener>();
            var sandboxCam = camGo.AddComponent<SandboxCamera>();

            // Player.
            var playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            var player = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab, scene);
            player.name = "Player";
            player.transform.position = new Vector3(-37f, -8.3f, 0f);
            var motor = player.GetComponent<PlayerMotor2D>();
            Set(sandboxCam, "target", player.transform);
            var camSo = new SerializedObject(sandboxCam);
            camSo.FindProperty("overviewCenter").vector2Value = new Vector2(0f, 4f);
            camSo.FindProperty("overviewSize").floatValue = 23f;
            camSo.ApplyModifiedPropertiesWithoutUndo();

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
            Spawn("Start", -37f, -8.3f);
            Spawn("Elevator", -29.5f, -5.4f);
            Spawn("FerryLedge", -24.5f, 0.7f);
            Spawn("WindmillTower", -6.5f, 0.7f);
            Spawn("OrbitCore", 18f, 5.3f);
            Spawn("PendulumLaunch", 29f, 5.7f);
            Spawn("BobbingStones", 23f, -3f);
            Spawn("Spinners", -29f, 5.1f);
            Spawn("SlidingSlab", -12f, 11.6f);

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

            // Arena: floor top y = -9, ceiling bottom y = 17, inner walls at x = ±41.
            Transform arena = Group(root, "Arena");
            B.Rect(arena, "Floor", 0f, -10f, 84f, 2f, 0.3f, ArenaFill, ArenaOutline);
            B.Rect(arena, "Ceiling", 0f, 18f, 84f, 2f, 0.3f, ArenaFill, ArenaOutline);
            B.Rect(arena, "LeftWall", -42f, 4f, 2f, 30f, 0.3f, ArenaFill, ArenaOutline);
            B.Rect(arena, "RightWall", 42f, 4f, 2f, 30f, 0.3f, ArenaFill, ArenaOutline);

            // 1: Elevator on a static pedestal, rising beside a static ledge (top y = 0, gap 1.5).
            Transform elevator = Group(root, "Elevator");
            StaticRect(elevator, "Pedestal", -29.5f, -8.25f, 3f, 2.5f, 0.3f);
            Move(MovingRect(elevator, "Elevator", -29.5f, -6.5f, 3f, 0.8f, 0.3f), new Vector2(0f, 6.2f), 6f);
            StaticRect(elevator, "Ledge", -24.5f, -4.75f, 4f, 9.5f, 0.4f);

            // 2: Spinners above the elevator: a static stepping circle, a spinning ellipse, a spinning blob
            //    and a static stalactite hanging from the ceiling.
            Transform spinners = Group(root, "Spinners");
            StaticEllipse(spinners, "SteppingCircle", -29f, 3.5f, 0.9f, 0.9f);
            Move(MovingEllipse(spinners, "SpinningEllipse", -35f, 8f, 2.6f, 1.1f), Vector2.zero, 1f, 0f, 35f);
            Move(B.Blob(spinners, "SpinningBlob", -24f, 11f, 41, 1.6f, 0.3f, 1f, 1f, 8, 0.4f, 0.2f, MovingFill, MovingOutline),
                Vector2.zero, 1f, 0f, -25f);
            StaticRect(spinners, "Stalactite", -30f, 15.5f, 1.2f, 3.2f, 0.4f);

            // 3: Ferry from the ledge to the windmill tower (1.5 clearance at both ends), a static circle to jump
            //    up to mid-ride, and a static lump on the floor underneath.
            Transform ferry = Group(root, "Ferry");
            Move(MovingRect(ferry, "Ferry", -19.5f, -0.4f, 3f, 0.8f, 0.3f), new Vector2(8.5f, 0f), 7f);
            StaticEllipse(ferry, "OverheadCircle", -15.5f, 3.6f, 1.1f, 1.1f);
            B.Blob(ferry, "FloorLump", -15.5f, -8f, 42, 1.8f, 0.2f, 1.6f, 0.8f, 9, 0.45f, 0.15f, StaticFill, StaticOutline);

            // 4: Windmill: a rotating bar around a static hub (the hub pokes out, so you can crawl between them),
            //    between the static tower and a static pillar, with a static landing lump below.
            Transform windmill = Group(root, "Windmill");
            StaticRect(windmill, "Tower", -6.5f, -4.75f, 3f, 9.5f, 0.4f);
            Move(MovingRect(windmill, "Bar", 1f, 4.5f, 10f, 0.9f, 0.4f), Vector2.zero, 1f, 0f, 22f);
            StaticEllipse(windmill, "Hub", 1f, 4.5f, 0.9f, 0.9f);
            StaticRect(windmill, "Pillar", 9.5f, -3.75f, 3f, 11.5f, 0.4f);
            B.Blob(windmill, "LandingLump", 1f, -7.8f, 43, 1.6f, 0.2f, 1.8f, 0.7f, 9, 0.45f, 0.15f, StaticFill, StaticOutline);

            // 5: Orbit: three stones circling a static core (the pivot rotates, the stones ride it).
            Transform orbit = Group(root, "Orbit");
            StaticEllipse(orbit, "Core", 18f, 3.5f, 1.2f, 1.2f);
            Transform orbitPivot = Group(orbit, "OrbitPivot");
            orbitPivot.position = new Vector3(18f, 3.5f, 0f);
            for (int i = 0; i < 3; i++)
            {
                float a = (90f + i * 120f) * Mathf.Deg2Rad;
                MovingEllipse(orbitPivot, $"Stone_{i + 1}", 18f + Mathf.Cos(a) * 3.6f, 3.5f + Mathf.Sin(a) * 3.6f, 0.7f, 0.7f);
            }
            Move(orbitPivot.gameObject, Vector2.zero, 1f, 0f, 28f);

            // 6: Pendulum hanging from the ceiling: a crawlable arm with a platform at the end, a static launch
            //    circle under it and a static ledge on the right wall to jump off onto.
            Transform pendulum = Group(root, "Pendulum");
            Transform pendulumPivot = Group(pendulum, "PendulumPivot");
            pendulumPivot.position = new Vector3(29f, 17f, 0f);
            MovingRect(pendulumPivot, "Arm", 29f, 12.7f, 0.4f, 8.6f, 0.15f);
            MovingRect(pendulumPivot, "Platform", 29f, 8.2f, 3.4f, 0.6f, 0.25f);
            Move(pendulumPivot.gameObject, Vector2.zero, 4.5f, 0f, 0f, 32f);
            StaticEllipse(pendulum, "LaunchCircle", 29f, 3.8f, 1.2f, 1.2f);
            StaticRect(pendulum, "WallLedge", 38.75f, 5.9f, 5.5f, 0.8f, 0.3f);

            // 7: Bobbing stones: static and bobbing stones alternate along the floor.
            Transform bobbing = Group(root, "BobbingStones");
            for (int i = 0; i < 5; i++)
            {
                float x = 23f + i * 3.5f;
                if (i % 2 == 0) StaticEllipse(bobbing, $"Stone_{i + 1}_Static", x, -4.5f, 0.8f, 0.8f);
                else Move(MovingEllipse(bobbing, $"Stone_{i + 1}_Bobbing", x, -6f, 0.8f, 0.8f), new Vector2(0f, 3f), 3.2f, i * 0.2f);
            }

            // 8: Upper slider: a slab sliding under the ceiling above a static stretched blob, with static
            //    stepping circles leading up to it and on past it.
            Transform slider = Group(root, "SlidingSlab");
            StaticEllipse(slider, "StepUp", -10f, 6f, 0.8f, 0.8f);
            B.Blob(slider, "StretchedBlob", -12f, 9.8f, 44, 1.4f, 0.2f, 2f, 0.6f, 9, 0.5f, 0.15f, StaticFill, StaticOutline);
            Move(MovingRect(slider, "Slab", -17f, 13.5f, 4f, 0.7f, 0.3f), new Vector2(9f, 0f), 8f);
            StaticEllipse(slider, "FarCircle", -2f, 13f, 0.9f, 0.9f);
        }

        // ------------------------------------------------------------------ helpers

        static GameObject StaticRect(Transform parent, string name, float x, float y, float w, float h, float corner) =>
            B.Rect(parent, name, x, y, w, h, corner, StaticFill, StaticOutline);

        static GameObject MovingRect(Transform parent, string name, float x, float y, float w, float h, float corner) =>
            B.Rect(parent, name, x, y, w, h, corner, MovingFill, MovingOutline);

        static GameObject StaticEllipse(Transform parent, string name, float x, float y, float rx, float ry) =>
            B.Ellipse(parent, name, x, y, rx, ry, StaticFill, StaticOutline);

        static GameObject MovingEllipse(Transform parent, string name, float x, float y, float rx, float ry) =>
            B.Ellipse(parent, name, x, y, rx, ry, MovingFill, MovingOutline);

        /// <summary>Adds a <see cref="PlaytestMover"/>: ping-pong offset, spin (deg/s) and/or pendulum swing (±deg).</summary>
        static void Move(GameObject go, Vector2 offset, float period, float phase = 0f, float rotationSpeed = 0f, float swingAngle = 0f)
        {
            var m = go.AddComponent<PlaytestMover>();
            m.offset = offset;
            m.period = period;
            m.phase = phase;
            m.rotationSpeed = rotationSpeed;
            m.swingAngle = swingAngle;
        }
    }

    /// <summary>Builds sandbox 2 automatically (once per Editor session) if it's missing.</summary>
    [InitializeOnLoad]
    static class MovementSandbox2AutoBuild
    {
        const string SessionKey = "Televised.MovementSandbox2.AutoBuildAttempted";

        static MovementSandbox2AutoBuild()
        {
            EditorApplication.delayCall += () =>
            {
                if (Application.isBatchMode || EditorApplication.isPlayingOrWillChangePlaymode) return;
                if (File.Exists(MovementSandbox2Builder.ScenePath) || SessionState.GetBool(SessionKey, false)) return;
                if (!File.Exists(MovementSandboxBuilder.ScenePath)) return; // let the first sandbox auto-build go first
                SessionState.SetBool(SessionKey, true);
                try { MovementSandbox2Builder.Build(false); }
                catch (System.Exception e) { Debug.LogException(e); }
            };
        }
    }
}
