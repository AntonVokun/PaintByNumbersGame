#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

// Test helpers are opt-in, Editor-only; preserve user progress before any Play test.
public static class LevelCreationVerification
{
    static Mouse testMouse;
    static int phase, nextFrame, activeId;
    static float nextTime;
    static Queue<int> colorQueue;
    public static string InputStatus = "idle";
    static Vector2 pointer;
    static Mouse TestMouse()
    {
        if(!SessionState.GetBool("LevelCreationInputOverride",false))
        {
            SessionState.SetInt("LevelCreationInputBackground",(int)InputSystem.settings.backgroundBehavior);
            SessionState.SetInt("LevelCreationInputEditor",(int)InputSystem.settings.editorInputBehaviorInPlayMode);
            SessionState.SetBool("LevelCreationInputOverride",true);
            InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        }
        testMouse=InputSystem.devices.OfType<Mouse>().FirstOrDefault(d=>d.name=="LevelVerificationMouse");
        if(testMouse==null)testMouse=InputSystem.AddDevice<Mouse>("LevelVerificationMouse");
        testMouse.MakeCurrent();
        return testMouse;
    }
    public static string QueuePointer(Vector2 point, ushort buttons=0, Vector2 scroll=default(Vector2))
    {
        var mouse=TestMouse();
        InputSystem.QueueStateEvent(mouse,new MouseState{position=point,delta=point-mouse.position.ReadValue(),buttons=buttons,scroll=scroll});
        return "Queued Input System pointer event";
    }
    public static Vector2 ButtonPoint(int id)
    {
        return RectTransformUtility.WorldToScreenPoint(null,PaintController.Instance.paletteButtons.Single(b=>b.colorId==id).transform.position);
    }
    public static Vector2 ZonePoint(int id)
    {
        var m=LevelCreationTools.Read("Assets/_Project/Art/Levels/"+SceneManager.GetActiveScene().name.Replace("_New",""));
        var s=m.groups.Single(g=>g.id==id).samples.OrderByDescending(v=>v.pixels).First();
        var z=PaintController.Instance.zones.Single(v=>v.colorId==id);
        var world=z.transform.TransformPoint(new Vector3((s.x+.5f-m.width*.5f)/m.pixelsPerUnit,(m.height*.5f-s.y-.5f)/m.pixelsPerUnit,0));
        return UnityEngine.Object.FindFirstObjectByType<CameraZoomAndPan>().GetComponent<Camera>().WorldToScreenPoint(world);
    }
    public static string AutoPaint()
    {
        colorQueue=new Queue<int>(PaintController.Instance.zones.Where(z=>!z.IsPainted).OrderBy(z=>z.colorId).Select(z=>z.colorId));
        phase=0;nextTime=Time.time;nextFrame=Time.frameCount;
        InputStatus="running";
        EditorApplication.update-=Tick;
        EditorApplication.update+=Tick;
        return "Input System auto-paint started: "+colorQueue.Count+" remaining colors";
    }
    static void Tick()
    {
        if(!EditorApplication.isPlaying){EditorApplication.update-=Tick;InputStatus="stopped";return;}
        if(Time.frameCount<nextFrame||Time.time<nextTime)return;
        try
        {
            if(phase==0)
            {
                if(colorQueue.Count==0){EditorApplication.update-=Tick;InputStatus="PASS: all colors painted through Input System pointer events";return;}
                activeId=colorQueue.Dequeue();pointer=ButtonPoint(activeId);QueuePointer(pointer,1);phase=1;
            }
            else if(phase==1){QueuePointer(pointer);phase=2;}
            else if(phase==2)
            {
                if(SelectColor.CurrentColorId!=activeId)throw new InvalidOperationException("Palette click did not select "+activeId);
                pointer=ZonePoint(activeId);QueuePointer(pointer,1);phase=3;
            }
            else if(phase==3){QueuePointer(pointer);phase=4;nextTime=Time.time+.35f;}
            else
            {
                if(!PaintController.Instance.zones.Single(z=>z.colorId==activeId).IsPainted)throw new InvalidOperationException("Zone click did not paint "+activeId);
                phase=0;
            }
            nextFrame=Time.frameCount+3;
        }
        catch(Exception e){InputStatus="FAIL: "+e.Message;EditorApplication.update-=Tick;}
    }
    public static void RemoveTestInput()
    {
        EditorApplication.update-=Tick;
        foreach(var mouse in InputSystem.devices.OfType<Mouse>().Where(d=>d.name=="LevelVerificationMouse").ToArray())InputSystem.RemoveDevice(mouse);
        if(SessionState.GetBool("LevelCreationInputOverride",false))
        {
            InputSystem.settings.backgroundBehavior=(InputSettings.BackgroundBehavior)SessionState.GetInt("LevelCreationInputBackground",0);
            InputSystem.settings.editorInputBehaviorInPlayMode=(InputSettings.EditorInputBehaviorInPlayMode)SessionState.GetInt("LevelCreationInputEditor",0);
            SessionState.SetBool("LevelCreationInputOverride",false);
        }
    }
    [Serializable] public class Pref { public string key, text; public int number; public bool exists, isString; }
    [Serializable] public class Prefs { public Pref[] values; }
    public static string BackupProgress(string destination=null)
    {
        string Snapshot=destination??("Logs/LevelCreation/"+DateTime.UtcNow.ToString("yyyyMMddTHHmmss")+"/playerprefs.json");
        SessionState.SetString("LevelCreationProgressBackup",Snapshot);
        if (File.Exists(Snapshot)) return "Existing progress backup retained";
        var keys = new List<Pref>();
        foreach (var buildScene in EditorBuildSettings.scenes.Where(s=>s.enabled&&Path.GetFileNameWithoutExtension(s.path).StartsWith("Level_")))
        {
            string scene=Path.GetFileNameWithoutExtension(buildScene.path);
            keys.Add(new Pref {key=SaveSystem.LevelCompletedKey(scene)});
            keys.Add(new Pref {key=SaveSystem.LevelResetPendingKey(scene)});
            keys.Add(new Pref {key=SaveSystem.LevelColorIdsKey(scene),isString=true});
            var ids=new HashSet<int>();
            foreach(System.Text.RegularExpressions.Match match in System.Text.RegularExpressions.Regex.Matches(File.ReadAllText(buildScene.path),@"\n  colorId: (\d+)"))
                ids.Add(int.Parse(match.Groups[1].Value));
            foreach(string value in PlayerPrefs.GetString(SaveSystem.LevelColorIdsKey(scene),"").Split(','))
                if(int.TryParse(value,out int id))ids.Add(id);
            foreach(int id in ids) keys.Add(new Pref {key=SaveSystem.PaintedColorKey(scene,id)});
        }
        keys.Add(new Pref {key="SoundEnabled"});
        foreach (var p in keys) { p.exists=PlayerPrefs.HasKey(p.key); if(p.isString)p.text=PlayerPrefs.GetString(p.key);else p.number=PlayerPrefs.GetInt(p.key); }
        Directory.CreateDirectory(Path.GetDirectoryName(Snapshot));
        File.WriteAllText(Snapshot,JsonUtility.ToJson(new Prefs {values=keys.ToArray()},true));
        return "Saved user progress before test";
    }
    public static string RestoreProgress(string snapshotPath=null)
    {
        string Snapshot=snapshotPath??SessionState.GetString("LevelCreationProgressBackup","");
        if(string.IsNullOrEmpty(Snapshot)||!File.Exists(Snapshot))throw new InvalidOperationException("Specify the verified progress snapshot path");
        var backup=JsonUtility.FromJson<Prefs>(File.ReadAllText(Snapshot));
        foreach(var p in backup.values)
            if(!p.exists)PlayerPrefs.DeleteKey(p.key);else if(p.isString)PlayerPrefs.SetString(p.key,p.text);else PlayerPrefs.SetInt(p.key,p.number);
        PlayerPrefs.Save();
        return "Original progress restored";
    }
    public static string PrepareFresh(string sceneName)
    {
        string Snapshot=SessionState.GetString("LevelCreationProgressBackup","");
        if(!File.Exists(Snapshot))throw new InvalidOperationException("Back up progress first");
        SaveSystem.ResetLevelProgress(sceneName);
        return "Prepared fresh test: " + sceneName;
    }
    public static object RuntimeStatus()
    {
        var pc=PaintController.Instance;
        var cam=UnityEngine.Object.FindFirstObjectByType<CameraZoomAndPan>();
        return new { scene=SceneManager.GetActiveScene().name,frame=Time.frameCount,
            zones=pc==null?0:pc.zones.Length,painted=pc==null?0:pc.zones.Count(z=>z.IsPainted),
            visibleButtons=pc==null?0:pc.paletteButtons.Count(b=>b.gameObject.activeInHierarchy),
            win=pc!=null&&pc.winPanel.activeInHierarchy,
            cameraCount=UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).Length,
            viewport=cam==null?"":cam.GetComponent<Camera>().pixelRect.ToString(),
            ortho=cam==null?0:cam.GetComponent<Camera>().orthographicSize };
    }
    public static string PaintThroughCollider(int colorId)
    {
        var pc=PaintController.Instance;
        var button=pc.paletteButtons.Single(b=>b.colorId==colorId);
        button.GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
        var z=pc.zones.Single(v=>v.colorId==colorId);
        // Calls the existing click entry point, after checking the native collider.
        if(z.GetComponent<PolygonCollider2D>().pathCount==0)throw new InvalidOperationException("No hit area");
        z.SendMessage("OnMouseDown",SendMessageOptions.RequireReceiver);
        return "Invoked palette + existing OnMouseDown: " + colorId;
    }
    public static string AssertRuntime()
    {
        var pc=PaintController.Instance;
        if(pc==null)throw new InvalidOperationException("No runtime controller");
        var camera=UnityEngine.Object.FindFirstObjectByType<CameraZoomAndPan>().GetComponent<Camera>();
        if(camera.pixelWidth<=0||camera.pixelHeight<=0)throw new InvalidOperationException("Invalid viewport");
        if(UnityEngine.Object.FindObjectsByType<CameraZoomAndPan>(FindObjectsSortMode.None).Length!=1)throw new InvalidOperationException("Camera duplicates");
        if(pc.zones.Any(z=>z.GetComponentsInChildren<TMPro.TMP_Text>(true).Any(t=>t.gameObject.activeInHierarchy)))throw new InvalidOperationException("Visible zone number label");
        if(pc.paletteButtons.Any(b=>b.GetComponentsInChildren<TMPro.TMP_Text>(true).Any(t=>t.gameObject.activeInHierarchy)))throw new InvalidOperationException("Visible palette number label");
        return "Runtime camera/viewport and hidden-label checks PASS";
    }
}
#endif
