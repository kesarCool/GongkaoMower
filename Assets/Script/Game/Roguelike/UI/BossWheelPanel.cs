using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>Fixed reward data drives the wheel; presentation never rolls or grants rewards.</summary>
public class BossWheelPanel : UIPanelBase
{
    [Header("环形卡槽")]
    [SerializeField] private WheelSlotCell slotPrefab;
    [SerializeField] private RectTransform slotContainer;
    [SerializeField] private float wheelRadius = 356f;
    private WheelSlotCell[] _wheelSlots;
    [Header("按钮")]
    public Button spinButton;
    public TextMeshProUGUI spinButtonText;
    public Toggle skipAnimationToggle;
    [Header("结算")]
    public GameObject resultGroup;
    public WheelRewardSkillCell[] rewardCells;
    public Button confirmButton;
    [SerializeField] private BossWheelPresentation presentation;
    [Header("动画参数（不影响奖励抽取）")]
    public float spinDuration = 1.6f;
    public float stopDuration = .10f;
    [Range(0,3)] public int extraLapsBeforeWinner;

    private const string SkipAnimKey = "boss_wheel_skip_animation";
    private WheelSlotData[] _slots;
    private int[] _winningIndices;
    private System.Action _onSpinComplete, _onClose;
    private bool _hasSpun, _resultShown, _closeRequested;
    private Coroutine _spinRoutine;

    private void Awake()
    {
        if(presentation==null)presentation=GetComponent<BossWheelPresentation>();
        if(spinButton!=null)spinButton.onClick.AddListener(OnSpinClicked);
        if(confirmButton!=null){confirmButton.onClick.AddListener(OnConfirmClicked);confirmButton.gameObject.SetActive(false);}
        if(skipAnimationToggle!=null)
        {
            skipAnimationToggle.SetIsOnWithoutNotify(PlayerPrefs.GetInt(SkipAnimKey,0)==1);
            skipAnimationToggle.onValueChanged.AddListener(OnSkipToggleChanged);
        }
        if(resultGroup!=null)resultGroup.SetActive(false);
    }
    public override void OnOpen(object payload)
    {
        var p=payload as BossWheelOpenPayload;
        if(p==null){Debug.LogError("[BossWheelPanel] BossWheelOpenPayload required.");return;}
        StopAnimation();
        _slots=p.Slots??new WheelSlotData[0];_winningIndices=p.WinningIndices??new int[0];
        _onSpinComplete=p.OnSpinComplete;_onClose=p.OnClose;
        _hasSpun=_resultShown=_closeRequested=false;
        BindSlots();
        if(resultGroup!=null)resultGroup.SetActive(false);
        if(spinButton!=null){spinButton.gameObject.SetActive(true);spinButton.interactable=true;}
        if(skipAnimationToggle!=null)skipAnimationToggle.gameObject.SetActive(true);
        if(confirmButton!=null){confirmButton.interactable=false;confirmButton.gameObject.SetActive(false);}
        if(spinButtonText!=null)spinButtonText.text="开始转盘";
        if(presentation!=null)presentation.ResetView(rewardCells);
    }
    public override void OnClose()
    {
        StopAnimation();DestroyAllSlots();
        _onSpinComplete=_onClose=null;_slots=null;_winningIndices=null;
        base.OnClose();
    }
    private void OnDisable(){StopAnimation();}
    private void StopAnimation()
    {
        if(_spinRoutine!=null){StopCoroutine(_spinRoutine);_spinRoutine=null;}
        if(presentation!=null)presentation.CancelMotion();
    }
    private void DestroyAllSlots()
    {
        if(_wheelSlots==null)return;
        foreach(var cell in _wheelSlots)if(cell!=null){cell.gameObject.SetActive(false);Destroy(cell.gameObject);}
        _wheelSlots=null;
    }
    private void BindSlots()
    {
        DestroyAllSlots();if(slotPrefab==null||slotContainer==null)return;
        _wheelSlots=new WheelSlotCell[_slots.Length];
        for(int i=0;i<_slots.Length;i++)
        {
            var cell=Instantiate(slotPrefab,slotContainer,false);cell.Bind(_slots[i]);_wheelSlots[i]=cell;
            // Preserve the existing index-to-angle mapping and upright icons.
            float angle=(90f+360f*i/_slots.Length)*Mathf.Deg2Rad;
            ((RectTransform)cell.transform).anchoredPosition=new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*wheelRadius;
        }
    }
    private void OnSpinClicked()
    {
        if(_hasSpun||_closeRequested)return;
        _hasSpun=true;UiClickSound.Play();
        bool skip=skipAnimationToggle!=null&&skipAnimationToggle.isOn;
        if(spinButton!=null)spinButton.gameObject.SetActive(false);
        if(skipAnimationToggle!=null)skipAnimationToggle.gameObject.SetActive(false);
        if(confirmButton!=null){confirmButton.interactable=false;confirmButton.gameObject.SetActive(false);}
        if(presentation!=null)presentation.BeginSpin();
        _spinRoutine=StartCoroutine(SpinRoutine(skip));
    }
    private void OnConfirmClicked()
    {
        if(!_resultShown||_closeRequested||(confirmButton!=null&&!confirmButton.interactable))return;
        _closeRequested=true;
        if(confirmButton!=null)confirmButton.interactable=false;
        UiClickSound.Play();var callback=_onClose;
        if(UIManager.Instance!=null)UIManager.Instance.CloseTop();
        callback?.Invoke();
    }
    private void OnSkipToggleChanged(bool on){PlayerPrefs.SetInt(SkipAnimKey,on?1:0);PlayerPrefs.Save();}
    private bool ValidSlot(int index)=>_wheelSlots!=null&&index>=0&&index<_wheelSlots.Length&&_wheelSlots[index]!=null&&_wheelSlots[index].gameObject.activeInHierarchy;
    private void Cursor(int previous,int next)
    {
        if(ValidSlot(previous))_wheelSlots[previous].SetHighlight(false);
        if(ValidSlot(next))_wheelSlots[next].SetHighlight(true);
    }
    private IEnumerator SpinRoutine(bool skip)
    {
        int n=_wheelSlots==null?0:_wheelSlots.Length,previous=-1;
        if(!skip&&n>0)
        {
            int start=Random.Range(0,n),last=-1;float elapsed=0,duration=Mathf.Max(.05f,spinDuration);
            while(elapsed<duration)
            {
                int next=(start+Mathf.FloorToInt(elapsed/duration*n*2))%n;
                if(next!=last){Cursor(previous,next);previous=next;last=next;AudioService.Ensure().Play(AudioId.WheelTick);}
                elapsed+=Time.unscaledDeltaTime;yield return null;
            }
        }
        for(int i=0;i<_winningIndices.Length;i++)
        {
            int target=_winningIndices[i];if(!ValidSlot(target))continue;
            if(!skip)
            {
                int from=previous<0?0:previous,steps=(target-from+n)%n+extraLapsBeforeWinner*n;
                float elapsed=0;
                while(elapsed<.3f)
                {
                    float p=Mathf.Clamp01(elapsed/.3f);p=1-(1-p)*(1-p);
                    int next=(from+Mathf.FloorToInt(p*steps))%n;
                    if(next!=previous){Cursor(previous,next);previous=next;AudioService.Ensure().Play(AudioId.WheelTick);}
                    elapsed+=Time.unscaledDeltaTime;yield return null;
                }
            }
            Cursor(previous,-1);_wheelSlots[target].SetWinner(true);previous=target;
            if(!skip&&presentation!=null)presentation.NotifyWinner(i,(RectTransform)_wheelSlots[target].transform);
            if(!skip){AudioService.Ensure().Play(AudioId.WheelTick);yield return new WaitForSecondsRealtime(Mathf.Max(0,stopDuration));}
        }
        if(presentation!=null)yield return presentation.Charge(skip);
        if(_resultShown)yield break;
        _resultShown=true;
        // Original one-time reward callback remains before result binding, including skip mode.
        _onSpinComplete?.Invoke();
        if(!isActiveAndEnabled)yield break;
        var display=BindRewards();
        if(resultGroup!=null)resultGroup.SetActive(true);
        if(presentation!=null)yield return presentation.Reveal(display,rewardCells,skip);
        if(confirmButton!=null){confirmButton.gameObject.SetActive(true);confirmButton.interactable=true;}
        _spinRoutine=null;
    }
    private WheelSlotData[] BindRewards()
    {
        int count=rewardCells==null?0:Mathf.Min(rewardCells.Length,_winningIndices.Length);
        var display=new List<WheelSlotData>(count);var bonuses=new Dictionary<SkillId,int>();
        for(int i=0;i<count;i++)
        {
            int index=_winningIndices[i];if(index<0||index>=_slots.Length||_slots[index]==null)continue;
            var src=_slots[index];int bonus=bonuses.TryGetValue(src.skillId,out int b)?b:0;bonuses[src.skillId]=bonus+1;
            var data=new WheelSlotData{skillId=src.skillId,def=src.def,currentLevel=src.currentLevel+bonus,targetLevel=src.currentLevel+bonus+1,isActive=src.isActive,weight=src.weight};
            if(rewardCells[display.Count]!=null)rewardCells[display.Count].Bind(data);
            display.Add(data);
        }
        if(rewardCells!=null)for(int i=display.Count;i<rewardCells.Length;i++)if(rewardCells[i]!=null)rewardCells[i].gameObject.SetActive(false);
        return display.ToArray();
    }
    private void OnDestroy()
    {
        if(spinButton!=null)spinButton.onClick.RemoveListener(OnSpinClicked);
        if(confirmButton!=null)confirmButton.onClick.RemoveListener(OnConfirmClicked);
        if(skipAnimationToggle!=null)skipAnimationToggle.onValueChanged.RemoveListener(OnSkipToggleChanged);
    }
}
