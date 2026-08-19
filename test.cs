using System;
public class Test {
    public static void Main() {
        string line = "AKCNS|ALPHA|60|2026-08-19T11:00:05.3305350+03:00|245,2|PULLBACK_ADAY||244|-0,49||0,00|0,00";
        var parts = line.Split('|');
        if (parts.Length < 6) { Console.WriteLine("Length < 6"); return; }
        string status = parts[5].Trim().ToUpperInvariant();
        if (status.Contains("ROKET")) status = "AKTIF";
        if (status == "KAPALI") { Console.WriteLine("KAPALI"); return; }
        if (status != "AKTIF" && status != "PULLBACK_ADAY") { Console.WriteLine($"Not AKTIF or PULLBACK: {status}"); return; }
        Console.WriteLine($"SUCCESS: {status}");
    }
}
