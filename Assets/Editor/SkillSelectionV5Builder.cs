#if UNITY_EDITOR
using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using U = HomeUIBuilderUtility;

/// <summary>Incremental edits to existing prefabs; preserves GUIDs, gameplay and platform spacing.</summary>
public static class SkillSelectionV5Builder
{
    private const string Cards = "Assets/Prefab/CardSelect/";
    [MenuItem("工具/技能选卡/Apply Stars and Press Feedback V5")]
    public static void Menu() { Debug.Log(Apply()); }
    public static string Apply()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play mode first.");
        U.Font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Resources/Fonts/msyh SDF.asset");
        U.TextMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Res/image/Game/select/SkillSelectTextV4.mat");
        Edit(Cards + "SkillSelectSlotV4.prefab", r => StyleSlot(r, false));
        Edit(Cards + "SkillSelectBondV4.prefab", r => StyleSlot(r, true));
        Edit(Cards + "CardView.prefab", StyleCard);
        Edit(Cards + "CardSelectionPanel.prefab", r => {
            var p = r.GetComponent<CardSelectionPanel>();
            if (p.selectionLayout != null && p.selectionLayout.hint != null)
            {
                p.selectionLayout.hint.GetComponent<TMP_Text>().text = "按下卡片，松开确认 · 滑出取消";
                p.selectionLayout.hint.GetComponent<TMP_Text>().color = U.ColorOf("FFF2CE");
                var shadow = p.selectionLayout.hint.GetComponent<Shadow>() ?? p.selectionLayout.hint.gameObject.AddComponent<Shadow>();
                shadow.effectColor = new Color(.2f, .13f, .08f, .8f); shadow.effectDistance = new Vector2(0, -2);
            }
        });
        Edit("Assets/Prefab/Battle/GameLayer.prefab", StyleEnergy);
        AssetDatabase.SaveAssets();
        return "V5 updated: five-star previews, matched icon shimmer/bounce, press/release confirmation, segmented energy HUD. Existing prefab GUIDs and single font retained.";
    }
    private static void Edit(string path, Action<GameObject> edit)
    {
        var root = PrefabUtility.LoadPrefabContents(path);
        try { edit(root); PrefabUtility.SaveAsPrefabAsset(root, path); }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }
    public static string ApplySlotRefinement()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play mode first.");
        Edit(Cards + "SkillSelectSlotV4.prefab", r => StyleSlot(r, false));
        Edit(Cards + "SkillSelectBondV4.prefab", r => StyleSlot(r, true));
        Edit(Cards + "CardView.prefab", root => {
            var card = root.GetComponent<CardView>();
            card.starRow = Stars(card.pressVisual, "LevelStarsV5", 26, 740, 40, 6);
        });
        return "Centered icon anchors/pivots, 84px equipped icons, original Lv badges and larger 40px card stars; pulse frequency unchanged.";
    }
    public static string ApplyLayeredStars()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play mode first.");
        Edit(Cards + "CardView.prefab", root => {
            var card = root.GetComponent<CardView>();
            card.starRow = Stars(card.pressVisual, "LevelStarsV5", 26, 740, 40, 6);
        });
        return "Card stars now have persistent empty backings and separate blinking filled overlays. Top Lv badges and icon feedback retained.";
    }
    private static SkillStarRow Stars(Transform parent, string name, float x, float y, float size, float gap)
    {
        float width = 5 * size + 4 * gap;
        var root = U.Node(parent, name, x, y, width, size);
        var row = root.GetComponent<SkillStarRow>() ?? root.gameObject.AddComponent<SkillStarRow>();
        row.filledSprite = U.Sprite("star_filled", "Assets/Res/image/Home/roadmap/");
        row.emptySprite = U.Sprite("star_empty", "Assets/Res/image/Home/roadmap/");
        row.stars = new Image[5];
        row.emptyStars = new Image[5];
        for (int i = 0; i < 5; i++)
        {
            var backing = U.Image(root, "Star" + (i + 1), row.emptySprite, i * (size + gap), 0, size, size);
            backing.preserveAspect = true;
            row.emptyStars[i] = backing;
            row.stars[i] = U.Image(backing.transform, "Filled", row.filledSprite, 0, 0, size, size);
            row.stars[i].preserveAspect = true;
            row.stars[i].rectTransform.anchorMin = row.stars[i].rectTransform.anchorMax = row.stars[i].rectTransform.pivot = new Vector2(.5f, .5f);
            row.stars[i].rectTransform.anchoredPosition = Vector2.zero;
        }
        return row;
    }
    private static void StyleSlot(GameObject root, bool bonded)
    {
        var cell = root.GetComponent<SkillSlotCell>();
        var so = new SerializedObject(cell);
        var icon = (Image)so.FindProperty("iconImage").objectReferenceValue;
        var badge = (GameObject)so.FindProperty("levelBadge").objectReferenceValue;
        var level = (TMP_Text)so.FindProperty("levelText").objectReferenceValue;
        if (badge != null) badge.SetActive(false);
        if (level != null) level.gameObject.SetActive(true);
        cell.starRow = null;
        var oldStars = root.transform.Find("SkillStarsV5");
        if (oldStars != null) UnityEngine.Object.DestroyImmediate(oldStars.gameObject);
        // The icon itself is animated, so both anchors AND the pivot must be centered.
        icon.rectTransform.anchorMin = icon.rectTransform.anchorMax = icon.rectTransform.pivot = new Vector2(.5f, .5f);
        icon.rectTransform.anchoredPosition = Vector2.zero;
        icon.rectTransform.sizeDelta = Vector2.one * (bonded ? 64 : 84);
        icon.rectTransform.localScale = Vector3.one;
        var mask = icon.GetComponent<Mask>() ?? icon.gameObject.AddComponent<Mask>();
        mask.showMaskGraphic = true;
        var stripe = U.Image(icon.transform, "BondShimmerV5", null, 0, 0, bonded ? 19 : 21, 110);
        stripe.rectTransform.anchorMin = stripe.rectTransform.anchorMax = stripe.rectTransform.pivot = new Vector2(.5f, .5f);
        stripe.rectTransform.anchoredPosition = Vector2.zero;
        stripe.rectTransform.localRotation = Quaternion.Euler(0, 0, -24);
        stripe.color = Color.clear; stripe.gameObject.SetActive(false);
        cell.shimmerImage = stripe;
    }
    private static void StyleCard(GameObject root)
    {
        var card = root.GetComponent<CardView>();
        var face = root.transform.Find("CardFaceV5") as RectTransform;
        if (face == null)
        {
            face = U.Node(root.transform, "CardFaceV5", 138, 400, 276, 800);
            face.pivot = new Vector2(.5f, .5f);
            foreach (var child in root.transform.Cast<Transform>().ToArray())
            {
                if (child == face || child == card.bondedPanel) continue;
                var rt = child as RectTransform;
                var position = rt != null ? rt.anchoredPosition : Vector2.zero;
                child.SetParent(face, false);
                if (rt != null) rt.anchoredPosition = position;
            }
        }
        card.pressVisual = face;
        if (card.levelText != null) card.levelText.gameObject.SetActive(false);
        U.Rect(card.gainLabelText.rectTransform, 12, 352, 252, 44);
        U.Rect(card.gainValueText.rectTransform, 12, 400, 252, 68);
        U.Rect(card.dividerImage.rectTransform, 24, 481, 228, 2);
        U.Rect(card.descriptionScroll.viewport, 22, 500, 232, 204);
        U.Rect(card.descText.rectTransform, 0, 0, 232, 204);
        if (card.descriptionScrollHint != null) U.Rect((RectTransform)card.descriptionScrollHint.transform, 18, 709, 240, 22);
        card.starRow = Stars(face, "LevelStarsV5", 26, 740, 40, 6);
        var settings = new SerializedObject(card);
        settings.FindProperty("clickTargetScale").floatValue = 1.035f;
        settings.FindProperty("clickAnimDuration").floatValue = .42f;
        settings.FindProperty("fadeOutDuration").floatValue = .25f;
        settings.ApplyModifiedPropertiesWithoutUndo();
    }
    private static void StyleEnergy(GameObject root)
    {
        var hud = root.GetComponent<GameLayer>();
        var bar = hud.energyProgressSlider;
        var group = bar.transform.parent;
        var oldBadge = group.Find("LevelFace");
        if (oldBadge != null) oldBadge.gameObject.SetActive(false);
        U.Rect((RectTransform)bar.transform, 40, 312, 1000, 54);
        foreach (string old in new[] { "Track", "FillArea" })
        {
            var child = bar.transform.Find(old);
            if (child != null) child.gameObject.SetActive(false);
        }
        bar.fillRect = null;
        var cream = U.Sprite("surface_soft_9slice", "Assets/Res/image/Game/battle/");
        U.Image(bar.transform, "EnergyFrameV5", cream, 0, 0, 1000, 54);
        U.Image(bar.transform, "EnergyTrackV5", cream, 3, 3, 994, 48).color = U.ColorOf("263D33");
        var segmentRoot = U.Node(bar.transform, "EnergySegmentsV5", 60, 10, 812, 34);
        if (segmentRoot.GetComponent<CanvasRenderer>() == null) segmentRoot.gameObject.AddComponent<CanvasRenderer>();
        var segments = segmentRoot.GetComponent<EnergySegmentGraphic>() ?? segmentRoot.gameObject.AddComponent<EnergySegmentGraphic>();
        segments.raycastTarget = false; segments.segmentCount = 20;
        var orb = U.Image(bar.transform, "EnergyOrbV5", U.Sprite("energy_orb", "Assets/Res/image/Game/battle/"), 8, 6, 42, 42);
        orb.preserveAspect = true;
        var badge = U.Image(bar.transform, "EnergyLevelV5", cream, 891, 4, 105, 46);
        badge.color = U.ColorOf("DAEDAA");
        hud.energyProgressText.transform.SetParent(badge.transform, false);
        U.Rect(hud.energyProgressText.rectTransform, 0, 0, 105, 46);
        U.Style(hud.energyProgressText, 29, U.ColorOf("28442D"), true);
        hud.energyLevelBadge = badge.gameObject;
        var view = bar.GetComponent<EnergyProgressPresentation>() ?? bar.gameObject.AddComponent<EnergyProgressPresentation>();
        view.segments = segments; view.levelBadge = badge.rectTransform; view.energyOrb = orb;
        view.caption = U.Text(bar.transform, "EnergyCaptionV5", "升级能量", 0, -26, 570, 24, 23, U.Ink, true, TextAlignmentOptions.Left);
        view.countText = U.Text(bar.transform, "EnergyCountV5", "0 / 10", 650, -26, 220, 24, 23, U.Ink, false, TextAlignmentOptions.Right);
        hud.energyPresentation = view;
    }
}
#endif
