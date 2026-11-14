using System;
using System.Text.Json;
using Fadenwerk.EditorTools;
internal static class Program
{
    static int Main()
    {
        try
        {
            string value = "</script>\"\\" + new string(new[] { (char)0x2028, (char)0x2029 }) + "Äß";
            for (int i = 0; i < 32; i++) value += (char)i;
            string json = new FwJson().BeginObject().Prop("text", value).EndObject().ToString();
            using (JsonDocument parsed = JsonDocument.Parse(json))
                Require(parsed.RootElement.GetProperty("text").GetString() == value, "all-control-and-unicode-roundtrip");
            Require(!json.Contains("<") && !json.Contains(">"), "inline-html-cannot-close-script");
            Require(!json.Contains(((char)0x2028).ToString()) && !json.Contains(((char)0x2029).ToString()), "line-separators-escaped-in-source-and-output");
            Require(json.Contains("\\u2028") && json.Contains("\\u2029"), "line-separators-preserve-codepoints");
            Console.WriteLine("ALL PASS: 4 real FwJson assertions");
            return 0;
        }
        catch (Exception e) { Console.Error.WriteLine(e); return 1; }
    }
    static void Require(bool ok, string name)
    {
        if (!ok) throw new Exception("FAIL " + name);
        Console.WriteLine("PASS " + name);
    }
}
