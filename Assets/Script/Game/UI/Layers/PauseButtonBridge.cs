using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 挂在 TouchLayer 下的暂停按钮上。
/// 因为 TouchLayer 的 Canvas 挡在 GameLayer 之上，暂停按钮需独立出来。
/// </summary>
[RequireComponent(typeof(Button))]
public class PauseButtonBridge : MonoBehaviour
{
    private void Start()
    {
        GetComponent<Button>().onClick.AddListener(OnClick);
    }

    private void OnClick()
    {
        UiClickSound.Play();
        UIManager.Instance.Open<GamePausePanel>();
    }
}
