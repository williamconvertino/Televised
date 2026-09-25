using System.Collections.Generic;
using System.IO;
using Televised.Prototyping.Movement.Playtest;
using Televised.Prototyping.Shared;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using A = Televised.Prototyping.Shared.EditorTools.PrototypeAssetBuilder;
using B = Televised.Prototyping.Movement.EditorTools.MovementSandboxBuilder;

namespace Televised.Prototyping.Movement.EditorTools
{
    /// <summary>
    /// Builds Scenes/PlaytestLevel.unity: nine courses (General / DoubleJump / Grapple × Easy / Medium / Hard)
    /// stacked vertically in one scene, plus the PlaytestSession that runs configurations and collects ratings.
    ///
    /// Courses are assembled from "modules". Each module forces a specific kind of movement (wrap under a circle,
    /// crawl a tunnel roof over pits, switch walls around hazard rungs...). Every module starts from the previous
    /// exit point p = (right end x, top y) of the last platform and returns the next exit point.
    /// See Playtest/PLAYTEST_GUIDE.md for the design and the solvability constraints.
    /// </summary>
    public static class PlaytestLevelBuilder
    {
        public const string ScenePath = B.Root + "/Scenes/PlaytestLevel.unity";

        /// <summary>
        /// Bump when the course layout/scene structure changes. The auto-builder rebuilds any saved scene
        /// that doesn't contain the current marker object.
        /// </summary>
        public const string VersionMarker = "PlaytestLevel_v3";

        public static bool SceneIsCurrent() =>
            File.Exists(ScenePath) && File.ReadAllText(ScenePath).Contains(VersionMarker);

        static readonly Color PlatformFill = new Color(0.22f, 0.25f, 0.33f);
        static readonly Color PlatformOutline = new Color(0.55f, 0.72f, 0.9f);
        static readonly Color ShapeFill = new Color(0.25f, 0.34f, 0.3f);
        static readonly Color ShapeOutline = new Color(0.6f, 0.95f, 0.65f);
        static readonly Color HazardFill = new Color(0.55f, 0.07f, 0.1f);
        static readonly Color HazardOutline = new Color(1f, 0.35f, 0.25f);

        [MenuItem("Prototyping/Movement/Build Playtest Level")]
        public static void BuildMenu() => Build(true);

        [MenuItem("Prototyping/Movement/Open Playtest Level")]
        public static void OpenMenu()
        {
            if (!File.Exists(ScenePath)) { Build(true); return; }
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) EditorSceneManager.OpenScene(ScenePath);
        }

        [MenuItem("Prototyping/Movement/Open Playtest Results Folder")]
        public static void OpenResults()
        {
            Directory.CreateDirectory(PlaytestResultsStore.Folder);
            EditorUtility.RevealInFinder(PlaytestResultsStore.Folder);
        }

        public static bool Build(bool confirmOverwrite)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[Playtest] Exit Play Mode before building the playtest level.");
                return false;
            }
            if (confirmOverwrite && File.Exists(ScenePath) &&
                !EditorUtility.DisplayDialog("Build Playtest Level",
                    "This overwrites PlaytestLevel.unity. Results in Playtest/Results are not touched.", "Build", "Cancel"))
                return false;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return false;

            A.EnsureAssets(out Material mat, out Sprite circle);
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            new GameObject(VersionMarker); // lets the auto-builder detect outdated scenes

            // Camera.
            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 9f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.06f, 0.05f, 0.08f);
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 100f;
            camGo.transform.position = new Vector3(-5f, 1f, -10f);
            camGo.AddComponent<AudioListener>();
            var sandboxCam = camGo.AddComponent<SandboxCamera>();

            // Player.
            var player = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(A.PlayerPrefabPath), scene);
            player.name = "Player";
            player.transform.position = new Vector3(-5f, 1f, 0f);
            var motor = player.GetComponent<PlayerMotor2D>();
            A.Set(sandboxCam, "target", player.transform);
            var camSo = new SerializedObject(sandboxCam);
            camSo.FindProperty("size").floatValue = 9f;
            camSo.ApplyModifiedPropertiesWithoutUndo();

            // Courses.
            var courses = new List<PlaytestCourse>
            {
                BuildCourse(circle, CourseTrack.General, PlaytestDifficulty.Easy, new Vector2(0f, 0f)),
                BuildCourse(circle, CourseTrack.General, PlaytestDifficulty.Medium, new Vector2(0f, 120f)),
                BuildCourse(circle, CourseTrack.General, PlaytestDifficulty.Hard, new Vector2(0f, 240f)),
                BuildCourse(circle, CourseTrack.DoubleJump, PlaytestDifficulty.Easy, new Vector2(0f, 380f)),
                BuildCourse(circle, CourseTrack.DoubleJump, PlaytestDifficulty.Medium, new Vector2(0f, 440f)),
                BuildCourse(circle, CourseTrack.DoubleJump, PlaytestDifficulty.Hard, new Vector2(0f, 500f)),
                BuildCourse(circle, CourseTrack.Grapple, PlaytestDifficulty.Easy, new Vector2(0f, 580f)),
                BuildCourse(circle, CourseTrack.Grapple, PlaytestDifficulty.Medium, new Vector2(0f, 640f)),
                BuildCourse(circle, CourseTrack.Grapple, PlaytestDifficulty.Hard, new Vector2(0f, 700f)),
            };

            // Debug lines (player-facing hints only; F5 toggles the full debug view).
            var debugGo = new GameObject("MovementDebug");
            debugGo.AddComponent<MeshFilter>();
            debugGo.AddComponent<MeshRenderer>().sharedMaterial = mat;
            debugGo.AddComponent<DebugLines>();
            var debugRenderer = debugGo.AddComponent<MovementDebugRenderer>();
            A.Set(debugRenderer, "motor", motor);

            // Session.
            var session = new GameObject("PlaytestSession").AddComponent<PlaytestSession>();
            var so = new SerializedObject(session);
            so.FindProperty("motor").objectReferenceValue = motor;
            so.FindProperty("debugRenderer").objectReferenceValue = debugRenderer;
            SerializedProperty list = so.FindProperty("courses");
            list.arraySize = courses.Count;
            for (int i = 0; i < courses.Count; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = courses[i];
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log($"[Playtest] Built {ScenePath} with {courses.Count} courses. Press Play to start a session.");
            return true;
        }

        // ------------------------------------------------------------------ course recipes

        static PlaytestCourse BuildCourse(Sprite circle, CourseTrack track, PlaytestDifficulty diff, Vector2 origin)
        {
            var root = new GameObject($"Course_{track}_{diff}");
            var course = root.AddComponent<PlaytestCourse>();
            course.track = track;
            course.difficulty = diff;
            var k = new Kit(root.transform, circle, (int)track * 1000 + (int)diff * 100);

            k.Section("Start", $"{track} · {diff}", origin + new Vector2(-2f, 4f));
            Vector2 p = Start(k, origin, out Transform start);
            course.startPoint = start;

            switch (track)
            {
                case CourseTrack.General: p = General(k, p, diff); break;
                case CourseTrack.DoubleJump: p = DoubleJump(k, p, diff); break;
                case CourseTrack.Grapple: p = Grapple(k, p, diff); break;
            }

            k.Section("Goal", "GOAL", p + new Vector2(7f, 3.5f));
            course.goal = Goal(k, p);
            course.checkpoints = k.Checkpoints;
            k.Lava();
            return course;
        }

        static Vector2 General(Kit k, Vector2 p, PlaytestDifficulty d)
        {
            switch (d)
            {
                case PlaytestDifficulty.Easy:
                    k.Section("Gaps & steps", "Gaps & steps", p + new Vector2(6f, 4f));
                    p = Gaps(k, p, (2.0f, 6f, 0f, true), (2.3f, 5f, 1.0f, false));
                    p = Steps(k, p, (5f, 2.5f), (5f, -2.0f), (4f, 1.5f));
                    p = Gaps(k, p, (2.0f, 6f, 0f, false));
                    k.Checkpoint(p);

                    k.Section("Sparse blobs", "Blob hop (one at a time)", p + new Vector2(8f, 5f));
                    p = SparseBlobs(k, p, 4, 1.0f, 2.0f, new[] { 0.5f, 1.2f, 0.3f, 0.9f }, 0, false);
                    k.Checkpoint(p);

                    k.Section("Tunnel", "Tunnel: cross the pit on the roof", p + new Vector2(8f, 5f));
                    p = Tunnel(k, p, 14f, 1.6f, new[] { (5f, 1.6f) }, false, false);
                    k.Checkpoint(p);

                    k.Section("Wrap", "Its top is deadly: go under (S)", p + new Vector2(4f, 5.5f));
                    p = WrapCircle(k, p, 2.2f, 2.0f, 2.0f, false);
                    k.Checkpoint(p);

                    k.Section("Columns", "Floating columns: go under each", p + new Vector2(4f, 5.5f));
                    p = Columns(k, p, 2, new[] { 0f, 0.8f });
                    k.Checkpoint(p);

                    k.Section("Chimney", "Chimney: switch walls around the red rungs", p + new Vector2(6f, 3.5f));
                    p = Chimney(k, p, 15f, 4f, new[] { 4f, 8f, 12f }, 0.55f);
                    k.Checkpoint(p);

                    k.Section("Ceiling chain", "Ceiling chain: tops are deadly", p + new Vector2(4f, 5f));
                    p = UndersideChain(k, p, 2, 7f, 3f, 0.9f);
                    return p;

                case PlaytestDifficulty.Medium:
                    // The former Medium and Hard merged: every section type once, at the harder settings.
                    k.Section("Gaps & steps", "Gaps & steps", p + new Vector2(6f, 4.5f));
                    p = Gaps(k, p, (2.8f, 4f, 0.8f, true), (2.9f, 3.5f, -1.2f, true));
                    p = Steps(k, p, (2.5f, 3.5f), (3f, -2.5f), (2.5f, 3f));
                    p = Gaps(k, p, (2.6f, 6f, 0f, false));
                    k.Checkpoint(p);

                    k.Section("Sparse blobs", "Every top is spiked", p + new Vector2(10f, 6.5f));
                    p = SparseBlobs(k, p, 6, 0.7f, 2.5f, new[] { 0.5f, 1.3f, 0.2f, 1.0f, -0.3f, 0.8f }, 1, true);
                    k.Checkpoint(p);

                    k.Section("Tunnel", "Tunnel: roof over pits, drop before spikes", p + new Vector2(10f, 5f));
                    p = Tunnel(k, p, 24f, 1.4f, new[] { (5f, 2.0f), (11f, 2.2f), (17f, 2.4f) }, true, true);
                    k.Checkpoint(p);

                    k.Section("Wrap & columns", "Under, under, under", p + new Vector2(6f, 6.5f));
                    p = WrapCircle(k, p, 2.8f, 2.4f, 2.4f, true);
                    p = Columns(k, p, 4, new[] { 0.5f, -0.5f, 1f, 0f });
                    k.Checkpoint(p);

                    k.Section("Chimney", "Tall chimney", p + new Vector2(6f, 3.5f));
                    p = Chimney(k, p, 27f, 3.8f, new[] { 3.5f, 7f, 10.5f, 14f, 17.5f, 21f, 24.5f }, 0.6f);
                    k.Checkpoint(p);

                    k.Section("Ceiling chain", "Ceiling chain", p + new Vector2(4f, 5f));
                    p = UndersideChain(k, p, 4, 5f, 3.4f, 0.6f);
                    k.Checkpoint(p);

                    k.Section("Drop shaft", "Deep drop", p + new Vector2(4f, 4f));
                    p = DropShaft(k, p, 18f, 3.8f, new[] { 3f, 6.5f, 10f, 13.5f }, 0.62f);
                    return p;

                default: // Hard: dedicated skill sections, longer jumps, more hazards, moving surfaces
                    k.Section("Long jumps", "SKILL: long, precise jumps", p + new Vector2(8f, 5f));
                    p = Gaps(k, p, (3.4f, 3f, 0.8f, true), (3.6f, 2.6f, -1.0f, true), (3.8f, 3f, 0.6f, false), (3.5f, 2.6f, -0.4f, true));
                    p = Gaps(k, p, (3.0f, 6f, 0f, false));
                    k.Checkpoint(p);

                    k.Section("Tiny targets", "SKILL: tiny spiked targets", p + new Vector2(10f, 6f));
                    p = SparseBlobs(k, p, 6, 0.55f, 2.8f, new[] { 0.6f, -0.2f, 1.2f, 0.3f, 1.0f, -0.4f }, 1, true);
                    k.Checkpoint(p);

                    k.Section("Ferry", "SKILL: ride the ferry (don't jump under the spikes)", p + new Vector2(8f, 4.5f));
                    p = Ferry(k, p, 16f, 6.5f);
                    k.Checkpoint(p);

                    k.Section("Pistons", "SKILL: timing, crawl under the pistons", p + new Vector2(8f, 5.5f));
                    p = Pistons(k, p, 15f, 4, 2.6f);
                    k.Checkpoint(p);

                    k.Section("Chimney", "SKILL: tight chimney", p + new Vector2(6f, 3.5f));
                    p = Chimney(k, p, 31f, 3.4f, new[] { 3.5f, 6.7f, 9.9f, 13.1f, 16.3f, 19.5f, 22.7f, 25.9f, 29.1f }, 0.64f);
                    k.Checkpoint(p);

                    k.Section("Windmill", "SKILL: ride the windmill (the hub is deadly)", p + new Vector2(6.5f, 6f));
                    p = Windmill(k, p, 9f, 30f);
                    k.Checkpoint(p);

                    k.Section("Ceiling marathon", "SKILL: ceiling marathon", p + new Vector2(8f, 5f));
                    p = UndersideChain(k, p, 5, 4.5f, 3.8f, 0.5f);
                    k.Checkpoint(p);

                    k.Section("Bobbing stones", "SKILL: bobbing stones", p + new Vector2(8f, 4.5f));
                    p = Bobbing(k, p, 4, 3.8f, 0.75f, 1.6f, 3f);
                    k.Checkpoint(p);

                    k.Section("Elevator & drop", "SKILL: elevator, then the deep drop", p + new Vector2(4f, 4f));
                    p = Elevator(k, p, 8f, 5f);
                    p = DropShaft(k, p, 22f, 3.4f, new[] { 3f, 6.2f, 9.4f, 12.6f, 15.8f, 19.0f }, 0.64f);
                    return p;
            }
        }

        static Vector2 DoubleJump(Kit k, Vector2 p, PlaytestDifficulty d)
        {
            switch (d)
            {
                case PlaytestDifficulty.Easy:
                    p = Gaps(k, p, (2.0f, 6f, 0f, false));
                    k.Section("DJ gap", "Too far for one jump: jump again in the air", p + new Vector2(4f, 4.5f));
                    p = DJGap(k, p, 8.0f, 0f);
                    k.Checkpoint(p);
                    k.Section("DJ ledge", "Red wall: double jump up and over", p + new Vector2(5f, 6f));
                    p = DJLedge(k, p, 4.2f);
                    p = Gaps(k, p, (2.0f, 6f, 0f, false));
                    k.Checkpoint(p);
                    k.Section("DJ gap 2", "", p);
                    p = DJGap(k, p, 8.2f, 1.0f);
                    return p;

                case PlaytestDifficulty.Medium:
                    k.Section("DJ gap", "Double jump", p + new Vector2(4f, 4.5f));
                    p = DJGap(k, p, 8.5f, 0f);
                    p = DJLedge(k, p, 4.5f);
                    k.Checkpoint(p);
                    k.Section("Blobs & gap", "Blobs, then a long gap", p + new Vector2(6f, 5f));
                    p = SparseBlobs(k, p, 3, 0.9f, 2.2f, new[] { 0.4f, 1.0f, 0.2f }, 0, false);
                    p = DJGap(k, p, 8.8f, -1f);
                    k.Checkpoint(p);
                    k.Section("Ledge & gap", "", p);
                    p = DJLedge(k, p, 4.5f);
                    p = DJGap(k, p, 9.0f, 0.5f);
                    return p;

                default: // Hard
                    k.Section("DJ gap", "Double jump", p + new Vector2(4f, 4.5f));
                    p = DJGap(k, p, 9.2f, 0.5f);
                    p = DJLedge(k, p, 4.7f);
                    k.Checkpoint(p);
                    k.Section("Tunnel & gap", "Tunnel, then a long climb-jump", p + new Vector2(8f, 5f));
                    p = Tunnel(k, p, 12f, 1.5f, new[] { (5f, 1.8f) }, true, false);
                    p = DJGap(k, p, 9.3f, 1.0f);
                    k.Checkpoint(p);
                    k.Section("Ledge, wrap, gap", "", p);
                    p = DJLedge(k, p, 4.7f);
                    p = WrapCircle(k, p, 2.4f, 2.2f, 2.2f, false);
                    p = DJGap(k, p, 9.5f, 0f);
                    return p;
            }
        }

        static Vector2 Grapple(Kit k, Vector2 p, PlaytestDifficulty d)
        {
            switch (d)
            {
                case PlaytestDifficulty.Easy:
                    p = Gaps(k, p, (2.0f, 6f, 0f, false));
                    k.Section("Cliff", "Red cliff: grapple the lip above", p + new Vector2(5f, 8.5f));
                    p = GrappleCliff(k, p, 6f);
                    p = Gaps(k, p, (2.2f, 6f, 0f, false));
                    k.Checkpoint(p);
                    k.Section("Gap", "Too wide to jump: use the anchor", p + new Vector2(4.5f, 6.5f));
                    p = GrappleGap(k, p, 9.5f, 4.5f);
                    k.Checkpoint(p);
                    k.Section("Ceiling", "Grapple up, crawl the ceiling", p + new Vector2(5f, 7.5f));
                    p = CeilingRun(k, p, 11f, 5.5f, 0);
                    return p;

                case PlaytestDifficulty.Medium:
                    k.Section("Gap", "Use the anchor", p + new Vector2(5f, 7f));
                    p = GrappleGap(k, p, 10.5f, 4.8f);
                    k.Checkpoint(p);
                    k.Section("Cliff", "Grapple the lip", p + new Vector2(5f, 9f));
                    p = GrappleCliff(k, p, 6.5f);
                    k.Checkpoint(p);
                    k.Section("Ceiling", "Spikes on the ceiling: re-grapple past them", p + new Vector2(6f, 8f));
                    p = CeilingRun(k, p, 14f, 6f, 1);
                    k.Checkpoint(p);
                    k.Section("Chain", "Anchor chain", p + new Vector2(6f, 7f));
                    p = GrappleChain(k, p, 2, 8.6f, 4.5f);
                    return p;

                default: // Hard
                    k.Section("Chain", "Anchor chain", p + new Vector2(6f, 7f));
                    p = GrappleChain(k, p, 3, 8.6f, 4.5f);
                    k.Checkpoint(p);
                    k.Section("Cliff", "High lip", p + new Vector2(5f, 9.5f));
                    p = GrappleCliff(k, p, 7f);
                    k.Checkpoint(p);
                    k.Section("Ceiling", "Spiked ceiling", p + new Vector2(6f, 8.5f));
                    p = CeilingRun(k, p, 16f, 6.5f, 2);
                    k.Checkpoint(p);
                    k.Section("Gap, blobs, chain", "", p);
                    p = GrappleGap(k, p, 11.5f, 5.0f);
                    p = SparseBlobs(k, p, 3, 0.8f, 2.3f, new[] { 0.3f, 0.9f, 0.1f }, 1, false);
                    p = GrappleChain(k, p, 3, 8.6f, 5.0f);
                    return p;
            }
        }

        // ------------------------------------------------------------------ modules
        // Convention: p = (right end x, top y) of the platform the player is currently standing on.

        static Vector2 Start(Kit k, Vector2 o, out Transform start)
        {
            k.Plat(o.x - 8f, o.x + 4f, o.y);
            start = k.Point("Start", new Vector2(o.x - 5f, o.y + 1f));
            return new Vector2(o.x + 4f, o.y);
        }

        static PlaytestCheckpoint Goal(Kit k, Vector2 p)
        {
            float x0 = p.x + 2.2f;
            k.Plat(x0, x0 + 9f, p.y);
            return k.Goal(new Vector2(x0 + 5f, p.y + 0.9f));
        }

        /// <summary>Platforms separated by gaps; optional hazard bump in the middle of each.</summary>
        static Vector2 Gaps(Kit k, Vector2 p, params (float gap, float width, float dy, bool bump)[] segs)
        {
            foreach (var s in segs)
            {
                float x0 = p.x + s.gap, top = p.y + s.dy;
                k.Plat(x0, x0 + s.width, top);
                if (s.bump) k.HazCircle(x0 + s.width * 0.5f, top + 0.05f, 0.42f);
                p = new Vector2(x0 + s.width, top);
            }
            return p;
        }

        /// <summary>Adjoining blocks that rise or fall: forces crawling up and down walls (rise must be non-zero).</summary>
        static Vector2 Steps(Kit k, Vector2 p, params (float run, float rise)[] steps)
        {
            foreach (var s in steps)
            {
                float top = p.y + s.rise;
                k.Box(p.x - 0.5f, p.x + s.run, Mathf.Min(p.y, top) - 3f, top, 0.35f);
                p = new Vector2(p.x + s.run, top);
            }
            return p;
        }

        /// <summary>
        /// Blobs spaced so you can never skip one (two-hop distance > jump range). capsEvery > 0 puts spikes on
        /// the tops of every Nth blob (land on the sides, wrap underneath). bar = spiked sky to stop floaty arcs.
        /// </summary>
        static Vector2 SparseBlobs(Kit k, Vector2 p, int n, float r, float gap, float[] dys, int capsEvery, bool bar)
        {
            float right = p.x, maxTop = p.y, firstLeft = p.x + gap;
            for (int i = 0; i < n; i++)
            {
                float cx = right + gap + r, cy = p.y + dys[i % dys.Length];
                if (capsEvery > 0 || i % 2 == 0) k.Circle(cx, cy, r);
                else k.Blob(cx, cy, r);
                if (capsEvery > 0 && i % capsEvery == 0) k.HazCircle(cx, cy + r, r * 0.45f);
                right = cx + r;
                maxTop = Mathf.Max(maxTop, cy + r);
            }
            float x0 = right + gap;
            k.Plat(x0, x0 + 6f, p.y);
            if (bar) k.HazBox(firstLeft - 1f, right + 1f, maxTop + 2.6f, maxTop + 3.1f);
            return new Vector2(x0 + 6f, p.y);
        }

        /// <summary>
        /// Low tunnel with a spiked roof top (can't go over). Pits in the floor must be crossed by jumping up to
        /// the roof and crawling its underside; roof spikes after each pit force a drop back to the floor.
        /// mouthOrb = a spike just outside the entrance at roof height, so you have to crawl in.
        /// </summary>
        static Vector2 Tunnel(Kit k, Vector2 p, float len, float clear, (float off, float w)[] pits, bool roofOrbs, bool mouthOrb)
        {
            const float lead = 3f;
            float y = p.y, x0 = p.x + 2f, s = x0;
            foreach (var pit in pits)
            {
                k.Plat(s, x0 + pit.off, y);
                k.HazBox(x0 + pit.off + 0.1f, x0 + pit.off + pit.w - 0.1f, y - 3.2f, y - 2.6f);
                s = x0 + pit.off + pit.w;
            }
            k.Plat(s, x0 + len + 4f, y);

            float rx0 = x0 + lead, rx1 = x0 + len, rb = y + clear;
            k.Box(rx0, rx1, rb, rb + 1.4f, 0.4f);
            k.HazBox(rx0 + 0.25f, rx1 - 0.25f, rb + 1.3f, rb + 1.7f);
            if (roofOrbs)
                foreach (var pit in pits)
                {
                    float ox = x0 + pit.off + pit.w + 2.5f;
                    if (ox < rx1 - 1f) k.HazCircle(ox, rb, 0.3f);
                }
            if (mouthOrb) k.HazCircle(rx0 - 0.7f, rb + 0.3f, 0.4f);
            return new Vector2(x0 + len + 4f, y);
        }

        /// <summary>
        /// A big circle between two platforms with a spiked top: land on its side, crawl underneath (S, then
        /// the lock carries you around), and jump off the far side. bar = spiked sky above it.
        /// </summary>
        static Vector2 WrapCircle(Kit k, Vector2 p, float R, float gapIn, float gapOut, bool bar)
        {
            float cx = p.x + gapIn + R, cy = p.y - 0.5f;
            k.Circle(cx, cy, R);
            k.HazCircle(cx, cy + R, R * 0.5f);
            if (bar) k.HazBox(cx - R - 1.5f, cx + R + 1.5f, cy + R + 2.8f, cy + R + 3.3f);
            float x0 = cx + R + gapOut;
            k.Plat(x0, x0 + 6f, p.y);
            return new Vector2(x0 + 6f, p.y);
        }

        /// <summary>Floating columns with spiked tops: land on the left face, go under, jump from the right face.</summary>
        static Vector2 Columns(Kit k, Vector2 p, int n, float[] dys)
        {
            float x = p.x + 2.2f;
            for (int i = 0; i < n; i++)
            {
                float top = p.y + 2.5f + dys[i % dys.Length];
                k.Box(x, x + 1.4f, top - 6.5f, top, 0.6f);
                k.HazBox(x - 0.05f, x + 1.45f, top - 0.2f, top + 0.35f);
                x += 1.4f + 3.0f;
            }
            float x0 = x - 3.0f + 2.4f;
            k.Plat(x0, x0 + 6f, p.y);
            return new Vector2(x0 + 6f, p.y);
        }

        /// <summary>
        /// Vertical shaft climbed from the bottom. Hazard rungs stick out from alternating walls (odd count, starting
        /// on the left), so you must switch walls to pass each one and can't fall straight through. Exit over the
        /// (lower) right wall onto a platform on its outside.
        /// </summary>
        static Vector2 Chimney(Kit k, Vector2 p, float H, float W, float[] rungs, float frac)
        {
            float y = p.y, x0 = p.x + 2f, wl0 = x0 + 2.5f, xL = wl0 + 1.5f, xR = xL + W;
            k.Plat(x0, xR + 1.5f, y);
            k.Box(wl0, xL, y + 2.5f, y + H + 0.8f, 0.5f); // left wall, raised to leave an entrance
            k.Box(xR, xR + 1.5f, y - 1f, y + H, 0.5f);    // right wall
            for (int i = 0; i < rungs.Length; i++)
            {
                float h = rungs[i];
                if (i % 2 == 0) k.HazBox(xL - 0.3f, xL + W * frac, y + h, y + h + 0.5f);
                else k.HazBox(xR - W * frac, xR + 0.3f, y + h, y + h + 0.5f);
            }
            float ex0 = xR + 1.0f;
            k.Plat(ex0, ex0 + 6f, y + H - 1f);
            return new Vector2(ex0 + 6f, y + H - 1f);
        }

        /// <summary>
        /// Descending shaft: land on the left wall's top, crawl down its inside, switch walls around alternating
        /// hazard rungs, exit under the right wall at the bottom. The right wall's top is spiked (no skipping over).
        /// </summary>
        static Vector2 DropShaft(Kit k, Vector2 p, float D, float W, float[] depths, float frac)
        {
            float y = p.y, wl0 = p.x + 2f, xL = wl0 + 2f, xR = xL + W;
            k.Box(wl0, xL, y - D - 1f, y, 0.5f);
            k.Box(xR, xR + 1.5f, y - D + 2.5f, y + 2.5f, 0.5f);
            k.HazBox(xR - 0.05f, xR + 1.55f, y + 2.3f, y + 2.85f);
            k.Plat(wl0, xR + 7.5f, y - D);
            for (int i = 0; i < depths.Length; i++)
            {
                float d = depths[i];
                if (i % 2 == 0) k.HazBox(xL - 0.3f, xL + W * frac, y - d, y - d + 0.5f);
                else k.HazBox(xR - W * frac, xR + 0.3f, y - d, y - d + 0.5f);
            }
            return new Vector2(xR + 7.5f, y - D);
        }

        /// <summary>
        /// Slabs with spiked tops; travel along their undersides. Between slabs, drop onto a hanging circle and
        /// jump back up to the next underside. Exit by dropping onto a platform below.
        /// </summary>
        static Vector2 UndersideChain(Kit k, Vector2 p, int n, float slabW, float gap, float circleR)
        {
            float yb = p.y + 2.3f, x = p.x - 3f;
            for (int i = 0; i < n; i++)
            {
                k.Box(x, x + slabW, yb, yb + 1.2f, 0.5f);
                k.HazBox(x + 0.2f, x + slabW - 0.2f, yb + 1.1f, yb + 1.5f);
                if (i < n - 1) k.Circle(x + slabW + gap * 0.5f, yb - 2.0f, circleR);
                x += slabW + gap;
            }
            float ex0 = x - gap + 1.5f;
            k.Plat(ex0, ex0 + 6f, yb - 3f);
            return new Vector2(ex0 + 6f, yb - 3f);
        }

        // ---- double jump modules (single-jump range ≈ 6.4 gap, ≈ 3.0 height; with a double jump ≈ 11 / 5.3)

        static Vector2 DJGap(Kit k, Vector2 p, float gap, float dy)
        {
            k.Plat(p.x + gap, p.x + gap + 6f, p.y + dy);
            return new Vector2(p.x + gap + 6f, p.y + dy);
        }

        /// <summary>A cliff whose face is a hazard; too tall for one jump. Double jump onto the top.</summary>
        static Vector2 DJLedge(Kit k, Vector2 p, float h)
        {
            float fx0 = p.x + 2.2f, face = fx0 + 5f;
            k.Plat(fx0, face, p.y);
            k.Box(face, face + 6f, p.y - 3f, p.y + h, 0.4f);
            k.HazBox(face - 0.25f, face + 0.1f, p.y - 0.2f, p.y + h - 0.45f);
            return new Vector2(face + 6f, p.y + h);
        }

        // ---- grapple modules (baseline grapple range 9; nothing here is reachable by plain jumps)

        /// <summary>A gap too wide to jump with an anchor circle above the middle.</summary>
        static Vector2 GrappleGap(Kit k, Vector2 p, float gap, float anchorH)
        {
            k.Circle(p.x + gap * 0.5f, p.y + anchorH, 0.8f);
            k.Plat(p.x + gap, p.x + gap + 6f, p.y);
            return new Vector2(p.x + gap + 6f, p.y);
        }

        /// <summary>
        /// A cliff with a hazard face and a safe lip overhanging the approach. The lip's underside is out of jump
        /// reach: grapple it (Pull), or grapple + reel in (Swing/SwingPull), then crawl around onto the top.
        /// The lip's top sits slightly above the cliff top so crawling off it transfers cleanly.
        /// </summary>
        static Vector2 GrappleCliff(Kit k, Vector2 p, float h)
        {
            float fx0 = p.x + 2.2f, face = fx0 + 6f;
            k.Plat(fx0, face, p.y);
            k.Box(face, face + 6f, p.y - 3f, p.y + h, 0.4f);
            k.HazBox(face - 0.25f, face + 0.1f, p.y - 0.2f, p.y + h - 1.25f);
            k.Box(face - 2.5f, face + 0.8f, p.y + h - 1.2f, p.y + h + 0.3f, 0.4f);
            return new Vector2(face + 6f, p.y + h);
        }

        /// <summary>
        /// A long gap under a high ceiling (out of jump reach, spiked top). Grapple up, crawl the underside, drop to
        /// the far platform. Spikes hanging from the ceiling force a drop and a mid-air re-grapple.
        /// </summary>
        static Vector2 CeilingRun(Kit k, Vector2 p, float len, float h, int orbs)
        {
            float ex0 = p.x + len, cb = p.y + h;
            k.Plat(ex0, ex0 + 6f, p.y);
            k.Box(p.x - 1.5f, ex0 + 1f, cb, cb + 1.2f, 0.5f);
            k.HazBox(p.x - 1.3f, ex0 + 0.8f, cb + 1.1f, cb + 1.5f);
            for (int j = 1; j <= orbs; j++) k.HazCircle(p.x + len * j / (orbs + 1f), cb, 0.45f);
            return new Vector2(ex0 + 6f, p.y);
        }

        /// <summary>Anchors with spiked tops over lava, spaced beyond plain-jump range but within grapple range.</summary>
        static Vector2 GrappleChain(Kit k, Vector2 p, int n, float spacing, float anchorH)
        {
            float x = p.x + spacing * 0.5f;
            for (int i = 0; i < n; i++)
            {
                k.Circle(x, p.y + anchorH, 0.8f);
                k.HazCircle(x, p.y + anchorH + 0.8f, 0.4f);
                x += spacing;
            }
            float ex0 = x - spacing + spacing * 0.5f;
            k.Plat(ex0, ex0 + 6f, p.y);
            return new Vector2(ex0 + 6f, p.y);
        }

        // ---- moving-surface modules (PlaytestMover; the motor carries the player and inherits platform velocity)

        /// <summary>
        /// A ferry platform shuttling across a wide gap. Spikes hang just above the ride height, so standing is
        /// safe but jumping while riding is not. Hop off at the far end.
        /// </summary>
        static Vector2 Ferry(Kit k, Vector2 p, float gap, float period)
        {
            float x0 = p.x + 1.5f, travel = gap - 7f;
            k.Box(x0, x0 + 4f, p.y - 0.8f, p.y, 0.35f);
            k.Move(new Vector2(travel, 0f), period);
            k.TrackRect(x0, x0 + 4f + travel, p.y - 0.8f);
            for (float x = x0 + 4.5f; x < p.x + gap - 4f; x += 3f) k.HazCircle(x, p.y + 1.85f, 0.35f);
            k.Plat(p.x + gap, p.x + gap + 6f, p.y);
            return new Vector2(p.x + gap + 6f, p.y);
        }

        /// <summary>
        /// A corridor with a spiked ceiling and hazard pistons that rise and fall out of phase. Crawl under each
        /// piston while it's up; jumping hits the ceiling.
        /// </summary>
        static Vector2 Pistons(Kit k, Vector2 p, float len, int n, float period)
        {
            float x0 = p.x + 2f, y = p.y;
            k.Plat(x0, x0 + len, y);
            k.HazBox(x0 + 0.5f, x0 + len - 0.5f, y + 3.4f, y + 3.9f);
            float spacing = (len - 6f) / Mathf.Max(1, n - 1);
            for (int i = 0; i < n; i++)
            {
                float px = x0 + 3f + i * spacing;
                k.HazBox(px - 0.45f, px + 0.45f, y + 0.02f, y + 2.2f);
                k.Move(new Vector2(0f, 2.3f), period, i * 0.25f);
            }
            return new Vector2(x0 + len, y);
        }

        /// <summary>
        /// A long rotating arm over lava with a deadly hub. Jump onto the near half, ride it round (you'll end up
        /// underneath the far half), then drop onto the lower exit platform.
        /// </summary>
        static Vector2 Windmill(Kit k, Vector2 p, float L, float degPerSec)
        {
            float cx = p.x + 2f + L * 0.5f, cy = p.y - 0.3f;
            k.Box(cx - L * 0.5f, cx + L * 0.5f, cy - 0.45f, cy + 0.45f, 0.4f);
            k.Move(Vector2.zero, 1f, 0f, degPerSec);
            k.TrackRect(cx - L * 0.5f, cx + L * 0.5f, cy - L * 0.5f);
            k.HazCircle(cx, cy, 0.55f);
            float x0 = cx + L * 0.5f + 2f;
            k.Plat(x0, x0 + 6f, p.y - 2f);
            return new Vector2(x0 + 6f, p.y - 2f);
        }

        /// <summary>Stepping stones over lava that bob up and down out of phase: time each jump.</summary>
        static Vector2 Bobbing(Kit k, Vector2 p, int n, float spacing, float r, float amp, float period)
        {
            float cx = p.x + 2.2f + r;
            for (int i = 0; i < n; i++)
            {
                float cy = p.y - amp * 0.5f;
                k.Circle(cx, cy, r);
                k.Move(new Vector2(0f, amp), period, i * 0.3f);
                if (i < n - 1) cx += spacing;
            }
            float x0 = cx + r + 2.2f;
            k.Plat(x0, x0 + 6f, p.y);
            return new Vector2(x0 + 6f, p.y);
        }

        /// <summary>An elevator beside a spiked cliff: ride it up and hop onto the ledge at the top.</summary>
        static Vector2 Elevator(Kit k, Vector2 p, float rise, float period)
        {
            float ex0 = p.x + 1.5f;
            k.Box(ex0, ex0 + 3f, p.y - 0.8f, p.y, 0.35f);
            k.Move(new Vector2(0f, rise), period);
            float lx0 = ex0 + 4.5f;
            k.Box(lx0, lx0 + 6f, p.y - 3f, p.y + rise, 0.4f);
            k.HazBox(lx0 - 0.25f, lx0 + 0.1f, p.y - 0.2f, p.y + rise - 0.45f);
            return new Vector2(lx0 + 6f, p.y + rise);
        }

        // ------------------------------------------------------------------ kit

        /// <summary>Placement helper for one course: shapes, hazards, checkpoints, signs, bounds for the lava floor.</summary>
        class Kit
        {
            readonly Transform _root;
            readonly Sprite _circle;
            Transform _group;
            GameObject _last;
            int _count, _seed;
            float _minX = float.MaxValue, _maxX = float.MinValue, _minY = float.MaxValue;

            public List<PlaytestCheckpoint> Checkpoints { get; } = new List<PlaytestCheckpoint>();

            public Kit(Transform root, Sprite circle, int seed)
            {
                _root = root;
                _circle = circle;
                _seed = seed;
                _group = root;
            }

            public void Section(string name, string sign, Vector2 signPos)
            {
                _group = A.Group(_root, $"{_root.childCount:00}_{name}");
                if (!string.IsNullOrEmpty(sign)) Sign(sign, signPos);
            }

            public void TrackRect(float x0, float x1, float y0) => Track(x0, x1, y0);

            /// <summary>Make the most recently created shape move and/or rotate (see PlaytestMover).</summary>
            public void Move(Vector2 offset, float period, float phase = 0f, float rotationSpeed = 0f)
            {
                var m = _last.AddComponent<PlaytestMover>();
                m.offset = offset;
                m.period = period;
                m.phase = phase;
                m.rotationSpeed = rotationSpeed;
            }

            void Track(float x0, float x1, float y0)
            {
                _minX = Mathf.Min(_minX, x0);
                _maxX = Mathf.Max(_maxX, x1);
                _minY = Mathf.Min(_minY, y0);
            }

            string Next(string kind) => $"{kind}_{++_count:000}";

            public void Box(float x0, float x1, float y0, float y1, float corner)
            {
                Track(x0, x1, y0);
                _last = Rect(_group, Next("Box"), (x0 + x1) * 0.5f, (y0 + y1) * 0.5f, x1 - x0, y1 - y0, corner, PlatformFill, PlatformOutline).gameObject;
            }

            public void Plat(float x0, float x1, float top) => Box(x0, x1, top - 3f, top, 0.7f);

            public void Circle(float cx, float cy, float r)
            {
                Track(cx - r, cx + r, cy - r);
                _last = PlaytestLevelBuilder.Circle(_group, Next("Circle"), cx, cy, r, ShapeFill, ShapeOutline).gameObject;
            }

            public void Blob(float cx, float cy, float r)
            {
                Track(cx - r, cx + r, cy - r);
                PlaytestLevelBuilder.Blob(_group, Next("Blob"), cx, cy, _seed + _count, r);
            }

            public void HazBox(float x0, float x1, float y0, float y1)
            {
                Track(x0, x1, y0);
                var shape = Rect(_group, Next("Hazard"), (x0 + x1) * 0.5f, (y0 + y1) * 0.5f, x1 - x0, y1 - y0,
                    Mathf.Min(x1 - x0, y1 - y0) * 0.45f, HazardFill, HazardOutline);
                MakeHazard(shape);
                _last = shape.gameObject;
            }

            public void HazCircle(float cx, float cy, float r)
            {
                Track(cx - r, cx + r, cy - r);
                var shape = PlaytestLevelBuilder.Circle(_group, Next("Hazard"), cx, cy, r, HazardFill, HazardOutline);
                MakeHazard(shape);
                _last = shape.gameObject;
            }

            public Transform Point(string name, Vector2 pos)
            {
                var t = new GameObject(name).transform;
                t.SetParent(_group, false);
                t.position = pos;
                return t;
            }

            public void Checkpoint(Vector2 p)
            {
                int index = Checkpoints.Count + 1;
                Checkpoints.Add(MakeCheckpoint(_group, _circle, $"Checkpoint_{index}", index, p.x - 2.5f, p.y + 0.9f));
            }

            public PlaytestCheckpoint Goal(Vector2 pos)
            {
                PlaytestCheckpoint goal = MakeCheckpoint(_group, _circle, "Goal", 99, pos.x, pos.y);
                goal.isGoal = true;
                goal.radius = 1.3f;
                return goal;
            }

            void Sign(string text, Vector2 pos)
            {
                var go = new GameObject("Sign");
                go.transform.SetParent(_group, false);
                go.transform.position = pos;
                go.AddComponent<PlaytestSign>().text = text;
            }

            /// <summary>Lava under the whole course (falling anywhere = back to the last checkpoint).</summary>
            public void Lava()
            {
                _group = A.Group(_root, "Lava");
                HazBox(_minX - 15f, _maxX + 15f, _minY - 8f, _minY - 6f);
            }
        }

        // ------------------------------------------------------------------ shape helpers

        static RectangleSurfaceShape Rect(Transform parent, string name, float x, float y, float w, float h, float corner,
            Color fill, Color outline)
        {
            var shape = A.Instance(A.RectPrefabPath, parent, name, x, y).GetComponent<RectangleSurfaceShape>();
            A.Configure(shape, so =>
            {
                so.FindProperty("size").vector2Value = new Vector2(w, h);
                so.FindProperty("cornerRadius").floatValue = corner;
                so.FindProperty("fillColor").colorValue = fill;
                so.FindProperty("outlineColor").colorValue = outline;
            });
            return shape;
        }

        static EllipseSurfaceShape Circle(Transform parent, string name, float x, float y, float r, Color fill, Color outline)
        {
            var shape = A.Instance(A.EllipsePrefabPath, parent, name, x, y).GetComponent<EllipseSurfaceShape>();
            A.Configure(shape, so =>
            {
                so.FindProperty("radii").vector2Value = new Vector2(r, r);
                so.FindProperty("segments").intValue = Mathf.Clamp(Mathf.RoundToInt(r * 40f), 24, 128);
                so.FindProperty("fillColor").colorValue = fill;
                so.FindProperty("outlineColor").colorValue = outline;
            });
            return shape;
        }

        static void Blob(Transform parent, string name, float x, float y, int seed, float r)
        {
            var shape = A.Instance(A.BlobPrefabPath, parent, name, x, y).GetComponent<OrganicSurfaceGenerator>();
            A.Configure(shape, so =>
            {
                so.FindProperty("seed").intValue = seed;
                so.FindProperty("radius").floatValue = r;
                so.FindProperty("radiusVariation").floatValue = 0.15f;
                so.FindProperty("asymmetry").floatValue = 0.08f;
                so.FindProperty("pointCount").intValue = 7;
                so.FindProperty("smoothing").floatValue = 0.45f;
                so.FindProperty("sampleCount").intValue = Mathf.Clamp(Mathf.RoundToInt(r * 48f), 32, 160);
                so.FindProperty("fillColor").colorValue = ShapeFill;
                so.FindProperty("outlineColor").colorValue = ShapeOutline;
            });
        }

        static void MakeHazard(SurfaceShapeBase shape)
        {
            var surfSo = new SerializedObject(shape.GetComponent<Surface2D>());
            surfSo.FindProperty("attachable").boolValue = false;
            surfSo.ApplyModifiedPropertiesWithoutUndo();
            shape.gameObject.AddComponent<PlaytestHazard>();
            var so = new SerializedObject(shape);
            so.FindProperty("sortingOrder").intValue = 1;
            so.ApplyModifiedPropertiesWithoutUndo();
            shape.Regenerate();
        }

        static PlaytestCheckpoint MakeCheckpoint(Transform parent, Sprite circle, string name, int index, float x, float y)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(x, y, 0f);
            var cp = go.AddComponent<PlaytestCheckpoint>();
            cp.index = index;

            SpriteRenderer ring = SpriteChild(go, "Ring", circle, cp.radius * 2f, 2);
            SpriteRenderer core = SpriteChild(go, "Core", circle, 0.35f, 3);
            var so = new SerializedObject(cp);
            so.FindProperty("ring").objectReferenceValue = ring;
            so.FindProperty("core").objectReferenceValue = core;
            so.ApplyModifiedPropertiesWithoutUndo();
            return cp;
        }

        static SpriteRenderer SpriteChild(GameObject parent, string name, Sprite sprite, float scale, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            go.transform.localScale = new Vector3(scale, scale, 1f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = order;
            return sr;
        }
    }

    /// <summary>Builds the playtest level automatically (once per Editor session) if it's missing or out of date.</summary>
    [InitializeOnLoad]
    static class PlaytestLevelAutoBuild
    {
        const string SessionKey = "Televised.PlaytestLevel.AutoBuildAttempted." + PlaytestLevelBuilder.VersionMarker;

        static PlaytestLevelAutoBuild()
        {
            EditorApplication.delayCall += () =>
            {
                if (Application.isBatchMode || EditorApplication.isPlayingOrWillChangePlaymode) return;
                if (PlaytestLevelBuilder.SceneIsCurrent() || SessionState.GetBool(SessionKey, false)) return;
                if (!File.Exists(MovementSandboxBuilder.ScenePath)) return; // let the sandbox auto-build go first
                SessionState.SetBool(SessionKey, true);
                try { PlaytestLevelBuilder.Build(false); }
                catch (System.Exception e) { Debug.LogException(e); }
            };
        }
    }
}
