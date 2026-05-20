using UnityEngine;

/// <summary>
/// 2D 抛物线运动：起点到终点的线性插值 + 弧高鼓包（4·h·t·(1-t)）。
/// </summary>
public static class ArcMotor2D
{
    public static Vector2 Evaluate(Vector2 start, Vector2 end, float arcHeight, float t)
    {
        t = Mathf.Clamp01(t);
        Vector2 pos = Vector2.Lerp(start, end, t);
        pos.y += arcHeight * 4f * t * (1f - t);
        return pos;
    }

    public static float ComputeFlightDuration(
        Vector2 start,
        Vector2 end,
        float baseTime,
        float perUnitDistance,
        float minTime,
        float maxTime)
    {
        float dist = Vector2.Distance(start, end);
        float duration = baseTime + dist * Mathf.Max(0f, perUnitDistance);
        return Mathf.Clamp(duration, Mathf.Max(0.05f, minTime), Mathf.Max(minTime, maxTime));
    }
}
