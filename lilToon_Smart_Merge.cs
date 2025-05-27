// author: kissa_
// source: https://github.com/Haor/lilToon_Smart_Merge
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System.IO;

namespace LilToonTools
{
    public class LilToonParameterMerger : EditorWindow
    {
        // ---------- optional always-copy / always-skip ----------
        private static readonly HashSet<string> forceCopy = new(); // e.g. { "_Cull" }
        private static readonly HashSet<string> skipCopy  = new(); // permanent skip list

        private const string CloneSuffix = "_mergedClone";
        private const string CloneLabel  = "SmartMergeClone";
        private const string UserDataKey = "OriginalMatPath";

        // ---------- GUI state ----------
        private GameObject avatarRoot;
        private Material   masterMat;
        private List<Material> mats = new();
        private List<bool>     sel  = new();
        private Vector2 scrollMat, scrollLog;
        private readonly System.Text.StringBuilder log = new();

        // ---------- menu ----------
        [MenuItem("Tools/lilToon/lilToon Smart Merge")]
        private static void Open() => GetWindow<LilToonParameterMerger>("lilToon Smart Merge");

        // ---------- GUI ----------
        private void OnGUI()
        {
            avatarRoot = (GameObject)EditorGUILayout.ObjectField("Avatar Root", avatarRoot, typeof(GameObject), true);
            masterMat  = (Material)  EditorGUILayout.ObjectField("Master Material", masterMat, typeof(Material), false);

            if (GUILayout.Button("Refresh Materials")) RefreshMatList();
            DrawMatTable();

            // selection helpers
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Select All"))   for (int i = 0; i < sel.Count; i++) sel[i] = true;
            if (GUILayout.Button("Invert"))       for (int i = 0; i < sel.Count; i++) sel[i] = !sel[i];
            if (GUILayout.Button("Deselect All")) for (int i = 0; i < sel.Count; i++) sel[i] = false;
            EditorGUILayout.EndHorizontal();

            GUILayout.Space(4);

            // main actions
            GUI.enabled = avatarRoot && sel.Any(s => s);
            if (GUILayout.Button("Backup (clone & re-link)", GUILayout.Height(24))) DoBackup();
            GUI.enabled = avatarRoot && masterMat;
            if (GUILayout.Button("Merge Parameters (non-destructive)", GUILayout.Height(24))) DoMerge();
            if (GUILayout.Button("Overwrite (full rewrite, keep textures)", GUILayout.Height(24))) DoOverwrite();
            GUI.enabled = avatarRoot;
            if (GUILayout.Button("Restore (delete clones)", GUILayout.Height(24))) DoRestore();
            GUI.enabled = true;

            // help box (bilingual)
            GUILayout.Space(8);
            EditorGUILayout.HelpBox(
                "Usage:\n" +
                "1. Backup: create one clone per selected material and redirect the avatar to use it.\n" +
                "2. Merge Parameters: copy only non-texture properties that are explicitly set in the master material.\n" +
                "3. Overwrite: force-copy every non-texture property from the master material (textures stay intact).\n" +
                "4. Restore: delete all clones and relink the avatar to the original materials.\n\n" +
                "使用步骤：\n" +
                "1. 点击 Backup 备份并重定向材质。\n" +
                "2. 点击 Merge Parameters 合并（保留贴图、空值不覆盖）。\n" +
                "3. 点击 Overwrite 强制覆盖所有非贴图参数（保留贴图）。\n" +
                "4. 点击 Restore 删除克隆并恢复原材质。", MessageType.Info);

            // log
            GUILayout.Space(8);
            GUILayout.Label("Log", EditorStyles.boldLabel);
            scrollLog = EditorGUILayout.BeginScrollView(scrollLog, GUILayout.Height(120));
            EditorGUILayout.TextArea(log.ToString(), GUILayout.ExpandHeight(true));
            EditorGUILayout.EndScrollView();
            if (GUILayout.Button("Clear Log")) log.Clear();
        }

        // ---------- material table ----------
        private void DrawMatTable()
        {
            GUILayout.Space(4);
            GUILayout.Label($"Materials in Avatar ({mats.Count})", EditorStyles.boldLabel);
            scrollMat = EditorGUILayout.BeginScrollView(scrollMat, GUILayout.Height(200));

            for (int i = 0; i < mats.Count; i++)
            {
                var m = mats[i];
                EditorGUILayout.BeginHorizontal();
                sel[i] = EditorGUILayout.Toggle(sel[i], GUILayout.Width(18));
                EditorGUILayout.ObjectField(m, typeof(Material), false);
                GUILayout.Label(m.shader.name, GUILayout.MinWidth(150));
                if (IsClone(m)) GUILayout.Label("[clone]", GUILayout.Width(50));
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndScrollView();
        }

        private void RefreshMatList()
        {
            mats.Clear();
            sel.Clear();
            if (!avatarRoot) return;

            var set = new HashSet<Material>();
            foreach (var r in avatarRoot.GetComponentsInChildren<Renderer>(true))
                foreach (var m in r.sharedMaterials)
                    if (m) set.Add(m);

            mats = set.ToList();
            sel  = Enumerable.Repeat(true, mats.Count).ToList();
            Log($"Found {mats.Count} materials.");
        }

        // ---------- backup ----------
        private void DoBackup()
        {
            var originals = mats.Where((m, idx) => sel[idx] && !IsClone(m)).ToArray();
            if (originals.Length == 0) { Log("Nothing to backup."); return; }

            var map = new Dictionary<Material, Material>(); // ori -> clone
            foreach (var ori in originals)
            {
                string oriPath = AssetDatabase.GetAssetPath(ori);
                string dir     = string.IsNullOrEmpty(oriPath) ? "Assets" : Path.GetDirectoryName(oriPath);
                string path    = AssetDatabase.GenerateUniqueAssetPath(Path.Combine(dir, ori.name + CloneSuffix + ".mat"));

                var clone = new Material(ori) { name = ori.name + CloneSuffix };
                AssetDatabase.CreateAsset(clone, path);
                AssetDatabase.SetLabels(clone, new[] { CloneLabel });

                var imp = AssetImporter.GetAtPath(path);
                imp.userData = oriPath;
                imp.SaveAndReimport();

                map.Add(ori, clone);
            }
            AssetDatabase.SaveAssets();
            Log($"Cloned {map.Count} materials.");

            // redirect
            var rds = avatarRoot.GetComponentsInChildren<Renderer>(true);
            Undo.RecordObjects(rds, "SmartMerge Replace Materials");
            int slots = 0;
            foreach (var rd in rds)
            {
                var arr = rd.sharedMaterials;
                bool chg = false;
                for (int i = 0; i < arr.Length; i++)
                    if (map.TryGetValue(arr[i], out var newMat)) { arr[i] = newMat; chg = true; slots++; }
                if (chg) rd.sharedMaterials = arr;
            }
            Log($"Redirected {slots} material slots to clones.");
            RefreshMatList();
            MarkSceneDirty();
        }

        // ---------- merge ----------
        private void DoMerge()
        {
            var targets = mats.Where((m, idx) => sel[idx] && IsClone(m)).ToArray();
            if (targets.Length == 0) { Log("No clone material selected for merge."); return; }

            Undo.RecordObjects(targets, "SmartMerge Merge");
            foreach (var t in targets)
            {
                CopySelective(masterMat, t);
                RefreshKeywords(t);
                EditorUtility.SetDirty(t);
            }
            AssetDatabase.SaveAssets();
            Log($"Merged parameters into {targets.Length} clone materials.");
        }

        // ---------- overwrite ----------
        private void DoOverwrite()
        {
            var targets = mats.Where((m, idx) => sel[idx] && IsClone(m)).ToArray();
            if (targets.Length == 0) { Log("No clone material selected for overwrite."); return; }

            Undo.RecordObjects(targets, "SmartMerge Overwrite");
            foreach (var t in targets)
            {
                CopyAllNoTex(masterMat, t);      // NEW: overwrite (keep textures)
                RefreshKeywords(t);
                EditorUtility.SetDirty(t);
            }
            AssetDatabase.SaveAssets();
            Log($"Overwrote {targets.Length} clone materials (textures kept).");
        }

        // ---------- restore ----------
        private void DoRestore()
        {
            var cloneMats = mats.Where(IsClone).ToList();
            if (cloneMats.Count == 0) { Log("No clone material found, nothing to restore."); return; }

            var map = new Dictionary<Material, Material>(); // clone -> original
            foreach (var c in cloneMats)
            {
                string p = AssetDatabase.GetAssetPath(c);
                var imp  = AssetImporter.GetAtPath(p);
                var ori  = imp && !string.IsNullOrEmpty(imp.userData) ?
                           AssetDatabase.LoadAssetAtPath<Material>(imp.userData) : null;
                if (ori) map[c] = ori;
            }
            if (map.Count == 0) { Log("Original material paths missing, restore aborted."); return; }

            // redirect back
            var rds = avatarRoot.GetComponentsInChildren<Renderer>(true);
            Undo.RecordObjects(rds, "SmartMerge Restore Materials");
            int slots = 0;
            foreach (var rd in rds)
            {
                var a = rd.sharedMaterials;
                bool chg = false;
                for (int i = 0; i < a.Length; i++)
                    if (map.TryGetValue(a[i], out var ori)) { a[i] = ori; chg = true; slots++; }
                if (chg) rd.sharedMaterials = a;
            }

            foreach (var c in map.Keys)
                AssetDatabase.DeleteAsset(AssetDatabase.GetAssetPath(c));
            AssetDatabase.SaveAssets();
            Log($"Restored {slots} slots and deleted {map.Count} clone files.");
            RefreshMatList();
            MarkSceneDirty();
        }

        // ---------- helpers ----------
        private static bool IsClone(Material m) => AssetDatabase.GetLabels(m).Contains(CloneLabel);

        // Merge: copy non-texture properties that are non-default
        private static void CopySelective(Material src, Material dst)
        {
            int n = ShaderUtil.GetPropertyCount(src.shader);
            for (int i = 0; i < n; i++)
            {
                string name = ShaderUtil.GetPropertyName(src.shader, i);
                if (skipCopy.Contains(name) || !dst.HasProperty(name)) continue;

                var type = ShaderUtil.GetPropertyType(src.shader, i);
                if (type == ShaderUtil.ShaderPropertyType.TexEnv) continue;

                bool copy = forceCopy.Contains(name);
                if (!copy)
                {
                    if (type == ShaderUtil.ShaderPropertyType.Color)
                        copy = src.GetColor(name) != Color.white && src.GetColor(name) != Color.clear;
                    else if (type == ShaderUtil.ShaderPropertyType.Vector)
                        copy = src.GetVector(name).sqrMagnitude > 1e-6f;
                    else if (type == ShaderUtil.ShaderPropertyType.Float ||
                             type == ShaderUtil.ShaderPropertyType.Range)
                        copy = Mathf.Abs(src.GetFloat(name)) > 1e-4f;
                }
                if (copy) CopyVal(src, dst, name, type);
            }
        }

        // Overwrite: copy every non-texture property (force)
        private static void CopyAllNoTex(Material src, Material dst)
        {
            int n = ShaderUtil.GetPropertyCount(src.shader);
            for (int i = 0; i < n; i++)
            {
                string name = ShaderUtil.GetPropertyName(src.shader, i);
                if (!dst.HasProperty(name)) continue;
                var type = ShaderUtil.GetPropertyType(src.shader, i);
                if (type == ShaderUtil.ShaderPropertyType.TexEnv) continue; // keep textures
                CopyVal(src, dst, name, type);
            }
        }

        private static void CopyVal(Material src, Material dst, string n, ShaderUtil.ShaderPropertyType t)
        {
            if (t == ShaderUtil.ShaderPropertyType.Color)         dst.SetColor (n, src.GetColor (n));
            else if (t == ShaderUtil.ShaderPropertyType.Vector)   dst.SetVector(n, src.GetVector(n));
            else if (t == ShaderUtil.ShaderPropertyType.Float ||
                     t == ShaderUtil.ShaderPropertyType.Range)    dst.SetFloat (n, src.GetFloat (n));
        }

        private static void RefreshKeywords(Material m)
        {
            foreach (var q in new[] { "lilToon.Utils.lilMaterialUtils, lilToon", "lilToon.lilMaterialUtils, lilToon" })
            {
                var t = System.Type.GetType(q);
                if (t == null) continue;
                var mi = t.GetMethod("SetupMaterialWithShader",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                if (mi != null) { mi.Invoke(null, new object[] { m.shader, m }); return; }
            }
        }

        private void MarkSceneDirty()
        {
            if (!EditorApplication.isPlaying)
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(avatarRoot.scene);
        }

        private void Log(string msg)
        {
            Debug.Log("[SmartMerge] " + msg);
            log.AppendLine(msg);
            Repaint();
        }
    }
}
#endif
