#if UNITY_EDITOR
using System;
using UnityEngine;

namespace GongkaoMower.VfxAuthoring
{
    [Serializable] public sealed class VfxCurveKey
    {
        public float time, value, inTangent, outTangent;
        public Keyframe Key() => new Keyframe(time, value, inTangent, outTangent);
        public static VfxCurveKey From(Keyframe k) => new VfxCurveKey { time = k.time, value = k.value, inTangent = k.inTangent, outTangent = k.outTangent };
    }
    [Serializable] public sealed class VfxAlphaKey { public float time, alpha; }
    [Serializable] public sealed class VfxLayer
    {
        public string name;
        public float seconds, sizeX, sizeY, offsetX, offsetY, startFrame;
        public bool size3D;
        public uint seed = 1;
        public VfxCurveKey[] frames;
        public VfxAlphaKey[] alpha;
    }
    [Serializable] public sealed class VfxPacking
    {
        public int sourceColumns = 4, sourceRows = 4, paddingPixels = 8;
        // Destination cells in left-to-right, top-to-bottom order; -1 leaves an empty cell.
        public int[] sourceFrames;
    }
    [Serializable] public sealed class VfxRecipe
    {
        public int schemaVersion = 1;
        public string id, displayName, category, usage;
        public string status = "draft-needs-combat-validation";
        public string texturePath, materialPath, prefabPath;
        public string briefPath, promptPath, sourceDirectory, validationReport;
        public int width, height, columns, rows, pngBudgetKiB = 128, compressionQuality = 65;
        public float poolSeconds, referenceRadius;
        public int sortingOrder = -5;
        public VfxLayer[] layers;
        public VfxPacking packing;
    }
}
#endif
