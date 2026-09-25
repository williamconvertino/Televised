using Televised.Prototyping.Shared;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Televised.Prototyping.Weapons
{
    /// <summary>
    /// The combat prototype's HUD and tabbed tuning panel (IMGUI, same look as the movement panel):
    /// PLAYER / ENEMIES / WEAPON / IMPACT / DEBUG / STATS. The weapon tab only shows the selected weapon's settings.
    /// Movement tuning stays in the movement panel (Tab); when both are open this one moves left of it.
    /// F10 toggles this panel.
    /// </summary>
    [DefaultExecutionOrder(-99)] // right after the movement panel, which resets PointerBlocked each frame
    public class CombatDebugPanel : MonoBehaviour
    {
        enum Tab { Player, Enemies, Weapon, Impact, Debug, Stats }

        [SerializeField] WeaponController weapons;
        [SerializeField] EnemyManager enemies;
        [SerializeField] PlayerHealth playerHealth;
        [SerializeField] BodyImpactDamage bodyImpact;
        [SerializeField] CombatWorldUI worldUI;
        [SerializeField] MovementDebugPanel movementPanel;
        [SerializeField] MovementDebugRenderer movementDebugRenderer;
        [SerializeField] bool panelVisible = true;
        [SerializeField, Range(0.5f, 3f)] float uiScale = 1f;
        [SerializeField] Tab tab = Tab.Weapon;

        const float PanelWidth = 320f;
        const float HudWidth = 440f, HudHeight = 96f;

        readonly TuningGui _g = new TuningGui();
        Vector2 _scroll;
        bool _weaponListOpen;
        string _message;
        float _messageTime;
        GUIStyle _box, _hud, _hudBig, _tabStyle;

        void Start()
        {
            if (CombatTuningStore.AutoLoad && CombatTuningStore.FileExists)
                Message(CombatTuningStore.Load(weapons, enemies, bodyImpact, playerHealth));
        }

        void Update()
        {
            if (PrototypeInput.Pressed(Key.F10)) panelVisible = !panelVisible;

            // Keep clicks on this panel from firing weapons / the grapple (the movement panel set the flag first).
            Vector2 mouse = PrototypeInput.MouseScreen;
            Vector2 gui = new Vector2(mouse.x, Screen.height - mouse.y) / uiScale;
            // (The HUD has no controls, so it doesn't block aiming at the top of the screen.)
            if (panelVisible && PanelRect.Contains(gui)) PrototypeInput.PointerBlocked = true;
        }

        float ScreenW => Screen.width / uiScale;
        float ScreenH => Screen.height / uiScale;

        Rect PanelRect
        {
            get
            {
                float right = movementPanel != null && movementPanel.PanelVisible
                    ? MovementDebugPanel.PanelWidth * movementPanel.UiScale / uiScale + 20f
                    : 10f;
                return new Rect(ScreenW - PanelWidth - right, 10, PanelWidth, ScreenH - 100);
            }
        }

        Rect HudRect => new Rect((ScreenW - HudWidth) * 0.5f, 10, HudWidth, HudHeight);

        void Message(string m)
        {
            _message = m;
            _messageTime = Time.time;
            Debug.Log("[Combat] " + m);
        }

        void EnsureStyles()
        {
            _g.EnsureStyles();
            if (_box != null) return;
            _box = new GUIStyle(GUI.skin.box) { alignment = TextAnchor.UpperLeft, padding = new RectOffset(8, 8, 6, 6) };
            _hud = new GUIStyle(GUI.skin.label) { fontSize = 12, richText = true, alignment = TextAnchor.UpperCenter };
            _hudBig = new GUIStyle(GUI.skin.label) { fontSize = 17, richText = true, alignment = TextAnchor.UpperCenter, fontStyle = FontStyle.Bold };
            _tabStyle = new GUIStyle(GUI.skin.button) { fontSize = 10, padding = new RectOffset(2, 2, 3, 3) };
        }

        void OnGUI()
        {
            if (weapons == null) return;
            EnsureStyles();
            GUI.matrix = Matrix4x4.Scale(new Vector3(uiScale, uiScale, 1f));

            DrawHud();

            GUILayout.BeginArea(new Rect(10, ScreenH - 82, 1200, 24));
            GUILayout.Label($"<b>{WeaponController.FireButtonName}</b> fire   <b>[ / ]</b> previous / next weapon   " +
                            "<b>F10</b> combat panel   <b>Tab</b> movement panel + status   <b>1-9</b> spawn points   <b>R</b> respawn   " +
                            "<b>T</b> teleport   <b>C</b> overview   <b>F9</b> grapple", _g.Label);
            GUILayout.EndArea();

            if (!panelVisible) return;
            GUILayout.BeginArea(PanelRect, _box);
            DrawTopBar();
            _scroll = GUILayout.BeginScrollView(_scroll);
            switch (tab)
            {
                case Tab.Player: PlayerTab(); break;
                case Tab.Enemies: EnemiesTab(); break;
                case Tab.Weapon: WeaponTab(); break;
                case Tab.Impact: ImpactTab(); break;
                case Tab.Debug: DebugTab(); break;
                case Tab.Stats: StatsTab(); break;
            }
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        // ---------------------------------------------------------------- HUD

        void DrawHud()
        {
            GUILayout.BeginArea(HudRect, _box);
            PrototypeWeapon w = weapons.Current;
            GUILayout.Label($"Weapon: {(w != null ? w.DisplayName : "-")}   {(w != null ? w.StatusText : "")}", _hudBig);
            if (w != null) HudBar(Mathf.Clamp01(w.PhaseProgress), PrototypeWeapon.PhaseColor(w.Phase), 10f);
            string hp = "-";
            if (playerHealth != null)
            {
                string col = playerHealth.Current / playerHealth.maxHealth > 0.3f ? "#9CFF9C" : "#FF7070";
                hp = playerHealth.IsDead ? "<color=#FF5050>DEAD</color>"
                    : $"<color={col}>{playerHealth.Current:0}/{playerHealth.maxHealth:0}</color>{(playerHealth.indestructible ? " <color=#9CC8FF>(indestructible)</color>" : "")}";
            }
            GUILayout.Label($"HP {hp}    DPS(3s) <b>{CombatTelemetry.RecentDps:0}</b>    Enemies {(enemies != null ? enemies.AliveCount : 0)}/{DummyEnemy.All.Count}", _hud);
            if (playerHealth != null)
            {
                float frac = playerHealth.maxHealth > 0f ? Mathf.Clamp01(playerHealth.Current / playerHealth.maxHealth) : 0f;
                Color c = playerHealth.indestructible ? new Color(0.55f, 0.8f, 1f)
                    : Color.Lerp(new Color(1f, 0.25f, 0.2f), new Color(0.35f, 1f, 0.4f), frac);
                HudBar(frac, c, 6f);
            }
            GUILayout.EndArea();
        }

        /// <summary>A full-width progress bar inside the HUD box.</summary>
        void HudBar(float fill, Color color, float height)
        {
            Rect r = GUILayoutUtility.GetRect(HudWidth - 20f, height, GUILayout.ExpandWidth(true));
            GUI.color = new Color(0f, 0f, 0f, 0.6f);
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = color;
            GUI.DrawTexture(new Rect(r.x, r.y, r.width * fill, r.height), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUILayout.Space(2);
        }

        void DrawTopBar()
        {
            GUILayout.BeginHorizontal();
            foreach (Tab t in System.Enum.GetValues(typeof(Tab)))
            {
                GUI.color = t == tab ? new Color(1f, 0.85f, 0.4f) : Color.white;
                if (GUILayout.Button(t.ToString().ToUpper(), _tabStyle)) tab = t;
            }
            GUI.color = Color.white;
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Reset Entire Sandbox")) ResetSandbox();
            if (GUILayout.Button("Save Tuning")) Message(CombatTuningStore.Save(weapons, enemies, bodyImpact, playerHealth));
            if (GUILayout.Button("Load")) Message(CombatTuningStore.Load(weapons, enemies, bodyImpact, playerHealth));
            GUILayout.EndHorizontal();
            if (!string.IsNullOrEmpty(_message) && Time.time - _messageTime < 4f) _g.Info(_message);
        }

        void ResetSandbox()
        {
            weapons.ResetAll();
            if (enemies != null) enemies.ResetAll(false);
            if (bodyImpact != null) bodyImpact.ResetState();
            if (playerHealth != null) playerHealth.Respawn();
            if (worldUI != null) worldUI.ClearNumbers();
            CombatDebug.ClearRecords();
            Message("Sandbox reset: player, enemies, projectiles, weapon state and cooldowns.");
        }

        // ---------------------------------------------------------------- tabs

        void PlayerTab()
        {
            if (playerHealth == null) { _g.Info("No PlayerHealth in the scene."); return; }
            PlayerHealth p = playerHealth;
            _g.Header("Player");
            _g.Info($"Current HP: <b>{p.Current:0.0}</b> / {p.maxHealth:0}   Deaths: {p.Deaths}");
            p.maxHealth = _g.Slider("Max Health", p.maxHealth, 1f, 500f, "0");
            p.indestructible = _g.Toggle("Indestructible Player", p.indestructible);
            p.invulnerabilityDuration = _g.Slider("Invulnerability After Damage", p.invulnerabilityDuration, 0f, 3f);
            p.respawnDelay = _g.Slider("Respawn Delay", p.respawnDelay, 0f, 3f);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Respawn Player")) p.Respawn();
            if (GUILayout.Button("Restore Health")) p.RestoreHealth();
            GUILayout.EndHorizontal();
            _g.Header("Movement");
            _g.Info("Movement tuning lives in the movement panel (Tab). F8 air jumps, F9 grapple.");
            if (movementPanel != null && GUILayout.Button(movementPanel.PanelVisible ? "Hide movement panel" : "Show movement panel"))
                movementPanel.PanelVisible = !movementPanel.PanelVisible;
        }

        void EnemiesTab()
        {
            if (enemies == null) { _g.Info("No EnemyManager in the scene."); return; }
            EnemyManager m = enemies;
            _g.Header("All Enemies");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Respawn All")) m.RespawnAll();
            if (GUILayout.Button("Respawn Dead")) m.RespawnDead();
            if (GUILayout.Button("Kill All")) m.KillAll();
            GUILayout.EndHorizontal();
            if (GUILayout.Button("Reset All (and these settings)")) m.ResetAll(true);
            m.autoRespawn = _g.Toggle("Auto Respawn", m.autoRespawn);
            m.respawnDelay = _g.Slider("Respawn Delay", m.respawnDelay, 0f, 10f, "0.0");
            m.freezeEnemies = _g.Toggle("Freeze Enemies (AI movement)", m.freezeEnemies);
            m.healthMultiplier = _g.Slider("Global Health Multiplier", m.healthMultiplier, 0.1f, 5f);
            m.contactDamageMultiplier = _g.Slider("Global Contact Damage Multiplier", m.contactDamageMultiplier, 0f, 5f);
            m.knockbackMultiplier = _g.Slider("Global Knockback Multiplier", m.knockbackMultiplier, 0f, 3f);
            m.movementOverride = _g.Enum("Movement", m.movementOverride);

            _g.Header("Spawn Groups");
            foreach (EnemySpawnGroup grp in m.Groups)
            {
                if (grp == null) continue;
                GUILayout.Space(4);
                GUILayout.Label($"<b>{grp.DisplayName}</b>  ({grp.AliveCount}/{grp.Members.Count} alive)", _g.Label);
                if (!string.IsNullOrEmpty(grp.Description)) _g.Info(grp.Description);
                bool en = _g.Toggle("Enabled", grp.GroupEnabled);
                if (en != grp.GroupEnabled) grp.GroupEnabled = en;
                GroupAutoRespawn ar = _g.Enum("  Auto Respawn", grp.autoRespawn);
                if (ar != grp.autoRespawn)
                {
                    grp.autoRespawn = ar;
                    m.RefreshRespawnTimers();
                }
                grp.movementOverride = _g.Enum("  Movement", grp.movementOverride);
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Respawn")) grp.RespawnAll();
                if (GUILayout.Button("Kill")) grp.KillAll();
                GUILayout.EndHorizontal();
            }
        }

        void WeaponTab()
        {
            PrototypeWeapon w = weapons.Current;
            _g.Header("Weapon Selection");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("<", GUILayout.Width(30))) weapons.Select(weapons.SelectedIndex - 1);
            if (GUILayout.Button($"{weapons.SelectedIndex + 1}. {(w != null ? w.DisplayName : "-")}  {(_weaponListOpen ? "▲" : "▼")}"))
                _weaponListOpen = !_weaponListOpen;
            if (GUILayout.Button(">", GUILayout.Width(30))) weapons.Select(weapons.SelectedIndex + 1);
            GUILayout.EndHorizontal();
            if (_weaponListOpen)
            {
                for (int i = 0; i < weapons.Weapons.Count; i++)
                {
                    GUI.color = i == weapons.SelectedIndex ? new Color(1f, 0.85f, 0.4f) : Color.white;
                    if (GUILayout.Button($"{i + 1}. {weapons.Weapons[i].DisplayName}"))
                    {
                        weapons.Select(i);
                        _weaponListOpen = false;
                    }
                }
                GUI.color = Color.white;
            }
            if (w == null) return;
            _g.Info(w.Summary);
            _g.Info($"Status: {w.StatusText}");

            _g.Header("General");
            WeaponController.Binding = _g.Enum("Fire Button", WeaponController.Binding);
            if (WeaponController.Binding == FireBinding.OtherMouseButton)
                _g.Info($"Fire = {WeaponController.FireButtonName} (the grapple's cancel button; Space releases the grapple).");
            if (GUILayout.Button("Reset this weapon to scene defaults")) w.ResetSettings();

            w.DrawTuning(_g);
        }

        void ImpactTab()
        {
            if (bodyImpact == null) { _g.Info("No BodyImpactDamage on the player."); return; }
            BodyImpactDamage b = bodyImpact;
            _g.Info($"Speed now: <b>{b.Motor.Velocity.magnitude:0.0}</b>   Approach now: <b>{b.CurrentApproachSpeed:0.0}</b>");
            if (b.HasImpact)
            {
                DamageEvent e = b.LastImpact;
                _g.Info($"Last impact: speed <b>{e.impactSpeed:0.0}</b> -> <b>{e.finalDamage:0.0}</b> dmg " +
                        $"(base {e.baseDamage:0} + speed {e.speedDamage:0.0}), self {e.playerDamageTaken:0.0}");
            }
            _g.Header("Body Damage");
            b.bodyDamageEnabled = _g.Toggle("Enable Body Damage", b.bodyDamageEnabled);
            b.baseImpactDamage = _g.Slider("Base Damage", b.baseImpactDamage, 0f, 100f, "0.0");
            b.minimumImpactSpeed = _g.Slider("Minimum Impact Speed", b.minimumImpactSpeed, 0f, 25f, "0.0");
            b.impactSpeedDamageScale = _g.Slider("Speed Damage Scaling", b.impactSpeedDamageScale, 0f, 20f, "0.0");
            b.maximumImpactDamage = _g.Slider("Maximum Damage", b.maximumImpactDamage, 0f, 500f, "0");
            b.impactCooldownPerEnemy = _g.Slider("Impact Cooldown (per enemy)", b.impactCooldownPerEnemy, 0f, 2f);
            b.approachThreshold = _g.Slider("Approach Threshold (counts as impact)", b.approachThreshold, 0f, 10f, "0.0");
            float example = 15f;
            float dmg = Mathf.Min(b.baseImpactDamage + Mathf.Max(0f, example - b.minimumImpactSpeed) * b.impactSpeedDamageScale, b.maximumImpactDamage);
            _g.Info($"  e.g. an impact at speed {example:0} deals {dmg:0.0}");
            _g.Header("Enemy Contact / Self-Damage");
            if (enemies != null) enemies.contactDamageMultiplier = _g.Slider("Enemy Contact Damage Multiplier", enemies.contactDamageMultiplier, 0f, 5f);
            b.enemyContactDamageOnImpact = _g.Toggle("Enemy contact damage still applies when ramming", b.enemyContactDamageOnImpact);
            b.selfDamageMode = _g.Enum("Self-Damage", b.selfDamageMode);
            if (b.selfDamageMode == ImpactSelfDamageMode.ContactPlusImpactSelfDamage)
            {
                b.additionalImpactSelfDamage = _g.Slider("  Additional Impact Self Damage", b.additionalImpactSelfDamage, 0f, 50f, "0.0");
                b.impactSelfDamageScale = _g.Slider("  Impact Self Damage Scale (x dealt)", b.impactSelfDamageScale, 0f, 1f);
            }
            _g.Header("Impact Knockback");
            b.impactKnockback = _g.Slider("Impact Knockback", b.impactKnockback, 0f, 40f, "0.0");
            b.knockbackSpeedScale = _g.Slider("Knockback Per Speed", b.knockbackSpeedScale, 0f, 3f);

            if (enemies == null) return;
            EnemyManager m = enemies;
            _g.Header("Wall Slam (enemies launched into terrain)");
            m.wallSlamEnabled = _g.Toggle("Enable Wall Slam", m.wallSlamEnabled);
            if (!m.wallSlamEnabled) return;
            m.wallSlamMinSpeed = _g.Slider("Minimum Speed Into Surface", m.wallSlamMinSpeed, 0f, 20f, "0.0");
            m.wallSlamBaseDamage = _g.Slider("Base Damage", m.wallSlamBaseDamage, 0f, 50f, "0.0");
            m.wallSlamDamagePerSpeed = _g.Slider("Damage Per Speed", m.wallSlamDamagePerSpeed, 0f, 10f, "0.0");
            m.wallSlamMaxDamage = _g.Slider("Maximum Damage", m.wallSlamMaxDamage, 0f, 200f, "0");
            m.wallSlamRestitution = _g.Slider("Bounce (restitution)", m.wallSlamRestitution, 0f, 1f);
            m.wallSlamCooldown = _g.Slider("Cooldown Per Enemy", m.wallSlamCooldown, 0f, 1f);
            float slam = Mathf.Min(m.wallSlamBaseDamage + Mathf.Max(0f, 15f - m.wallSlamMinSpeed) * m.wallSlamDamagePerSpeed, m.wallSlamMaxDamage);
            _g.Info($"  e.g. a slam at speed 15 deals {slam:0.0}. Credited to the attack that launched the enemy (STATS).");
        }

        void DebugTab()
        {
            _g.Header("Overlays");
            CombatDebug.DamageNumbers = _g.Toggle("Damage Numbers", CombatDebug.DamageNumbers);
            if (CombatDebug.DamageNumbers) CombatDebug.MergeRapidHits = _g.Toggle("  Merge rapid hits (beam / flame ticks)", CombatDebug.MergeRapidHits);
            CombatDebug.HealthLabels = _g.Toggle("Enemy Health Labels", CombatDebug.HealthLabels);
            CombatDebug.DetailedHitInfo = _g.Toggle("Detailed Hit Information", CombatDebug.DetailedHitInfo);
            CombatDebug.ZoneLabels = _g.Toggle("Area Labels", CombatDebug.ZoneLabels);
            CombatDebug.PlayerBars = _g.Toggle("Player Health / Charge Bars", CombatDebug.PlayerBars);
            _g.Header("Debug Lines");
            CombatDebug.ProjectilePaths = _g.Toggle("Projectile Paths", CombatDebug.ProjectilePaths);
            CombatDebug.WeaponRange = _g.Toggle("Weapon Range", CombatDebug.WeaponRange);
            CombatDebug.AttackGeometry = _g.Toggle("Attack Geometry", CombatDebug.AttackGeometry);
            CombatDebug.AimAssist = _g.Toggle("Aim Assist Region (Burst)", CombatDebug.AimAssist);
            CombatDebug.KnockbackVectors = _g.Toggle("Knockback Vectors", CombatDebug.KnockbackVectors);
            CombatDebug.ImpactVelocity = _g.Toggle("Impact Velocity", CombatDebug.ImpactVelocity);
            if (movementDebugRenderer != null)
                movementDebugRenderer.drawEnabled = _g.Toggle("Movement debug lines (F5)", movementDebugRenderer.drawEnabled);
            _g.Header("Clear");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Clear Projectiles") && weapons.Projectiles != null) weapons.Projectiles.Clear();
            if (GUILayout.Button("Clear Weapon Effects")) weapons.CancelAllEffects();
            GUILayout.EndHorizontal();
            _g.Header("Tuning File");
            CombatTuningStore.AutoLoad = _g.Toggle("Load saved tuning when Play starts", CombatTuningStore.AutoLoad);
            _g.Info(CombatTuningStore.FileExists ? "Saved: Weapons/Settings/CombatTuning.json" : "Nothing saved yet.");
            uiScale = _g.Slider("UI Scale", uiScale, 0.5f, 2f);
        }

        void StatsTab()
        {
            _g.Header("Session");
            _g.Info($"Damage Dealt: <b>{CombatTelemetry.DamageDealt:0}</b>   Damage Taken: <b>{CombatTelemetry.DamageTaken:0}</b>\n" +
                    $"Kills: <b>{CombatTelemetry.Kills}</b>   Self Damage: {CombatTelemetry.SelfDamage:0}\n" +
                    $"Body Impacts: {CombatTelemetry.BodyImpactHits} ({CombatTelemetry.BodyImpactDamage:0} dmg)\n" +
                    $"DPS (last {CombatTelemetry.DpsWindow:0}s): {CombatTelemetry.RecentDps:0.0}");
            _g.Header("By Weapon");
            foreach (var kv in CombatTelemetry.ByWeapon)
            {
                CombatTelemetry.WeaponStats s = kv.Value;
                string acc = s.shots > 0 ? $"   Accuracy {s.Accuracy} ({s.shotsThatHit}/{s.shots} shots)" : "";
                if (s.slamDamage > 0f) acc += $"   +{s.slamDamage:0} via wall slams";
                _g.Info($"<b>{kv.Key}</b>\n  Uses {s.activations}   Hits {s.hits}   Damage {s.damage:0}   Kills {s.kills}{acc}");
            }
            if (CombatTelemetry.ByWeapon.Count == 0) _g.Info("Nothing yet.");
            if (GUILayout.Button("Reset Stats")) CombatTelemetry.Reset();
        }
    }
}
