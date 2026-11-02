using UnityEngine;

/// <summary>五张整身瞄准姿态，左右镜像覆盖八向；不拆分身体或武器。</summary>
[CreateAssetMenu(menuName = "Game/Character Aim Frames", fileName = "CharacterAimFrames")]
public sealed class CharacterAimFrames : ScriptableObject
{
    public Sprite right;
    public Sprite upRight;
    public Sprite up;
    public Sprite downRight;
    public Sprite down;

    [Range(0f, 10f)] public float directionHysteresis = 6f;
    public bool IsComplete => right != null && upRight != null && up != null && downRight != null && down != null;

    // Sector 0 = right, 1 = up-right, ... counterclockwise.
    public Sprite GetFrame(int sector, out bool mirrored)
    {
        sector = (sector % 8 + 8) % 8;
        mirrored = sector >= 3 && sector <= 5;
        switch (sector)
        {
            case 0: case 4: return right;
            case 1: case 3: return upRight;
            case 2: return up;
            case 5: case 7: return downRight;
            default: return down;
        }
    }
}
