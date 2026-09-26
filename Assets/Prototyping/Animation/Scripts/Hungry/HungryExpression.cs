using System;
using System.Collections.Generic;
using UnityEngine;

namespace Televised.Prototyping.AnimationLab
{
    // Enum names match the sprite suffixes (hungry_mouth_<lowercase name>, hungry_glove_<lowercase name>).
    public enum HungryMouth { Grin, GrinWide, Smirk, SmirkSide, Smile, Frown, FrownOpen, Snarl, Roar, Wavy, O }
    public enum HungryBrows { Sharp, Soft }
    public enum HungryPupils { Ring, Arc }
    public enum HungryGlove { Open, Claw, Point, Fist }

    /// <summary>
    /// One of Hungry's facial expressions: sprite choices plus the named parameters HungryFace blends between.
    /// Distances are world units, angles degrees. Glove offsets are mirrored per side: +x is away from the face.
    /// </summary>
    [Serializable]
    public class HungryExpression
    {
        public string name = "Expression";

        [Header("Sprites")]
        public HungryMouth mouth = HungryMouth.Grin;
        public HungryBrows brows = HungryBrows.Sharp;
        public HungryPupils pupils = HungryPupils.Ring;
        public HungryGlove gloveL = HungryGlove.Open, gloveR = HungryGlove.Open;

        [Header("Brows")]
        [Range(-0.5f, 0.5f)] public float browRaiseL, browRaiseR;
        [Tooltip("+ = angrier (outer end up, inner end down), - = worried.")]
        [Range(-35f, 35f)] public float browTiltL, browTiltR;
        [Range(-0.2f, 0.2f)] public float browInward;
        [Range(0.6f, 1.4f)] public float browSquash = 1f;

        [Header("Eyes")]
        [Range(0f, 1f)] public float squintL, squintR;
        [Tooltip("Upper lid slant. + = angry (inner corner lower), - = droopy.")]
        [Range(-30f, 30f)] public float lidTilt;
        [Range(0f, 1f)] public float cheek;
        [Range(0.2f, 1.6f)] public float pupilSize = 1f;
        public Vector2 pupilLook;

        [Header("Mouth")]
        [Range(0.6f, 1.4f)] public float mouthWidth = 1f;
        [Range(0.4f, 1.6f)] public float mouthOpen = 1f;
        [Range(-20f, 20f)] public float mouthTilt;
        public Vector2 mouthOffset;

        [Header("Head")]
        [Range(-25f, 25f)] public float headTilt;
        [Range(0.85f, 1.15f)] public float headStretch = 1f;
        public Vector2 headOffset;
        [Range(0f, 1f)] public float redness;
        [Range(0f, 1f)] public float shake;
        [Range(0f, 1f)] public float laugh;

        [Header("Gloves")]
        public Vector2 gloveOffsetL, gloveOffsetR;
        [Range(-60f, 60f)] public float gloveAngleL, gloveAngleR;
        [Range(0f, 1f)] public float gloveTwiddle;

        public HungryExpression Clone() => (HungryExpression)MemberwiseClone();

        /// <summary>Moves every continuous parameter a fraction <paramref name="k"/> toward <paramref name="t"/>.</summary>
        public void BlendToward(HungryExpression t, float k)
        {
            static float L(float a, float b, float k) => a + (b - a) * k;
            static Vector2 V(Vector2 a, Vector2 b, float k) => a + (b - a) * k;
            browRaiseL = L(browRaiseL, t.browRaiseL, k); browRaiseR = L(browRaiseR, t.browRaiseR, k);
            browTiltL = L(browTiltL, t.browTiltL, k); browTiltR = L(browTiltR, t.browTiltR, k);
            browInward = L(browInward, t.browInward, k); browSquash = L(browSquash, t.browSquash, k);
            squintL = L(squintL, t.squintL, k); squintR = L(squintR, t.squintR, k);
            lidTilt = L(lidTilt, t.lidTilt, k); cheek = L(cheek, t.cheek, k);
            pupilSize = L(pupilSize, t.pupilSize, k); pupilLook = V(pupilLook, t.pupilLook, k);
            mouthWidth = L(mouthWidth, t.mouthWidth, k); mouthOpen = L(mouthOpen, t.mouthOpen, k);
            mouthTilt = L(mouthTilt, t.mouthTilt, k); mouthOffset = V(mouthOffset, t.mouthOffset, k);
            headTilt = L(headTilt, t.headTilt, k); headStretch = L(headStretch, t.headStretch, k);
            headOffset = V(headOffset, t.headOffset, k); redness = L(redness, t.redness, k);
            shake = L(shake, t.shake, k); laugh = L(laugh, t.laugh, k);
            gloveOffsetL = V(gloveOffsetL, t.gloveOffsetL, k); gloveOffsetR = V(gloveOffsetR, t.gloveOffsetR, k);
            gloveAngleL = L(gloveAngleL, t.gloveAngleL, k); gloveAngleR = L(gloveAngleR, t.gloveAngleR, k);
            gloveTwiddle = L(gloveTwiddle, t.gloveTwiddle, k);
        }

        // Symmetric setters for the presets below.
        HungryExpression Brows(float raise, float tilt) { browRaiseL = browRaiseR = raise; browTiltL = browTiltR = tilt; return this; }
        HungryExpression Squint(float s) { squintL = squintR = s; return this; }
        HungryExpression Gloves(HungryGlove pose, Vector2 offset, float angle = 0f)
        {
            gloveL = gloveR = pose; gloveOffsetL = gloveOffsetR = offset; gloveAngleL = gloveAngleR = angle;
            return this;
        }

        /// <summary>The starting expression set. Mostly evil; tweak the copies on HungryFace in the Inspector.</summary>
        public static List<HungryExpression> Defaults() => new List<HungryExpression>
        {
            new HungryExpression { name = "Menace", mouth = HungryMouth.Smirk, lidTilt = 10f, pupilSize = 0.9f }
                .Squint(0.15f).Gloves(HungryGlove.Open, new Vector2(0.3f, -0.35f), -8f),

            new HungryExpression { name = "Evil Grin", mouth = HungryMouth.Grin, lidTilt = 12f, cheek = 0.1f }
                .Squint(0.2f).Gloves(HungryGlove.Claw, Vector2.zero),

            new HungryExpression
            {
                name = "Maniacal", mouth = HungryMouth.GrinWide, browSquash = 1.12f, pupilSize = 0.4f,
                mouthWidth = 1.05f, headStretch = 1.05f, shake = 0.2f,
            }.Brows(0.28f, -4f).Gloves(HungryGlove.Claw, new Vector2(0.1f, 0.6f), 12f),

            new HungryExpression
            {
                name = "Evil Laugh", mouth = HungryMouth.GrinWide, pupils = HungryPupils.Arc, cheek = 0.35f,
                lidTilt = 6f, laugh = 1f, headTilt = -8f,
            }.Brows(0.05f, 6f).Squint(0.4f).Gloves(HungryGlove.Claw, new Vector2(-0.2f, 0.25f), 10f),

            new HungryExpression
            {
                name = "Gleeful", mouth = HungryMouth.Grin, pupils = HungryPupils.Arc, cheek = 0.45f, headTilt = 10f,
                gloveL = HungryGlove.Claw, gloveR = HungryGlove.Open, gloveOffsetR = new Vector2(-0.55f, 0.3f), gloveAngleR = 12f,
            }.Brows(0f, 8f).Squint(0.3f),

            new HungryExpression
            {
                name = "Smug", mouth = HungryMouth.SmirkSide, browRaiseL = 0.22f, browTiltL = -12f, browRaiseR = -0.06f,
                browTiltR = 10f, pupilLook = new Vector2(0.5f, 0f), headTilt = -6f, lidTilt = 0f,
                gloveL = HungryGlove.Open, gloveR = HungryGlove.Point, gloveOffsetL = new Vector2(0.3f, -0.4f),
                gloveOffsetR = new Vector2(-0.1f, 0.05f), gloveAngleR = 10f,
            }.Squint(0.4f),

            new HungryExpression
            {
                name = "Scheming", mouth = HungryMouth.Smirk, browInward = 0.05f, browSquash = 0.85f, lidTilt = 18f,
                pupilLook = new Vector2(-0.8f, -0.2f), pupilSize = 0.8f, headTilt = 5f, gloveTwiddle = 1f,
            }.Brows(-0.12f, 18f).Squint(0.5f).Gloves(HungryGlove.Claw, new Vector2(-0.9f, -0.15f), 20f),

            new HungryExpression
            {
                name = "Hungry", mouth = HungryMouth.GrinWide, mouthOpen = 1.3f, mouthWidth = 0.95f, pupilSize = 0.3f,
                pupilLook = new Vector2(0f, -0.4f), lidTilt = 12f, headOffset = new Vector2(0f, 0.1f), gloveTwiddle = 0.6f,
            }.Brows(-0.05f, 14f).Squint(0.25f).Gloves(HungryGlove.Claw, new Vector2(-0.3f, 0.2f), 18f),

            new HungryExpression
            {
                name = "Happy", mouth = HungryMouth.Smile, brows = HungryBrows.Soft, cheek = 0.3f, pupilSize = 1.2f,
                laugh = 0.25f,
            }.Brows(0.2f, -5f).Gloves(HungryGlove.Open, new Vector2(0.2f, 0.5f), -10f),

            new HungryExpression
            {
                name = "Sad", mouth = HungryMouth.Frown, brows = HungryBrows.Soft, lidTilt = -18f, pupilSize = 1.1f,
                pupilLook = new Vector2(0f, -0.6f), headStretch = 0.97f, headOffset = new Vector2(0f, -0.15f),
            }.Brows(0.05f, -22f).Squint(0.35f).Gloves(HungryGlove.Open, new Vector2(0.2f, -0.6f), -20f),

            new HungryExpression
            {
                name = "Upset", mouth = HungryMouth.FrownOpen, brows = HungryBrows.Soft, browSquash = 1.1f, lidTilt = -20f,
                pupilSize = 1.3f, shake = 0.15f,
            }.Brows(0.12f, -28f).Squint(0.25f).Gloves(HungryGlove.Fist, new Vector2(-0.45f, 0.45f), 10f),

            new HungryExpression
            {
                name = "Confused", mouth = HungryMouth.Wavy, browRaiseL = 0.2f, browTiltL = -15f, browRaiseR = -0.1f,
                browTiltR = 15f, squintR = 0.35f, pupilLook = new Vector2(0.3f, 0.4f), headTilt = 12f, mouthTilt = -6f,
                mouthOffset = new Vector2(0.1f, 0f), gloveL = HungryGlove.Open, gloveR = HungryGlove.Point,
                gloveOffsetL = new Vector2(0.2f, -0.5f), gloveOffsetR = new Vector2(-0.35f, 2.7f), gloveAngleR = 0f,
                gloveTwiddle = 0.3f,
            },

            new HungryExpression
            {
                name = "Surprised", mouth = HungryMouth.O, brows = HungryBrows.Soft, browSquash = 1.1f, pupilSize = 0.5f,
                headStretch = 1.06f,
            }.Brows(0.35f, 0f).Gloves(HungryGlove.Open, new Vector2(0.35f, 0.8f), -15f),

            new HungryExpression
            {
                name = "Angry", mouth = HungryMouth.Snarl, browInward = 0.08f, browSquash = 0.9f, lidTilt = 22f,
                pupilSize = 0.7f, redness = 0.25f,
            }.Brows(-0.18f, 12f).Squint(0.35f).Gloves(HungryGlove.Fist, new Vector2(0f, -0.1f)),

            new HungryExpression
            {
                name = "Furious", mouth = HungryMouth.Roar, browInward = 0.12f, browSquash = 0.8f, lidTilt = 25f,
                pupilSize = 0.35f, redness = 0.75f, shake = 1f, headStretch = 1.05f, mouthWidth = 1.05f,
            }.Brows(-0.25f, 22f).Squint(0.3f).Gloves(HungryGlove.Fist, new Vector2(0.1f, 0.5f), -10f),
        };
    }
}
