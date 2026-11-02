using UnityEngine;
using UnityEngine.UI;

/// <summary>Persistent empty-star backing with independent filled-star overlays.</summary>
public sealed class SkillStarRow : MonoBehaviour
{
    public Image[] stars;
    public Image[] emptyStars;
    public Sprite filledSprite, emptySprite;
    public int CurrentLevel { get; private set; }
    public int PreviewLevel { get; private set; }
    public bool IsRed { get; private set; }
    private int _max;
    private bool _preview;
    private static readonly Color Gold = Color.white;
    private static readonly Color Red = new Color(1f, .24f, .22f);
    private static readonly Color Empty = new Color(.58f, .56f, .49f, 1f);

    public void Bind(int current, int next, int maxLevel, bool preview)
    {
        _max = Mathf.Clamp(maxLevel, 1, stars.Length);
        CurrentLevel = Mathf.Clamp(current, 0, _max);
        PreviewLevel = Mathf.Clamp(next, CurrentLevel, _max);
        _preview = preview && PreviewLevel > CurrentLevel;
        IsRed = PreviewLevel >= _max && _max == 5;
        for (int i = 0; i < stars.Length; i++)
        {
            var star = stars[i];
            bool lit = i < (_preview ? PreviewLevel : CurrentLevel);
            if (emptyStars != null && i < emptyStars.Length && emptyStars[i] != null)
            {
                var backing = emptyStars[i];
                backing.gameObject.SetActive(i < _max);
                backing.sprite = emptySprite;
                backing.color = Empty;
                backing.rectTransform.localScale = Vector3.one;
            }
            star.gameObject.SetActive(i < _max && lit);
            star.sprite = filledSprite;
            star.color = IsRed ? Red : Gold;
            star.rectTransform.localScale = Vector3.one;
        }
        gameObject.SetActive(true);
    }

    private void Update()
    {
        if (!_preview) return;
        var star = stars[PreviewLevel - 1];
        float pulse = .5f + .5f * Mathf.Sin(Time.unscaledTime * 7f);
        Color color = IsRed ? Red : Gold;
        color.a = Mathf.SmoothStep(0f, 1f, pulse);
        star.color = color;
        // Both layers keep identical geometry: only the filled overlay blinks.
        star.rectTransform.localScale = Vector3.one;
    }

    public void ConfirmPreview() { Bind(PreviewLevel, PreviewLevel, _max, false); }
}
