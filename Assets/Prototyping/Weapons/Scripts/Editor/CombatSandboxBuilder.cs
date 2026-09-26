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

namespace Televised.Prototyping.Weapons.EditorTools
{
    /// <summary>
    /// Builds the combat laboratory: the DummyEnemy prefab and Scenes/CombatSandbox.unity, a movement-first arena that
    /// combines both movement sandboxes' shapes and moving objects, with enemies placed in patterns around them
    /// (rings, orbits, nests, lines, perches) in named spawn groups, and spawn points on keys 1-9.
    /// Surfaces reuse the shared prefabs and the movement sandbox's shape helpers; moving platforms use PlaytestMover.
    /// Menu: Prototyping/Weapons/Rebuild Combat Sandbox.
    /// </summary>
    public static class CombatSandboxBuilder
    {
        public const string Root = "Assets/Prototyping/Weapons";
        public const string ScenePath = Root + "/Scenes/CombatSandbox.unity";
        public const string EnemyPrefabPath = Root + "/Prefabs/Enemies/DummyEnemy.prefab";
        /// <summary>Bump when the generated layout changes: the editor then rebuilds the scene once automatically.</summary>
        public const int LayoutVersion = 3;
        internal const string LayoutVersionKey = "Televised.CombatSandbox.LayoutVersion";

        static readonly Color ArenaFill = new Color(0.20f, 0.22f, 0.28f);
        static readonly Color ArenaOutline = new Color(0.50f, 0.60f, 0.72f);
        static readonly Color ShapeFill = new Color(0.24f, 0.30f, 0.40f);
        static readonly Color ShapeOutline = new Color(0.55f, 0.82f, 0.95f);
        static readonly Color MovingFill = new Color(0.42f, 0.27f, 0.14f);
        static readonly Color MovingOutline = new Color(1.0f, 0.72f, 0.32f);
        static readonly Color ZoneLabelColor = new Color(1f, 0.9f, 0.55f, 0.75f);

        // ------------------------------------------------------------------ menu

        [MenuItem("Prototyping/Weapons/Rebuild Combat Sandbox")]
        public static void RebuildMenu() => Build(true);

        [MenuItem("Prototyping/Weapons/Open Combat Sandbox")]
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
                Debug.LogWarning("[CombatSandbox] Exit Play Mode before rebuilding.");
                return false;
            }
            if (confirmOverwrite && File.Exists(ScenePath) &&
                !EditorUtility.DisplayDialog("Rebuild Combat Sandbox",
                    "This regenerates the DummyEnemy prefab and overwrites CombatSandbox.unity.", "Rebuild", "Cancel"))
                return false;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                Debug.Log("[CombatSandbox] Build cancelled.");
                return false;
            }

            EnsureAssets(out Material mat, out Sprite circle);
            EnsureFolders();
            BuildEnemyPrefab(circle);
            BuildScene(mat);
            EditorPrefs.SetInt(LayoutVersionKey, LayoutVersion);
            Debug.Log($"[CombatSandbox] Built {ScenePath}. Press Play; [ / ] switch weapons, F10 toggles the combat panel.");
            return true;
        }

        static void EnsureFolders()
        {
            foreach (string f in new[] { "Scenes", "Prefabs", "Prefabs/Enemies", "Settings" })
            {
                string full = $"{Root}/{f}";
                if (AssetDatabase.IsValidFolder(full)) continue;
                string parent = Path.GetDirectoryName(full)?.Replace('\\', '/');
                AssetDatabase.CreateFolder(parent, Path.GetFileName(full));
            }
        }

        static void BuildEnemyPrefab(Sprite circle)
        {
            var root = new GameObject("DummyEnemy");
            var enemy = root.AddComponent<DummyEnemy>(); // RequireComponent adds Health, movement and contact damage

            var body = new GameObject("Body");
            body.transform.SetParent(root.transform, false);
            var bodySr = body.AddComponent<SpriteRenderer>();
            bodySr.sprite = circle;
            bodySr.color = DummyEnemy.ModeColor(EnemyMovementMode.Static);
            bodySr.sortingOrder = 5;

            var core = new GameObject("Core");
            core.transform.SetParent(body.transform, false);
            core.transform.localScale = Vector3.one * 0.55f;
            var coreSr = core.AddComponent<SpriteRenderer>();
            coreSr.sprite = circle;
            coreSr.color = new Color(0.12f, 0.05f, 0.07f);
            coreSr.sortingOrder = 6;

            Set(enemy, "body", bodySr);
            Set(enemy, "core", coreSr);
            body.transform.localScale = Vector3.one; // radius 0.5 -> diameter 1

            PrefabUtility.SaveAsPrefabAsset(root, EnemyPrefabPath);
            Object.DestroyImmediate(root);
        }

        // ------------------------------------------------------------------ scene

        static void BuildScene(Material mat)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Camera.
            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 9f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.07f, 0.06f, 0.09f);
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 100f;
            camGo.transform.position = new Vector3(-57f, 4f, -10f);
            camGo.AddComponent<AudioListener>();
            var sandboxCam = camGo.AddComponent<SandboxCamera>();

            // Player (shared prefab) + the combat components that sit on it.
            var playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            var player = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab, scene);
            player.name = "Player";
            player.transform.position = new Vector3(-57f, 0.5f, 0f);
            var motor = player.GetComponent<PlayerMotor2D>();
            var playerHealth = player.AddComponent<PlayerHealth>();
            var bodyImpact = player.AddComponent<BodyImpactDamage>();
            Set(sandboxCam, "target", player.transform);
            var camSo = new SerializedObject(sandboxCam);
            camSo.FindProperty("size").floatValue = 9f;
            camSo.FindProperty("sizeRange").vector2Value = new Vector2(3f, 36f);
            camSo.FindProperty("overviewCenter").vector2Value = new Vector2(0f, 15f);
            camSo.FindProperty("overviewSize").floatValue = 33f;
            camSo.ApplyModifiedPropertiesWithoutUndo();

            BuildSurfaces();
            Transform markers = new GameObject("Markers").transform;
            BuildEnemies(markers);

            // Spawn points (keys 1-9).
            var spawnRoot = new GameObject("SpawnPoints");
            var spawns = new List<Transform>();
            void Spawn(string n, float x, float y)
            {
                var s = new GameObject($"{spawns.Count + 1}_{n}");
                s.transform.SetParent(spawnRoot.transform, false);
                s.transform.position = new Vector3(x, y, 0f);
                spawns.Add(s.transform);
            }
            Spawn("WestFloor", -57f, 0.5f);
            Spawn("Compass", -37f, 14f);
            Spawn("BigBlob", -17f, 16.5f);
            Spawn("NarrowGap", -12f, 0.5f);
            Spawn("WindmillTower", 1f, 10.6f);
            Spawn("OrbitCore", 30f, 11.8f);
            Spawn("PendulumLaunch", 37f, 16.8f);
            Spawn("BobbingStones", 44f, 5.9f);
            Spawn("ConcaveBlob", 55f, 16.5f);

            // Movement tooling (debug lines, movement panel hidden by default, spawn keys).
            var debugGo = new GameObject("MovementDebug");
            debugGo.AddComponent<MeshFilter>();
            debugGo.AddComponent<MeshRenderer>().sharedMaterial = mat;
            debugGo.AddComponent<DebugLines>();
            var debugRenderer = debugGo.AddComponent<MovementDebugRenderer>();
            var movementPanel = debugGo.AddComponent<MovementDebugPanel>();
            var spawner = debugGo.AddComponent<SandboxSpawner>();
            Set(debugRenderer, "motor", motor);
            debugRenderer.drawEnabled = false;
            EditorUtility.SetDirty(debugRenderer);
            Set(movementPanel, "motor", motor);
            Set(movementPanel, "debugRenderer", debugRenderer);
            Set(movementPanel, "legRig", player.GetComponentInChildren<ProceduralLegRig>());
            var mpSo = new SerializedObject(movementPanel);
            mpSo.FindProperty("panelVisible").boolValue = false;
            mpSo.FindProperty("showSurfaceLabels").boolValue = false;
            mpSo.FindProperty("alwaysShowStatus").boolValue = false;
            mpSo.ApplyModifiedPropertiesWithoutUndo();
            Set(spawner, "motor", motor);
            var spSo = new SerializedObject(spawner);
            SerializedProperty list = spSo.FindProperty("spawnPoints");
            list.arraySize = spawns.Count;
            for (int i = 0; i < spawns.Count; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = spawns[i];
            spSo.ApplyModifiedPropertiesWithoutUndo();

            // Combat systems.
            var shapesGo = new GameObject("CombatShapes");
            shapesGo.AddComponent<MeshFilter>();
            shapesGo.AddComponent<MeshRenderer>().sharedMaterial = mat;
            shapesGo.AddComponent<CombatShapes>();

            var combat = new GameObject("Combat");
            var enemyManager = combat.AddComponent<EnemyManager>();
            var projectiles = combat.AddComponent<ProjectileSystem>();
            var controller = combat.AddComponent<WeaponController>();
            var worldUI = combat.AddComponent<CombatWorldUI>();
            var panel = combat.AddComponent<CombatDebugPanel>();

            var weaponsGo = new GameObject("Weapons");
            weaponsGo.transform.SetParent(combat.transform, false);
            var weapons = new List<PrototypeWeapon>
            {
                weaponsGo.AddComponent<SimpleShotWeapon>(),
                weaponsGo.AddComponent<BurstWeapon>(),
                weaponsGo.AddComponent<EyeBeamWeapon>(),
                weaponsGo.AddComponent<HeavyShotWeapon>(),
                weaponsGo.AddComponent<FlamethrowerWeapon>(),
                weaponsGo.AddComponent<EyePulseWeapon>(),
                weaponsGo.AddComponent<SniperWeapon>(),
                weaponsGo.AddComponent<HolyBeamWeapon>(),
                weaponsGo.AddComponent<HolyBeamWeapon>(),
                weaponsGo.AddComponent<ChainWeapon>(),
            };
            // Flail is disabled for now: kept on the object for its tuning, but not selectable.
            weaponsGo.AddComponent<FlailWeapon>().enabled = false;
            var crossSo = new SerializedObject(weapons[8]);
            crossSo.FindProperty("displayName").stringValue = "Cross Holy Beam";
            crossSo.FindProperty("settings.mode").enumValueIndex = (int)HolyBeamMode.Cross;
            crossSo.FindProperty("settings.cooldown").floatValue = 1f;
            crossSo.ApplyModifiedPropertiesWithoutUndo();

            Set(controller, "motor", motor);
            Set(controller, "projectiles", projectiles);
            var wcSo = new SerializedObject(controller);
            SerializedProperty wl = wcSo.FindProperty("weapons");
            wl.arraySize = weapons.Count;
            for (int i = 0; i < weapons.Count; i++) wl.GetArrayElementAtIndex(i).objectReferenceValue = weapons[i];
            wcSo.ApplyModifiedPropertiesWithoutUndo();

            Set(panel, "weapons", controller);
            Set(panel, "enemies", enemyManager);
            Set(panel, "playerHealth", playerHealth);
            Set(panel, "bodyImpact", bodyImpact);
            Set(panel, "worldUI", worldUI);
            Set(panel, "movementPanel", movementPanel);
            Set(panel, "movementDebugRenderer", debugRenderer);

            EditorSceneManager.SaveScene(scene, ScenePath);
        }

        // ------------------------------------------------------------------ surfaces
        //
        // One 120 x 30 arena (floor top y = 0, ceiling bottom y = 30, inner walls at x = ±60) built from the movement
        // sandboxes' motifs, west to east:
        //   Elevator & Compass | Big Blob & Narrow Gap | Windmill | Orbit & Pendulum | Bobbing Stones & Slab
        // Static surfaces are blue, moving ones orange. Movers never sweep into static surfaces the player can stand on.

        static void BuildSurfaces()
        {
            var root = new GameObject("Surfaces").transform;

            Transform arena = Group(root, "Arena");
            B.Rect(arena, "Floor", 0f, -1f, 124f, 2f, 0.3f, ArenaFill, ArenaOutline);
            B.Rect(arena, "Ceiling", 0f, 31f, 124f, 2f, 0.3f, ArenaFill, ArenaOutline);
            B.Rect(arena, "LeftWall", -61f, 15f, 2f, 34f, 0.3f, ArenaFill, ArenaOutline);
            B.Rect(arena, "RightWall", 61f, 15f, 2f, 34f, 0.3f, ArenaFill, ArenaOutline);

            // West: an elevator beside a ledge, an overhang off the wall, a spinner under a stalactite, the compass.
            Transform west = Group(root, "West_ElevatorCompass");
            Rect(west, "Pedestal", -52f, 1.25f, 3f, 2.5f);
            Move(MovingRect(west, "Elevator", -52f, 3f, 3f, 0.8f), new Vector2(0f, 7.5f), 6f);
            Rect(west, "Ledge", -46.5f, 5f, 4f, 10f);
            Rect(west, "Overhang", -55.5f, 16f, 11f, 1.2f);
            Move(MovingEllipse(west, "Spinner", -53f, 23f, 2.6f, 1.1f), Vector2.zero, 1f, 0f, 35f);
            Rect(west, "Stalactite", -44f, 26.5f, 1.2f, 9f);
            Ellipse(west, "Compass_Up", -37f, 17.2f, 1f, 1f);
            Ellipse(west, "Compass_Down", -37f, 10.8f, 1f, 1f);
            Ellipse(west, "Compass_Left", -40.2f, 14f, 1f, 1f);
            Ellipse(west, "Compass_Right", -33.8f, 14f, 1f, 1f);
            B.Blob(west, "SmallBlob", -41f, 3.2f, 3, 0.9f, 0.25f, 1f, 1f, 7, 0.4f);

            // West-centre: the big irregular blob, a tall pillar, the narrow gap, jumpable circle pairs.
            Transform blob = Group(root, "BigBlob_NarrowGap");
            Rect(blob, "TallPillar", -28f, 4.75f, 1.2f, 9.5f);
            B.Blob(blob, "LargeIrregularBlob", -17f, 10f, 11, 3.6f, 0.35f, 1.25f, 1f, 11, 0.3f, 0.25f);
            Rect(blob, "NarrowGap_Left", -9f, 2f, 2f, 4f);
            Rect(blob, "NarrowGap_Right", -5.7f, 2f, 2f, 4f);
            Ellipse(blob, "ClosePair_A", -24f, 22f, 1.4f, 1.4f);
            Ellipse(blob, "ClosePair_B", -20.1f, 22f, 1.4f, 1.4f);
            Ellipse(blob, "FarPair_A", -12.5f, 24f, 1f, 1f);
            Ellipse(blob, "FarPair_B", -8.8f, 24f, 1f, 1f);

            // Centre: a windmill (rotating bar round a hub) between a tower and a pillar, a ferry over the top.
            Transform windmill = Group(root, "Windmill");
            Rect(windmill, "Tower", 1f, 5f, 3f, 10f);
            Move(MovingRect(windmill, "Bar", 10f, 13f, 10f, 0.9f), Vector2.zero, 1f, 0f, 22f);
            Ellipse(windmill, "Hub", 10f, 13f, 0.9f, 0.9f);
            Rect(windmill, "Pillar", 19.5f, 6f, 3f, 12f);
            B.Blob(windmill, "LandingLump", 10f, 1.2f, 43, 1.6f, 0.2f, 1.8f, 0.7f, 9, 0.45f, 0.15f, ShapeFill, ShapeOutline);
            Move(MovingRect(windmill, "Ferry", -2f, 22.5f, 3f, 0.6f), new Vector2(12f, 0f), 7f);

            // Centre-east: stones orbiting a core, a pebble cluster, a pendulum over a launch circle.
            Transform orbit = Group(root, "Orbit_Pendulum");
            Ellipse(orbit, "Core", 30f, 10f, 1.2f, 1.2f);
            Transform pivot = Group(orbit, "OrbitPivot");
            pivot.position = new Vector3(30f, 10f, 0f);
            for (int i = 0; i < 3; i++)
            {
                float a = (90f + i * 120f) * Mathf.Deg2Rad;
                MovingEllipse(pivot, $"Stone_{i + 1}", 30f + Mathf.Cos(a) * 3.6f, 10f + Mathf.Sin(a) * 3.6f, 0.7f, 0.7f);
            }
            Move(pivot.gameObject, Vector2.zero, 1f, 0f, 28f);
            Vector2[] pebbles = { new(23f, 3f), new(25f, 4.3f), new(26.6f, 2.5f), new(24.6f, 1.1f), new(22.5f, 0.2f) };
            for (int i = 0; i < pebbles.Length; i++)
                B.Blob(orbit, $"Pebble_{i + 1}", pebbles[i].x, pebbles[i].y, 31 + i, 0.75f, 0.2f, 1f, 1f, 6, 0.4f);
            Transform pendulum = Group(orbit, "PendulumPivot");
            pendulum.position = new Vector3(37f, 30f, 0f);
            MovingRect(pendulum, "Arm", 37f, 25.7f, 0.4f, 8.6f);
            MovingRect(pendulum, "Platform", 37f, 21.1f, 3.4f, 0.6f);
            Move(pendulum.gameObject, Vector2.zero, 4.5f, 0f, 0f, 32f);
            Ellipse(orbit, "LaunchCircle", 37f, 15f, 1.2f, 1.2f);

            // East: alternating static / bobbing stones, a slab sliding under the ceiling, stretched + concave blobs.
            Transform east = Group(root, "East_StonesSlab");
            for (int i = 0; i < 5; i++)
            {
                float x = 44f + i * 3.5f;
                if (i % 2 == 0) Ellipse(east, $"Stone_{i + 1}_Static", x, 4.5f, 0.8f, 0.8f);
                else Move(MovingEllipse(east, $"Stone_{i + 1}_Bobbing", x, 3f, 0.8f, 0.8f), new Vector2(0f, 3f), 3.2f, i * 0.2f);
            }
            Move(MovingRect(east, "Slab", 44f, 26.5f, 4f, 0.7f), new Vector2(10f, 0f), 8f);
            B.Blob(east, "StretchedBlob", 51f, 20f, 21, 2f, 0.2f, 2.2f, 0.5f, 9, 0.5f, 0.15f, ShapeFill, ShapeOutline);
            B.Blob(east, "ConcaveBlob", 55f, 12f, 7, 2.2f, 0.5f, 1f, 1f, 7, 0.1f, 0.3f);
            Ellipse(east, "StepCircle", 46f, 11f, 0.9f, 0.9f);
        }

        static GameObject Rect(Transform p, string n, float x, float y, float w, float h) =>
            B.Rect(p, n, x, y, w, h, 0.25f, ShapeFill, ShapeOutline);

        static GameObject MovingRect(Transform p, string n, float x, float y, float w, float h) =>
            B.Rect(p, n, x, y, w, h, 0.25f, MovingFill, MovingOutline);

        static GameObject Ellipse(Transform p, string n, float x, float y, float rx, float ry) =>
            B.Ellipse(p, n, x, y, rx, ry, ShapeFill, ShapeOutline);

        static GameObject MovingEllipse(Transform p, string n, float x, float y, float rx, float ry) =>
            B.Ellipse(p, n, x, y, rx, ry, MovingFill, MovingOutline);

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

        // ------------------------------------------------------------------ enemies

        class E
        {
            public EnemyMovementMode mode = EnemyMovementMode.Static;
            public float hp = 50f, radius = 0.5f, mass = 1f, speed = 2.5f, contact = 10f;
            public Vector2 patrolAxis = Vector2.right;
            public float patrolDistance = 5f, wanderRadius = 4f, seekRange = 10f;
            public Vector2 orbitCenterOffset = new Vector2(0f, -3f);
            public float orbitSpeed = 30f;
            public bool invulnerable;

            public E With(System.Action<E> edit)
            {
                var copy = (E)MemberwiseClone();
                edit(copy);
                return copy;
            }
        }

        // Size classes: tiny = one good hit, normal = 2-3 good hits, large = a real fight.
        static E Tiny => new E { hp = 10f, radius = 0.3f, mass = 0.5f, contact = 5f, speed = 3f };
        static E Normal => new E();
        static E Large(float hp) => new E { hp = hp, radius = 1.1f, mass = 3f, contact = 15f, speed = 1.5f };

        static void BuildEnemies(Transform markers)
        {
            var root = new GameObject("Enemies").transform;

            // ---------------- West: elevator & compass
            Label(markers, "ELEVATOR & COMPASS", -45f, 28.8f);
            Transform perch = SpawnGroup(root, "ElevatorPerch", "Normals on the ledge beside the elevator, a tiny drifting over the shaft.");
            Enemy(perch, "Ledge_1", -47.5f, 10.55f, Normal);
            Enemy(perch, "Ledge_2", -45.5f, 10.55f, Normal);
            Enemy(perch, "Shaft_Wander", -53f, 13f, Tiny.With(e => { e.mode = EnemyMovementMode.Wander; e.wanderRadius = 1.5f; }));
            Transform bats = SpawnGroup(root, "OverhangBats", "Tinies hanging under the wall overhang.");
            for (int i = 0; i < 3; i++) Enemy(bats, $"Bat_{i + 1}", -58f + i * 2.5f, 14.7f, Tiny);
            Transform spinner = SpawnGroup(root, "SpinnerOrbit", "Tinies circling the spinner the opposite way.");
            OrbitRing(spinner, "Spin", new Vector2(-53f, 23f), 4f, 3, -35f, Tiny);
            Transform compass = SpawnGroup(root, "CompassRing", "Tinies in the diagonals of the compass (spawn 2 is its centre).");
            for (int i = 0; i < 4; i++)
            {
                float a = (45f + i * 90f) * Mathf.Deg2Rad;
                Enemy(compass, $"Diagonal_{i + 1}", -37f + Mathf.Cos(a) * 2.26f, 14f + Mathf.Sin(a) * 2.26f, Tiny);
            }
            Transform westFloor = SpawnGroup(root, "WestFloorLine", "A line of normals on the floor: crawl-ramming and piercing along the ground.");
            for (int i = 0; i < 5; i++) Enemy(westFloor, $"Floor_{i + 1}", -43f + i * 2.5f, 1.05f, Normal);

            // ---------------- West-centre: big blob & narrow gap
            Label(markers, "BIG BLOB & NARROW GAP", -17f, 28.8f);
            Transform blobOrbit = SpawnGroup(root, "BlobOrbit", "Six normals circling the big blob. Crawl the blob and shoot outward.");
            OrbitRing(blobOrbit, "Orbit", new Vector2(-17f, 10f), 7.5f, 6, 25f, Normal);
            Transform gapNest = SpawnGroup(root, "GapNest", "Tinies stacked in the narrow gap: pulse / flame / chain them out.");
            for (int i = 0; i < 3; i++) Enemy(gapNest, $"Nest_{i + 1}", -7.35f, 0.35f + i * 0.65f, Tiny);
            Transform pillarTop = SpawnGroup(root, "PillarBruiser", "A large enemy on the tall pillar.");
            Enemy(pillarTop, "Bruiser", -28f, 10.65f, Large(250f));
            Transform pairs = SpawnGroup(root, "PairGuards", "Tinies sitting on the circle pairs.");
            Enemy(pairs, "OnPair_A", -24f, 23.75f, Tiny);
            Enemy(pairs, "OnPair_B", -20.1f, 23.75f, Tiny);
            Enemy(pairs, "OnFar_A", -12.5f, 25.35f, Tiny);
            Enemy(pairs, "OnFar_B", -8.8f, 25.35f, Tiny);
            Transform skyLine = SpawnGroup(root, "SkyLine", "A straight row of tinies under the ceiling: piercing from the ceiling or the pairs.");
            for (int i = 0; i < 6; i++) Enemy(skyLine, $"Sky_{i + 1}", -34f + i * 2.4f, 28.2f, Tiny);

            // ---------------- Centre: windmill
            Label(markers, "WINDMILL", 10f, 28.8f);
            Transform ring = SpawnGroup(root, "WindmillRing", "Eight normals ringing the windmill just outside the bar's reach.");
            for (int i = 0; i < 8; i++)
            {
                float a = i * 45f * Mathf.Deg2Rad;
                Enemy(ring, $"Ring_{i + 1}", 10f + Mathf.Cos(a) * 7f, 13f + Mathf.Sin(a) * 7f, Normal);
            }
            Transform lump = SpawnGroup(root, "LumpWanderers", "Tinies wandering either side of the landing lump.");
            Enemy(lump, "Wander_L", 5f, 1.5f, Tiny.With(e => { e.mode = EnemyMovementMode.Wander; e.wanderRadius = 2.5f; }));
            Enemy(lump, "Wander_R", 15f, 1.5f, Tiny.With(e => { e.mode = EnemyMovementMode.Wander; e.wanderRadius = 2.5f; }));

            // ---------------- Centre-east: orbit & pendulum
            Label(markers, "ORBIT & PENDULUM", 32f, 28.8f);
            Transform orbitRing = SpawnGroup(root, "OrbitRing", "Normals circling outside the orbiting stones, against their spin.");
            OrbitRing(orbitRing, "Orbit", new Vector2(30f, 10f), 6f, 4, -30f, Normal);
            Transform pebbleNest = SpawnGroup(root, "PebbleNest", "Tinies tucked between the pebbles.");
            Enemy(pebbleNest, "Nest_1", 24f, 2.6f, Tiny);
            Enemy(pebbleNest, "Nest_2", 25.9f, 3.4f, Tiny);
            Enemy(pebbleNest, "Nest_3", 23.7f, 4.5f, Tiny);
            Enemy(pebbleNest, "Nest_4", 25.8f, 1.1f, Tiny);
            Transform bruiser = SpawnGroup(root, "LaunchBruiser", "A large enemy under the launch circle: dive onto it.");
            Enemy(bruiser, "Bruiser", 37f, 8.5f, Large(300f));
            Transform pendulumPerch = SpawnGroup(root, "PendulumPerch", "Tinies just outside the pendulum's swing.");
            Enemy(pendulumPerch, "Perch_L", 32.5f, 25f, Tiny);
            Enemy(pendulumPerch, "Perch_R", 41.5f, 25f, Tiny);

            // ---------------- East: stones & slab
            Label(markers, "STONES & SLAB", 51f, 28.8f);
            Transform hoppers = SpawnGroup(root, "StoneHoppers", "Normals patrolling over the bobbing stones.");
            Enemy(hoppers, "Hopper_Low", 51f, 7.8f, Normal.With(e => { e.mode = EnemyMovementMode.Patrol; e.patrolDistance = 12f; e.speed = 3f; }));
            Enemy(hoppers, "Hopper_Air", 44f, 17f, Normal.With(e => { e.mode = EnemyMovementMode.Patrol; e.patrolAxis = new Vector2(1f, 0.4f); e.patrolDistance = 6f; e.speed = 2f; }));
            Transform slabRiders = SpawnGroup(root, "SlabRiders", "Tinies patrolling fast under the sliding slab.");
            Enemy(slabRiders, "Rider_1", 47f, 24.4f, Tiny.With(e => { e.mode = EnemyMovementMode.Patrol; e.patrolDistance = 10f; e.speed = 4f; }));
            Enemy(slabRiders, "Rider_2", 53f, 23.6f, Tiny.With(e => { e.mode = EnemyMovementMode.Patrol; e.patrolDistance = 10f; e.speed = 5f; }));
            Transform seekers = SpawnGroup(root, "Seekers", "Normals that chase you within 10 units: pressure for flame, pulse, flail and ramming.");
            Enemy(seekers, "Seeker_1", 47f, 14.5f, Normal.With(e => { e.mode = EnemyMovementMode.SeekPlayer; e.speed = 3f; }));
            Enemy(seekers, "Seeker_2", 57.5f, 18f, Normal.With(e => { e.mode = EnemyMovementMode.SeekPlayer; e.speed = 3f; }));
            Enemy(seekers, "Seeker_3", 50f, 1.5f, Normal.With(e => { e.mode = EnemyMovementMode.SeekPlayer; e.speed = 3f; }));
            Transform wallStack = SpawnGroup(root, "WallStack", "Normals stacked against the right wall: knock them into it for wall slams.");
            for (int i = 0; i < 3; i++) Enemy(wallStack, $"Stack_{i + 1}", 59.4f, 1.05f + i * 1.1f, Normal);
            Transform heavy = SpawnGroup(root, "Heavy", "A 400 HP heavy beside the concave blob.");
            Enemy(heavy, "Heavy", 49f, 16f, Large(400f).With(e => e.radius = 1.2f));
        }

        /// <summary>Enemies evenly spaced on a circle, all orbiting its centre.</summary>
        static void OrbitRing(Transform parent, string prefix, Vector2 center, float radius, int count, float speed, E template)
        {
            for (int i = 0; i < count; i++)
            {
                float a = 360f * i / count * Mathf.Deg2Rad;
                Vector2 p = center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius;
                Enemy(parent, $"{prefix}_{i + 1}", p.x, p.y, template.With(e =>
                {
                    e.mode = EnemyMovementMode.Orbit;
                    e.orbitCenterOffset = center - p;
                    e.orbitSpeed = speed;
                }));
            }
        }

        static Transform SpawnGroup(Transform root, string name, string description)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root, false);
            var g = go.AddComponent<EnemySpawnGroup>();
            var so = new SerializedObject(g);
            so.FindProperty("displayName").stringValue = name;
            so.FindProperty("description").stringValue = description;
            so.ApplyModifiedPropertiesWithoutUndo();
            return go.transform;
        }

        static void Enemy(Transform parent, string name, float x, float y, E cfg)
        {
            GameObject go = Instance(EnemyPrefabPath, parent, name, x, y);
            var enemy = go.GetComponent<DummyEnemy>();
            var so = new SerializedObject(enemy);
            so.FindProperty("radius").floatValue = cfg.radius;
            so.FindProperty("mass").floatValue = cfg.mass;
            so.ApplyModifiedPropertiesWithoutUndo();
            enemy.GetComponentInChildren<SpriteRenderer>().transform.localScale = Vector3.one * (cfg.radius * 2f);

            so = new SerializedObject(go.GetComponent<Health>());
            so.FindProperty("maxHealth").floatValue = cfg.hp;
            so.FindProperty("invulnerable").boolValue = cfg.invulnerable;
            so.ApplyModifiedPropertiesWithoutUndo();

            so = new SerializedObject(go.GetComponent<EnemyMovementController>());
            so.FindProperty("mode").enumValueIndex = (int)cfg.mode;
            so.FindProperty("moveSpeed").floatValue = cfg.speed;
            so.FindProperty("patrolAxis").vector2Value = cfg.patrolAxis;
            so.FindProperty("patrolDistance").floatValue = cfg.patrolDistance;
            so.FindProperty("wanderRadius").floatValue = cfg.wanderRadius;
            so.FindProperty("seekRange").floatValue = cfg.seekRange;
            so.FindProperty("orbitCenterOffset").vector2Value = cfg.orbitCenterOffset;
            so.FindProperty("orbitSpeed").floatValue = cfg.orbitSpeed;
            so.ApplyModifiedPropertiesWithoutUndo();

            so = new SerializedObject(go.GetComponent<EnemyContactDamage>());
            so.FindProperty("contactDamage").floatValue = cfg.contact;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void Label(Transform parent, string text, float x, float y, int size = 13) =>
            Marker(parent, text, x, y, ZoneLabelColor, size, true);

        static void Marker(Transform parent, string text, float x, float y, Color color, int size, bool bold = false)
        {
            var go = new GameObject($"Marker_{text.Split('\n')[0]}");
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(x, y, 0f);
            var m = go.AddComponent<WorldMarker>();
            m.text = text;
            m.color = color;
            m.fontSize = size;
            m.bold = bold;
        }
    }

    /// <summary>
    /// Builds the combat sandbox automatically (once per Editor session) if it's missing, or if it was generated by an
    /// older version of the layout.
    /// </summary>
    [InitializeOnLoad]
    static class CombatSandboxAutoBuild
    {
        const string SessionKey = "Televised.CombatSandbox.AutoBuildAttempted.v3";

        static CombatSandboxAutoBuild()
        {
            EditorApplication.delayCall += () =>
            {
                if (Application.isBatchMode || EditorApplication.isPlayingOrWillChangePlaymode) return;
                bool outdated = EditorPrefs.GetInt(CombatSandboxBuilder.LayoutVersionKey, 0) < CombatSandboxBuilder.LayoutVersion;
                if ((File.Exists(CombatSandboxBuilder.ScenePath) && !outdated) || SessionState.GetBool(SessionKey, false)) return;
                if (outdated && File.Exists(CombatSandboxBuilder.ScenePath))
                    Debug.Log("[CombatSandbox] The sandbox layout changed; rebuilding CombatSandbox.unity.");
                if (!File.Exists(B.ScenePath)) return; // let the movement sandbox build the shared assets first
                SessionState.SetBool(SessionKey, true);
                try { CombatSandboxBuilder.Build(false); }
                catch (System.Exception e) { Debug.LogException(e); }
            };
        }
    }
}
