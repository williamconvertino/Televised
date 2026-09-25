using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Televised.Prototyping.Movement.EditorTools
{
    /// <summary>
    /// Generates every asset the movement prototype needs (sprite, material, tuning asset,
    /// player + surface prefabs) and builds the MovementSandbox scene.
    /// Menu: Prototyping/Movement/Rebuild Sandbox (Assets + Scene).
    /// Runs automatically once if the sandbox scene does not exist yet.
    /// </summary>
    public static class MovementSandboxBuilder
    {
        public const string Root = "Assets/Prototyping/Movement";
        public const string ScenePath = Root + "/Scenes/MovementSandbox.unity";
        internal const string SpritePath = Root + "/Sprites/Circle.png";
        internal const string MaterialPath = Root + "/Materials/PrototypeUnlit.mat";
        internal const string TuningPath = Root + "/Settings/DefaultMovementTuning.asset";
        internal const string PlayerPrefabPath = Root + "/Prefabs/Player/EyeballPlayer.prefab";
        internal const string RectPrefabPath = Root + "/Prefabs/Surfaces/RectSurface.prefab";
        internal const string EllipsePrefabPath = Root + "/Prefabs/Surfaces/EllipseSurface.prefab";
        internal const string BlobPrefabPath = Root + "/Prefabs/Surfaces/OrganicBlob.prefab";

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

        [MenuItem("Prototyping/Movement/Rebuild Prefabs + Assets Only")]
        public static void RebuildAssetsOnly()
        {
            BuildAssets(out _, out _, out _);
            Debug.Log("[MovementSandbox] Assets and prefabs rebuilt.");
        }

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

        /// <summary>Build the shared assets/prefabs only if they don't exist yet.</summary>
        internal static void EnsureAssets(out Material mat, out Sprite circle)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath) == null ||
                AssetDatabase.LoadAssetAtPath<GameObject>(RectPrefabPath) == null)
            {
                BuildAssets(out mat, out _, out circle);
                return;
            }
            mat = EnsureMaterial();
            circle = EnsureCircleSprite();
        }

        internal static void BuildAssets(out Material mat, out MovementTuning tuning, out Sprite circle)
        {
            EnsureFolders();
            circle = EnsureCircleSprite();
            mat = EnsureMaterial();
            tuning = EnsureTuning();
            BuildSurfacePrefabs(mat);
            BuildPlayerPrefab(mat, tuning, circle);
            AssetDatabase.SaveAssets();
        }

        static void EnsureFolders()
        {
            string[] folders =
            {
                "Scenes", "Scenes/OptionalFocusedTests", "Scripts", "Prefabs", "Prefabs/Player", "Prefabs/Surfaces",
                "Materials", "Sprites", "Settings"
            };
            foreach (string f in folders)
            {
                string full = $"{Root}/{f}";
                if (AssetDatabase.IsValidFolder(full)) continue;
                string parent = Path.GetDirectoryName(full)?.Replace('\\', '/');
                AssetDatabase.CreateFolder(parent, Path.GetFileName(full));
            }
        }

        static Sprite EnsureCircleSprite()
        {
            if (!File.Exists(SpritePath))
            {
                const int size = 256;
                var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
                var px = new Color32[size * size];
                float c = (size - 1) * 0.5f, r = size * 0.5f - 1f;
                for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c));
                    byte a = (byte)(Mathf.Clamp01(r - d + 0.5f) * 255f);
                    px[y * size + x] = new Color32(255, 255, 255, a);
                }
                tex.SetPixels32(px);
                File.WriteAllBytes(SpritePath, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                AssetDatabase.ImportAsset(SpritePath, ImportAssetOptions.ForceSynchronousImport);

                var ti = (TextureImporter)AssetImporter.GetAtPath(SpritePath);
                ti.textureType = TextureImporterType.Sprite;
                ti.spriteImportMode = SpriteImportMode.Single;
                ti.spritePixelsPerUnit = size; // 1 unit diameter
                ti.mipmapEnabled = false;
                ti.alphaIsTransparency = true;
                ti.filterMode = FilterMode.Bilinear;
                ti.textureCompression = TextureImporterCompression.Uncompressed;
                ti.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(SpritePath);
        }

        static Material EnsureMaterial()
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (mat != null) return mat;

            Shader shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            mat = new Material(shader) { name = "PrototypeUnlit" };
            AssetDatabase.CreateAsset(mat, MaterialPath);
            return mat;
        }

        static MovementTuning EnsureTuning()
        {
            var t = AssetDatabase.LoadAssetAtPath<MovementTuning>(TuningPath);
            if (t != null) return t;
            t = ScriptableObject.CreateInstance<MovementTuning>();
            AssetDatabase.CreateAsset(t, TuningPath);
            return t;
        }

        // ------------------------------------------------------------------ prefabs

        static void BuildSurfacePrefabs(Material mat)
        {
            SaveSurfacePrefab<RectangleSurfaceShape>("RectSurface", RectPrefabPath, mat);
            SaveSurfacePrefab<EllipseSurfaceShape>("EllipseSurface", EllipsePrefabPath, mat);
            SaveSurfacePrefab<OrganicSurfaceGenerator>("OrganicBlob", BlobPrefabPath, mat);
        }

        static void SaveSurfacePrefab<T>(string name, string path, Material mat) where T : SurfaceShapeBase
        {
            var go = new GameObject(name);
            go.AddComponent<T>(); // RequireComponent adds Surface2D, MeshFilter, MeshRenderer
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
        }

        static void BuildPlayerPrefab(Material mat, MovementTuning tuning, Sprite circle)
        {
            var root = new GameObject("EyeballPlayer");

            var rb = root.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.gravityScale = 0f;

            var motor = root.AddComponent<PlayerMotor2D>();
            var inputResolver = root.AddComponent<SurfaceInputResolver>();
            var jumpResolver = root.AddComponent<JumpDirectionResolver>();
            var attachment = root.AddComponent<SurfaceAttachmentController>();
            var grapple = root.AddComponent<GrappleController>();

            var colliderGo = Child(root, "PhysicsCollider");
            colliderGo.AddComponent<CircleCollider2D>().radius = tuning.playerRadius;

            var eyeVisual = Child(root, "EyeVisual");
            SpriteRenderer rim = SpriteChild(eyeVisual, "EyeballRim", circle, new Color(0.35f, 0.12f, 0.16f), 1.08f, 9);
            SpriteRenderer eyeball = SpriteChild(eyeVisual, "Eyeball", circle, new Color(0.96f, 0.94f, 0.9f), 1f, 10);
            SpriteRenderer iris = SpriteChild(eyeVisual, "Iris", circle, new Color(0.2f, 0.55f, 0.85f), 0.5f, 11);
            SpriteRenderer pupil = SpriteChild(eyeVisual, "Pupil", circle, new Color(0.04f, 0.04f, 0.06f), 0.26f, 12);
            SpriteChild(pupil.gameObject, "Glint", circle, new Color(1f, 1f, 1f, 0.85f), 0.3f, 13)
                .transform.localPosition = new Vector3(0.22f, 0.25f, 0f);
            rim.transform.SetParent(eyeball.transform, true); // rim squashes with the eyeball

            var aim = eyeVisual.AddComponent<EyeAimController>();
            var feedback = eyeVisual.AddComponent<EyeVisualFeedback>();

            var legRigGo = Child(root, "LegRig");
            var legRig = legRigGo.AddComponent<ProceduralLegRig>();

            var sensorGo = Child(root, "SurfaceSensor");
            var sensor = sensorGo.AddComponent<SurfaceSensor>();

            Set(motor, "tuning", tuning);
            Set(motor, "sensor", sensor);
            Set(motor, "attachment", attachment);
            Set(motor, "jumpResolver", jumpResolver);
            Set(motor, "inputResolver", inputResolver);
            Set(motor, "grapple", grapple);
            Set(aim, "root", root.transform);
            Set(aim, "iris", iris.transform);
            Set(aim, "pupil", pupil.transform);
            Set(feedback, "motor", motor);
            Set(feedback, "eyeball", eyeball.transform);
            Set(legRig, "motor", motor);
            Set(legRig, "legMaterial", mat);
            Set(legRig, "footSprite", circle);

            PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
            Object.DestroyImmediate(root);
        }

        static GameObject Child(GameObject parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            return go;
        }

        static SpriteRenderer SpriteChild(GameObject parent, string name, Sprite sprite, Color color, float scale, int order)
        {
            var go = Child(parent, name);
            go.transform.localScale = Vector3.one * scale;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = color;
            sr.sortingOrder = order;
            return sr;
        }

        internal static void Set(Object target, string property, Object value)
        {
            var so = new SerializedObject(target);
            SerializedProperty p = so.FindProperty(property);
            if (p == null) { Debug.LogError($"[MovementSandbox] {target.GetType().Name}.{property} not found"); return; }
            p.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
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

        internal static Transform Group(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        internal static GameObject Instance(string prefabPath, Transform parent, string name, float x, float y)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.name = name;
            go.transform.position = new Vector3(x, y, 0f);
            return go;
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

        internal static void Configure(SurfaceShapeBase shape, System.Action<SerializedObject> edit)
        {
            var so = new SerializedObject(shape);
            edit(so);
            so.ApplyModifiedPropertiesWithoutUndo();
            shape.Regenerate();
        }

        // ------------------------------------------------------------------ quick-create menu

        [MenuItem("GameObject/Prototyping/Movement/Organic Blob", false, 10)]
        static void CreateBlob(MenuCommand cmd) => CreateFromPrefab(BlobPrefabPath, "OrganicBlob", cmd, true);

        [MenuItem("GameObject/Prototyping/Movement/Ellipse Surface", false, 11)]
        static void CreateEllipse(MenuCommand cmd) => CreateFromPrefab(EllipsePrefabPath, "EllipseSurface", cmd, false);

        [MenuItem("GameObject/Prototyping/Movement/Rectangle Surface", false, 12)]
        static void CreateRect(MenuCommand cmd) => CreateFromPrefab(RectPrefabPath, "RectSurface", cmd, false);

        static void CreateFromPrefab(string path, string name, MenuCommand cmd, bool randomizeSeed)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) { RebuildAssetsOnly(); prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path); }
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            go.name = name;
            GameObjectUtility.SetParentAndAlign(go, cmd.context as GameObject);
            if (SceneView.lastActiveSceneView != null && cmd.context == null)
            {
                Vector3 p = SceneView.lastActiveSceneView.pivot;
                go.transform.position = new Vector3(p.x, p.y, 0f);
            }
            if (randomizeSeed)
            {
                var blob = go.GetComponent<OrganicSurfaceGenerator>();
                Configure(blob, so => so.FindProperty("seed").intValue = Random.Range(0, 100000));
            }
            Undo.RegisterCreatedObjectUndo(go, $"Create {name}");
            Selection.activeGameObject = go;
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
