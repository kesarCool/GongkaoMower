using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Presentation-only sizing for shared modals, confirmation and non-blocking toast.</summary>
[DisallowMultipleComponent]
public sealed class DialogThemeLayout : MonoBehaviour
{
    public enum Kind { Modal, Confirm, Toast }
    public Kind kind;
    public RectTransform panel;
    public TMP_Text title, body;
    public ScrollRect scroll;
    public Button confirm, cancel;
    public float width = 864, height = 960;
    private bool fitting;

    private void OnEnable() { Refresh(); }
    private void OnRectTransformDimensionsChange() { Refresh(); }

    public void Refresh()
    {
        if (fitting || panel == null) return;
        fitting = true;
        try
        {
            var root = (RectTransform)transform;
            var bounds = kind == Kind.Toast ? root.parent as RectTransform : root;
            if (bounds == null || bounds.rect.width <= 0 || bounds.rect.height <= 0) return;
            float scale = Mathf.Min(1, (bounds.rect.width - 64) / width);
            scale = Mathf.Max(.1f, scale);
            float available = Mathf.Max(180, (bounds.rect.height - 160) / scale);
            float targetHeight = Mathf.Min(height, available);
            if (kind == Kind.Confirm)
            {
                float header = title != null && title.gameObject.activeSelf ? 128 : 48;
                float textHeight = body != null ? body.GetPreferredValues(body.text, width - 112, 0).y : 0;
                targetHeight = Mathf.Min(Mathf.Max(480, header + textHeight + 214), available);
                if (scroll != null)
                {
                    var view = (RectTransform)scroll.transform;
                    view.offsetMax = new Vector2(-56, -header);
                    scroll.content.sizeDelta = new Vector2(0, Mathf.Max(textHeight + 12, targetHeight - header - 180));
                    scroll.verticalNormalizedPosition = 1;
                }
                if (confirm != null)
                {
                    var rt = (RectTransform)confirm.transform;
                    rt.anchoredPosition = new Vector2(cancel != null && cancel.gameObject.activeSelf ? 176 : 0, 42);
                }
            }
            else if (kind == Kind.Toast)
            {
                // UIManager.ParentTo stretches overlay roots; a toast is a centered small surface.
                panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(.5f, .5f);
                float textHeight = body != null ? body.GetPreferredValues(body.text, width - 160, 0).y : 0;
                targetHeight = Mathf.Min(Mathf.Max(116, textHeight + 60), available);
                var cg = GetComponent<CanvasGroup>();
                if (cg != null) { cg.blocksRaycasts = false; cg.interactable = false; }
            }
            panel.sizeDelta = new Vector2(width, targetHeight);
            panel.localScale = Vector3.one * scale;
        }
        finally { fitting = false; }
    }
}
