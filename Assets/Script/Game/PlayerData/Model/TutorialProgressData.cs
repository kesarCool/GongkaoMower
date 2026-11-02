using System;

/// <summary>Only successful actions count as completed. Opt-outs are stored separately.</summary>
[Serializable]
public sealed class TutorialProgressData
{
    public bool skipped;
    public bool battleSettled;
    public string[] completed = Array.Empty<string>();
    public string[] skippedSteps = Array.Empty<string>();

    public bool HasCompleted(string id) => Array.IndexOf(completed ?? Array.Empty<string>(), id) >= 0;
    public bool HasHandled(string id) => skipped || HasCompleted(id) ||
        Array.IndexOf(skippedSteps ?? Array.Empty<string>(), id) >= 0;

    public bool Record(string id, bool skip)
    {
        if (HasHandled(id)) return false;
        if (skip) Append(ref skippedSteps, id);
        else Append(ref completed, id);
        return true;
    }

    static void Append(ref string[] values, string id)
    {
        if (values == null) values = Array.Empty<string>();
        Array.Resize(ref values, values.Length + 1);
        values[values.Length - 1] = id;
    }
}
