using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Televised.Art.EditorTools
{
    /// <summary>
    /// Applies the output of Tools/ArtPipeline/export.mjs. A texture whose folder holds a sprite_layout.json that
    /// lists it is imported as a single Sprite with the layout's pivot and pixels per unit, so the SVG masters stay
    /// the source of truth for pivots. Re-exporting reimports any sprite whose settings changed.
    /// Menu: Assets/Art Pipeline/Assemble Parts In Scene (with a sprite_layout.json selected). See ART_PIPELINE.md.
    /// </summary>
    public class SpriteLayoutPostprocessor : AssetPostprocessor
    {
        public const string LayoutFileName = "sprite_layout.json";

        [Serializable]
        public class Layout
        {
            public float pixelsPerUnit;
            public Part[] parts;
        }

        [Serializable]
        public class Part
        {
            public string name;
            public Vector2 pivot;     // normalized, bottom-left origin
            public Vector2 position;  // pivot position in the asset's local space, Unity units
            public int order;         // draw order, bottom to top
        }

        public static Layout LoadLayout(string folder)
        {
            string path = folder + "/" + LayoutFileName;
            if (!File.Exists(path)) return null;
            var layout = JsonUtility.FromJson<Layout>(File.ReadAllText(path));
            return layout?.parts != null && layout.pixelsPerUnit > 0f ? layout : null;
        }

        static string FolderOf(string assetPath) => Path.GetDirectoryName(assetPath)?.Replace('\\', '/');

        static Part FindPart(Layout layout, string assetPath)
        {
            string name = Path.GetFileNameWithoutExtension(assetPath);
            return Array.Find(layout.parts, p => p.name == name);
        }

        // ------------------------------------------------------------------ import

        void OnPreprocessTexture()
        {
            var layout = LoadLayout(FolderOf(assetPath));
            if (layout == null) return;
            var part = FindPart(layout, assetPath);
            if (part == null) return;

            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;

            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.Custom;
            settings.spritePivot = part.pivot;
            settings.spritePixelsPerUnit = layout.pixelsPerUnit;
            importer.SetTextureSettings(settings);
        }

        static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            foreach (string path in imported)
            {
                if (Path.GetFileName(path) != LayoutFileName) continue;
                string folder = FolderOf(path);
                var layout = LoadLayout(folder);
                if (layout == null) continue;

                foreach (var part in layout.parts)
                {
                    string png = $"{folder}/{part.name}.png";
                    if (AssetImporter.GetAtPath(png) is TextureImporter ti && !Matches(ti, layout, part))
                        AssetDatabase.ImportAsset(png, ImportAssetOptions.ForceUpdate);
                }
            }
        }

        static bool Matches(TextureImporter importer, Layout layout, Part part) =>
            importer.textureType == TextureImporterType.Sprite &&
            importer.spriteImportMode == SpriteImportMode.Single &&
            Mathf.Approximately(importer.spritePixelsPerUnit, layout.pixelsPerUnit) &&
            (importer.spritePivot - part.pivot).sqrMagnitude < 1e-8f;

        // ------------------------------------------------------------------ assemble

        const string AssembleMenu = "Assets/Art Pipeline/Assemble Parts In Scene";

        /// <summary>
        /// Creates a flat GameObject per part in the active scene, positioned and sorted as exported. Parenting
        /// (e.g. pupil under eye) and rigging are done afterwards; reparenting keeps the world positions.
        /// </summary>
        [MenuItem(AssembleMenu)]
        static void AssembleSelected()
        {
            string layoutPath = AssetDatabase.GetAssetPath(Selection.activeObject);
            string folder = FolderOf(layoutPath);
            var layout = LoadLayout(folder);
            if (layout == null)
            {
                Debug.LogWarning($"[ArtPipeline] Couldn't read {layoutPath}.");
                return;
            }

            // Assets/Art/<Category>/<Asset>/Sprites/sprite_layout.json -> "<Asset>"
            var root = new GameObject(Path.GetFileName(Path.GetDirectoryName(folder)));
            Undo.RegisterCreatedObjectUndo(root, "Assemble Parts");
            foreach (var part in layout.parts)
            {
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{folder}/{part.name}.png");
                if (sprite == null)
                {
                    Debug.LogWarning($"[ArtPipeline] Missing sprite for {part.name} in {folder}.");
                    continue;
                }
                var go = new GameObject(part.name);
                go.transform.SetParent(root.transform, false);
                go.transform.localPosition = part.position;
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = sprite;
                sr.sortingOrder = part.order;
            }
            Selection.activeGameObject = root;
            EditorSceneManager.MarkSceneDirty(root.scene);
        }

        [MenuItem(AssembleMenu, true)]
        static bool AssembleSelectedValidate() =>
            Path.GetFileName(AssetDatabase.GetAssetPath(Selection.activeObject)) == LayoutFileName;
    }
}
