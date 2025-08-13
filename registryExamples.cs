// TARGET:dummy.exe
// START_IN:
using LoginPI.Engine.ScriptBase;
using System;
using System.Diagnostics;
using System.Text;

/// <summary>
/// Single-file, non-admin-safe registry helpers and examples for Login Enterprise workloads.
/// Assumption: script runs as a standard user; examples use HKCU so they succeed without elevation.
/// Each helper has XML docs and a short usage note. Execute() only contains one-line invocations.
/// </summary>
public class RegistryExamples : ScriptBase
{
    void Execute()
    {
        // 1) Ensure demo key under HKCU so all examples are non-admin-safe.
        EnsureHkcuDemoKey(); // one-liner

        // 2) Query the whole key (prints all values under the demo key).
        CreateEvent(title: "QueryKey",
            description: QueryKey(@"HKCU\Software\LE.Demo"));

        // 3) Read a specific value with metadata.
        CreateEvent(title: "QueryValue: DemoString",
            description: QueryValue(@"HKCU\Software\LE.Demo", "DemoString"));

        // 4) Read only the data portion of a value (no type/name clutter).
        CreateEvent(title: "QueryValueOnly: DemoString",
            description: QueryValueOnly(@"HKCU\Software\LE.Demo", "DemoString") ?? "<null>");

        // 5) Read the default value of the key.
        CreateEvent(title: "QueryDefaultValue",
            description: QueryDefaultValue(@"HKCU\Software\LE.Demo"));

        // 6) Existence checks (handy for asserts).
        CreateEvent(title: "KeyExists?",
            description: KeyExists(@"HKCU\Software\LE.Demo").ToString());
        CreateEvent(title: "ValueExists? DemoDWORD",
            description: ValueExists(@"HKCU\Software\LE.Demo", "DemoDWORD").ToString());

        // 7) Write examples that do something useful for operators.
        SetHkcuString(@"HKCU\Software\LE.Demo", "LastRunUtc", DateTime.UtcNow.ToString("o")); // one-liner
        CreateEvent(title: "Write LastRunUtc", description: "Updated LastRunUtc under HKCU\\Software\\LE.Demo");

        // 8) Optional cleanup example
        //DeleteHkcuValue(@"HKCU\Software\LE.Demo", "TempValue");

        // Example timer usage
        int passFail = ValueEqualsDword(@"HKCU\Software\LE.Demo", "DemoDWORD", expected: 1) ? 0 : 10000;
        SetTimer("DemoDWORD==1", passFail);
    }

    // -----------------------
    // Registry helper methods
    // -----------------------

    private static string RunReg(string args, int timeoutMs = 10000)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "reg.exe",
            Arguments = args,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8
        };

        using (var p = Process.Start(psi))
        {
            if (!p.WaitForExit(timeoutMs))
            {
                try { p.Kill(); } catch { }
                throw new InvalidOperationException("reg.exe timed out.");
            }
            string stdout = p.StandardOutput.ReadToEnd();
            string stderr = p.StandardError.ReadToEnd();
            if (p.ExitCode != 0)
                throw new InvalidOperationException($"reg.exe failed: {stderr}");
            return stdout;
        }
    }

    public static string QueryKey(string key, bool recursive = false, string? regView = null)
    {
        var args = $"query \"{key}\"";
        if (recursive) args += " /s";
        if (!string.IsNullOrEmpty(regView)) args += $" /reg:{regView}";
        return RunReg(args);
    }

    public static string QueryValue(string key, string valueName, string? regView = null)
    {
        var args = $"query \"{key}\" /v \"{valueName}\"";
        if (!string.IsNullOrEmpty(regView)) args += $" /reg:{regView}";
        return RunReg(args);
    }

    public static string? QueryValueOnly(string key, string valueName, string? regView = null)
    {
        var output = QueryValue(key, valueName, regView);
        var lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var line in lines)
        {
            if (line.TrimStart().StartsWith(key, StringComparison.OrdinalIgnoreCase)) continue;
            var parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 3 && parts[0].Equals(valueName, StringComparison.OrdinalIgnoreCase))
            {
                return string.Join(" ", parts, 2, parts.Length - 2);
            }
        }
        return null;
    }

    public static string QueryDefaultValue(string key, string? regView = null)
    {
        var args = $"query \"{key}\" /ve";
        if (!string.IsNullOrEmpty(regView)) args += $" /reg:{regView}";
        return RunReg(args);
    }

    public static bool KeyExists(string key, string? regView = null)
    {
        try
        {
            var args = $"query \"{key}\"";
            if (!string.IsNullOrEmpty(regView)) args += $" /reg:{regView}";
            RunReg(args);
            return true;
        }
        catch { return false; }
    }

    public static bool ValueExists(string key, string valueName, string? regView = null)
    {
        try
        {
            var args = $"query \"{key}\" /v \"{valueName}\"";
            if (!string.IsNullOrEmpty(regView)) args += $" /reg:{regView}";
            RunReg(args);
            return true;
        }
        catch { return false; }
    }

    // -----------------------
    // HKCU write helpers
    // -----------------------

    public static void EnsureHkcuDemoKey()
    {
        RunReg("add \"HKCU\\Software\\LE.Demo\" /f");
        RunReg("add \"HKCU\\Software\\LE.Demo\" /ve /t REG_SZ /d \"LE Demo Root\" /f");
        RunReg("add \"HKCU\\Software\\LE.Demo\" /v \"DemoString\" /t REG_SZ /d \"Hello from Login Enterprise\" /f");
        RunReg("add \"HKCU\\Software\\LE.Demo\" /v \"DemoDWORD\" /t REG_DWORD /d 1 /f");
    }

    public static void SetHkcuString(string key, string name, string data)
    {
        RunReg($"add \"{key}\" /v \"{name}\" /t REG_SZ /d \"{data}\" /f");
    }

    public static void SetHkcuDword(string key, string name, int data)
    {
        RunReg($"add \"{key}\" /v \"{name}\" /t REG_DWORD /d {data} /f");
    }

    public static void DeleteHkcuValue(string key, string name)
    {
        try { RunReg($"delete \"{key}\" /v \"{name}\" /f"); } catch { }
    }

    public static bool ValueEqualsDword(string key, string name, int expected)
    {
        var data = QueryValueOnly(key, name);
        if (string.IsNullOrWhiteSpace(data)) return false;
        if (int.TryParse(data, out var dec) && dec == expected) return true;
        if (data.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            if (int.TryParse(data.Substring(2), System.Globalization.NumberStyles.HexNumber,
                             System.Globalization.CultureInfo.InvariantCulture, out var hex)
                && hex == expected) return true;
        }
        return false;
    }
}
