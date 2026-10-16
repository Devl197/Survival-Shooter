using UnityEngine;
using UnityEngine.UI;

// Small runtime overlay, refreshed at 4 Hz in both recording and playback.
public class ReplayStatusUI : MonoBehaviour
{
    public SessionReplay replay;
    Text label;
    GameObject canvasObject;
    BenchmarkRunner benchmark;
    float nextRefresh;

    void Start()
    {
        benchmark = GetComponent<BenchmarkRunner>();
        canvasObject = new GameObject("Session status", typeof(Canvas), typeof(CanvasScaler));
        canvasObject.transform.SetParent(transform, false);
        var canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        var panel = new GameObject("Background", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(canvasObject.transform, false);
        var rect = panel.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
        rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = new Vector2(12, -12);
        rect.sizeDelta = new Vector2(430, 90);
        panel.GetComponent<Image>().color = new Color(0, 0, 0, 0.8f);
        panel.GetComponent<Image>().raycastTarget = false;
        var textObject = new GameObject("Status", typeof(RectTransform), typeof(Text));
        textObject.transform.SetParent(panel.transform, false);
        label = textObject.GetComponent<Text>();
        label.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        label.fontSize = 18;
        label.color = Color.white;
        label.raycastTarget = false;
        label.rectTransform.anchorMin = Vector2.zero;
        label.rectTransform.anchorMax = Vector2.one;
        label.rectTransform.offsetMin = new Vector2(10, 5);
        label.rectTransform.offsetMax = new Vector2(-10, -5);
    }

    void Update()
    {
        if (replay == null || label == null || Time.unscaledTime < nextRefresh) return;
        nextRefresh = Time.unscaledTime + 0.25f;
        bool active = replay.IsRecording || replay.IsPlaying;
        string state = replay.Status ?? "Preparing";
        if (benchmark != null && benchmark.Selected && !active) state = benchmark.Status;
        string time = active ? string.Format("{0:F1} / {1:F1} s", replay.Elapsed, replay.SessionDuration) : "";
        label.text = state + (active && Time.timeScale == 0f ? " (paused)" : "") + "  " + time +
            (active ? "\nF8: stop" : "\nSession inactive. Play Mode remains open.");
    }

    void OnDestroy() { if (canvasObject != null) Destroy(canvasObject); }
}
