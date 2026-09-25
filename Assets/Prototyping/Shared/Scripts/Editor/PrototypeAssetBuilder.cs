using System.IO;
using UnityEditor;
using UnityEngine;

namespace Televised.Prototyping.Shared.EditorTools
{
    /// <summary>
    /// Generates the assets shared by every prototype (sprite, material, tuning asset,
    /// player + surface prefabs) under Assets/Prototyping/Shared, plus helpers for scene builders.
    /// Menu: Prototyping/Shared/Rebuild Prefabs + Assets.
    /// </summary>
    public static class PrototypeAssetBuilder
    {
        public const string Root = "Assets/Prototyping/Shared";
        internal const string SpritePath = Root + "/Sprites/Circle.png";
        internal const string MaterialPath = Root + "/Materials/PrototypeUnlit.mat";
        internal const string TuningPath = Root + "/Settings/DefaultMovementTuning.asset";
        internal const string PlayerPrefabPath = Root + "/Prefabs/Player/EyeballPlayer.prefab";
        internal const string RectPrefabPath = Root + "/Prefabs/Surfaces/RectSurface.prefab";
        internal const string EllipsePrefabPath = Root + "/Prefabs/Surfaces/EllipseSurface.prefab";
        internal const string BlobPrefabPath = Root + "/Prefabs/Surfaces/OrganicBlob.prefab";

        // ------------------------------------------------------------------ menu

        [MenuItem("Prototyping/Shared/Rebuild Prefabs + Assets")]
        public static void RebuildAssetsOnly()
        {
            BuildAssets(out _, out _, out _);
            Debug.Log("[PrototypeAssets] Assets and prefabs rebuilt.");
        }

        // ------------------------------------------------------------------ build

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
                "Prefabs", "Prefabs/Player", "Prefabs/Surfaces", "Materials", "Sprites", "Settings"
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

        // ------------------------------------------------------------------ scene-builder helpers

        internal static void Set(Object target, string property, Object value)
        {
            var so = new SerializedObject(target);
            SerializedProperty p = so.FindProperty(property);
            if (p == null) { Debug.LogError($"[PrototypeAssets] {target.GetType().Name}.{property} not found"); return; }
            p.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
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

        internal static void Configure(SurfaceShapeBase shape, System.Action<SerializedObject> edit)
        {
            var so = new SerializedObject(shape);
            edit(so);
            so.ApplyModifiedPropertiesWithoutUndo();
            shape.Regenerate();
        }

        // ------------------------------------------------------------------ quick-create menu

        [MenuItem("GameObject/Prototyping/Surfaces/Organic Blob", false, 10)]
        static void CreateBlob(MenuCommand cmd) => CreateFromPrefab(BlobPrefabPath, "OrganicBlob", cmd, true);

        [MenuItem("GameObject/Prototyping/Surfaces/Ellipse Surface", false, 11)]
        static void CreateEllipse(MenuCommand cmd) => CreateFromPrefab(EllipsePrefabPath, "EllipseSurface", cmd, false);

        [MenuItem("GameObject/Prototyping/Surfaces/Rectangle Surface", false, 12)]
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
}
