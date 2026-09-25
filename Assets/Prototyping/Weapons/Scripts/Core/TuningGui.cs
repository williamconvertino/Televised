using System;
using UnityEngine;

namespace Televised.Prototyping.Weapons
{
    /// <summary>
    /// IMGUI helpers for the combat panel, in the same style as the movement tuning panel (label + value, slider),
    /// plus drawers for the shared tuning blocks (range, falloff, piercing, knockback).
    /// </summary>
    public class TuningGui
    {
        public GUIStyle Label { get; private set; }
        public GUIStyle HeaderStyle { get; private set; }
        public GUIStyle Small { get; private set; }

        public void EnsureStyles()
        {
            if (Label != null) return;
            Label = new GUIStyle(GUI.skin.label) { fontSize = 12, richText = true, wordWrap = true };
            HeaderStyle = new GUIStyle(GUI.skin.label) { fontSize = 13, fontStyle = FontStyle.Bold, richText = true };
            Small = new GUIStyle(GUI.skin.label) { fontSize = 11, richText = true, wordWrap = true };
            Small.normal.textColor = new Color(0.8f, 0.8f, 0.8f);
        }

        public void Header(string text)
        {
            GUILayout.Space(6);
            GUILayout.Label(text, HeaderStyle);
        }

        public void Info(string text) => GUILayout.Label(text, Small);

        public float Slider(string label, float value, float min, float max, string format = "0.00")
        {
            GUILayout.Label($"{label}: <b>{value.ToString(format)}</b>", Label);
            return GUILayout.HorizontalSlider(value, min, max);
        }

        public int IntSlider(string label, int value, int min, int max)
        {
            GUILayout.Label($"{label}: <b>{value}</b>", Label);
            return Mathf.RoundToInt(GUILayout.HorizontalSlider(value, min, max));
        }

        public bool Toggle(string label, bool value) => GUILayout.Toggle(value, " " + label);

        public T Enum<T>(string label, T value) where T : Enum
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, Label, GUILayout.Width(120));
            if (GUILayout.Button(value.ToString())) value = Next(value);
            GUILayout.EndHorizontal();
            return value;
        }

        public static T Next<T>(T value) where T : Enum
        {
            var values = (T[])System.Enum.GetValues(typeof(T));
            int i = Array.IndexOf(values, value);
            return values[(i + 1) % values.Length];
        }

        public bool Button(string label) => GUILayout.Button(label);

        // ---------------------------------------------------------------- shared blocks

        public void Range(RangeSettings r, float maxRange, string label = "Max Range")
        {
            r.maxRange = Slider(label, r.maxRange, 0.5f, maxRange, "0.0");
            r.fadeStartFraction = Slider("Fade Start Fraction", r.fadeStartFraction, 0f, 1f);
        }

        public void Falloff(DistanceFalloff f, string label = "Distance Falloff")
        {
            f.mode = Enum(label, f.mode);
            if (f.mode == FalloffMode.Linear)
            {
                f.startFraction = Slider("  Falloff Starts At (fraction)", f.startFraction, 0f, 1f);
                f.endMultiplier = Slider("  Multiplier At Max", f.endMultiplier, 0f, 1f);
            }
            else if (f.mode == FalloffMode.CustomCurve)
            {
                Info("  Edit the curve on the weapon component in the Inspector (x = distance fraction, y = multiplier).");
            }
            if (f.mode != FalloffMode.None)
                Info($"  At 0 / 50 / 100%: x{f.Evaluate(0f):0.00} / x{f.Evaluate(0.5f):0.00} / x{f.Evaluate(1f):0.00}");
        }

        public void Pierce(PierceSettings p, int maxCount = 10, bool showCount = true)
        {
            if (showCount)
            {
                p.infinite = Toggle("Infinite piercing", p.infinite);
                if (!p.infinite) p.pierceCount = IntSlider("Pierce Count (extra enemies)", p.pierceCount, 0, maxCount);
            }
            p.falloff = Enum("Pierce Falloff", p.falloff);
            switch (p.falloff)
            {
                case PierceFalloffMode.Multiplicative:
                    p.multiplier = Slider("  Pierce Damage Multiplier", p.multiplier, 0f, 1f);
                    break;
                case PierceFalloffMode.FlatSubtraction:
                    p.flatSubtraction = Slider("  Damage Lost Per Pierce", p.flatSubtraction, 0f, 100f, "0.0");
                    break;
                case PierceFalloffMode.CustomCurve:
                    Info("  Edit the curve in the Inspector (x = hit index, y = multiplier).");
                    break;
            }
            if (p.falloff != PierceFalloffMode.None)
                Info($"  Hits 1-4 (of 100): {100f:0} / {100f * p.Evaluate(1, 100f):0.#} / {100f * p.Evaluate(2, 100f):0.#} / {100f * p.Evaluate(3, 100f):0.#}");
        }

        public void Knockback(KnockbackSettings k, float maxForce = 30f)
        {
            k.force = Slider("Knockback Force", k.force, 0f, maxForce, "0.0");
            if (k.force <= 0f) return;
            k.direction = Enum("Direction", k.direction);
            if (k.direction == KnockbackDirection.Custom) k.customAngle = Slider("  Custom Angle (deg)", k.customAngle, -180f, 180f, "0");
            k.scaleWithFalloff = Toggle("Scale with damage falloff", k.scaleWithFalloff);
        }
    }
}
