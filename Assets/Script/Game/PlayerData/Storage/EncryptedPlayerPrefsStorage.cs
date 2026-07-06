using System;
using System.Text;
using UnityEngine;

/// <summary>
/// 加密 PlayerPrefs 存储：XOR + 校验和 + Base64。
/// 自动兼容旧版明文 JSON 格式：读到旧格式时透明迁移为加密格式。
/// </summary>
public sealed class EncryptedPlayerPrefsStorage : ISaveStorage
{
    // 16 字节密钥（IL2CPP 编译后藏在 native 里，非记事本能直接搜到）
    private static readonly byte[] Key =
    {
        0x4A, 0xF2, 0x8C, 0x1E, 0x9B, 0x55, 0xD7, 0x30,
        0xE3, 0x7B, 0xA1, 0x6D, 0xCC, 0x0F, 0x58, 0x92,
    };

    /// <summary>最近一次 TryLoad 是否因篡改/损坏而失败（不含「键不存在」）。</summary>
    public bool WasLastLoadCorrupted { get; private set; }

    // ── ISaveStorage ──────────────────────────────────

    public bool TryLoad(string key, out string json)
    {
        WasLastLoadCorrupted = false;

        string raw = PlayerPrefs.GetString(key, null);
        if (string.IsNullOrEmpty(raw))
        {
            json = null;
            return false;
        }

        // 1) 尝试解密
        bool decrypted = TryDecrypt(raw, out json);
        if (decrypted)
            return !string.IsNullOrEmpty(json);

        // 2) 校验失败/解密异常 → 可能是旧版明文？透明迁移
        //    明文 JSON 以 { 或 [ 开头，guest ID 以 guest_ 开头
        if (raw.Length > 0 && (raw[0] == '{' || raw[0] == '[' || raw.StartsWith("guest_")))
        {
            GameLog.Info($"[EncryptedStorage] 检测到旧版明文存档（key={key}），迁移为加密格式。");
            json = raw;
            Save(key, json); // 立刻写回加密版本
            return true;
        }

        // 3) 既解不了也不是可识别明文 → 篡改或损坏
        Debug.LogWarning($"[EncryptedStorage] 存档损坏或格式未知（key={key}），丢弃。");
        WasLastLoadCorrupted = true;
        json = null;
        return false;
    }

    public void Save(string key, string json)
    {
        string encoded = Encrypt(json);
        PlayerPrefs.SetString(key, encoded);
        PlayerPrefs.Save();
    }

    public bool TryLoadString(string key, out string value) => TryLoad(key, out value);
    public void SaveString(string key, string value) => Save(key, value);

    // ── 加解密 ────────────────────────────────────────

    private static string Encrypt(string plain)
    {
        byte[] data = Encoding.UTF8.GetBytes(plain);
        byte[] encrypted = new byte[data.Length + 4];

        // XOR
        for (int i = 0; i < data.Length; i++)
            encrypted[i] = (byte)(data[i] ^ Key[i % Key.Length]);

        // 校验和（对明文算）
        uint checksum = ComputeChecksum(data);
        byte[] cs = BitConverter.GetBytes(checksum);
        Buffer.BlockCopy(cs, 0, encrypted, data.Length, 4);

        return Convert.ToBase64String(encrypted);
    }

    private static bool TryDecrypt(string encoded, out string plain)
    {
        plain = null;
        if (string.IsNullOrEmpty(encoded) || encoded.Length < 8 || !IsLikelyBase64(encoded))
            return false;

        byte[] data;
        try { data = Convert.FromBase64String(encoded); }
        catch { return false; }

        if (data.Length < 5) return false;

        int dataLen = data.Length - 4;

        // 校验和
        uint storedCs = BitConverter.ToUInt32(data, dataLen);

        // 解密
        for (int i = 0; i < dataLen; i++)
            data[i] = (byte)(data[i] ^ Key[i % Key.Length]);

        uint computedCs = ComputeChecksum(data, dataLen);
        if (storedCs != computedCs)
        {
            Debug.LogWarning("[EncryptedStorage] 校验和不匹配，存档可能被篡改。");
            return false;
        }

        plain = Encoding.UTF8.GetString(data, 0, dataLen);
        return true;
    }

    private static uint ComputeChecksum(byte[] data, int length)
    {
        uint hash = 0xDEADBEEF;
        for (int i = 0; i < length; i++)
            hash = ((hash << 5) + hash) + data[i]; // FNV-1a 变体
        return hash;
    }

    private static uint ComputeChecksum(byte[] data) => ComputeChecksum(data, data.Length);

    /// <summary>快速筛查：避免在 IL2CPP WebGL (exceptionSupport=None) 中触发 Convert.FromBase64String 的异常导致 Native 级崩溃。</summary>
    private static bool IsLikelyBase64(string s)
    {
        if (s.Length % 4 != 0) return false;
        for (int i = 0; i < s.Length; i++)
        {
            char c = s[i];
            if (c >= 'A' && c <= 'Z') continue;
            if (c >= 'a' && c <= 'z') continue;
            if (c >= '0' && c <= '9') continue;
            if (c == '+' || c == '/') continue;
            if (c == '=' && (i >= s.Length - 2)) continue;
            return false;
        }
        return true;
    }
}
