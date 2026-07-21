using System;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

/// <summary>
/// 礼包码服务：本地 SHA256 验证 + 每码一次 + 每日限兑一次。
/// 无后端依赖，哈希硬编码防小白反编译。
/// </summary>
public static class GiftCodeService
{
    private const string ClaimedHashesKey = "gift_code_claimed_hashes";
    private const string LastClaimDateKey = "gift_code_last_claim_date";

    /// <summary>
    /// 有效礼包码的 SHA256 哈希列表（由 Tools/生成礼包码哈希 编辑器工具生成）。
    /// 输入统一 ToUpper 后再做哈希比对。
    /// </summary>
    private static readonly string[] ValidHashes =
    {
        "2a4dc8efef0445508ac214b058982eaa11acde509b09da3756a8f1f88c6f0a50", // WENZIGECAO666
        "93dc5b31431aa65bdc5cdecfc06ed417a19c326931237a2672d5d64da7c0c606", // WENZIGECAO777
        "7914cdb18fd4179b9ae6cb7a6b2e04f69b8a0a77184734fa8b948e4bf202d0e8", // WENZIGECAO888
        "1efc13a15a9a379e4602191d07177c2dafa1110d5eab829ddca1245b86d4ffa4", // WENZIGECAO999

    };

    /// <summary>今日是否还可以兑换（每日限 1 次）。</summary>
    public static bool CanClaimToday
    {
        get
        {
            string today = DateTime.Now.ToString("yyyy-MM-dd");
            string lastDate = PlayerPrefs.GetString(LastClaimDateKey, "");
            return lastDate != today;
        }
    }

    /// <summary>
    /// 尝试兑换礼包码。
    /// </summary>
    /// <returns>null 表示成功；否则返回用户可读的错误文案。</returns>
    public static string TryRedeem(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
            return "请输入礼包码";

        if (!CanClaimToday)
            return "今日已兑换过，每日限兑换一次";

        string normalized = code.Trim().ToUpperInvariant();
        string hash = ComputeSha256(normalized);

        // 校验是否在有效码列表中
        bool valid = false;
        for (int i = 0; i < ValidHashes.Length; i++)
        {
            if (string.Equals(ValidHashes[i], hash, StringComparison.Ordinal))
            {
                valid = true;
                break;
            }
        }

        if (!valid)
            return "礼包码无效";

        // 检查该码是否已被兑换过
        if (HasClaimedHash(hash))
            return "该礼包码已兑换过";

        // 发钻
        if (PlayerProfileService.Instance == null)
            return "数据服务未就绪，请稍后再试";

        MarkClaimed(hash);
        PlayerProfileService.Instance.AddDiamond(100);

        return null; // success
    }

    /// <summary>指定哈希的码是否已兑换过。</summary>
    private static bool HasClaimedHash(string hash)
    {
        string claimed = PlayerPrefs.GetString(ClaimedHashesKey, "");
        if (string.IsNullOrEmpty(claimed)) return false;

        string[] parts = claimed.Split(',');
        for (int i = 0; i < parts.Length; i++)
        {
            if (parts[i] == hash) return true;
        }
        return false;
    }

    /// <summary>标记兑换：记入已兑哈希列表 + 更新今日日期。</summary>
    private static void MarkClaimed(string hash)
    {
        string claimed = PlayerPrefs.GetString(ClaimedHashesKey, "");
        if (!string.IsNullOrEmpty(claimed))
            claimed += ",";
        claimed += hash;

        PlayerPrefs.SetString(ClaimedHashesKey, claimed);
        PlayerPrefs.SetString(LastClaimDateKey, DateTime.Now.ToString("yyyy-MM-dd"));
        PlayerPrefs.Save();
    }

    private static string ComputeSha256(string input)
    {
        using var sha = SHA256.Create();
        byte[] bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(input));
        var sb = new StringBuilder(bytes.Length * 2);
        for (int i = 0; i < bytes.Length; i++)
            sb.Append(bytes[i].ToString("x2"));
        return sb.ToString();
    }
}
