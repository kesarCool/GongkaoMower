using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;

public static class CartoonTilePlanTests
{
    public class Layout { public string[] terrainNamesRowMajor; public string foregroundFamily, background; }
    private static int checks;
    private static void Equal<T>(T expected, T actual, string label)
    {
        checks++;
        if (!EqualityComparer<T>.Default.Equals(expected, actual)) throw new Exception(label + ": expected=" + expected + ", actual=" + actual);
    }
    private static void Reject(Action action, string label)
    {
        checks++;
        try { action(); } catch (ArgumentException) { return; }
        throw new Exception("Expected validation failure: " + label);
    }
    public static int Main(string[] args)
    {
        try
        {
            Equal("dirt_on_grass_m000", CartoonTilePlan.Build(new[] { "#" }, "dirt_on_grass", "grass")[0], "isolated tile");
            Equal("dirt_on_grass_m068", CartoonTilePlan.Build(new[] { "###" }, "dirt_on_grass", "grass")[1], "horizontal corridor");
            Equal("dirt_on_grass_m000", CartoonTilePlan.Build(new[] { "#.", ".#" }, "dirt_on_grass", "grass")[0], "diagonal does not join without cardinal neighbors");
            Equal("dirt_3", CartoonTilePlan.Build(new[] { "###", "###", "###" }, "dirt_on_grass", "grass")[4], "full neighborhood uses quiet variation");
            Equal("stone_3", CartoonTilePlan.Build(new[] { "###", "###", "###" }, "stone_on_grass", "grass")[4], "stone material");
            Equal("water_1", CartoonTilePlan.Build(new[] { "." }, "sand_on_water", "water")[0], "water background");
            Reject(() => CartoonTilePlan.Build(new[] { "##", "#" }, "dirt_on_grass", "grass"), "ragged blueprint");
            Reject(() => CartoonTilePlan.Build(new[] { "x" }, "dirt_on_grass", "grass"), "unknown character");
            Reject(() => CartoonTilePlan.Build(new[] { "#" }, "dirt_on_grass", "water"), "incompatible transition materials");
            var patterns = new HashSet<string>();
            int[,] neighbor = { { 1, 0 }, { 2, 0 }, { 2, 1 }, { 2, 2 }, { 1, 2 }, { 0, 2 }, { 0, 1 }, { 0, 0 } };
            for (int m = 0; m < 256; m++)
            {
                char[][] rows = { "...".ToCharArray(), ".#.".ToCharArray(), "...".ToCharArray() };
                for (int bit = 0; bit < 8; bit++) if ((m & (1 << bit)) != 0) rows[neighbor[bit, 1]][neighbor[bit, 0]] = '#';
                patterns.Add(CartoonTilePlan.Build(rows.Select(r => new string(r)).ToArray(), "dirt_on_grass", "grass")[4]);
            }
            Equal(47, patterns.Count, "all 256 neighborhoods reduce to 47 valid patterns");
            string source = Path.Combine(args[0], "美术资源/地图设计/明快卡通_战斗地图_v1");
            var serializer = new JavaScriptSerializer();
            foreach (string file in Directory.GetFiles(Path.Combine(source, "03_使用说明"), "*_实拼布局.json"))
            {
                var expected = serializer.Deserialize<Layout>(File.ReadAllText(file));
                string blueprintFile = Path.Combine(source, "06_现有工具蓝图", Path.GetFileName(file).Replace("_实拼布局.json", "_基础地形.txt"));
                string[] rows = File.ReadAllText(blueprintFile).TrimEnd('\r', '\n').Split('\n').Select(r => r.TrimEnd('\r')).ToArray();
                string[] actual = CartoonTilePlan.Build(rows, expected.foregroundFamily, expected.background);
                Equal(expected.terrainNamesRowMajor.Length, actual.Length, file + " cell count");
                for (int i = 0; i < actual.Length; i++) Equal(expected.terrainNamesRowMajor[i], actual[i], file + " cell " + i);
            }
            Console.WriteLine("PASS: " + checks + " assertions; three layouts and all 256 neighborhoods verified.");
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
}
