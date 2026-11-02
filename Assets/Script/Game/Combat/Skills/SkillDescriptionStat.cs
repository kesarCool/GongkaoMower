using System.Globalization;
using UnityEngine;

/// <summary>A configured stat and its display unit; contains no handwritten numerical claims.</summary>
public readonly struct SkillDescriptionStat
{
    private enum ChangeKind { Values, RelativeDamage, AttackRate, Count }
    public string Label { get; }
    public float Value { get; }
    private readonly string unit;
    private readonly float scale;
    private readonly bool signed;
    private readonly ChangeKind change;

    private SkillDescriptionStat(string label, float value, string unit, float scale, bool signed, ChangeKind change)
    {
        Label = label;
        Value = value;
        this.unit = unit;
        this.scale = scale;
        this.signed = signed;
        this.change = change;
    }

    public static SkillDescriptionStat Damage(string label, float multiplier) =>
        new SkillDescriptionStat(label, multiplier, "%攻击力", 100, false, ChangeKind.RelativeDamage);

    public static SkillDescriptionStat Number(string label, float value, string unit = "") =>
        new SkillDescriptionStat(label, value, unit, 1, false, ChangeKind.Values);

    public static SkillDescriptionStat Count(string label, int value, string unit) =>
        new SkillDescriptionStat(label, value, unit, 1, false, ChangeKind.Count);

    public static SkillDescriptionStat AttackInterval(float seconds) =>
        new SkillDescriptionStat("攻击间隔", seconds, "秒", 1, false, ChangeKind.AttackRate);

    public static SkillDescriptionStat Bonus(string label, float value, bool percentage, bool signed, string unit = "") =>
        new SkillDescriptionStat(label, value, (percentage ? "%" : "") + unit, percentage ? 100 : 1, signed, ChangeKind.Values);

    public string FormatValue() => Format(Value * scale, signed) + unit;
    public string FormatSnapshot() => Label + "：" + FormatValue();

    public string FormatUpgrade(SkillDescriptionStat before)
    {
        if (Mathf.Approximately(Value, before.Value)) return null;
        if (change == ChangeKind.RelativeDamage && before.Value > 0)
            return Label + Format((Value / before.Value - 1) * 100, true) + "%";
        if (change == ChangeKind.AttackRate && Value > 0 && before.Value > 0)
            return "射速" + Format((before.Value / Value - 1) * 100, true) + "%";
        if (change == ChangeKind.Count)
            return Label + Format(Value - before.Value, true) + unit;
        return Label + "：" + before.FormatValue() + " → " + FormatValue();
    }

    private static string Format(float value, bool signed) =>
        value.ToString(signed ? "+0.##;-0.##;0" : "0.##", CultureInfo.InvariantCulture);
}
