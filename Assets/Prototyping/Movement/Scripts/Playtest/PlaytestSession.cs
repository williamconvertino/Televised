using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Televised.Prototyping.Shared;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Televised.Prototyping.Movement.Playtest
{
    /// <summary>
    /// Runs a playtest session: tester setup, then for each planned run (configuration × course) an intro
    /// screen, a timed run (checkpoints, hazards, goal), and a rating screen (1-10 + comment).
    /// Results are appended to Results/ratings.csv, saved per session as JSON, and summarised in summary.md.
    /// </summary>
    [DefaultExecutionOrder(-100)] // before the motor, so PointerBlocked is set before the grapple reads input
    public class PlaytestSession : MonoBehaviour
    {
        [SerializeField] PlayerMotor2D motor;
        [SerializeField] MovementDebugRenderer debugRenderer;
        [SerializeField] List<PlaytestCourse> courses = new List<PlaytestCourse>();
        [SerializeField, Range(0.5f, 3f)] float uiScale = 1f;

        enum Phase { Setup, Intro, Playing, Rating, Finished, Results }

        struct RunPlan
        {
            public PlaytestConfig config;
            public PlaytestCourse course;
            public string Label => $"{config.id} @ {course.track}/{course.difficulty}";
        }

        Phase _phase = Phase.Setup;

        // Setup options.
        string _tester = "";
        bool _allConfigs;
        int _configCount = 6;
        bool _shuffle;           // off: C01, C02, ... in catalog order
        bool _preferLeastRated;  // off: a subset is simply the first N configs
        bool _incCore = true, _incDoubleJump = true, _incGrapple = true;
        readonly bool[] _difficulties = { true, true, false };
        bool _featureCourses = true;

        // Session.
        SessionRecord _session;
        List<RunPlan> _queue = new List<RunPlan>();
        int _index;
        RunPlan _current;
        string _sessionComment = "";

        // Current run.
        float _time;
        int _deaths, _manualRespawns, _restarts, _skips, _attempts, _checkpoint, _jumps, _airJumps, _latches;
        readonly List<float> _splits = new List<float>();
        bool _finished;
        bool _expectRespawn;
        string _flash;
        float _flashUntil;
        Rect _hudRect;

        // Rating.
        int _rating;
        string _comment = "";
        bool _stopConfigAfterRating;

        // Results view.
        List<ConfigAggregate> _aggregates, _perCourse;
        List<AxisAggregate> _axes;
        int _ratingCount, _testerCount;
        Vector2 _scroll;
        Phase _resultsReturn = Phase.Setup;

        PlaytestSign[] _signs;
        GUIStyle _box, _label, _big, _header, _hint, _signStyle, _flashStyle;

        PlaytestCourse Course => _current.course;
        bool _misconfigured;

        // ---------------------------------------------------------------- lifecycle

        void Start()
        {
            if (courses.All(c => c == null))
            {
                _misconfigured = true;
                if (motor != null) motor.enabled = false;
                Debug.LogError("[Playtest] This scene has no courses. Rebuild it: Prototyping > Movement > Build Playtest Level.");
                return;
            }

            _signs = FindObjectsByType<PlaytestSign>(FindObjectsSortMode.None);

            if (debugRenderer != null)
            {
                debugRenderer.drawEnabled = true;
                debugRenderer.hintsOnly = true;
            }

            motor.Respawned += OnRespawned;
            motor.Detached += OnDetached;
            motor.AirJumped += OnAirJumped;
            if (motor.Grapple != null) motor.Grapple.Latched += OnLatched;

            _current = new RunPlan { config = PlaytestConfigCatalog.All[0], course = FindCourse(CourseTrack.General, PlaytestDifficulty.Easy) };
            ApplyConfig(_current.config);
            ResetRun();
            motor.enabled = false;
        }

        void OnDestroy()
        {
            PrototypeInput.PointerBlocked = false;
            PrototypeInput.KeyboardBlocked = false;
            if (motor == null) return;
            motor.Respawned -= OnRespawned;
            motor.Detached -= OnDetached;
            motor.AirJumped -= OnAirJumped;
            if (motor.Grapple != null) motor.Grapple.Latched -= OnLatched;
        }

        void OnRespawned()
        {
            if (_expectRespawn) { _expectRespawn = false; return; }
            if (_phase != Phase.Playing) return;
            _deaths++; // fell out of the world (motor kill plane)
            Flash("Fell!");
        }

        void OnDetached(Surface2D s) { if (_phase == Phase.Playing && s != null) _jumps++; }
        void OnAirJumped(Vector2 _) { if (_phase == Phase.Playing) _airJumps++; }
        void OnLatched(GrappleController _) { if (_phase == Phase.Playing) _latches++; }

        // ---------------------------------------------------------------- courses & planning

        PlaytestCourse FindCourse(CourseTrack track, PlaytestDifficulty difficulty)
        {
            PlaytestCourse c = courses.FirstOrDefault(x => x != null && x.track == track && x.difficulty == difficulty);
            if (c == null && track != CourseTrack.General) c = FindCourse(CourseTrack.General, difficulty);
            return c != null ? c : courses.FirstOrDefault(x => x != null);
        }

        CourseTrack TrackFor(PlaytestConfig config)
        {
            if (!_featureCourses) return CourseTrack.General;
            return config.category switch
            {
                PlaytestCategory.DoubleJump => CourseTrack.DoubleJump,
                PlaytestCategory.Grapple => CourseTrack.Grapple,
                _ => CourseTrack.General,
            };
        }

        List<PlaytestDifficulty> SelectedDifficulties() =>
            Enumerable.Range(0, 3).Where(i => _difficulties[i]).Select(i => (PlaytestDifficulty)i).ToList();

        void StartSession()
        {
            string tester = string.IsNullOrWhiteSpace(_tester) ? "anon" : _tester.Trim();
            var rng = new System.Random(Environment.TickCount);
            List<RatingRecord> existing = PlaytestResultsStore.LoadAll();
            List<PlaytestDifficulty> diffs = SelectedDifficulties();

            List<PlaytestConfig> configs = PlaytestConfigCatalog.SelectForSession(_allConfigs ? PoolSize() : _configCount, _shuffle, _preferLeastRated,
                _incCore, _incDoubleJump, _incGrapple, PlaytestResultsStore.RatingCounts(existing), rng);

            // One config at a time: each config plays every selected difficulty, easiest first, rated after each.
            _queue = new List<RunPlan>();
            foreach (PlaytestConfig c in configs)
                foreach (PlaytestDifficulty d in diffs)
                    _queue.Add(new RunPlan { config = c, course = FindCourse(TrackFor(c), d) });

            _session = new SessionRecord
            {
                sessionId = DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture),
                tester = tester,
                startedAt = DateTime.Now.ToString("s", CultureInfo.InvariantCulture),
                orderMode = (_shuffle ? "Shuffled" : "Catalog order") + (_preferLeastRated ? ", least-rated subset" : ""),
                difficultyPlan = "Per config: " + string.Join(" > ", diffs) + (_featureCourses ? " (feature courses)" : ""),
                plannedRuns = _queue.Select(q => q.Label).ToList(),
            };
            _sessionComment = "";

            ShowIntro(0);
        }

        void ShowIntro(int index)
        {
            _index = index;
            _current = _queue[_index];
            ApplyConfig(_current.config);
            ResetRun();
            _attempts = 0;
            motor.enabled = false;
            _phase = Phase.Intro;
        }

        void BeginRun()
        {
            _attempts++;
            ResetRun();
            motor.enabled = true;
            _phase = Phase.Playing;
        }

        void EndRun(bool finished)
        {
            _finished = finished;
            motor.enabled = false;
            PrototypeInput.PointerBlocked = false;
            _rating = 0;
            _comment = "";
            _stopConfigAfterRating = false;
            _phase = Phase.Rating;
        }

        RatingRecord BuildRecord()
        {
            MovementTuning t = motor.Tuning;
            return new RatingRecord
            {
                sessionId = _session.sessionId,
                tester = _session.tester,
                timestamp = DateTime.Now.ToString("s", CultureInfo.InvariantCulture),
                order = _index + 1,
                configId = _current.config.id,
                configTitle = _current.config.title,
                category = _current.config.category.ToString(),
                difficulty = Course.difficulty.ToString(),
                course = Course.track.ToString(),
                jumpMode = t.jumpDirectionMode.ToString(),
                attachMode = t.attachmentMode.ToString(),
                selectionMode = t.candidateSelectionMode.ToString(),
                feature = PlaytestConfig.FeatureOf(t),
                feel = _current.config.feelTag,
                finished = _finished,
                timeSeconds = _time,
                deaths = _deaths,
                manualRespawns = _manualRespawns,
                restarts = _restarts,
                skips = _skips,
                attempts = _attempts,
                checkpoints = _checkpoint,
                jumps = _jumps,
                airJumps = _airJumps,
                grappleLatches = _latches,
                splits = string.Join(";", _splits.Select(s => s.ToString("0.0", CultureInfo.InvariantCulture))),
                rating = _rating,
                comment = _comment.Trim(),
            };
        }

        void SubmitRating()
        {
            RatingRecord r = BuildRecord();
            _session.ratings.Add(r);
            try
            {
                PlaytestResultsStore.AppendRating(r);
                PlaytestResultsStore.SaveSession(_session);
                PlaytestResultsStore.WriteSummary();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
            AfterLevel();
        }

        /// <summary>Played the level but chose not to rate it: keep the metrics in the session JSON only.</summary>
        void SkipRating()
        {
            _rating = 0;
            _session.unratedRuns.Add(BuildRecord());
            SaveSessionSafe();
            AfterLevel();
        }

        /// <summary>Skip the current level without finishing it; its rating is skipped automatically.</summary>
        void SkipLevel()
        {
            motor.enabled = false;
            PrototypeInput.PointerBlocked = false;
            _session.skippedLevels.Add(_current.Label + (_phase == Phase.Playing ? $" (after {_time:0}s)" : " (not started)"));
            SaveSessionSafe();
            NextOrFinish();
        }

        /// <summary>Skip every remaining level of the current config and move on to the next config.</summary>
        void SkipRestOfConfig()
        {
            PlaytestConfig config = _current.config;
            int next = _index + 1;
            while (next < _queue.Count && _queue[next].config == config)
            {
                _session.skippedLevels.Add(_queue[next].Label + " (config skipped)");
                next++;
            }
            SaveSessionSafe();
            if (next < _queue.Count) ShowIntro(next);
            else _phase = Phase.Finished;
        }

        void AfterLevel()
        {
            bool stop = _stopConfigAfterRating;
            _stopConfigAfterRating = false;
            if (stop) SkipRestOfConfig();
            else NextOrFinish();
        }

        void NextOrFinish()
        {
            if (_index + 1 < _queue.Count) ShowIntro(_index + 1);
            else _phase = Phase.Finished;
        }

        void SaveSessionSafe()
        {
            try { PlaytestResultsStore.SaveSession(_session); } catch (Exception e) { Debug.LogException(e); }
        }

        bool HasMoreLevelsOfConfig => _index + 1 < _queue.Count && _queue[_index + 1].config == _current.config;

        /// <summary>(level number within this config, level count for this config, config number, config count).</summary>
        (int level, int levels, int configNo, int configs) Progress()
        {
            var order = _queue.Select(q => q.config).Distinct().ToList();
            int levels = _queue.Count(q => q.config == _current.config);
            int first = _queue.FindIndex(q => q.config == _current.config);
            return (_index - first + 1, levels, order.IndexOf(_current.config) + 1, order.Count);
        }

        void EndSession()
        {
            if (_session != null)
            {
                _session.sessionComment = _sessionComment.Trim();
                try { PlaytestResultsStore.SaveSession(_session); } catch (Exception e) { Debug.LogException(e); }
            }
            _session = null;
            _phase = Phase.Setup;
        }

        void ShowResults(Phase returnTo)
        {
            List<RatingRecord> all = PlaytestResultsStore.LoadAll();
            _aggregates = PlaytestResultsStore.AggregateByConfig(all);
            _perCourse = PlaytestResultsStore.AggregateByConfigAndCourse(all);
            _axes = PlaytestResultsStore.AggregateByAxes(all);
            _ratingCount = all.Count;
            _testerCount = all.Select(r => r.tester).Distinct().Count();
            _resultsReturn = returnTo;
            _scroll = Vector2.zero;
            _phase = Phase.Results;
        }

        // ---------------------------------------------------------------- run

        void ApplyConfig(PlaytestConfig config)
        {
            MovementTuning t = config.Build();
            motor.LoadRuntimeTuning(t);
            Destroy(t);
        }

        void ResetRun()
        {
            _time = 0f;
            _deaths = _manualRespawns = _restarts = _skips = _jumps = _airJumps = _latches = 0;
            _splits.Clear();
            _finished = false;
            ResetLevelState();
        }

        void ResetLevelState()
        {
            _checkpoint = 0;
            foreach (PlaytestCourse c in courses) if (c != null) c.ResetCheckpoints();
            motor.SpawnPosition = Course.startPoint.position;
            ExpectedRespawn();
        }

        void ExpectedRespawn()
        {
            _expectRespawn = true;
            motor.Respawn();
            _expectRespawn = false;
        }

        void ReachCheckpoint(int i)
        {
            for (int j = 0; j <= i; j++) Course.checkpoints[j].SetReached(true);
            _checkpoint = i + 1;
            while (_splits.Count < _checkpoint) _splits.Add(_time);
            motor.SpawnPosition = Course.checkpoints[i].Position;
        }

        /// <summary>Stuck? Jump to the next checkpoint, or end the run if only the goal is left.</summary>
        void SkipToNextCheckpoint()
        {
            _skips++;
            if (_checkpoint < Course.checkpoints.Count)
            {
                ReachCheckpoint(_checkpoint);
                ExpectedRespawn();
                Flash($"Skipped to checkpoint {_checkpoint}/{Course.checkpoints.Count}");
            }
            else
            {
                _splits.Add(_time);
                EndRun(false); // skipped the last section: counts as not finished
            }
        }

        void Update()
        {
            // Forms and menus take typing (tester name, comments): no gameplay/camera hotkeys while they're open.
            PrototypeInput.KeyboardBlocked = _misconfigured || _phase == Phase.Setup || _phase == Phase.Rating ||
                                             _phase == Phase.Finished || _phase == Phase.Results;
            if (_misconfigured) return;
            if (_phase == Phase.Intro && (PrototypeInput.Pressed(Key.Enter) || PrototypeInput.Pressed(Key.NumpadEnter)))
            {
                BeginRun();
                return;
            }
            if (_phase != Phase.Playing)
            {
                PrototypeInput.PointerBlocked = false;
                return;
            }

            // Clicking the HUD (skip button) must not fire the grapple.
            Vector2 mouse = PrototypeInput.MouseScreen;
            PrototypeInput.PointerBlocked = _hudRect.Contains(new Vector2(mouse.x, Screen.height - mouse.y) / uiScale);

            _time += Time.deltaTime;
            Vector2 p = motor.Position;
            float r = motor.Radius;

            foreach (PlaytestHazard h in Course.Hazards)
            {
                if (h == null || !h.Touches(p, r)) continue;
                _deaths++;
                Flash("Ouch!");
                ExpectedRespawn();
                return;
            }

            for (int i = _checkpoint; i < Course.checkpoints.Count; i++)
            {
                if (!Course.checkpoints[i].Contains(p)) continue;
                ReachCheckpoint(i);
                Flash($"Checkpoint {_checkpoint}/{Course.checkpoints.Count}");
            }

            if (Course.goal != null && Course.goal.Contains(p))
            {
                _splits.Add(_time);
                EndRun(true);
                return;
            }

            if (PrototypeInput.Pressed(Key.R)) { _manualRespawns++; ExpectedRespawn(); }
            if (PrototypeInput.Pressed(Key.Backspace)) { _restarts++; ResetLevelState(); }
            if (PrototypeInput.Pressed(Key.N)) { SkipToNextCheckpoint(); return; }
            if (PrototypeInput.Pressed(Key.Escape)) { EndRun(false); return; }
            if (PrototypeInput.Pressed(Key.F5) && debugRenderer != null) debugRenderer.hintsOnly = !debugRenderer.hintsOnly;
        }

        void Flash(string text)
        {
            _flash = text;
            _flashUntil = Time.time + 1.2f;
        }

        int PoolSize() => PlaytestConfigCatalog.All.Count(c =>
            (_incCore && c.category == PlaytestCategory.Core) ||
            (_incDoubleJump && c.category == PlaytestCategory.DoubleJump) ||
            (_incGrapple && c.category == PlaytestCategory.Grapple));

        string CourseLabel(PlaytestCourse c) =>
            c.track == CourseTrack.General ? $"{c.difficulty} course" : $"{c.difficulty} {Pretty(c.track)} course";

        static string Pretty(CourseTrack t) => t == CourseTrack.DoubleJump ? "double-jump" : t.ToString().ToLowerInvariant();

        // ---------------------------------------------------------------- GUI

        void EnsureStyles()
        {
            if (_box != null) return;
            _box = new GUIStyle(GUI.skin.box) { padding = new RectOffset(14, 14, 12, 12) };
            _label = new GUIStyle(GUI.skin.label) { fontSize = 14, richText = true, wordWrap = true };
            _hint = new GUIStyle(_label) { fontSize = 13 };
            _big = new GUIStyle(_label) { fontSize = 24, fontStyle = FontStyle.Bold };
            _header = new GUIStyle(_label) { fontSize = 16, fontStyle = FontStyle.Bold };
            _signStyle = new GUIStyle(GUI.skin.label) { fontSize = 14, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            _signStyle.normal.textColor = new Color(1f, 1f, 1f, 0.6f);
            _flashStyle = new GUIStyle(GUI.skin.label) { fontSize = 28, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
        }

        void OnGUI()
        {
            EnsureStyles();
            GUI.matrix = Matrix4x4.Scale(new Vector3(uiScale, uiScale, 1f));
            float w = Screen.width / uiScale, h = Screen.height / uiScale;
            if (_misconfigured)
            {
                GUI.Label(Centered(w, h, 600, 80),
                    "<b>This playtest scene is out of date.</b>\nRebuild it: Prototyping > Movement > Build Playtest Level.", _big);
                return;
            }

            DrawSigns(h);

            switch (_phase)
            {
                case Phase.Setup: DrawSetup(w, h); break;
                case Phase.Intro: DrawIntro(w, h); break;
                case Phase.Playing: DrawHud(w, h); break;
                case Phase.Rating: DrawRating(w, h); break;
                case Phase.Finished: DrawFinished(w, h); break;
                case Phase.Results: DrawResults(w, h); break;
            }
        }

        Rect Centered(float w, float h, float width, float height) =>
            new Rect((w - width) * 0.5f, (h - height) * 0.5f, width, Mathf.Min(height, h - 20f));

        void DrawSetup(float w, float h)
        {
            GUILayout.BeginArea(Centered(w, h, 600, 720), _box);
            GUILayout.Label("Movement Playtest", _big);
            GUILayout.Space(6);

            GUILayout.Label("Tester name", _header);
            _tester = GUILayout.TextField(_tester, 40);
            GUILayout.Space(6);

            GUILayout.Label("Configurations", _header);
            _incCore = GUILayout.Toggle(_incCore, " Core movement configs (C01–C20)");
            _incDoubleJump = GUILayout.Toggle(_incDoubleJump, " Double jump configs (C21–C22)");
            _incGrapple = GUILayout.Toggle(_incGrapple, " Grapple configs (C23–C28)");
            int pool = PoolSize();
            _allConfigs = GUILayout.Toggle(_allConfigs, $" All selected ({pool})");
            if (!_allConfigs)
            {
                _configCount = Mathf.Clamp(_configCount, 1, Mathf.Max(1, pool));
                GUILayout.Label($"Number of configs: <b>{_configCount}</b>", _label);
                _configCount = Mathf.RoundToInt(GUILayout.HorizontalSlider(_configCount, 1, Mathf.Max(1, pool)));
            }
            _shuffle = GUILayout.Toggle(_shuffle, " Shuffle config order (off = C01, C02, ...)");
            if (!_allConfigs)
                _preferLeastRated = GUILayout.Toggle(_preferLeastRated,
                    " Pick the least-rated configs (off = the first N configs)");
            GUILayout.Space(6);

            GUILayout.Label("Difficulty", _header);
            GUILayout.BeginHorizontal();
            for (int i = 0; i < 3; i++) _difficulties[i] = GUILayout.Toggle(_difficulties[i], " " + (PlaytestDifficulty)i, GUILayout.Width(110));
            GUILayout.EndHorizontal();
            if (!_difficulties.Any(d => d)) _difficulties[0] = true;
            GUILayout.Label("<i>Each config is played on every ticked difficulty, easiest first, with a rating after each level. " +
                            "Testers can stop a config early and still keep the ratings they gave.</i>", _hint);
            _featureCourses = GUILayout.Toggle(_featureCourses, " Double-jump / grapple configs play courses that REQUIRE the feature");

            int runs = (_allConfigs ? pool : _configCount) * SelectedDifficulties().Count;
            GUILayout.Label($"Up to <b>{runs}</b> levels  (~{runs * 3}–{runs * 6} min if every level is played)", _label);

            GUILayout.FlexibleSpace();
            GUI.enabled = pool > 0;
            if (GUILayout.Button("Start session", GUILayout.Height(40))) StartSession();
            GUI.enabled = true;
            if (GUILayout.Button("View results so far")) ShowResults(Phase.Setup);
            GUILayout.EndArea();
        }

        void DrawIntro(float w, float h)
        {
            GUILayout.BeginArea(Centered(w, h, 620, 420), _box);
            var pr = Progress();
            GUILayout.Label($"Configuration {_current.config.id}   <size=16>(config {pr.configNo} of {pr.configs})</size>", _big);
            GUILayout.Label($"<b>{CourseLabel(Course)}</b>   level {pr.level} of {pr.levels} for this config", _header);
            if (Course.track != CourseTrack.General)
                GUILayout.Label($"<color=#fd8>This course can't be finished without the {Pretty(Course.track)}.</color>", _label);
            GUILayout.Label("Play through the course with this configuration, then rate how it felt. " +
                            "Reach the checkpoints and the gold goal; red shapes are hazards. " +
                            "You can skip a level, or stop this config, at any time.", _label);
            GUILayout.Space(8);
            GUILayout.Label("How it controls", _header);
            GUILayout.Label("<b>WASD</b> crawl along surfaces.  " + _current.config.playerHint, _label);
            GUILayout.Space(8);
            GUILayout.Label("<b>R</b> last checkpoint   <b>Backspace</b> restart   <b>N</b> skip to next checkpoint (if stuck)   <b>Esc</b> stop and rate", _hint);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Start  (Enter)", GUILayout.Height(40))) BeginRun();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Skip this level (no rating)")) SkipLevel();
            if (HasMoreLevelsOfConfig && GUILayout.Button("Skip the rest of this config")) SkipRestOfConfig();
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        void DrawHud(float w, float h)
        {
            _hudRect = new Rect(10, 10, 440, 215);
            GUILayout.BeginArea(_hudRect, _box);
            GUILayout.Label($"<b>Config {_current.config.id}</b>  ({_index + 1}/{_queue.Count}) · {CourseLabel(Course)}", _label);
            GUILayout.Label($"Time {PlaytestResultsStore.FormatTime(_time)}    Deaths {_deaths}    Checkpoint {_checkpoint}/{Course.checkpoints.Count}", _label);
            GUILayout.Label(_current.config.playerHint, _hint);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(_checkpoint < Course.checkpoints.Count ? "Next checkpoint  (N)" : "Skip to the end  (N)"))
                SkipToNextCheckpoint();
            if (GUILayout.Button("Skip level (no rating)")) SkipLevel();
            GUILayout.EndHorizontal();
            GUILayout.EndArea();

            GUI.Label(new Rect(10, h - 30, w - 20, 24),
                "R: last checkpoint    Backspace: restart    N: skip to next checkpoint    Esc: stop and rate" +
                (debugRenderer != null && !debugRenderer.hintsOnly ? "    [F5 debug view]" : ""), _hint);

            if (Time.time < _flashUntil && !string.IsNullOrEmpty(_flash))
                GUI.Label(new Rect(0, h * 0.25f, w, 40), _flash, _flashStyle);
        }

        void DrawRating(float w, float h)
        {
            GUILayout.BeginArea(Centered(w, h, 660, 490), _box);
            GUILayout.Label($"Rate configuration {_current.config.id} · {Course.difficulty}", _big);
            GUILayout.Label($"{CourseLabel(Course)}   " +
                            (_finished ? "<color=#7f7>Reached the goal</color>" : "<color=#fb7>Didn't reach the goal</color>") +
                            $"   Time {PlaytestResultsStore.FormatTime(_time)}   Deaths {_deaths}   Checkpoints {_checkpoint}/{Course.checkpoints.Count}" +
                            (_skips > 0 ? $"   Skips {_skips}" : "") + (_attempts > 1 ? $"   Attempts {_attempts}" : ""), _label);
            GUILayout.Space(8);

            GUILayout.Label("How good did the movement feel? (1 = awful, 10 = great)", _header);
            GUILayout.BeginHorizontal();
            for (int i = 1; i <= 10; i++)
            {
                GUI.color = i == _rating ? new Color(0.5f, 1f, 0.6f) : Color.white;
                if (GUILayout.Button(i.ToString(), GUILayout.Height(40))) _rating = i;
            }
            GUI.color = Color.white;
            GUILayout.EndHorizontal();
            GUILayout.Space(8);

            GUILayout.Label("Comment (optional): what worked, what was frustrating?", _header);
            _comment = GUILayout.TextArea(_comment, 1000, GUILayout.Height(110));

            if (HasMoreLevelsOfConfig)
                _stopConfigAfterRating = GUILayout.Toggle(_stopConfigAfterRating,
                    " Stop this config here (skip its harder levels)");

            GUILayout.FlexibleSpace();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Play this level again", GUILayout.Height(36))) BeginRun();
            if (GUILayout.Button("Skip rating", GUILayout.Height(36))) SkipRating();
            GUI.enabled = _rating > 0;
            if (GUILayout.Button(_rating > 0 ? "Submit rating" : "Pick a rating first", GUILayout.Height(36))) SubmitRating();
            GUI.enabled = true;
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        void DrawFinished(float w, float h)
        {
            GUILayout.BeginArea(Centered(w, h, 640, 520), _box);
            GUILayout.Label("Session complete — thank you!", _big);
            GUILayout.Space(6);
            if (_session != null)
            {
                GUILayout.Label("Your ratings:", _header);
                foreach (RatingRecord r in _session.ratings.OrderByDescending(x => x.rating))
                    GUILayout.Label($"{r.configId} ({r.course}/{r.difficulty}): <b>{r.rating}/10</b>{(r.finished ? "" : "  (didn't finish)")}", _label);
            }
            GUILayout.Space(8);
            GUILayout.Label("Any overall thoughts? (optional)", _header);
            _sessionComment = GUILayout.TextArea(_sessionComment, 2000, GUILayout.Height(80));
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Save and start a new session", GUILayout.Height(36))) EndSession();
            if (GUILayout.Button("View all results"))
            {
                EndSession();
                ShowResults(Phase.Setup);
            }
            GUILayout.EndArea();
        }

        void DrawResults(float w, float h)
        {
            GUILayout.BeginArea(new Rect(20, 20, w - 40, h - 40), _box);
            GUILayout.Label($"Results: {_ratingCount} ratings from {_testerCount} tester(s)", _big);
            GUILayout.Label("Δ = rating minus that tester's average on the same course & difficulty (corrects for harsh/generous " +
                            "raters and course difficulty). Sorted by Δ. Full report: Playtest/Results/summary.md.", _hint);
            _scroll = GUILayout.BeginScrollView(_scroll);

            GUILayout.Label("Per configuration (all courses)", _header);
            Row("<b>ID</b>", "<b>Title</b>", "<b>n</b>", "<b>Mean</b>", "<b>Δ</b>", "<b>Finish</b>", "<b>Avg time</b>", "<b>Deaths</b>");
            foreach (ConfigAggregate a in _aggregates) AggRow(a, a.title);

            GUILayout.Space(10);
            GUILayout.Label("Per configuration × course", _header);
            foreach (ConfigAggregate a in _perCourse) AggRow(a, $"{a.course}/{a.difficulty} · {a.title}");

            GUILayout.Space(10);
            GUILayout.Label("By axis", _header);
            string lastAxis = null;
            foreach (AxisAggregate a in _axes)
            {
                if (a.axis != lastAxis) { GUILayout.Label($"<b>{a.axis}</b>", _label); lastAxis = a.axis; }
                GUILayout.Label($"    {a.value}:  mean {a.mean:0.0}   Δ {a.meanDelta:+0.0;-0.0;0.0}   (n={a.n})", _hint);
            }
            GUILayout.EndScrollView();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Back", GUILayout.Height(32))) _phase = _resultsReturn;
#if UNITY_EDITOR
            if (GUILayout.Button("Open results folder", GUILayout.Height(32)))
                UnityEditor.EditorUtility.RevealInFinder(PlaytestResultsStore.CsvPath);
#endif
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        void AggRow(ConfigAggregate a, string title) =>
            Row(a.configId, title, a.n.ToString(), a.mean.ToString("0.0"), a.meanDelta.ToString("+0.0;-0.0;0.0"),
                $"{a.finishRate * 100f:0}%", a.meanTime > 0f ? PlaytestResultsStore.FormatTime(a.meanTime) : "-", a.meanDeaths.ToString("0.0"));

        void Row(string id, string title, string n, string mean, string delta, string finish, string time, string deaths)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(id, _hint, GUILayout.Width(50));
            GUILayout.Label(title, _hint, GUILayout.Width(420));
            GUILayout.Label(n, _hint, GUILayout.Width(40));
            GUILayout.Label(mean, _hint, GUILayout.Width(60));
            GUILayout.Label(delta, _hint, GUILayout.Width(60));
            GUILayout.Label(finish, _hint, GUILayout.Width(70));
            GUILayout.Label(time, _hint, GUILayout.Width(80));
            GUILayout.Label(deaths, _hint, GUILayout.Width(60));
            GUILayout.EndHorizontal();
        }

        void DrawSigns(float screenH)
        {
            Camera cam = Camera.main;
            if (cam == null || _signs == null) return;
            foreach (PlaytestSign s in _signs)
            {
                if (s == null) continue;
                Vector3 sp = cam.WorldToScreenPoint(s.transform.position);
                if (sp.z < 0f || sp.x < -200f || sp.x > Screen.width + 200f || sp.y < -50f || sp.y > Screen.height + 50f) continue;
                GUI.Label(new Rect(sp.x / uiScale - 170f, screenH - sp.y / uiScale - 12f, 340f, 24f), s.text, _signStyle);
            }
        }
    }
}
