#if UNITY_EDITOR
using System.Collections;
using System.Globalization;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>Displayed growth must equal the displayed endpoints of the purchased level.</summary>
public sealed class CharacterAttributeDeltaValidationRunner : UIValidationRunnerBase
{
    protected override string OutputFolder => "CharacterAttributeDelta";
    private bool previousBackground;

    private void Awake() { previousBackground = Application.runInBackground; Application.runInBackground = true; }

    protected override IEnumerator RunChecks()
    {
        CheckDelta("Rounded attack increase", 42.75862f, 45.51724f, "F0", "", "", "+3");
        CheckDelta("Following level has a different shown increase", 45.51724f, 48.27586f, "F0", "", "", "+2");
        CheckDelta("Round endpoints before subtracting HP", 85.51724f, 91.03448f, "F0", "", "", "+5");
        CheckDelta("Movement speed retains one decimal", 6.041379f, 6.082759f, "F1", "", "", "+0.1");
        CheckDelta("Percentage uses percentage-point difference", 30.34483f, 30.68966f, "F0", "", "%", "+1%");
        CheckDelta("Multiplier retains one decimal", 2.103448f, 2.206897f, "F1", "×", "", "+0.1");
        CheckDelta("Unchanged integer stays quiet", 10f, 10.34483f, "F0", "", "", "");
        CheckDelta("Unchanged decimal stays quiet", 1f, 1.006897f, "F1", "×", "", "");
        CheckDelta("Unchanged percentage stays quiet", 10f, 10f, "F0", "", "%", "");
        CheckDelta("Signed decline", 45f, 42f, "F0", "", "", "−3");
        var culture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
            CheckDelta("Formatting independent of system decimal separator", 6f, 6.1f, "F1", "", "", "+0.1");
        }
        finally { CultureInfo.CurrentCulture = culture; }

        PlayerProfileService.Instance.AddGold(100000);
        tabs.SwitchTo("character"); yield return null;
        var panel = FindObjectOfType<CharacterSelectionPanel>();
        var catalog = AssetDatabase.LoadAssetAtPath<CharacterCatalog>("Assets/ScriptableObject/Character/CharacterCatalog.asset");
        var hero = catalog.characters.First(c => c.startingSkill == SkillId.AutoProjectileSword);
        panel.SelectCharacter(hero.characterId); panel.SetStatsView(true);
        Record("Fixture starts on actual level two", PlayerProfileService.Instance.GetHeroLevel(hero.characterId) == 2);
        yield return CheckUpgrade(panel, hero, 3, new[] { "+3", "+5", "+1", "+0.1", "", "+0.1", "+1%", "", "+1%" });
        yield return CheckUpgrade(panel, hero, 4, new[] { "+2", "+6", "", "", "", "+0.1", "", "", "+1%" });
    }

    private void CheckDelta(string name, float before, float after, string format, string prefix, string suffix, string expected)
    {
        var oldValue = new CharacterGrowthValue("test", before, prefix, suffix, format);
        var newValue = new CharacterGrowthValue("test", after, prefix, suffix, format);
        string actual = newValue.FormatDeltaFrom(oldValue);
        Record(name, actual == expected, oldValue.FormattedValue + " -> " + newValue.FormattedValue + "; expected '" + expected + "', actual '" + actual + "'");
    }

    private IEnumerator CheckUpgrade(CharacterSelectionPanel panel, CharacterDefinition hero, int targetLevel, string[] expected)
    {
        bool upgraded = panel.TryUpgradeSelected();
        Record("Level " + targetLevel + " commits actual upgrade", upgraded && PlayerProfileService.Instance.GetHeroLevel(hero.characterId) == targetLevel);
        Record("Level " + targetLevel + " all row deltas match visible endpoints", panel.AttributeRows.Select(r => r.DeltaText).SequenceEqual(expected),
            string.Join(", ", panel.AttributeRows.Select(r => r.DeltaText)));
        Record("Level " + targetLevel + " zero display deltas do not animate", panel.AttributeRows.Select((r,i) => expected[i] != "" || !r.IsAnimating).All(value => value));
        yield return Capture("upgrade-to-" + targetLevel);
        yield return new WaitForSecondsRealtime(.5f);
        var final = CharacterGrowthSnapshot.Capture(hero);
        Record("Level " + targetLevel + " settles on actual final stats", !panel.IsGrowthFeedbackPlaying && panel.AttributeRows.Select((r,i) =>
            r.DisplayedText == final.Stats[i].FormattedValue && Mathf.Approximately(r.DisplayedValue, final.Stats[i].Value) && r.DeltaText == "").All(value => value));
    }

    protected override void OnDestroy() { Application.runInBackground = previousBackground; base.OnDestroy(); }
}
#endif
