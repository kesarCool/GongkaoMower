using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

public partial class CardView : IPointerDownHandler, IPointerUpHandler, IPointerExitHandler,
    IBeginDragHandler, IDragHandler, IEndDragHandler
{
    private bool _pointerCycle, _pressValid, _releaseValid, _confirming;
    private int _pointerId;
    private float _pressAmount;
    private Vector2 _visualRest;
    private bool _visualRestCached;

    public bool IsConfirming => _confirming;
    private RectTransform Visual => pressVisual != null ? pressVisual : transform as RectTransform;

    public void OnPointerDown(PointerEventData e)
    {
        if (e.button != PointerEventData.InputButton.Left || _confirming || _pressValid) return;
        _pointerCycle = true; _pointerId = e.pointerId; _releaseValid = false;
        _pressValid = clickButton != null && clickButton.IsInteractable() && (_ownerPanel == null || _ownerPanel.CanAcceptCardInput);
        if (_pressValid && selectionFrame != null) selectionFrame.gameObject.SetActive(true);
    }

    public void OnPointerUp(PointerEventData e)
    {
        if (!_pointerCycle || e.pointerId != _pointerId) return;
        _releaseValid = _pressValid && !e.dragging && e.eligibleForClick &&
            RectTransformUtility.RectangleContainsScreenPoint((RectTransform)transform, e.position, e.pressEventCamera);
        _pressValid = false;
        if (!_releaseValid && selectionFrame != null) selectionFrame.gameObject.SetActive(false);
    }

    public void OnPointerExit(PointerEventData e)
    {
        if (!_pointerCycle || e.pointerId != _pointerId || _confirming) return;
        _pressValid = false; _releaseValid = false;
        if (selectionFrame != null) selectionFrame.gameObject.SetActive(false);
    }

    public void OnBeginDrag(PointerEventData e)
    {
        if (e.pointerId != _pointerId) return;
        _pressValid = _releaseValid = false; e.eligibleForClick = false;
        if (selectionFrame != null) selectionFrame.gameObject.SetActive(false);
    }
    public void OnDrag(PointerEventData e) { }
    public void OnEndDrag(PointerEventData e) { }

    private void Update()
    {
        if (_confirming || Visual == null) return;
        _pressAmount = Mathf.MoveTowards(_pressAmount, _pressValid ? 1 : 0, Time.unscaledDeltaTime / .07f);
        Visual.localScale = Vector3.one * Mathf.Lerp(1, .96f, _pressAmount);
        Visual.anchoredPosition = _visualRest + Vector2.down * (6 * _pressAmount);
    }

    public void OnClick()
    {
        if (_confirming || _onClick == null || clickButton == null || !clickButton.IsInteractable()) return;
        if (_pointerCycle && !_releaseValid) return;
        if (_ownerPanel != null && !_ownerPanel.TryBeginCardConfirmation(this)) return;
        _confirming = true; _pressValid = false; _pointerCycle = false;
        UiClickSound.Play();
        if (animator != null) animator.SetTrigger("Selected");
        var siblings = GetSiblingCardViews();
        foreach (var card in siblings) if (card.clickButton != null) card.clickButton.interactable = false;
        if (selectionFrame != null) selectionFrame.gameObject.SetActive(true);
        _clickBounceRoutine = StartCoroutine(ClickFeedbackThenConfirm(siblings));
    }

    private IEnumerator ClickFeedbackThenConfirm(CardView[] siblings)
    {
        float elapsed = 0;
        float duration = Mathf.Max(.38f, clickAnimDuration);
        float startScale = Visual.localScale.x;
        if (starRow != null) starRow.ConfirmPreview();
        while (elapsed < duration)
        {
            float t = Mathf.Clamp01(elapsed / duration);
            float release = Mathf.SmoothStep(0, 1, Mathf.Clamp01(t / .35f));
            float scale = Mathf.Lerp(startScale, 1, release) + Mathf.Sin(t * Mathf.PI) * (clickTargetScale - 1);
            Visual.localScale = Vector3.one * scale;
            Visual.anchoredPosition = _visualRest + Vector2.up * (Mathf.Sin(t * Mathf.PI) * 9);
            foreach (var card in siblings)
                if (card != this && card.canvasGroup != null)
                    card.canvasGroup.alpha = Mathf.Lerp(1f, .32f, Mathf.Clamp01(elapsed / Mathf.Max(.15f, fadeOutDuration)));
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
        _clickBounceRoutine = null;
        _onClick?.Invoke();
    }

    private void ResetPressState()
    {
        _pointerCycle = _pressValid = _releaseValid = _confirming = false;
        _pressAmount = 0;
        if (Visual == null) return;
        if (!_visualRestCached) { _visualRest = Visual.anchoredPosition; _visualRestCached = true; }
        Visual.localScale = Vector3.one;
        Visual.anchoredPosition = _visualRest;
    }
}
