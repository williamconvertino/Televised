using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Televised.Prototyping.Weapons
{
    /// <summary>
    /// Saves / loads every weapon's settings plus the impact, player-health and global enemy tuning to one JSON file
    /// (Weapons/Settings/CombatTuning.json), so a Play Mode tuning session can be kept and diffed in git.
    /// </summary>
    public static class CombatTuningStore
    {
        [Serializable]
        class Entry
        {
            public string key;
            public string json;
        }

        [Serializable]
        class TuningFile
        {
            public List<Entry> entries = new List<Entry>();
        }

        const string AutoLoadPref = "Televised.Prototyping.CombatAutoLoadTuning";

        public static string FilePath => Path.Combine(Application.dataPath, "Prototyping/Weapons/Settings/CombatTuning.json");
        public static bool FileExists => File.Exists(FilePath);

        public static bool AutoLoad
        {
            get => PlayerPrefs.GetInt(AutoLoadPref, 1) == 1;
            set => PlayerPrefs.SetInt(AutoLoadPref, value ? 1 : 0);
        }

        static IEnumerable<(string key, object target)> Targets(WeaponController weapons, EnemyManager enemies,
            BodyImpactDamage impact, PlayerHealth player)
        {
            if (weapons != null)
                foreach (PrototypeWeapon w in weapons.Weapons) yield return ("weapon:" + w.DisplayName, w.Settings);
            if (enemies != null) yield return ("enemies", enemies);
            if (impact != null) yield return ("impact", impact);
            if (player != null) yield return ("player", player);
        }

        public static string Save(WeaponController weapons, EnemyManager enemies, BodyImpactDamage impact, PlayerHealth player)
        {
            var file = new TuningFile();
            foreach (var (key, target) in Targets(weapons, enemies, impact, player))
                file.entries.Add(new Entry { key = key, json = JsonUtility.ToJson(target) });
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
            File.WriteAllText(FilePath, JsonUtility.ToJson(file, true));
#if UNITY_EDITOR
            UnityEditor.AssetDatabase.Refresh();
#endif
            return $"Saved {file.entries.Count} entries to {FilePath}";
        }

        public static string Load(WeaponController weapons, EnemyManager enemies, BodyImpactDamage impact, PlayerHealth player)
        {
            if (!FileExists) return "No saved tuning file yet.";
            TuningFile file;
            try
            {
                file = JsonUtility.FromJson<TuningFile>(File.ReadAllText(FilePath));
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                return "Couldn't read the tuning file (see console).";
            }
            var byKey = new Dictionary<string, string>();
            foreach (Entry e in file.entries) byKey[e.key] = e.json;
            int n = 0;
            foreach (var (key, target) in Targets(weapons, enemies, impact, player))
            {
                if (!byKey.TryGetValue(key, out string json)) continue;
                JsonUtility.FromJsonOverwrite(json, target);
                n++;
            }
            return $"Loaded tuning for {n} systems.";
        }
    }
}
