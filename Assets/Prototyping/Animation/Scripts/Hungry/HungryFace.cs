using System.Collections.Generic;
using Televised.Prototyping.Shared;
using UnityEngine;

namespace Televised.Prototyping.AnimationLab
{
    /// <summary>
    /// Drives Hungry's sprite rig: blends toward the selected HungryExpression and layers procedural motion on
    /// top (hover, breathing, laugh bounce, shake, blinks, brow twitches, darting pupils, glove twiddle).
    /// Sprite choices (mouth, brow style, pupil style, glove poses) swap instantly with a small squash "pop".
    /// The rig and its references are created by HungryRigBuilder (Animation Lab rebuild).
    /// </summary>
    public class HungryFace : MonoBehaviour
    {
        [Header("Rig (filled by HungryRigBuilder)")]
        [SerializeField] Transform head;
        [SerializeField] SpriteRenderer face;
        [SerializeField] Transform browL, browR;
        [SerializeField] SpriteRenderer browLSharp, browLSoft, browRSharp, browRSoft;
        [SerializeField] Transform pupilL, pupilR;
        [SerializeField] SpriteRenderer pupilLRing, pupilLArc, pupilRRing, pupilRArc;
        [SerializeField] Transform lidL, lidR, cheekL, cheekR;
        [SerializeField] Transform mouth;
        [SerializeField] SpriteRenderer[] mouths;       // indexed by HungryMouth
        [SerializeField] Transform gloveL, gloveR;      // motion transforms; the left glove's parent is mirrored
        [SerializeField] SpriteRenderer[] gloveLPoses;  // indexed by HungryGlove
        [SerializeField] SpriteRenderer[] gloveRPoses;

        [Header("Expressions")]
        [SerializeField] List<HungryExpression> expressions = HungryExpression.Defaults();
        [SerializeField] int current = 1;
        [Tooltip("How quickly parameters settle on a new expression (1/s).")]
        [SerializeField, Min(0.5f)] float blendSharpness = 9f;

        [Header("Procedural")]
        public bool lookAtMouse;
        public bool wanderingPupils = true;
        public bool blinks = true;
        public bool browTwitches = true;
        [SerializeField, Range(0f, 2f)] float hover = 1f;

        // Travel ranges, world units (canvas units / 150).
        const float LidTravel = 1.4f, CheekTravel = 0.8f;
        static readonly Vector2 PupilRange = new Vector2(0.24f, 0.3f);
        static readonly Color FuriousTint = new Color(1f, 0.62f, 0.58f);

        struct Rest { public Vector3 pos; public Vector3 scale; }
        readonly Dictionary<Transform, Rest> _rest = new Dictionary<Transform, Rest>();

        HungryExpression _live;
        HungryMouth _shownMouth = (HungryMouth)(-1);
        HungryGlove _shownGloveL = (HungryGlove)(-1), _shownGloveR = (HungryGlove)(-1);
        float _mouthPop, _glovePopL, _glovePopR;

        float _blinkTimer, _blinkT = -1f;
        float _twitchTimer, _twitch; int _twitchSide;
        float _saccadeTimer; Vector2 _saccadeGoal, _saccade;
        Vector2 _shakeOffset; float _shakeAngle, _shakeTimer;

        public IReadOnlyList<HungryExpression> Expressions => expressions;
        public int Current => current;
        public string CurrentName => Target?.name ?? "";
        HungryExpression Target => expressions.Count > 0 ? expressions[Mathf.Clamp(current, 0, expressions.Count - 1)] : null;

        public void SetExpression(int index) => current = Mathf.Clamp(index, 0, expressions.Count - 1);

        public bool SetExpression(string expressionName)
        {
            int i = expressions.FindIndex(e => e.name == expressionName);
            if (i >= 0) current = i;
            return i >= 0;
        }

        void Awake()
        {
            foreach (var t in new[] { head, browL, browR, pupilL, pupilR, lidL, lidR, cheekL, cheekR, mouth, gloveL, gloveR })
                if (t != null) _rest[t] = new Rest { pos = t.localPosition, scale = t.localScale };
            _live = Target?.Clone() ?? new HungryExpression();
            _blinkTimer = Random.Range(2f, 6f);
            _twitchTimer = Random.Range(2f, 5f);
        }

        void LateUpdate()
        {
            var target = Target;
            if (target == null || head == null) return;
            float dt = Time.deltaTime, t = Time.time;

            _live.BlendToward(target, 1f - Mathf.Exp(-blendSharpness * dt));
            SwapSprites(target);
            TickTimers(dt);

            var e = _live;
            float pop = 1f - Mathf.Exp(-14f * dt);
            _mouthPop -= _mouthPop * pop; _glovePopL -= _glovePopL * pop; _glovePopR -= _glovePopR * pop;

            // ---- head: hover, breathing, laugh bounce, shake
            float laughWave = Mathf.Abs(Mathf.Sin(t * 13f)) * e.laugh;
            float breath = 1f + 0.012f * Mathf.Sin(t * 2.1f);
            Vector2 headPos = e.headOffset + new Vector2(0f, Mathf.Sin(t * 1.3f) * 0.07f * hover + laughWave * 0.09f)
                            + _shakeOffset * e.shake;
            Place(head, headPos, e.headTilt + _shakeAngle * e.shake + Mathf.Sin(t * 0.9f) * 1.5f * hover,
                  new Vector2(1f / Mathf.Sqrt(e.headStretch), e.headStretch * breath));
            if (face != null) face.color = Color.Lerp(Color.white, FuriousTint, e.redness);

            // ---- brows (pivot at the inner end)
            float twitchL = _twitchSide == 0 ? _twitch : 0f, twitchR = _twitchSide == 1 ? _twitch : 0f;
            float browX = 1f + (1f - e.browSquash) * 0.4f;
            float browShake = _shakeOffset.y * e.shake * 0.6f;
            Place(browL, new Vector2(e.browInward, e.browRaiseL + twitchL * 0.08f + browShake),
                  -(e.browTiltL + twitchL * 5f), new Vector2(browX, e.browSquash));
            Place(browR, new Vector2(-e.browInward, e.browRaiseR + twitchR * 0.08f - browShake),
                  e.browTiltR + twitchR * 5f, new Vector2(browX, e.browSquash));

            // ---- eyes: lids (masked to the socket), cheeks, pupils
            float blink = _blinkT >= 0f ? Mathf.Sin(_blinkT * Mathf.PI) : 0f;
            Place(lidL, new Vector2(0f, -Mathf.Max(e.squintL, blink) * LidTravel), -e.lidTilt, Vector2.one);
            Place(lidR, new Vector2(0f, -Mathf.Max(e.squintR, blink) * LidTravel), e.lidTilt, Vector2.one);
            Place(cheekL, new Vector2(0f, e.cheek * CheekTravel), 0f, Vector2.one);
            Place(cheekR, new Vector2(0f, e.cheek * CheekTravel), 0f, Vector2.one);

            Vector2 look = e.pupilLook + (wanderingPupils ? _saccade : Vector2.zero);
            Place(pupilL, Vector2.Scale(Look(pupilL, look), PupilRange), 0f, Vector2.one * e.pupilSize);
            Place(pupilR, Vector2.Scale(Look(pupilR, look), PupilRange), 0f, Vector2.one * e.pupilSize);

            // ---- mouth
            float mouthOpen = e.mouthOpen * (1f - 0.35f * _mouthPop) + laughWave * 0.22f;
            Place(mouth, e.mouthOffset, e.mouthTilt, new Vector2(e.mouthWidth * (1f + 0.08f * _mouthPop), mouthOpen));

            // ---- gloves (+x is away from the face on both sides; the left glove's parent is mirrored)
            float twiddle = e.gloveTwiddle;
            for (int side = 0; side < 2; side++)
            {
                var g = side == 0 ? gloveL : gloveR;
                float phase = side * 2.1f, popAmount = side == 0 ? _glovePopL : _glovePopR;
                Vector2 offset = (side == 0 ? e.gloveOffsetL : e.gloveOffsetR)
                    + new Vector2(Mathf.Sin(t * 7f + phase * 1.5f) * 0.08f * twiddle,
                                  Mathf.Sin(t * 1.1f + phase) * 0.1f * hover + laughWave * 0.12f)
                    + _shakeOffset * (e.shake * 0.7f);
                float angle = (side == 0 ? e.gloveAngleL : e.gloveAngleR)
                    + Mathf.Sin(t * 0.8f + phase) * 3f * hover + Mathf.Sin(t * 11f + phase) * 7f * twiddle;
                Place(g, offset, angle, Vector2.one * (1f - 0.15f * popAmount));
            }
        }

        // ------------------------------------------------------------------ helpers

        void Place(Transform tr, Vector2 offset, float angle, Vector2 scale)
        {
            if (tr == null || !_rest.TryGetValue(tr, out var r)) return;
            tr.localPosition = r.pos + (Vector3)offset;
            tr.localRotation = Quaternion.Euler(0f, 0f, angle);
            tr.localScale = new Vector3(r.scale.x * scale.x, r.scale.y * scale.y, r.scale.z);
        }

        /// <summary>Normalized look direction (-1..1 box) for one pupil, following the mouse if enabled.</summary>
        Vector2 Look(Transform pupil, Vector2 bias)
        {
            if (lookAtMouse && Camera.main != null && pupil.parent != null)
            {
                Vector2 mouse = Camera.main.ScreenToWorldPoint(PrototypeInput.MouseScreen);
                Vector2 d = mouse - (Vector2)pupil.parent.position;
                bias = d.normalized * Mathf.Clamp01(d.magnitude / 3f) + bias * 0.25f;
            }
            return Vector2.ClampMagnitude(bias, 1f);
        }

        void SwapSprites(HungryExpression target)
        {
            if (target.mouth != _shownMouth)
            {
                if ((int)_shownMouth >= 0) _mouthPop = 1f;
                _shownMouth = target.mouth;
                for (int i = 0; i < mouths.Length; i++) mouths[i].enabled = i == (int)target.mouth;
            }
            if (target.gloveL != _shownGloveL)
            {
                if ((int)_shownGloveL >= 0) _glovePopL = 1f;
                _shownGloveL = target.gloveL;
                for (int i = 0; i < gloveLPoses.Length; i++) gloveLPoses[i].enabled = i == (int)target.gloveL;
            }
            if (target.gloveR != _shownGloveR)
            {
                if ((int)_shownGloveR >= 0) _glovePopR = 1f;
                _shownGloveR = target.gloveR;
                for (int i = 0; i < gloveRPoses.Length; i++) gloveRPoses[i].enabled = i == (int)target.gloveR;
            }
            bool soft = target.brows == HungryBrows.Soft, arc = target.pupils == HungryPupils.Arc;
            browLSharp.enabled = browRSharp.enabled = !soft;
            browLSoft.enabled = browRSoft.enabled = soft;
            pupilLRing.enabled = pupilRRing.enabled = !arc;
            pupilLArc.enabled = pupilRArc.enabled = arc;
        }

        void TickTimers(float dt)
        {
            // Blinks are rare and a little slow: the sockets should mostly feel dead.
            if (_blinkT >= 0f) { _blinkT += dt / 0.22f; if (_blinkT >= 1f) _blinkT = -1f; }
            else if (blinks && (_blinkTimer -= dt) <= 0f) { _blinkT = 0f; _blinkTimer = Random.Range(3.5f, 9f); }

            _twitch = Mathf.MoveTowards(_twitch, 0f, dt * 6f);
            if (browTwitches && (_twitchTimer -= dt) <= 0f)
            {
                _twitch = 1f; _twitchSide = Random.Range(0, 2); _twitchTimer = Random.Range(2.5f, 7f);
            }

            // Pupils hold still, then dart (shark-like), instead of drifting.
            if ((_saccadeTimer -= dt) <= 0f)
            {
                _saccadeGoal = Random.insideUnitCircle * 0.45f;
                _saccadeTimer = Random.Range(0.6f, 2.8f);
            }
            _saccade = Vector2.Lerp(_saccade, _saccadeGoal, 1f - Mathf.Exp(-30f * dt));

            // Shake: re-rolled at a fixed rate so it reads as trembling rather than per-frame noise.
            if ((_shakeTimer -= dt) <= 0f)
            {
                _shakeTimer = 0.035f;
                _shakeOffset = Random.insideUnitCircle * 0.06f;
                _shakeAngle = Random.Range(-2.5f, 2.5f);
            }
        }
    }
}
