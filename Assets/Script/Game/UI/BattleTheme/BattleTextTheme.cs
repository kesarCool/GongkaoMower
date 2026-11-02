using TMPro;
using UnityEngine;

/// <summary>Four shared SDF material presets; every preset references the existing Chinese font atlas.</summary>
public static class BattleTextTheme
{
    public enum Role { Word, Damage, Health, Hud }
    static readonly Material[] materials = new Material[4];
    static TMP_FontAsset sourceFont;
    public static readonly Color WordFace = new Color32(255,245,221,255);
    public static readonly Color BossFace = new Color32(255,139,101,255);
    public static readonly Color WordOutline = new Color32(52,39,31,255);
    public static readonly Color Cream = new Color32(255,243,212,255);
    public static readonly Color Brown = new Color32(73,54,39,255);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset()
    {
        for(int i=0;i<materials.Length;i++)
        {
            if(materials[i]!=null) Object.Destroy(materials[i]);
            materials[i]=null;
        }
        sourceFont=null;
    }
    public static Material MaterialFor(TMP_FontAsset font,Role role)
    {
        if(font==null)return null;
        if(sourceFont!=font){Reset();sourceFont=font;}
        int index=(int)role;
        if(materials[index]!=null)return materials[index];
        var material=new Material(font.material){name="battle_text_"+role.ToString().ToLowerInvariant(),hideFlags=HideFlags.HideAndDontSave};
        material.SetColor("_FaceColor",Color.white);
        material.SetColor("_OutlineColor",role==Role.Word?WordOutline:role==Role.Damage?Brown:Cream);
        material.SetFloat("_OutlineWidth",role==Role.Hud ? .08f : role==Role.Health ? .16f : role==Role.Damage ? .24f : .32f);
        material.SetFloat("_OutlineSoftness",role==Role.Word ? .006f : .015f);
        material.SetFloat("_FaceDilate",role==Role.Word ? .16f : role==Role.Damage ? .04f : 0);
        material.EnableKeyword("OUTLINE_ON");
        foreach(var keyword in new[]{"UNDERLAY_ON","UNDERLAY_INNER","GLOW_ON"})material.DisableKeyword(keyword);
        if(material.HasProperty("_UnderlayColor"))material.SetColor("_UnderlayColor",Color.clear);
        materials[index]=material;return material;
    }
    public static void Apply(TMP_Text text,Role role)
    {
        if(text==null)return;
        BattleChineseFontRuntime.EnsureLoaded();var font=BattleChineseFontRuntime.LoadedFont;
        if(font==null)return;
        if(text.font!=font)text.font=font;
        text.fontSharedMaterial=MaterialFor(font,role);
        text.UpdateMeshPadding();
    }
}
