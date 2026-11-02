using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Unscaled, view-only reveal. All effects reuse the prefab's small fixed set of Images.</summary>
[DisallowMultipleComponent]
public sealed class BossWheelPresentation : MonoBehaviour
{
    public RectTransform effectsLayer, chestMotion;
    public CanvasGroup chestClosed, chestOpen, warmGlow, revealRays, revealFlash;
    public TextMeshProUGUI centerTitle, centerSubtitle, status, footer, resultCount;
    public GameObject instructions;
    public CanvasGroup[] flyingCards;
    public Image[] flyingIcons, sparkles, energyOrbs;
    public float chargeDuration=.45f, openDuration=.32f, flightDuration=.65f, flightStagger=.18f;
    public string Stage { get; private set; } = "Idle";
    public bool IsComplete => Stage=="Complete";
    Vector2 chestPosition;
    bool cached;
    readonly float[] energyStart={-100,-100,-100};
    readonly Vector2[] energyFrom=new Vector2[3];

    static float Ease(float t){t=Mathf.Clamp01(t);return 1-(1-t)*(1-t)*(1-t);}
    static float Pop(float t){t=Mathf.Clamp01(t)-1;return 1+2.70158f*t*t*t+1.70158f*t*t;}
    static void Alpha(CanvasGroup g,float value){if(g!=null){g.alpha=Mathf.Clamp01(value);g.interactable=false;g.blocksRaycasts=false;}}
    static CanvasGroup Group(WheelRewardSkillCell cell){var group=cell.GetComponent<CanvasGroup>();if(group==null)group=cell.gameObject.AddComponent<CanvasGroup>();return group;}
    static Vector2 Point(float x,float y)=>new Vector2(x-60,162-y);
    void Cache(){if(!cached&&chestMotion!=null){chestPosition=chestMotion.anchoredPosition;cached=true;}}

    public void ResetView(WheelRewardSkillCell[] cells)
    {
        Cache();CancelMotion();Stage="Idle";
        Alpha(chestClosed,1);Alpha(chestOpen,0);
        if(chestMotion!=null){chestMotion.anchoredPosition=chestPosition;chestMotion.localScale=Vector3.one;chestMotion.localRotation=Quaternion.identity;}
        if(instructions!=null)instructions.SetActive(true);
        centerTitle.text="幸运转一转";centerTitle.color=new Color32(73,54,39,255);centerTitle.fontSize=40;
        centerSubtitle.text="闭合的宝箱，藏着新的力量";status.text="";footer.text="击败首领奖励 · 无需消耗";
        if(cells!=null)foreach(var cell in cells)if(cell!=null){Alpha(Group(cell),0);cell.transform.localScale=Vector3.one;}
    }
    public void CancelMotion()
    {
        Alpha(warmGlow,0);Alpha(revealRays,0);Alpha(revealFlash,0);
        if(flyingCards!=null)foreach(var g in flyingCards)Alpha(g,0);
        if(sparkles!=null)foreach(var image in sparkles)if(image!=null)image.gameObject.SetActive(false);
        if(energyOrbs!=null)foreach(var image in energyOrbs)if(image!=null)image.gameObject.SetActive(false);
        for(int i=0;i<energyStart.Length;i++)energyStart[i]=-100;
        Stage="Idle";
    }
    public void BeginSpin(){Stage="Spin";centerTitle.text="好运转动中";centerSubtitle.text="即将揭晓强化奖励";status.text="强化抽取中…";footer.text="";}
    public void NotifyWinner(int index,RectTransform slot)
    {
        if(index<0||index>=energyStart.Length||energyOrbs==null||index>=energyOrbs.Length||effectsLayer==null)return;
        energyFrom[index]=effectsLayer.InverseTransformPoint(slot.TransformPoint(slot.rect.center));
        energyStart[index]=Time.unscaledTime;
    }
    void LateUpdate()
    {
        if(energyOrbs==null)return;
        for(int i=0;i<Mathf.Min(3,energyOrbs.Length);i++)
        {
            var image=energyOrbs[i];if(image==null)continue;
            float t=(Time.unscaledTime-energyStart[i])/.32f;
            image.gameObject.SetActive(t>=0&&t<1);if(t<0||t>=1)continue;
            float p=Ease(t),u=1-p;
            image.rectTransform.anchoredPosition=u*u*energyFrom[i]+2*u*p*Point(540,650)+p*p*Point(540,820);
            image.color=new Color(1,.87f,.55f,1-t*.6f);
        }
    }
    public IEnumerator Charge(bool skip)
    {
        Cache();Stage="Charge";centerTitle.text="惊喜即将开启";centerSubtitle.text="准备迎接新的力量";status.text="宝箱开启中…";
        if(!skip)
        {
            float t=0,duration=Mathf.Max(.01f,chargeDuration);
            while(t<duration)
            {
                float p=t/duration;
                chestMotion.localScale=Vector3.one*(1+p*.055f);
                chestMotion.localRotation=Quaternion.Euler(0,0,Mathf.Sin(p*38)*3.1f*p);
                chestMotion.anchoredPosition=chestPosition+Vector2.right*(Mathf.Sin(p*45)*5.5f*p);
                Alpha(warmGlow,p*.6f);t+=Time.unscaledDeltaTime;yield return null;
            }
        }
        chestMotion.anchoredPosition=chestPosition;chestMotion.localRotation=Quaternion.identity;
    }
    public IEnumerator Reveal(WheelSlotData[] data,WheelRewardSkillCell[] cells,bool skip)
    {
        int count=Mathf.Min(data.Length,cells.Length);Stage="Opening";
        if(instructions!=null)instructions.SetActive(false);
        centerTitle.text=count>=3?"三重强化！":count==2?"双重强化！":"技能强化！";
        centerTitle.fontSize=46;centerTitle.color=new Color32(164,111,35,255);
        centerSubtitle.text="收获 "+count+" 次技能升级";status.text="正在收取强化奖励…";
        resultCount.text=count+" 次升级";
        LayoutRewards(cells,count);Canvas.ForceUpdateCanvases();
        AudioService.Ensure().Play(AudioId.UiSwitch);
        float opening=skip?.13f:Mathf.Max(.01f,openDuration),elapsed=0;
        while(elapsed<opening)
        {
            float p=elapsed/opening;Alpha(chestClosed,1-Mathf.Clamp01(elapsed/.1f));Alpha(chestOpen,Mathf.Clamp01(elapsed/.1f));
            chestMotion.localScale=Vector3.one*(.92f+.08f*Pop(p));
            Alpha(revealFlash,Mathf.Sin(Mathf.Clamp01(elapsed/.22f)*Mathf.PI)*.66f);
            ApplyBurst(elapsed,count);elapsed+=Time.unscaledDeltaTime;yield return null;
        }
        Alpha(chestClosed,0);Alpha(chestOpen,1);Alpha(revealFlash,0);chestMotion.localScale=Vector3.one;
        Stage="Rewards";
        float duration=skip?.29f:Mathf.Max(.01f,flightDuration),stagger=skip?.08f:flightStagger;
        float total=duration+Mathf.Max(0,count-1)*stagger+.16f;
        var targets=new Vector2[count];var landed=new bool[count];
        for(int i=0;i<count;i++)
        {
            targets[i]=effectsLayer.InverseTransformPoint(cells[i].icon.rectTransform.TransformPoint(cells[i].icon.rectTransform.rect.center));
            if(i<flyingIcons.Length){flyingIcons[i].sprite=data[i].def!=null?data[i].def.icon:null;flyingIcons[i].preserveAspect=true;}
        }
        elapsed=0;
        while(elapsed<total)
        {
            ApplyBurst(opening+elapsed,count);
            for(int i=0;i<count;i++)
            {
                float q=(elapsed-i*stagger)/duration;
                if(i>=flyingCards.Length)continue;
                var flying=flyingCards[i];var rt=(RectTransform)flying.transform;
                if(q>=0&&q<1)
                {
                    Alpha(flying,Mathf.Clamp01(q/.09f));
                    rt.anchoredPosition=Flight(Ease(q),i,count,targets[i]);
                    rt.localScale=Vector3.one*(1+Mathf.Sin(q*Mathf.PI)*.2f);
                    rt.localRotation=Quaternion.Euler(0,0,Mathf.Sin(q*Mathf.PI)*(i%2==0?-8:8));
                }
                else Alpha(flying,0);
                if(q>=1)
                {
                    Alpha(Group(cells[i]),1);
                    cells[i].transform.localScale=Vector3.one*(.9f+.1f*Pop((q-1)*duration/.16f));
                    if(!landed[i]){landed[i]=true;AudioService.Ensure().Play(AudioId.WheelTick);}
                }
            }
            elapsed+=Time.unscaledDeltaTime;yield return null;
        }
        for(int i=0;i<count;i++){Alpha(Group(cells[i]),1);cells[i].transform.localScale=Vector3.one;}
        Alpha(warmGlow,0);Alpha(revealRays,0);if(sparkles!=null)foreach(var image in sparkles)image.gameObject.SetActive(false);
        status.text="";footer.text="技能已提升，继续清扫字灵！";Stage="Complete";
    }
    void LayoutRewards(WheelRewardSkillCell[] cells,int count)
    {
        float width=count==1?480:count==2?374:264,gap=count==3?36:32;
        float start=(960-count*width-Mathf.Max(0,count-1)*gap)/2;
        for(int i=0;i<cells.Length;i++)
        {
            if(cells[i]==null)continue;
            cells[i].gameObject.SetActive(i<count);Alpha(Group(cells[i]),0);
            if(i>=count)continue;
            var rt=(RectTransform)cells[i].transform;rt.anchorMin=rt.anchorMax=rt.pivot=new Vector2(0,1);
            rt.anchoredPosition=new Vector2(start+i*(width+gap),-1138);rt.sizeDelta=new Vector2(width,190);rt.localScale=Vector3.one;
        }
    }
    static Vector2 Flight(float p,int index,int count,Vector2 destination)
    {
        float u=1-p,side=index-(count-1)/2f;
        return u*u*u*Point(540,738)+3*u*u*p*Point(540+side*170,475)+3*u*p*p*new Vector2(destination.x,Point(0,1060).y)+p*p*p*destination;
    }
    void ApplyBurst(float elapsed,int count)
    {
        float fade=Mathf.Clamp01(1-elapsed/1.25f),intensity=count>=3?1:count==2?.8f:.6f;
        Alpha(warmGlow,fade*.65f*intensity);Alpha(revealRays,fade*.68f*intensity);
        if(revealRays!=null)revealRays.transform.localRotation=Quaternion.Euler(0,0,elapsed*12);
        if(sparkles==null)return;
        for(int i=0;i<sparkles.Length;i++)
        {
            var image=sparkles[i];bool active=i<6+count*3&&elapsed<1.2f;
            image.gameObject.SetActive(active);if(!active)continue;
            float angle=i*2.39996f,r=Mathf.Lerp(50,180,Mathf.Clamp01(elapsed*1.1f))+(i%3)*12;
            image.rectTransform.anchoredPosition=Point(540+Mathf.Cos(angle)*r,770+Mathf.Sin(angle)*r-elapsed*58);
            image.color=i%2==0?new Color(1,.9f,.65f,fade):new Color(.77f,.63f,.32f,fade);
        }
    }
}
