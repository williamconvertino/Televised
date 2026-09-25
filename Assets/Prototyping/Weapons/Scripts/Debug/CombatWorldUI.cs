using System.Collections.Generic;
using Televised.Prototyping.Shared;
using UnityEngine;

namespace Televised.Prototyping.Weapons
{
    /// <summary>
    /// World-space overlays drawn with IMGUI: floating damage numbers (with optional per-hit breakdown), enemy HP
    /// labels / bars, zone markers, impact velocity readouts, and the knockback-vector debug records.
    /// </summary>
    public class CombatWorldUI : MonoBehaviour
    {
        [Tooltip("Hits at or above this are shown as 'large'.")]
        [SerializeField] float largeDamageThreshold = 50f;
        [SerializeField] float numberLifetime = 0.9f;
        [Tooltip("Repeated hits from the same source on the same target within this window merge into one number.")]
        [SerializeField] float mergeWindow = 0.3f;

        class FloatingNumber
        {
            public Vector2 position;
            public Object target;
            public string source;
            public float value;
            public bool blocked;
            public DamageKind kind;
            public float created, lastHit;
            public string detail;
            public bool onPlayer;
        }

        readonly List<FloatingNumber> _numbers = new List<FloatingNumber>();
        GUIStyle _number, _label, _small, _marker;
        Texture2D _white;
        PlayerHealth _subscribedPlayer;

        static readonly Color NormalColor = new Color(1f, 1f, 1f);
        static readonly Color LargeColor = new Color(1f, 0.75f, 0.2f);
        static readonly Color ImpactColor = new Color(0.4f, 0.95f, 1f);
        static readonly Color SlamColor = new Color(1f, 0.45f, 0.95f);
        static readonly Color PlayerHurtColor = new Color(1f, 0.35f, 0.35f);
        static readonly Color BlockedColor = new Color(0.65f, 0.75f, 0.9f);

        void OnEnable() => Health.AnyDamaged += OnEnemyDamaged;

        void OnDisable()
        {
            Health.AnyDamaged -= OnEnemyDamaged;
            if (_subscribedPlayer != null) _subscribedPlayer.Damaged -= OnPlayerDamaged;
            _subscribedPlayer = null;
        }

        void Update()
        {
            if (_subscribedPlayer == null && PlayerHealth.Instance != null)
            {
                _subscribedPlayer = PlayerHealth.Instance;
                _subscribedPlayer.Damaged += OnPlayerDamaged;
            }
            _numbers.RemoveAll(n => Time.time - n.lastHit > numberLifetime + (CombatDebug.DetailedHitInfo ? 1.2f : 0f));
            CombatDebug.DrawRecords();

            if (CombatDebug.ImpactVelocity && PlayerHealth.Instance != null)
            {
                PlayerMotor2D m = PlayerHealth.Instance.Motor;
                DebugLines.Arrow(m.Position, m.Position + m.Velocity * 0.2f, ImpactColor, 0.2f);
            }
        }

        void OnEnemyDamaged(Health h, DamageEvent e) => AddNumber(h, e, e.hitPoint, false);

        void OnPlayerDamaged(DamageEvent e) =>
            AddNumber(PlayerHealth.Instance, e, PlayerHealth.Instance.Motor.Position + Vector2.up * 0.6f, true);

        void AddNumber(Object target, DamageEvent e, Vector2 at, bool onPlayer)
        {
            if (!CombatDebug.DamageNumbers) return;
            if (CombatDebug.MergeRapidHits && !CombatDebug.DetailedHitInfo)
            {
                foreach (FloatingNumber n in _numbers)
                {
                    if (n.target != target || n.source != e.sourceName || Time.time - n.lastHit > mergeWindow) continue;
                    n.value += e.finalDamage;
                    n.lastHit = Time.time;
                    n.blocked = e.blocked;
                    return;
                }
            }
            _numbers.Add(new FloatingNumber
            {
                position = at + Random.insideUnitCircle * 0.15f,
                target = target,
                source = e.sourceName,
                value = e.finalDamage,
                blocked = e.blocked,
                kind = e.kind,
                created = Time.time,
                lastHit = Time.time,
                detail = CombatDebug.DetailedHitInfo ? e.DetailText() : null,
                onPlayer = onPlayer,
            });
        }

        public void ClearNumbers() => _numbers.Clear();

        void EnsureStyles()
        {
            if (_number != null) return;
            _number = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, fontSize = 16 };
            _label = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 11 };
            _small = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.UpperLeft, fontSize = 11, wordWrap = false };
            _marker = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 13, richText = true };
            _white = Texture2D.whiteTexture;
        }

        void OnGUI()
        {
            Camera cam = Camera.main;
            if (cam == null) return;
            EnsureStyles();

            if (CombatDebug.ZoneLabels) DrawMarkers(cam);
            if (CombatDebug.HealthLabels) DrawEnemyLabels(cam);
            DrawNumbers(cam);
            if (CombatDebug.ImpactVelocity) DrawImpactReadout(cam);
        }

        bool ToGui(Camera cam, Vector2 world, out Vector2 gui)
        {
            Vector3 sp = cam.WorldToScreenPoint(new Vector3(world.x, world.y, 0f));
            gui = new Vector2(sp.x, Screen.height - sp.y);
            return sp.z >= 0f && gui.x > -200 && gui.x < Screen.width + 200 && gui.y > -200 && gui.y < Screen.height + 200;
        }

        float PixelsPerUnit(Camera cam) => Screen.height / (2f * cam.orthographicSize);

        void Shadowed(Rect r, string text, GUIStyle style, Color color)
        {
            Color prev = style.normal.textColor;
            style.normal.textColor = new Color(0f, 0f, 0f, color.a * 0.8f);
            GUI.Label(new Rect(r.x + 1, r.y + 1, r.width, r.height), text, style);
            style.normal.textColor = color;
            GUI.Label(r, text, style);
            style.normal.textColor = prev;
        }

        void DrawMarkers(Camera cam)
        {
            foreach (WorldMarker m in WorldMarker.All)
            {
                if (!ToGui(cam, m.transform.position, out Vector2 g)) continue;
                _marker.fontSize = m.fontSize;
                _marker.fontStyle = m.bold ? FontStyle.Bold : FontStyle.Normal;
                Shadowed(new Rect(g.x - 200, g.y - 30, 400, 60), m.text, _marker, m.color);
            }
        }

        void DrawEnemyLabels(Camera cam)
        {
            float ppu = PixelsPerUnit(cam);
            foreach (DummyEnemy e in DummyEnemy.All)
            {
                if (e == null || !e.isActiveAndEnabled) continue;
                Vector2 top = e.Position + Vector2.up * (e.Radius + 0.1f);
                if (!ToGui(cam, top, out Vector2 g)) continue;
                if (!e.IsAlive)
                {
                    float r = e.RespawnRemaining;
                    Shadowed(new Rect(g.x - 50, g.y - 18, 100, 16), r >= 0f ? $"respawn {r:0.0}s" : "dead", _label, new Color(0.7f, 0.7f, 0.7f, 0.8f));
                    continue;
                }
                Health h = e.Health;
                float barW = Mathf.Clamp(e.Radius * 2f * ppu, 30f, 90f);
                var bar = new Rect(g.x - barW * 0.5f, g.y - 6, barW, 4);
                GUI.color = new Color(0f, 0f, 0f, 0.6f);
                GUI.DrawTexture(bar, _white);
                GUI.color = h.Invulnerable ? new Color(0.6f, 0.8f, 1f) : Color.Lerp(new Color(1f, 0.25f, 0.2f), new Color(0.35f, 1f, 0.4f), h.Fraction);
                GUI.DrawTexture(new Rect(bar.x, bar.y, bar.width * h.Fraction, bar.height), _white);
                GUI.color = Color.white;
                string hp = h.Invulnerable ? $"{h.Current:0} / {h.MaxHealth:0} (inv)" : $"{h.Current:0} / {h.MaxHealth:0}";
                Shadowed(new Rect(g.x - 60, g.y - 22, 120, 16), hp, _label, new Color(1f, 1f, 1f, 0.85f));
            }
        }

        void DrawNumbers(Camera cam)
        {
            foreach (FloatingNumber n in _numbers)
            {
                float age = Time.time - n.created;
                float sinceLast = Time.time - n.lastHit;
                float life = numberLifetime + (n.detail != null ? 1.2f : 0f);
                float alpha = 1f - Mathf.Clamp01((sinceLast - life * 0.6f) / (life * 0.4f));
                Vector2 world = n.position + Vector2.up * Mathf.Min(0.9f, age * 1.4f);
                if (!ToGui(cam, world, out Vector2 g)) continue;

                bool large = n.value >= largeDamageThreshold;
                Color c = n.onPlayer ? PlayerHurtColor : n.kind == DamageKind.BodyImpact ? ImpactColor
                    : n.kind == DamageKind.WallSlam ? SlamColor : large ? LargeColor : NormalColor;
                if (n.blocked) c = BlockedColor;
                c.a = alpha;
                _number.fontSize = large || n.kind == DamageKind.BodyImpact || n.kind == DamageKind.WallSlam ? 22 : 16;
                string text = n.value >= 10f ? n.value.ToString("0") : n.value.ToString("0.0");
                if (n.onPlayer) text = "-" + text;
                if (n.blocked) text += "*";
                if (n.kind == DamageKind.WallSlam) text += "!";
                Shadowed(new Rect(g.x - 60, g.y - 14, 120, 28), text, _number, c);

                if (n.detail != null)
                {
                    var r = new Rect(g.x + 22, g.y - 8, 240, 160);
                    GUI.color = new Color(0f, 0f, 0f, 0.55f * alpha);
                    Vector2 size = _small.CalcSize(new GUIContent(n.detail));
                    GUI.DrawTexture(new Rect(r.x - 3, r.y - 2, size.x + 6, size.y + 4), _white);
                    GUI.color = Color.white;
                    Shadowed(r, n.detail, _small, new Color(1f, 1f, 1f, alpha));
                }
            }
        }

        void DrawImpactReadout(Camera cam)
        {
            PlayerHealth p = PlayerHealth.Instance;
            if (p == null) return;
            var impact = p.GetComponent<BodyImpactDamage>();
            Vector2 at = p.Motor.Position + Vector2.down * (p.Motor.Radius + 0.35f);
            if (!ToGui(cam, at, out Vector2 g)) return;
            string text = $"speed {p.Motor.Velocity.magnitude:0.0}";
            if (impact != null && impact.CurrentApproachSpeed > 0f) text += $"  approach {impact.CurrentApproachSpeed:0.0}";
            if (impact != null && impact.HasImpact && Time.time - impact.LastImpactTime < 1.5f)
                text += $"\nlast impact {impact.LastImpact.impactSpeed:0.0} -> {impact.LastImpact.finalDamage:0}";
            Shadowed(new Rect(g.x - 120, g.y, 240, 36), text, _label, ImpactColor);
        }
    }
}
