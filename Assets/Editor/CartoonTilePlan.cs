using System;

/// <summary>纯数据排图：从 . / # 蓝图计算47种边缘及地表变体，不依赖Unity运行时。</summary>
public static class CartoonTilePlan
{
    public static string[] Build(string[] rows, string family, string background)
    {
        if (rows == null || rows.Length == 0 || string.IsNullOrEmpty(rows[0]))
            throw new ArgumentException("蓝图为空。");
        string foreground;
        if (family == "dirt_on_grass" && background == "grass") foreground = "dirt";
        else if (family == "stone_on_grass" && background == "grass") foreground = "stone";
        else if (family == "sand_on_water" && background == "water") foreground = "dirt";
        else if (family == "autumn_stone_on_earth" && background == "autumn_earth") foreground = "autumn_stone";
        else throw new ArgumentException("不支持的地形过渡：" + family + "/" + background);
        int width = rows[0].Length, height = rows.Length;
        foreach (string row in rows)
        {
            if (row == null || row.Length != width) throw new ArgumentException("蓝图行宽不一致。");
            foreach (char c in row) if (c != '.' && c != '#') throw new ArgumentException("蓝图只支持 . 和 #。");
        }
        var names = new string[width * height];
        for (int y = 0; y < height; y++) for (int x = 0; x < width; x++)
        {
            bool autumn = family == "autumn_stone_on_earth";
            int variant = autumn ? 1 + (x & 1) + (y & 1) * 2 : 1 + (x * 7 + y * 11) % 4;
            if (!At(rows, x, y)) { names[y * width + x] = background + "_" + variant; continue; }
            int mask = 0;
            if (At(rows, x, y - 1)) mask |= 1;
            if (At(rows, x + 1, y - 1)) mask |= 2;
            if (At(rows, x + 1, y)) mask |= 4;
            if (At(rows, x + 1, y + 1)) mask |= 8;
            if (At(rows, x, y + 1)) mask |= 16;
            if (At(rows, x - 1, y + 1)) mask |= 32;
            if (At(rows, x - 1, y)) mask |= 64;
            if (At(rows, x - 1, y - 1)) mask |= 128;
            if ((mask & 1) == 0 || (mask & 4) == 0) mask &= ~2;
            if ((mask & 4) == 0 || (mask & 16) == 0) mask &= ~8;
            if ((mask & 16) == 0 || (mask & 64) == 0) mask &= ~32;
            if ((mask & 64) == 0 || (mask & 1) == 0) mask &= ~128;
            names[y * width + x] = mask == 255 ? foreground + "_" + variant : family + "_m" + mask.ToString("000") + (autumn ? "_v" + variant : "");
        }
        return names;
    }

    private static bool At(string[] rows, int x, int y)
    {
        return y >= 0 && y < rows.Length && x >= 0 && x < rows[0].Length && rows[y][x] == '#';
    }
}
