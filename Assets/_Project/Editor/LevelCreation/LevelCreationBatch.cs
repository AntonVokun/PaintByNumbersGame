#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Opt-in Editor-only P0 checks. No automatic authoring, Play entry or progress reset.
public static class LevelCreationBatch
{
    [Serializable] public class Request { public string[] folders; public string output; }
    [Serializable] public class Result
    {
        public string status, code, detail, playMode = "NOT_RUN";
        public int manifests, groups, regions, paths, pointerContractChecks;
        public double durationMs;
    }

    public sealed class PointerSequence
    {
        readonly int[] ids;
        readonly Action<int> reveal;
        readonly Func<int,Vector2> button, zone;
        readonly Action<Vector2,ushort> send;
        readonly Func<int,bool> selected, painted;
        int index, phase;
        Vector2 point;
        public bool AwaitAnimation => phase==7;
        public PointerSequence(int[] ids, Action<int> reveal, Func<int,Vector2> button,
            Func<int,Vector2> zone, Action<Vector2,ushort> send, Func<int,bool> selected, Func<int,bool> painted)
        {
            this.ids=ids;this.reveal=reveal;this.button=button;this.zone=zone;
            this.send=send;this.selected=selected;this.painted=painted;
        }
        public bool Advance()
        {
            if(index==ids.Length)return true;
            int id=ids[index];
            switch(phase)
            {
                case 0:reveal(id);break;
                case 1:point=button(id);send(point,0);break;
                case 2:send(point,1);break;
                case 3:send(point,0);break;
                case 4:
                    if(!selected(id))throw new InvalidOperationException("POINTER_NOT_SELECTED group "+id);
                    point=zone(id);send(point,0);break;
                case 5:send(point,1);break;
                case 6:send(point,0);break;
                default:
                    if(!painted(id))throw new InvalidOperationException("ZONE_NOT_PAINTED group "+id);
                    index++;phase=-1;break;
            }
            phase++;return index==ids.Length;
        }
    }

    public static int TestPointerContract()
    {
        var events=new System.Collections.Generic.List<string>();
        int shown=0;
        var s=new PointerSequence(new[]{17},id=>shown=id,id=>new Vector2(10,20),id=>new Vector2(30,40),
            (p,b)=>events.Add(p.x+":"+b),id=>shown==id,id=>true);
        int steps=0;while(!s.Advance()&&++steps<20){}
        if(steps>=20||string.Join(",",events)!="10:0,10:1,10:0,30:0,30:1,30:0")
            throw new InvalidOperationException("POINTER_SEQUENCE_CONTRACT");
        foreach(bool selectionFails in new[]{true,false})
        {
            var bad=new PointerSequence(new[]{7},id=>{},id=>Vector2.zero,id=>Vector2.one,(p,b)=>{},
                id=>!selectionFails,id=>false);
            bool rejected=false;
            try {for(int i=0;i<12;i++)if(bad.Advance())break;}
            catch(InvalidOperationException e){rejected=e.Message.Contains(selectionFails?"POINTER_NOT_SELECTED":"ZONE_NOT_PAINTED");}
            if(!rejected)throw new InvalidOperationException("FALSE_POINTER_PASS");
        }
        return 3;
    }

    static string DiagnosticPath(string path)
    {
        string root=Path.GetFullPath("Logs/LevelCreation")+Path.DirectorySeparatorChar;
        string full=Path.GetFullPath(path);
        if(!full.StartsWith(root,StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("DIAGNOSTIC_OUTSIDE_LOGS");
        Directory.CreateDirectory(Path.GetDirectoryName(full));return full;
    }

    public static Result RunReadOnly(string requestFile)
    {
        if(EditorApplication.isPlaying||EditorApplication.isCompiling)
            throw new InvalidOperationException("EDITOR_NOT_READY");
        var request=JsonUtility.FromJson<Request>(File.ReadAllText(requestFile));
        string output=DiagnosticPath(request.output);
        var result=new Result{status="RUNNING",code="OK"};
        var started=System.Diagnostics.Stopwatch.StartNew();
        var before=Enumerable.Range(0,SceneManager.sceneCount).Select(i=>SceneManager.GetSceneAt(i))
            .Select(s=>s.path+":"+s.isDirty).ToArray();
        var preview=EditorSceneManager.NewPreviewScene();
        try
        {
            result.pointerContractChecks=TestPointerContract();
            foreach(string folder in request.folders)
            {
                // Read manifests/geometry only. Do not open or save existing gameplay scenes.
                var m=LevelCreationTools.Read(folder);result.manifests++;
                foreach(var g in m.groups)
                {
                    var go=new GameObject("P0 Native Check");SceneManager.MoveGameObjectToScene(go,preview);
                    try
                    {
                        var collider=go.AddComponent<PolygonCollider2D>();collider.pathCount=g.paths.Length;
                        for(int p=0;p<g.paths.Length;p++)collider.SetPath(p,g.paths[p].points.Select(v=>new Vector2(
                            (v.x-m.width*.5f)/m.pixelsPerUnit,(m.height*.5f-v.y)/m.pixelsPerUnit)).ToArray());
                        Physics2D.SyncTransforms();
                        foreach(var sample in g.samples)
                        {
                            var point=new Vector2((sample.x+.5f-m.width*.5f)/m.pixelsPerUnit,
                                (m.height*.5f-sample.y-.5f)/m.pixelsPerUnit);
                            if(!collider.OverlapPoint(point))throw new InvalidOperationException("COLLIDER_HIT group "+g.id+" sample "+sample.x+","+sample.y);
                            result.regions++;
                        }
                        result.groups++;result.paths+=g.paths.Length;
                    }
                    finally {UnityEngine.Object.DestroyImmediate(go);}
                }
            }
            result.status="PASS";
        }
        catch(Exception error){result.status="FAILED";result.code=error.Message.Split(' ')[0];result.detail=error.ToString();}
        finally
        {
            EditorSceneManager.ClosePreviewScene(preview);
            var after=Enumerable.Range(0,SceneManager.sceneCount).Select(i=>SceneManager.GetSceneAt(i))
                .Select(s=>s.path+":"+s.isDirty).ToArray();
            if(!before.SequenceEqual(after)){result.status="FAILED";result.code="SCENE_STATE_CHANGED";}
            result.durationMs=started.Elapsed.TotalMilliseconds;
            File.WriteAllText(output,JsonUtility.ToJson(result,true));
        }
        // Full exception is on disk; caller gets typed status and counts only.
        return new Result{status=result.status,code=result.code,manifests=result.manifests,groups=result.groups,
            regions=result.regions,paths=result.paths,pointerContractChecks=result.pointerContractChecks,durationMs=result.durationMs};
    }

    public static string CaptureScreen(string output)
    {
        if(!EditorApplication.isPlaying)throw new InvalidOperationException("PLAY_REQUIRED");
        output=DiagnosticPath(output);
        var texture=ScreenCapture.CaptureScreenshotAsTexture();
        try {if(texture==null)throw new InvalidOperationException("CAPTURE_FAILED");File.WriteAllBytes(output,texture.EncodeToPNG());}
        finally {if(texture!=null)UnityEngine.Object.DestroyImmediate(texture);}
        return output;
    }
}
#endif
