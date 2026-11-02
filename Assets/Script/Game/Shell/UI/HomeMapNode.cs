using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class HomeMapNode : MonoBehaviour
{
    public Image face, stateIcon, selectionRing;
    public Image[] stars;
    public TMP_Text label;
    public Button button;
    public Sprite lockedFace, availableFace, clearedFace, lockIcon, flagIcon, checkIcon, filledStar, emptyStar;
    public int LevelId { get; private set; }
    public bool IsUnlocked { get; private set; }
    public bool IsSelected { get; private set; }

    public void Bind(int id, bool unlocked, bool cleared, int earnedStars, bool selected, Action<int> clicked)
    {
        LevelId = id; IsUnlocked = unlocked || cleared; IsSelected = selected;
        face.sprite = cleared ? clearedFace : IsUnlocked ? availableFace : lockedFace;
        face.rectTransform.sizeDelta = IsUnlocked && !cleared ? new Vector2(181.44f, 91.8f) : new Vector2(165.24f, 79.92f);
        stateIcon.sprite = cleared ? checkIcon : IsUnlocked ? flagIcon : lockIcon;
        label.text = $"{id / 100}-{id % 100}";
        label.fontSize = IsUnlocked && !cleared ? 43 : 38;
        selectionRing.gameObject.SetActive(selected);
        for (int i = 0; i < stars.Length; i++)
        {
            stars[i].gameObject.SetActive(cleared);
            stars[i].sprite = i < earnedStars ? filledStar : emptyStar;
        }
        button.interactable = IsUnlocked;
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => { UiClickSound.Play(); clicked?.Invoke(id); });
    }
}
