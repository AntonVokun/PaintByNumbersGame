using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

// One short-lived loading overlay shared by the existing menu/Next/back actions.
public sealed class LevelTransition : MonoBehaviour
{
    private static LevelTransition active;
    private UnityEngine.UI.Image fill;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetState() { active=null; }

    public static void Load(string scene)
    {
        if (active != null) return;
        if (!Application.CanStreamedLevelBeLoaded(scene))
        {
            Debug.LogError($"Сцена '{scene}' не найдена в Build Settings.");
            return;
        }
        var go=new GameObject("Loading",typeof(RectTransform),typeof(Canvas),typeof(UnityEngine.UI.CanvasScaler),typeof(UnityEngine.UI.GraphicRaycaster));
        DontDestroyOnLoad(go);
        active=go.AddComponent<LevelTransition>();
        active.CreateOverlay();
        active.StartCoroutine(active.Open(scene));
    }

    private void CreateOverlay()
    {
        var canvas=GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=32760;
        var scaler=GetComponent<UnityEngine.UI.CanvasScaler>();
        scaler.uiScaleMode=UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution=new Vector2(1080,1920);scaler.matchWidthOrHeight=.5f;
        var panel=MakeImage("Background",transform,new Color(.96f,.97f,.94f));
        panel.rectTransform.anchorMin=Vector2.zero;panel.rectTransform.anchorMax=Vector2.one;
        panel.rectTransform.offsetMin=panel.rectTransform.offsetMax=Vector2.zero;
        var label=new GameObject("Label",typeof(RectTransform),typeof(TMPro.TextMeshProUGUI));
        label.transform.SetParent(transform,false);
        var text=label.GetComponent<TMPro.TextMeshProUGUI>();
        text.font=TMPro.TMP_Settings.defaultFontAsset;text.text="Загрузка…";text.fontSize=48;
        text.alignment=TMPro.TextAlignmentOptions.Center;text.color=new Color(.12f,.3f,.29f);text.raycastTarget=false;
        text.rectTransform.sizeDelta=new Vector2(700,100);
        var track=MakeImage("Track",transform,new Color(.78f,.84f,.8f));
        track.rectTransform.sizeDelta=new Vector2(420,14);track.rectTransform.anchoredPosition=new Vector2(0,-90);
        fill=MakeImage("Progress",track.transform,new Color(.2f,.55f,.38f));
        fill.rectTransform.anchorMin=Vector2.zero;fill.rectTransform.anchorMax=Vector2.one;
        fill.rectTransform.offsetMin=fill.rectTransform.offsetMax=Vector2.zero;
        fill.rectTransform.pivot=new Vector2(0,.5f);fill.rectTransform.localScale=new Vector3(.05f,1,1);
    }

    private static UnityEngine.UI.Image MakeImage(string name,Transform parent,Color color)
    {
        var go=new GameObject(name,typeof(RectTransform),typeof(UnityEngine.UI.Image));
        go.transform.SetParent(parent,false);var image=go.GetComponent<UnityEngine.UI.Image>();image.color=color;return image;
    }

    private IEnumerator Open(string scene)
    {
        // Render the feedback before scheduling scene activation and asset upload.
        yield return null;
        yield return new WaitForEndOfFrame();
        var operation=SceneManager.LoadSceneAsync(scene);
        if(operation!=null)
        {
            while(!operation.isDone)
            {
                fill.rectTransform.localScale=new Vector3(Mathf.Clamp(operation.progress/.9f,.05f,1f),1,1);
                yield return null;
            }
        }
        fill.rectTransform.localScale=Vector3.one;
        yield return null;
        active=null;Destroy(gameObject);
    }

    private void OnDestroy() { if(active==this)active=null; }
}
