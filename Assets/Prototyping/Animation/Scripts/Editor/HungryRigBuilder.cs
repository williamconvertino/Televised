using System;
using System.Linq;
using Televised.Art.EditorTools;
using Televised.Prototyping.Shared.EditorTools;
using UnityEditor;
using UnityEngine;

namespace Televised.Prototyping.AnimationLab.EditorTools
{
    /// <summary>
    /// Assembles Hungry's rig from the exported sprites and Assets/Art/Characters/Hungry/Sprites/sprite_layout.json:
    /// Head (face, eyes with socket masks, pupils, lids, cheeks, mouth swaps, nose, brows) plus two floating gloves
    /// (the left one is the right glove mirrored). Wires HungryFace and HungryLabControls. Called by
    /// AnimationLabBuilder, so a lab rebuild always gets the current sprites.
    /// </summary>
    public static class HungryRigBuilder
    {
        public const string SpritesFolder = "Assets/Art/Characters/Hungry/Sprites";

        public static GameObject Build(Transform parent)
        {
            var layout = SpriteLayoutPostprocessor.LoadLayout(SpritesFolder);
            if (layout == null)
            {
                Debug.LogWarning($"[AnimationLab] No Hungry sprite layout at {SpritesFolder}; run the art export first.");
                return null;
            }
            PrototypeAssetBuilder.EnsureAssets(out var material, out _);

            var root = new GameObject("Hungry");
            root.transform.SetParent(parent, false);

            SpriteLayoutPostprocessor.Part Part(string name) =>
                layout.parts.FirstOrDefault(p => p.name == name) ?? throw new InvalidOperationException($"Hungry layout has no {name}");

            // Creates an empty at a canvas position (root space), then parents it keeping that position.
            Transform Pivot(string name, Transform under, Vector2 pos)
            {
                var t = new GameObject(name).transform;
                t.SetParent(root.transform, false);
                t.localPosition = pos;
                t.SetParent(under, true);
                return t;
            }

            SpriteRenderer Sprite(string name, Transform under, bool masked = false)
            {
                var part = Part(name);
                var sr = Pivot(name, under, part.position).gameObject.AddComponent<SpriteRenderer>();
                sr.sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{SpritesFolder}/{name}.png");
                if (sr.sprite == null) Debug.LogWarning($"[AnimationLab] Missing sprite {name}.png");
                sr.sharedMaterial = material;
                sr.sortingOrder = part.order;
                if (masked) sr.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
                return sr;
            }

            var head = Pivot("Head", root.transform, Vector2.zero);
            var face = Sprite("hungry_face", head);

            // Eyes: the socket sprite doubles as a SpriteMask, so pupils, lids and cheeks only show inside it.
            Transform[] eyes = new Transform[2], pupils = new Transform[2], lids = new Transform[2], cheeks = new Transform[2];
            SpriteRenderer[] rings = new SpriteRenderer[2], arcs = new SpriteRenderer[2];
            string[] sides = { "L", "R" };
            for (int i = 0; i < 2; i++)
            {
                string s = sides[i];
                eyes[i] = Pivot("Eye_" + s, head, Part("hungry_eye_" + s).position);
                var socket = Sprite("hungry_eye_" + s, eyes[i]);
                var mask = new GameObject("hungry_eye_" + s + "_mask").AddComponent<SpriteMask>();
                mask.transform.SetParent(socket.transform, false);
                mask.sprite = socket.sprite;

                pupils[i] = Pivot("Pupil_" + s, eyes[i], Part("hungry_pupil_" + s).position);
                rings[i] = Sprite("hungry_pupil_" + s, pupils[i], true);
                arcs[i] = Sprite("hungry_pupil_" + s + "_arc", pupils[i], true);
                lids[i] = Sprite("hungry_lid_" + s, eyes[i], true).transform;
                cheeks[i] = Sprite("hungry_cheek_" + s, eyes[i], true).transform;
            }

            var mouth = Pivot("Mouth", head, Part("hungry_mouth_grin").position);
            var mouths = Enum.GetNames(typeof(HungryMouth)).Select(m => Sprite("hungry_mouth_" + m.ToLowerInvariant(), mouth)).ToArray();

            Sprite("hungry_nose", head);

            var browL = Pivot("Brow_L", head, Part("hungry_brow_L").position);
            var browR = Pivot("Brow_R", head, Part("hungry_brow_R").position);
            var browLSharp = Sprite("hungry_brow_L", browL);
            var browLSoft = Sprite("hungry_brow_L_soft", browL);
            var browRSharp = Sprite("hungry_brow_R", browR);
            var browRSoft = Sprite("hungry_brow_R_soft", browR);

            // Gloves: drawn once as the screen-right hand; the left root mirrors it across the face's centre line.
            string[] poses = Enum.GetNames(typeof(HungryGlove)).Select(p => "hungry_glove_" + p.ToLowerInvariant()).ToArray();
            Vector2 wrist = Part(poses[0]).position;
            Transform gloveRRoot = Pivot("Glove_R", root.transform, wrist);
            Transform gloveLRoot = Pivot("Glove_L", root.transform, new Vector2(-wrist.x, wrist.y));
            gloveLRoot.localScale = new Vector3(-1f, 1f, 1f);
            var gloveR = new GameObject("Motion").transform; gloveR.SetParent(gloveRRoot, false);
            var gloveL = new GameObject("Motion").transform; gloveL.SetParent(gloveLRoot, false);
            var gloveRPoses = poses.Select(p => Sprite(p, gloveR)).ToArray();
            var gloveLPoses = poses.Select(p => Sprite(p, gloveL)).ToArray();
            foreach (var sr in gloveLPoses)
            {
                // Pivot is the wrist; undo the counter-mirroring SetParent(worldPositionStays) applied.
                sr.transform.localPosition = Vector3.zero;
                sr.transform.localScale = Vector3.one;
            }

            var driver = root.AddComponent<HungryFace>();
            var so = new SerializedObject(driver);
            void Ref(string prop, UnityEngine.Object value) => so.FindProperty(prop).objectReferenceValue = value;
            void Refs(string prop, UnityEngine.Object[] values)
            {
                var p = so.FindProperty(prop);
                p.arraySize = values.Length;
                for (int i = 0; i < values.Length; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            }
            Ref("head", head); Ref("face", face);
            Ref("browL", browL); Ref("browR", browR);
            Ref("browLSharp", browLSharp); Ref("browLSoft", browLSoft); Ref("browRSharp", browRSharp); Ref("browRSoft", browRSoft);
            Ref("pupilL", pupils[0]); Ref("pupilR", pupils[1]);
            Ref("pupilLRing", rings[0]); Ref("pupilLArc", arcs[0]); Ref("pupilRRing", rings[1]); Ref("pupilRArc", arcs[1]);
            Ref("lidL", lids[0]); Ref("lidR", lids[1]); Ref("cheekL", cheeks[0]); Ref("cheekR", cheeks[1]);
            Ref("mouth", mouth); Refs("mouths", mouths);
            Ref("gloveL", gloveL); Ref("gloveR", gloveR);
            Refs("gloveLPoses", gloveLPoses); Refs("gloveRPoses", gloveRPoses);
            so.ApplyModifiedPropertiesWithoutUndo();

            // Edit-mode look before Play: default expression's sprites only.
            for (int i = 0; i < mouths.Length; i++) mouths[i].enabled = i == (int)HungryMouth.Grin;
            for (int i = 0; i < poses.Length; i++) gloveLPoses[i].enabled = gloveRPoses[i].enabled = i == (int)HungryGlove.Claw;
            browLSoft.enabled = browRSoft.enabled = arcs[0].enabled = arcs[1].enabled = false;

            var controls = root.AddComponent<HungryLabControls>();
            var cso = new SerializedObject(controls);
            cso.FindProperty("face").objectReferenceValue = driver;
            cso.ApplyModifiedPropertiesWithoutUndo();
            return root;
        }
    }
}
