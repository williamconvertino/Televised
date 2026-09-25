using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Televised.Prototyping.Shared;
using UnityEngine;

namespace Televised.Prototyping.Movement.Playtest
{
    /// <summary>One rated run of one configuration on one course.</summary>
    [Serializable]
    public class RatingRecord
    {
        public string sessionId;
        public string tester;
        public string timestamp;
        public int order;
        public string configId;
        public string configTitle;
        public string category;
        public string difficulty;  // Easy / Medium / Hard
        public string course;      // General / DoubleJump / Grapple
        public string jumpMode;
        public string attachMode;
        public string selectionMode;
        public string feature;
        public string feel;
        public bool finished;      // reached the goal by touching it
        public float timeSeconds;
        public int deaths;
        public int manualRespawns;
        public int restarts;
        public int skips;          // "skip to next checkpoint" uses
        public int attempts;
        public int checkpoints;
        public int jumps;
        public int airJumps;
        public int grappleLatches;
        public string splits;      // seconds at each checkpoint, ";"-separated
        public int rating;         // 1-10
        public string comment;

        /// <summary>Ratings are only compared within the same course + difficulty (Δ is computed per group).</summary>
        public string Group => $"{course}/{difficulty}";
    }

    [Serializable]
    public class SessionRecord
    {
        public string sessionId;
        public string tester;
        public string startedAt;
        public string orderMode;
        public string difficultyPlan;
        public List<string> plannedRuns = new List<string>();
        public List<RatingRecord> ratings = new List<RatingRecord>();
        /// <summary>Levels that were played but whose rating was skipped (metrics kept, rating = 0). JSON only.</summary>
        public List<RatingRecord> unratedRuns = new List<RatingRecord>();
        /// <summary>Levels skipped without playing to the end (from the intro or mid-level). JSON only.</summary>
        public List<string> skippedLevels = new List<string>();
        public string sessionComment;
    }

    public class ConfigAggregate
    {
        public string configId, title, category, difficulty, course, jumpMode, attachMode, selectionMode, feature, feel;
        public int n;
        public float mean, min, max, finishRate, meanTime, meanDeaths, meanSkips, meanDelta;
        public List<string> comments = new List<string>();
    }

    public class AxisAggregate
    {
        public string axis, value;
        public int n;
        public float mean, meanDelta;
    }

    /// <summary>
    /// Persists playtest results:
    ///   Results/ratings.csv            one row per rated run (all sessions, all testers; append-only)
    ///   Results/sessions/*.json        full record of each session
    ///   Results/summary.md             regenerated aggregate report
    /// In the Editor, results live in Assets/Prototyping/Movement/Playtest/Results/.
    /// </summary>
    public static class PlaytestResultsStore
    {
        static readonly string[] Columns =
        {
            "sessionId", "tester", "timestamp", "order", "configId", "configTitle", "category", "difficulty", "course",
            "jumpMode", "attachMode", "selectionMode", "feature", "feel", "finished", "timeSeconds", "deaths",
            "manualRespawns", "restarts", "skips", "attempts", "checkpoints", "jumps", "airJumps", "grappleLatches",
            "splits", "rating", "comment"
        };

        // No byte-order mark: a BOM would become part of the first header name ("\uFEFFsessionId").
        static readonly Encoding Utf8 = new UTF8Encoding(false);

        public static string Folder => Application.isEditor
            ? Path.Combine(Application.dataPath, "Prototyping", "Movement", "Playtest", "Results")
            : Path.Combine(Application.persistentDataPath, "PlaytestResults");

        public static string CsvPath => Path.Combine(Folder, "ratings.csv");
        public static string SummaryPath => Path.Combine(Folder, "summary.md");
        static string SessionsFolder => Path.Combine(Folder, "sessions");

        // ---------------------------------------------------------------- write

        public static void AppendRating(RatingRecord r)
        {
            Directory.CreateDirectory(Folder);
            MigrateHeaderIfNeeded();
            bool newFile = !File.Exists(CsvPath);
            var sb = new StringBuilder();
            if (newFile) sb.AppendLine(string.Join(",", Columns));
            sb.AppendLine(Serialize(r));
            File.AppendAllText(CsvPath, sb.ToString(), Utf8);
        }

        /// <summary>If ratings.csv was written by an older version (different columns), rewrite it with the current header.</summary>
        static void MigrateHeaderIfNeeded()
        {
            if (!File.Exists(CsvPath)) return;
            string first = (File.ReadLines(CsvPath, Utf8).FirstOrDefault() ?? "").TrimStart('\uFEFF');
            if (first == string.Join(",", Columns)) return;
            List<RatingRecord> old = LoadAll();
            File.Copy(CsvPath, CsvPath + ".bak", true);
            var sb = new StringBuilder();
            sb.AppendLine(string.Join(",", Columns));
            foreach (RatingRecord r in old) sb.AppendLine(Serialize(r));
            File.WriteAllText(CsvPath, sb.ToString(), Utf8);
        }

        public static void SaveSession(SessionRecord s)
        {
            Directory.CreateDirectory(SessionsFolder);
            string safeTester = new string((s.tester ?? "anon").Where(ch => char.IsLetterOrDigit(ch) || ch == '_' || ch == '-').ToArray());
            if (string.IsNullOrEmpty(safeTester)) safeTester = "anon";
            File.WriteAllText(Path.Combine(SessionsFolder, $"{s.sessionId}_{safeTester}.json"), JsonUtility.ToJson(s, true), Utf8);
        }

        static string Serialize(RatingRecord r)
        {
            CultureInfo ci = CultureInfo.InvariantCulture;
            string[] f =
            {
                r.sessionId, r.tester, r.timestamp, r.order.ToString(ci), r.configId, r.configTitle, r.category,
                r.difficulty, r.course, r.jumpMode, r.attachMode, r.selectionMode, r.feature, r.feel,
                r.finished ? "1" : "0", r.timeSeconds.ToString("0.0", ci), r.deaths.ToString(ci),
                r.manualRespawns.ToString(ci), r.restarts.ToString(ci), r.skips.ToString(ci), r.attempts.ToString(ci),
                r.checkpoints.ToString(ci), r.jumps.ToString(ci), r.airJumps.ToString(ci), r.grappleLatches.ToString(ci),
                r.splits, r.rating.ToString(ci), r.comment,
            };
            return string.Join(",", f.Select(Escape));
        }

        static string Escape(string s)
        {
            s ??= "";
            s = s.Replace("\r", " ").Replace("\n", " ");
            return s.IndexOfAny(new[] { ',', '"' }) >= 0 ? "\"" + s.Replace("\"", "\"\"") + "\"" : s;
        }

        // ---------------------------------------------------------------- read

        public static List<RatingRecord> LoadAll()
        {
            var result = new List<RatingRecord>();
            if (!File.Exists(CsvPath)) return result;

            string[] lines = File.ReadAllLines(CsvPath, Utf8);
            if (lines.Length < 2) return result;
            string[] header = ParseLine(lines[0].TrimStart('\uFEFF'));
            var col = new Dictionary<string, int>();
            for (int i = 0; i < header.Length; i++) col[header[i]] = i;

            CultureInfo ci = CultureInfo.InvariantCulture;
            for (int li = 1; li < lines.Length; li++)
            {
                if (string.IsNullOrWhiteSpace(lines[li])) continue;
                string[] f = ParseLine(lines[li]);
                string Get(string name) => col.TryGetValue(name, out int i) && i < f.Length ? f[i] : "";
                int GetI(string name) => int.TryParse(Get(name), NumberStyles.Integer, ci, out int v) ? v : 0;
                float GetF(string name) => float.TryParse(Get(name), NumberStyles.Float, ci, out float v) ? v : 0f;
                string Or(string v, string fallback) => string.IsNullOrEmpty(v) ? fallback : v;

                result.Add(new RatingRecord
                {
                    sessionId = Get("sessionId"), tester = Get("tester"), timestamp = Get("timestamp"),
                    order = GetI("order"), configId = Get("configId"), configTitle = Get("configTitle"),
                    category = Get("category"),
                    difficulty = Or(Get("difficulty"), "Easy(v1)"), // rows from before difficulties existed
                    course = Or(Get("course"), "General"),
                    jumpMode = Get("jumpMode"), attachMode = Get("attachMode"),
                    selectionMode = Get("selectionMode"), feature = Get("feature"), feel = Get("feel"),
                    finished = Get("finished") == "1", timeSeconds = GetF("timeSeconds"), deaths = GetI("deaths"),
                    manualRespawns = GetI("manualRespawns"), restarts = GetI("restarts"), skips = GetI("skips"),
                    attempts = GetI("attempts"), checkpoints = GetI("checkpoints"), jumps = GetI("jumps"),
                    airJumps = GetI("airJumps"), grappleLatches = GetI("grappleLatches"), splits = Get("splits"),
                    rating = GetI("rating"), comment = Get("comment"),
                });
            }
            return result;
        }

        static string[] ParseLine(string line)
        {
            var fields = new List<string>();
            var sb = new StringBuilder();
            bool quoted = false;
            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (quoted)
                {
                    if (c == '"' && i + 1 < line.Length && line[i + 1] == '"') { sb.Append('"'); i++; }
                    else if (c == '"') quoted = false;
                    else sb.Append(c);
                }
                else if (c == '"') quoted = true;
                else if (c == ',') { fields.Add(sb.ToString()); sb.Clear(); }
                else sb.Append(c);
            }
            fields.Add(sb.ToString());
            return fields.ToArray();
        }

        public static Dictionary<string, int> RatingCounts(List<RatingRecord> all) =>
            all.GroupBy(r => r.configId).ToDictionary(g => g.Key, g => g.Count());

        public static Dictionary<string, int> RatingCountsByDifficulty(List<RatingRecord> all) =>
            all.GroupBy(r => r.configId + "|" + r.difficulty).ToDictionary(g => g.Key, g => g.Count());

        // ---------------------------------------------------------------- aggregate

        /// <summary>
        /// Per-rating delta vs. that tester's own average rating on the same course + difficulty.
        /// Removes "harsh vs generous rater" bias (each tester only rates a subset), and stops hard courses
        /// from dragging down whichever configs happened to be played on them.
        /// </summary>
        static Dictionary<RatingRecord, float> Deltas(List<RatingRecord> all)
        {
            var mean = all.GroupBy(r => r.tester + "|" + r.Group).ToDictionary(g => g.Key, g => (float)g.Average(r => r.rating));
            return all.ToDictionary(r => r, r => r.rating - mean[r.tester + "|" + r.Group]);
        }

        static ConfigAggregate Aggregate(IGrouping<string, RatingRecord> g, Dictionary<RatingRecord, float> delta, bool perDifficulty)
        {
            RatingRecord first = g.First();
            var finished = g.Where(r => r.finished).ToList();
            return new ConfigAggregate
            {
                configId = first.configId, title = first.configTitle, category = first.category,
                difficulty = perDifficulty ? first.difficulty : "all", course = perDifficulty ? first.course : "all",
                jumpMode = first.jumpMode, attachMode = first.attachMode, selectionMode = first.selectionMode,
                feature = first.feature, feel = first.feel, n = g.Count(),
                mean = (float)g.Average(r => r.rating), min = g.Min(r => r.rating), max = g.Max(r => r.rating),
                finishRate = (float)g.Count(r => r.finished) / g.Count(),
                meanTime = finished.Count > 0 ? (float)finished.Average(r => r.timeSeconds) : 0f,
                meanDeaths = (float)g.Average(r => r.deaths),
                meanSkips = (float)g.Average(r => r.skips),
                meanDelta = g.Average(r => delta[r]),
                comments = g.Where(r => !string.IsNullOrWhiteSpace(r.comment))
                    .Select(r => $"[{r.tester}, {r.course}/{r.difficulty}, {r.rating}/10] {r.comment}").ToList(),
            };
        }

        public static List<ConfigAggregate> AggregateByConfig(List<RatingRecord> all)
        {
            var delta = Deltas(all);
            return all.GroupBy(r => r.configId).Select(g => Aggregate(g, delta, false))
                .OrderByDescending(a => a.meanDelta).ThenByDescending(a => a.mean).ToList();
        }

        public static List<ConfigAggregate> AggregateByConfigAndCourse(List<RatingRecord> all)
        {
            var delta = Deltas(all);
            return all.GroupBy(r => r.configId + "|" + r.Group).Select(g => Aggregate(g, delta, true))
                .OrderBy(a => a.course).ThenBy(a => a.difficulty).ThenByDescending(a => a.meanDelta).ToList();
        }

        public static List<AxisAggregate> AggregateByAxes(List<RatingRecord> all)
        {
            var delta = Deltas(all);
            var result = new List<AxisAggregate>();
            void Axis(string name, Func<RatingRecord, string> key, IEnumerable<RatingRecord> subset, bool useDelta = true)
            {
                foreach (var g in subset.GroupBy(key).OrderByDescending(g => useDelta ? g.Average(r => delta[r]) : g.Average(r => r.rating)))
                    result.Add(new AxisAggregate
                    {
                        axis = name, value = g.Key, n = g.Count(),
                        mean = (float)g.Average(r => r.rating), meanDelta = g.Average(r => delta[r]),
                    });
            }
            // Mechanic axes are compared on Core configs only, so the feature configs don't skew them.
            var core = all.Where(r => r.category == nameof(PlaytestCategory.Core)).ToList();
            Axis("Jump mode (core)", r => r.jumpMode, core);
            Axis("Attach mode (core)", r => r.attachMode, core);
            Axis("Selection (core)", r => r.selectionMode, core);
            Axis("Feel lever (core)", r => r.feel, core);
            Axis("Feature (all)", r => r.feature, all);
            // Difficulty/course: Δ is ~0 by construction here, so the raw mean is what's informative.
            Axis("Course / difficulty (raw mean)", r => r.Group, all, false);
            return result;
        }

        public static void WriteSummary()
        {
            List<RatingRecord> all = LoadAll();
            var sb = new StringBuilder();
            CultureInfo ci = CultureInfo.InvariantCulture;
            sb.AppendLine("# Movement Playtest Summary");
            sb.AppendLine();
            sb.AppendLine($"Generated {DateTime.Now:yyyy-MM-dd HH:mm}. {all.Count} ratings from " +
                          $"{all.Select(r => r.tester).Distinct().Count()} tester(s), {all.Select(r => r.sessionId).Distinct().Count()} session(s).");
            sb.AppendLine();
            sb.AppendLine("**Δ** = rating − that tester's average on the same course & difficulty. It corrects for harsh/generous " +
                          "raters and for course difficulty. Sort by it. Axis tables are rough (configs differ in more than one lever).");
            sb.AppendLine();

            List<ConfigAggregate> byConfig = AggregateByConfig(all);
            sb.AppendLine("## Per configuration (all courses)");
            sb.AppendLine();
            AppendConfigTable(sb, byConfig, ci, false);

            sb.AppendLine("## Per configuration × course/difficulty");
            sb.AppendLine();
            AppendConfigTable(sb, AggregateByConfigAndCourse(all), ci, true);

            sb.AppendLine("## By axis");
            sb.AppendLine();
            sb.AppendLine("| Axis | Value | n | Mean | Δ |");
            sb.AppendLine("|---|---|---|---|---|");
            foreach (var a in AggregateByAxes(all))
                sb.AppendLine(string.Format(ci, "| {0} | {1} | {2} | {3:0.0} | {4:+0.0;-0.0;0.0} |", a.axis, a.value, a.n, a.mean, a.meanDelta));
            sb.AppendLine();

            var unrated = PlaytestConfigCatalog.All.Where(c => byConfig.All(a => a.configId != c.id)).Select(c => c.id).ToList();
            if (unrated.Count > 0)
            {
                sb.AppendLine($"**Not yet rated:** {string.Join(", ", unrated)}");
                sb.AppendLine();
            }

            sb.AppendLine("## Comments");
            sb.AppendLine();
            foreach (var a in byConfig.Where(a => a.comments.Count > 0))
            {
                sb.AppendLine($"### {a.configId}: {a.title}");
                foreach (string c in a.comments) sb.AppendLine($"- {c}");
                sb.AppendLine();
            }

            Directory.CreateDirectory(Folder);
            File.WriteAllText(SummaryPath, sb.ToString(), Utf8);
        }

        static void AppendConfigTable(StringBuilder sb, List<ConfigAggregate> rows, CultureInfo ci, bool withCourse)
        {
            sb.AppendLine(withCourse
                ? "| Config | Title | Course | n | Mean | Δ | Min–Max | Finish % | Avg time (finished) | Avg deaths | Avg skips |"
                : "| Config | Title | n | Mean | Δ | Min–Max | Finish % | Avg time (finished) | Avg deaths | Avg skips |");
            sb.AppendLine(withCourse ? "|---|---|---|---|---|---|---|---|---|---|---|" : "|---|---|---|---|---|---|---|---|---|---|");
            foreach (var a in rows)
                sb.AppendLine(string.Format(ci, "| {0} | {1} |{2} {3} | {4:0.0} | {5:+0.0;-0.0;0.0} | {6:0}–{7:0} | {8:0}% | {9} | {10:0.0} | {11:0.0} |",
                    a.configId, a.title, withCourse ? $" {a.course}/{a.difficulty} |" : "", a.n, a.mean, a.meanDelta, a.min, a.max,
                    a.finishRate * 100f, a.meanTime > 0f ? FormatTime(a.meanTime) : "-", a.meanDeaths, a.meanSkips));
            sb.AppendLine();
        }

        public static string FormatTime(float seconds)
        {
            int m = Mathf.FloorToInt(seconds / 60f);
            return $"{m}:{seconds - m * 60f:00.0}";
        }
    }
}
