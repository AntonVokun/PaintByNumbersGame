#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Editor-only, opt-in authoring tool. Never runs on import or in a Player.
public static class LevelCreationTools
{
    [Serializable] public class Point { public float x, y; }
    [Serializable] public class PathData { public Point[] points; }
    [Serializable] public class Sample { public int x, y, pixels; }
    [Serializable] public class Group
    {
        public int id, regions;
        public string file, sha256, source;
        public int[] color;
        public Sample[] samples;
        public PathData[] paths;
    }
    [Serializable] public class Manifest
    {
        public int schema, width, height, colors, regions;
        public float pixelsPerUnit;
        public string sceneName, displayName;
        public string outlineFile, colorPreviewFile, bwPreviewFile, hitmaskFile;
        public string geometryFile;
        public int hitmaskBytesPerPixel;
        public Group[] groups;
    }
    [Serializable] public class Geometry { public Group[] groups; }
    public const string Template = "Assets/_Project/Scenes/Level_002_New.unity";
    public const string Menu = "Assets/_Project/Scenes/MainMenu.unity";

    public static Manifest Read(string folder)
    {
        string manifestPath = "ArtSources/" + System.IO.Path.GetFileName(folder) + "/level_manifest.json";
        if (!File.Exists(manifestPath)) manifestPath = folder + "/level.json";
        var m = JsonUtility.FromJson<Manifest>(File.ReadAllText(manifestPath));
        if (string.IsNullOrEmpty(m.outlineFile)) m.outlineFile="outlines.png";
        if (string.IsNullOrEmpty(m.colorPreviewFile)) m.colorPreviewFile="Preview_Color.png";
        if (string.IsNullOrEmpty(m.bwPreviewFile)) m.bwPreviewFile="Preview_BW.png";
        if (string.IsNullOrEmpty(m.hitmaskFile)) m.hitmaskFile=folder+"/hitmask.bytes";
        if (!string.IsNullOrEmpty(m.geometryFile))
        {
            var geometry=JsonUtility.FromJson<Geometry>(File.ReadAllText(m.geometryFile));
            foreach(var g in m.groups)
            {
                var shape=geometry.groups.Single(v=>v.id==g.id);
                g.paths=shape.paths;g.samples=shape.samples;
            }
        }
        if (m.schema != 1 || m.groups == null || m.groups.Length != m.colors || m.colors < 1)
            throw new InvalidOperationException("Invalid level manifest");
        if (m.groups.Select(g => g.id).Distinct().Count() != m.colors)
            throw new InvalidOperationException("Duplicate color IDs");
        if(m.hitmaskBytesPerPixel==0)m.hitmaskBytesPerPixel=1;
        return m;
    }

    public static string ImportCreateValidate(string folder, string previousSceneName, bool replaceExisting=false)
    {
        // One CLI entry, same checks and mutation boundaries as the original three commands.
        var imported=Import(folder);
        var created=Create(folder,previousSceneName,replaceExisting);
        EditorSceneManager.OpenScene("Assets/_Project/Scenes/"+Read(folder).sceneName+".unity");
        var pixels=ValidatePixels(folder,4);
        return imported+"; "+created+"; "+pixels;
    }

    public static string Import(string folder)
    {
        var m = Read(folder);
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        foreach (string file in m.groups.Select(g => g.file).Concat(new[] { m.outlineFile, m.colorPreviewFile, m.bwPreviewFile }))
        {
            string path = folder + "/" + file;
            var t = AssetImporter.GetAtPath(path) as TextureImporter;
            if (t == null) throw new InvalidOperationException("Missing texture: " + path);
            string settingsBefore = EditorJsonUtility.ToJson(t);
            t.textureType = TextureImporterType.Sprite;
            t.spriteImportMode = SpriteImportMode.Single;
            t.spritePixelsPerUnit = m.pixelsPerUnit;
            t.spritePivot = new Vector2(0.5f, 0.5f);
            t.alphaSource = TextureImporterAlphaSource.FromInput;
            t.alphaIsTransparency = true;
            t.mipmapEnabled = false;
            t.npotScale = TextureImporterNPOTScale.None;
            t.wrapMode = TextureWrapMode.Clamp;
            t.filterMode = FilterMode.Bilinear;
            t.isReadable = false;
            t.maxTextureSize = Mathf.NextPowerOfTwo(Mathf.Max(m.width, m.height));
            t.textureCompression = TextureImporterCompression.Uncompressed;
            var settings = new TextureImporterSettings();
            t.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.Center;
            settings.spriteMeshType = SpriteMeshType.FullRect;
            t.SetTextureSettings(settings);
            t.SetPlatformTextureSettings(new TextureImporterPlatformSettings {
                name = "Android", overridden = true, maxTextureSize = t.maxTextureSize,
                format = TextureImporterFormat.RGBA32, textureCompression = TextureImporterCompression.Uncompressed
            });
            if (settingsBefore != EditorJsonUtility.ToJson(t)) t.SaveAndReimport();
        }
        return $"Imported {m.colors + 3} full-canvas sprites, {m.width}x{m.height}, PPU {m.pixelsPerUnit}";
    }

    static T Find<T>(Scene scene) where T : Component
    {
        return scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<T>(true)).Single();
    }
    static Transform Root(Scene s, string name) { return s.GetRootGameObjects().Single(r => r.name == name).transform; }
    static Sprite SpriteAt(string folder, string file)
    {
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(folder + "/" + file);
        if (sprite == null) throw new InvalidOperationException("Missing sprite: " + file);
        return sprite;
    }

    // Caller must have made a backup and inspected Git, Console and unsaved scenes.
    public static string Create(string folder, string previousSceneName, bool replaceExisting = false)
    {
        if (EditorApplication.isPlaying || EditorApplication.isCompiling)
            throw new InvalidOperationException("Stop Play Mode and finish compilation first");
        for (int i = 0; i < SceneManager.sceneCount; i++)
            if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Unsaved scene; refusing to replace it");
        var m = Read(folder);
        string target = "Assets/_Project/Scenes/" + m.sceneName + ".unity";
        if (File.Exists(target) && !replaceExisting)
        {
            EditorSceneManager.OpenScene(target, OpenSceneMode.Single);
            return "Existing scene retained; " + Validate(folder) + "; " + ValidateIntegration(folder, previousSceneName);
        }
        if (replaceExisting && !File.Exists(target)) throw new InvalidOperationException("Replacement requires an existing scene and external backup");
        if (!File.Exists(target) && !AssetDatabase.CopyAsset(Template, target)) throw new InvalidOperationException("Template copy failed");
        var scene = EditorSceneManager.OpenScene(target, OpenSceneMode.Single);
        var levelRoot = Root(scene, "LevelRoot");
        var art = levelRoot.Find("Art");
        var zonesRoot = art.Find("Zones");
        var oldZones = zonesRoot.GetComponentsInChildren<PaintZone>(true).OrderBy(z => z.name).ToList();
        var zones = new List<PaintZone>();
        for (int index = 0; index < m.groups.Length; index++)
        {
            var g = m.groups[index];
            PaintZone z = index < oldZones.Count ? oldZones[index] : UnityEngine.Object.Instantiate(oldZones[0], zonesRoot);
            z.name = "Zone_" + g.id.ToString("D3");
            z.transform.localPosition = Vector3.zero;
            z.transform.localRotation = Quaternion.identity;
            z.transform.localScale = Vector3.one;
            z.colorId = g.id;
            var sr = z.GetComponent<SpriteRenderer>();
            sr.sprite = SpriteAt(folder, g.file);
            sr.color = Color.white;
            sr.sortingOrder = 0;
            var col = z.GetComponent<PolygonCollider2D>();
            col.offset = Vector2.zero;
            var expectedPaths = g.paths.Select(path => path.points.Select(v => new Vector2(
                (v.x - m.width * 0.5f) / m.pixelsPerUnit,
                (m.height * 0.5f - v.y) / m.pixelsPerUnit)).ToArray()).ToArray();
            bool pathsMatch = col.pathCount == expectedPaths.Length &&
                expectedPaths.Select((path, i) => col.GetPath(i).SequenceEqual(path)).All(v => v);
            if (!pathsMatch)
            {
                // Avoid rebuilding unchanged collider data, especially large multi-island groups.
                bool colliderEnabled = col.enabled;
                col.enabled = false;
                try
                {
                    col.pathCount = expectedPaths.Length;
                    for (int p = 0; p < expectedPaths.Length; p++) col.SetPath(p, expectedPaths[p]);
                }
                finally { col.enabled = colliderEnabled; }
            }
            EditorUtility.SetDirty(z);
            zones.Add(z);
        }
        // These are only surplus GameObjects in the NEW scene, never assets.
        foreach (var z in oldZones.Skip(m.colors)) UnityEngine.Object.DestroyImmediate(z.gameObject);
        art.Find("Outlines").GetComponentInChildren<SpriteRenderer>().sprite = SpriteAt(folder, m.outlineFile);
        var paper = levelRoot.Find("Background/WhitePaper").GetComponent<SpriteRenderer>();
        paper.transform.localPosition = art.localPosition;
        paper.transform.localScale = new Vector3(
            m.width / m.pixelsPerUnit * art.localScale.x * 1.12f / paper.sprite.bounds.size.x,
            m.height / m.pixelsPerUnit * art.localScale.y * 1.12f / paper.sprite.bounds.size.y, 1f);

        var controller = Find<PaintController>(scene);
        var oldButtons = controller.paletteButtons.OrderBy(b => b.colorId).ToList();
        var buttons = new List<SelectColor>();
        for (int index = 0; index < m.colors; index++)
        {
            var g = m.groups[index];
            var b = index < oldButtons.Count ? oldButtons[index] : UnityEngine.Object.Instantiate(oldButtons[0], oldButtons[0].transform.parent);
            b.name = "Button " + g.id;
            b.colorId = g.id;
            b.transform.SetSiblingIndex(index);
            b.GetComponent<UnityEngine.UI.Image>().color = new Color(g.color[0]/255f, g.color[1]/255f, g.color[2]/255f);
            var uiButton = b.GetComponent<UnityEngine.UI.Button>();
            while (uiButton.onClick.GetPersistentEventCount() > 0) UnityEventTools.RemovePersistentListener(uiButton.onClick, 0);
            UnityEventTools.AddPersistentListener(uiButton.onClick, b.Select);
            buttons.Add(b);
        }
        foreach (var b in oldButtons.Skip(m.colors)) UnityEngine.Object.DestroyImmediate(b.gameObject);
        controller.zones = zones.ToArray();
        controller.paletteButtons = buttons.ToArray();
        controller.nextLevelSceneName = "MainMenu";
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        string levelValidation = Validate(folder);

        var previous = EditorSceneManager.OpenScene("Assets/_Project/Scenes/" + previousSceneName + ".unity", OpenSceneMode.Single);
        var previousController = Find<PaintController>(previous);
        if (previousController.nextLevelSceneName != m.sceneName)
        {
            previousController.nextLevelSceneName = m.sceneName;
            EditorSceneManager.MarkSceneDirty(previous);
            EditorSceneManager.SaveScene(previous);
        }

        var menu = EditorSceneManager.OpenScene(Menu, OpenSceneMode.Single);
        var cards = menu.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<LevelCardUI>(true)).ToList();
        var matches = cards.Where(c => new SerializedObject(c).FindProperty("levelSceneName").stringValue == m.sceneName).ToArray();
        if (matches.Length > 1) throw new InvalidOperationException("Duplicate menu cards; review before replacing");
        var last = cards.OrderBy(c => c.name).Last();
        var card = matches.Length == 1 ? matches[0] : UnityEngine.Object.Instantiate(last, last.transform.parent);
        card.name = "LevelCard_" + m.sceneName.Replace("Level_", "").Replace("_New", "");
        card.transform.SetAsLastSibling();
        var so = new SerializedObject(card);
        so.FindProperty("levelSceneName").stringValue = m.sceneName;
        so.FindProperty("levelDisplayName").stringValue = m.displayName;
        so.FindProperty("blackWhitePreview").objectReferenceValue = SpriteAt(folder, m.bwPreviewFile);
        so.FindProperty("colorPreview").objectReferenceValue = SpriteAt(folder, m.colorPreviewFile);
        so.ApplyModifiedPropertiesWithoutUndo();
        card.transform.Find("Preview").GetComponent<UnityEngine.UI.Image>().sprite = SpriteAt(folder, m.bwPreviewFile);
        var menuController = new SerializedObject(Find<MainMenuController>(menu));
        var names = menuController.FindProperty("levelSceneNames");
        var existingNames = Enumerable.Range(0, names.arraySize).Select(i => names.GetArrayElementAtIndex(i).stringValue).ToList();
        if (!existingNames.Contains(m.sceneName)) { names.arraySize++; names.GetArrayElementAtIndex(names.arraySize-1).stringValue = m.sceneName; }
        if (!cards.Contains(card)) cards.Add(card);
        if (replaceExisting)
        {
            foreach (var item in cards)
            {
                var fields = new SerializedObject(item);
                var match = System.Text.RegularExpressions.Regex.Match(fields.FindProperty("levelSceneName").stringValue, @"^Level_(\d+)_New$");
                if (!match.Success) throw new InvalidOperationException("Unexpected menu scene name");
                string title = "Уровень " + int.Parse(match.Groups[1].Value);
                fields.FindProperty("levelDisplayName").stringValue = title;
                var label = fields.FindProperty("levelNameText").objectReferenceValue as TMPro.TMP_Text;
                fields.ApplyModifiedPropertiesWithoutUndo();
                if (label != null) label.text = title;
            }
        }
        var assigned = menuController.FindProperty("levelCards");
        assigned.arraySize = cards.Count;
        for (int i = 0; i < cards.Count; i++) assigned.GetArrayElementAtIndex(i).objectReferenceValue = cards[i];
        menuController.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.MarkSceneDirty(menu);
        EditorSceneManager.SaveScene(menu);
        var buildScenes = EditorBuildSettings.scenes.ToList();
        if (!buildScenes.Any(s => s.path == target))
            buildScenes.Insert(buildScenes.FindLastIndex(s => s.enabled)+1, new EditorBuildSettingsScene(target, true));
        if (!EditorBuildSettings.scenes.Select(s => s.path + ":" + s.enabled).SequenceEqual(buildScenes.Select(s => s.path + ":" + s.enabled)))
            EditorBuildSettings.scenes = buildScenes.ToArray();
        if (!replaceExisting) AssetDatabase.SaveAssets();
        return $"{levelValidation}; menu card updated without duplication, reset registration, {previousSceneName} -> {m.sceneName} -> MainMenu, enabled build scene";
    }

    public static string Validate(string folder)
    {
        var m = Read(folder);
        var scene = SceneManager.GetActiveScene();
        if (scene.name != m.sceneName) throw new InvalidOperationException("Open " + m.sceneName + " before validation");
        var pc = Find<PaintController>(scene);
        var errors = new List<string>();
        if (pc.zones.Length != m.colors || pc.paletteButtons.Length != m.colors) errors.Add("Controller counts");
        if (pc.winPanel == null || pc.winPanel.activeSelf) errors.Add("WinPanel reference/default state");
        if (Find<UnityEngine.EventSystems.EventSystem>(scene) == null) errors.Add("EventSystem");
        Physics2D.SyncTransforms();
        foreach (var g in m.groups)
        {
            var z = pc.zones.SingleOrDefault(v => v != null && v.colorId == g.id);
            var b = pc.paletteButtons.SingleOrDefault(v => v != null && v.colorId == g.id);
            if (z == null || b == null) { errors.Add("Missing color " + g.id); continue; }
            var sprite = z.GetComponent<SpriteRenderer>().sprite;
            if (sprite == null || sprite.rect.width != m.width || sprite.rect.height != m.height ||
                sprite.pivot != new Vector2(m.width*0.5f,m.height*0.5f) || sprite.pixelsPerUnit != m.pixelsPerUnit)
                errors.Add("Sprite canvas/pivot " + g.id);
            if(AssetDatabase.GetAssetPath(sprite)!=folder+"/"+g.file)errors.Add("Wrong sprite reference "+g.id);
            var tint=z.GetComponent<SpriteRenderer>().color;
            if(tint!=Color.white)errors.Add("Unexpected sprite tint "+g.id);
            var color=b.GetComponent<UnityEngine.UI.Image>().color;
            if(Mathf.Abs(color.r-g.color[0]/255f)>0.001f||Mathf.Abs(color.g-g.color[1]/255f)>0.001f||Mathf.Abs(color.b-g.color[2]/255f)>0.001f)
                errors.Add("Wrong palette color "+g.id);
            var col = z.GetComponent<PolygonCollider2D>();
            if (col == null || col.pathCount != g.paths.Length) { errors.Add("Collider " + g.id); continue; }
            foreach (var sample in g.samples)
            {
                var local = new Vector3((sample.x+0.5f-m.width*0.5f)/m.pixelsPerUnit,
                    (m.height*0.5f-sample.y-0.5f)/m.pixelsPerUnit,0);
                var point = z.transform.TransformPoint(local);
                if (!col.OverlapPoint(point) && !ContainsWithPrecisionTolerance(col, point)) errors.Add($"Unclickable region {g.id} at {sample.x},{sample.y}");
                foreach (var other in pc.zones.Where(v => v != z))
                    if (other.GetComponent<PolygonCollider2D>().OverlapPoint(point)) errors.Add("Cross-color collider " + g.id + "/" + other.colorId);
            }
            var button = b.GetComponent<UnityEngine.UI.Button>();
            if (button.onClick.GetPersistentEventCount() != 1 || button.onClick.GetPersistentTarget(0) != b || button.onClick.GetPersistentMethodName(0) != "Select")
                errors.Add("Palette event " + g.id);
        }
        foreach (var root in scene.GetRootGameObjects())
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject) > 0) errors.Add("Missing script " + t.name);
        if (errors.Count > 0) throw new InvalidOperationException(string.Join("; ", errors.Take(20)));
        return $"STATIC PASS: {m.colors} colors, {m.regions} regions, matching canvas/pivot/PPU, isolated collider samples, references and palette events";
    }

    public static string ValidatePixels(string folder, int stride = 4)
    {
        var m = Read(folder);
        var pc = Find<PaintController>(SceneManager.GetActiveScene());
        var mask = File.ReadAllBytes(m.hitmaskFile);
        var colliders = pc.zones.ToDictionary(z => z.colorId, z => z.GetComponent<PolygonCollider2D>());
        var t = pc.zones[0].transform;
        Physics2D.SyncTransforms();
        int tested = 0, missed = 0, cross = 0, seams = 0;
        for (int y = 0; y < m.height; y += stride)
        for (int x = 0; x < m.width; x += stride)
        {
            int offset=(y*m.width+x)*m.hitmaskBytesPerPixel;
            int id=mask[offset];
            if(m.hitmaskBytesPerPixel==2)id|=mask[offset+1]<<8;
            if (id == 0) continue;
            var point = t.TransformPoint(new Vector3((x+0.5f-m.width*0.5f)/m.pixelsPerUnit,
                (m.height*0.5f-y-0.5f)/m.pixelsPerUnit,0));
            tested++;
            if (!colliders[id].OverlapPoint(point))
            {
                if (ContainsWithPrecisionTolerance(colliders[id], point)) seams++;
                else missed++;
            }
            foreach (var entry in colliders)
                if (entry.Key != id && entry.Value.OverlapPoint(point)) cross++;
        }
        if (missed > 0 || cross > 0) throw new InvalidOperationException($"Collider raster check: {tested} tested, {missed} missed, {cross} wrong-color hits");
        return $"Collider raster PASS: {tested} solid pixel samples, stride {stride}, zero missed/wrong-color hits; {seams} internal triangle-edge precision cases (<0.002 pixel)";
    }

    static bool ContainsWithPrecisionTolerance(PolygonCollider2D col, Vector2 p)
    {
        // Native point queries can miss a point exactly on a shared triangle edge.
        const float e=0.00001f;
        return col.OverlapPoint(p+new Vector2(e,0)) && col.OverlapPoint(p-new Vector2(e,0)) &&
               col.OverlapPoint(p+new Vector2(0,e)) && col.OverlapPoint(p-new Vector2(0,e));
    }

    public static string ValidateIntegration(string folder, string previousSceneName)
    {
        var m=Read(folder);
        string target="Assets/_Project/Scenes/"+m.sceneName+".unity";
        var menu=EditorSceneManager.OpenScene(Menu,OpenSceneMode.Single);
        var cards=menu.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<LevelCardUI>(true)).ToArray();
        var matched=cards.Where(c=>new SerializedObject(c).FindProperty("levelSceneName").stringValue==m.sceneName).ToArray();
        if(matched.Length!=1)throw new InvalidOperationException("Expected exactly one menu card");
        var card=new SerializedObject(matched[0]);
        if(card.FindProperty("blackWhitePreview").objectReferenceValue!=SpriteAt(folder,m.bwPreviewFile)||
           card.FindProperty("colorPreview").objectReferenceValue!=SpriteAt(folder,m.colorPreviewFile))
            throw new InvalidOperationException("Wrong preview references");
        var names=new SerializedObject(Find<MainMenuController>(menu)).FindProperty("levelSceneNames");
        if(!Enumerable.Range(0,names.arraySize).Any(i=>names.GetArrayElementAtIndex(i).stringValue==m.sceneName))
            throw new InvalidOperationException("Missing reset registration");
        var previous=EditorSceneManager.OpenScene("Assets/_Project/Scenes/"+previousSceneName+".unity",OpenSceneMode.Single);
        if(Find<PaintController>(previous).nextLevelSceneName!=m.sceneName)throw new InvalidOperationException("Previous level transition");
        var scene=EditorSceneManager.OpenScene(target,OpenSceneMode.Single);
        if(Find<PaintController>(scene).nextLevelSceneName!="MainMenu")throw new InvalidOperationException("Final level transition");
        if(!EditorBuildSettings.scenes.Any(s=>s.path==target&&s.enabled))throw new InvalidOperationException("Missing enabled build scene");
        return "Integration PASS: one menu card, previews, reset, transitions and enabled build scene";
    }
}
#endif
