// TARGET:code
// START_IN:
using LoginPI.Engine.ScriptBase;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

public class RegistryAppAssertSuite : ScriptBase
{
    // ======================
    // User variables (edit me)
    // ======================
    private const string AppDisplayNamePattern = "Visual Studio Code*"; // wildcard against DisplayName
    private const string ExeFileName          = "Code.exe";             // primary executable file name
    private const string TargetProcessName    = "code";                 // process name for START/STOP
    private const bool   AbortIfAppMissing    = false;                  // set true to fail the run if missing

    // Version assertion (generic)
    private const string ExpectedVersion        = "1.95.0";            // example for VS Code
    private const bool   AbortIfVersionMismatch = true;

    private const string TargetWindowTitle    = null;                   // optional START hints
    private const string TargetWindowClass    = null;
    private const int    StartTimeoutSeconds  = 30;
    private const int    StopTimeoutSeconds   = 5;

    // ======================
    // Internal constants
    // ======================
    private const string BaseKey      = @"HKCU\Software\LoginVSI";
    private const string DemoKey      = BaseKey + @"\Demo";
    private const string AppStatusKey = BaseKey + @"\AppStatus";
    private const string EnvVarName   = "LOGINVSI_APP_VERSION";
    private const int    PassTimer    = 0;
    private const int    FailTimer    = 10000;

    private AppInfo _app = new AppInfo(); // non-null default

    void Execute()
    {
        Log("== Login Enterprise: Registry & App Assertion Suite ==");
        Log($"Using HKCU base: {BaseKey}");

        // 1) Demo HKCU key: create & assert reads/writes
        Log("[1/4] Preparing demo registry data...");
        EnsureDemoKey();
        AssertAndReport("Demo: QueryKey",           "Demo key should list values",         () => NonEmpty(QueryKey(DemoKey)));
        AssertAndReport("Demo: QueryValue",         "DemoString should be present",        () => NonEmpty(QueryValue(DemoKey, "DemoString")));
        AssertAndReport("Demo: ValueOnly",          "DemoString value text should parse",  () => NonEmpty(QueryValueOnly(DemoKey, "DemoString")));
        AssertAndReport("Demo: DefaultValue",       "Default value should be present",     () => NonEmpty(QueryDefaultValue(DemoKey)));
        AssertAndReport("Demo: KeyExists",          "Key should exist",                    () => KeyExists(DemoKey));
        AssertAndReport("Demo: ValueExists",        "DemoDWORD should exist",              () => ValueExists(DemoKey, "DemoDWORD"));
        SetHkcuString(DemoKey, "LastRunUtc", DateTime.UtcNow.ToString("o"));
        TimerPass("Demo: LastRunUtc");
        CreateEvent(title:"PASS: Demo LastRunUtc", description:$"{DemoKey}\\LastRunUtc updated");
        AssertAndReport("Demo: DemoDWORD==1",       "DemoDWORD must equal 1",              () => ValueEqualsDword(DemoKey, "DemoDWORD", 1));
        AssertAndReport("Demo: DemoString==literal","DemoString must match expected text", () => ValueEqualsString(DemoKey, "DemoString", "Hello from Login Enterprise"));

        // 2) App detection
        Log("[2/4] Detecting target app from registry...");
        _app = DetectAppMulti(AppDisplayNamePattern, ExeFileName);

        // Final fallback: if we have a LaunchPath but no version, read EXE ProductVersion
        if (_app.Found && string.IsNullOrWhiteSpace(_app.Version)
            && !string.IsNullOrWhiteSpace(_app.LaunchPath) && File.Exists(_app.LaunchPath))
        {
            var fileVer = GetExeProductVersion(_app.LaunchPath);
            if (!string.IsNullOrWhiteSpace(fileVer))
            {
                _app.Version = fileVer;
                Log($"Version filled from EXE metadata: '{_app.Version}'");
            }
        }

        if (!_app.Found)
        {
            EnsureKey(AppStatusKey);
            SetHkcuString(AppStatusKey, "Installed", "No");
            DeleteHkcuValue(AppStatusKey, "Version");
            DeleteHkcuValue(AppStatusKey, "LaunchPath");
            SetHkcuEnvVar(EnvVarName, "notInstalled");
            TimerFail("AppDetected");
            CreateEvent(title:"FAIL: AppDetected", description:$"Could not locate '{AppDisplayNamePattern}' ({ExeFileName}).");
            if (AbortIfAppMissing)
            {
                ABORT(error:$"Required app not installed or not discoverable: '{AppDisplayNamePattern}' ({ExeFileName}).");
            }
            else
            {
                Log("App not found; continuing without START/STOP.");
            }
        }
        else
        {
            TimerPass("AppDetected");
            CreateEvent(title:"PASS: AppDetected", description:$"Found '{_app.DisplayName}' v{_app.Version}\r\nLaunchPath: {_app.LaunchPath}");
        }

        // 3) Log app status + env var
        Log("[3/4] Logging app status and setting env var...");
        if (_app.Found)
        {
            EnsureKey(AppStatusKey);
            SetHkcuString(AppStatusKey, "Installed", "Yes");
            SetHkcuString(AppStatusKey, "Version", string.IsNullOrWhiteSpace(_app.Version) ? "unknown" : _app.Version);
            if (!string.IsNullOrWhiteSpace(_app.LaunchPath))
                SetHkcuString(AppStatusKey, "LaunchPath", _app.LaunchPath);
            SetHkcuEnvVar(EnvVarName, string.IsNullOrWhiteSpace(_app.Version) ? "unknown" : _app.Version);
            TimerPass("AppStatus_Logged");
            CreateEvent(title:"PASS: AppStatus_Logged", description:$"{AppStatusKey} updated, {EnvVarName} set.");
        }
        else
        {
            TimerPass("AppStatus_Logged");
            CreateEvent(title:"INFO: AppStatus_Logged", description:"App missing; basic status already written.");
        }

        // 3b) Version assertion
        if (_app.Found)
        {
            var ok = VersionEquals(_app.Version, ExpectedVersion);
            if (ok)
            {
                TimerPass("AppVersion");
                CreateEvent(title:"PASS: AppVersion", description:$"Detected version '{_app.Version}' matches expected '{ExpectedVersion}'.");
            }
            else
            {
                TimerFail("AppVersion");
                var msg = $"Detected version '{_app.Version}' does not match expected '{ExpectedVersion}'.";
                CreateEvent(title:"FAIL: AppVersion", description:msg);
                if (AbortIfVersionMismatch)
                {
                    ABORT(error: msg);
                }
            }
        }

        // 4) Conditional START/STOP
        Log("[4/4] Conditional START/STOP...");
        if (_app.Found && !string.IsNullOrWhiteSpace(_app.LaunchPath) && File.Exists(_app.LaunchPath))
        {
            CreateEvent(title:"Starting app", description:_app.LaunchPath);
            START(mainWindowTitle: TargetWindowTitle, mainWindowClass: TargetWindowClass, processName: TargetProcessName, timeout: StartTimeoutSeconds, continueOnError: false);
            Log("App started, waiting 2 seconds...");
            Wait(2);
            STOP(timeout: StopTimeoutSeconds);
            TimerPass("App_StartStop");
            CreateEvent(title:"PASS: App_StartStop", description:$"Process '{TargetProcessName}' started and stopped.");
        }
        else
        {
            Log("No valid LaunchPath. Skipping START/STOP.");
            TimerPass("App_StartStop"); // by design
            CreateEvent(title:"INFO: App_StartStop", description:"LaunchPath not available; start/stop skipped.");
        }

        Log("== Suite complete ==");
    }

    // ======================
    // Tiny assert wrapper that logs, timers, events
    // ======================
    private void AssertAndReport(string timerName, string expectation, Func<bool> check)
    {
        try
        {
            if (check())
            {
                TimerPass(timerName);
                Log($"[PASS] {timerName} - {expectation}");
                CreateEvent(title:$"PASS: {timerName}", description:expectation);
            }
            else
            {
                TimerFail(timerName);
                Log($"[FAIL] {timerName} - {expectation}");
                CreateEvent(title:$"FAIL: {timerName}", description:expectation);
            }
        }
        catch (Exception ex)
        {
            TimerFail(timerName);
            var msg = $"{expectation}. Exception: {ex.Message}";
            Log($"[ERROR] {timerName} - {msg}");
            CreateEvent(title:$"ERROR: {timerName}", description:msg);
            ABORT(error:$"{timerName} threw: {ex.Message}");
        }
    }

    private static bool NonEmpty(string? s) => !string.IsNullOrWhiteSpace(s);
    private void TimerPass(string name) => SetTimer(SanitizeTimerName(name), PassTimer);
    private void TimerFail(string name) => SetTimer(SanitizeTimerName(name), FailTimer);

    private static string SanitizeTimerName(string name)
    {
        var sb = new StringBuilder(name.Length);
        foreach (var ch in name) sb.Append(char.IsLetterOrDigit(ch) || ch == '_' ? ch : '_');
        var s = sb.ToString();
        if (s.Length == 0 || !char.IsLetterOrDigit(s[0])) s = "t_" + s;
        return s.Length > 32 ? s.Substring(0, 32) : s;
    }

    // ======================
    // App detection (HKCU/HKLM Uninstall + App Paths + known folders)
    // ======================
    private class AppInfo
    {
        public bool   Found = false;
        public string DisplayName = "";
        public string Version = "";
        public string LaunchPath = "";
        public string InstallLocation = "";
        public string Subkey = "";
    }

    private AppInfo DetectAppMulti(string displayNameWildcard, string exeFileName)
    {
        // 1) HKCU Uninstall
        var info = DetectFromUninstallRoot(@"HKCU\Software\Microsoft\Windows\CurrentVersion\Uninstall", displayNameWildcard, exeFileName, regView: null);
        if (info.Found) return info;

        // 2) HKLM Uninstall (64-bit)
        info = DetectFromUninstallRoot(@"HKLM\Software\Microsoft\Windows\CurrentVersion\Uninstall", displayNameWildcard, exeFileName, regView: "64");
        if (info.Found) return info;

        // 3) HKLM Uninstall (32-bit/WOW6432Node)
        info = DetectFromUninstallRoot(@"HKLM\Software\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall", displayNameWildcard, exeFileName, regView: "32");
        if (info.Found) return info;

        // 4) HKLM App Paths
        info = DetectFromAppPaths(exeFileName);
        if (info.Found) return info;

        // 5) Known locations
        info = DetectFromKnownPaths(exeFileName);
        return info; // Found may still be false
    }

    private AppInfo DetectFromUninstallRoot(string rootKey, string displayNameWildcard, string exeFileName, string? regView)
    {
        var info = new AppInfo();
        if (!KeyExists(rootKey, regView)) return info;

        // Fast listing of immediate subkeys (no recursion)
        var raw = QueryKey(rootKey, recursive: false, regView: regView);
        var subkeys = ParseSubkeyPaths(raw, rootKey);

        foreach (var subkey in subkeys)
        {
            var name = QueryValueOnly(subkey, "DisplayName", regView) ?? "";
            if (string.IsNullOrWhiteSpace(name)) continue;

            if (WildcardMatch(name, displayNameWildcard))
            {
                info.Found = true;
                info.DisplayName = name;
                info.Subkey = subkey;

                // Version with robust fallbacks
                var ver = QueryValueOnly(subkey, "DisplayVersion", regView) ?? "";
                if (string.IsNullOrWhiteSpace(ver))
                {
                    var vMaj = QueryValueOnly(subkey, "VersionMajor", regView) ?? "";
                    var vMin = QueryValueOnly(subkey, "VersionMinor", regView) ?? "";
                    if (!string.IsNullOrWhiteSpace(vMaj) && !string.IsNullOrWhiteSpace(vMin))
                        ver = $"{vMaj}.{vMin}";
                    else
                    {
                        var v = QueryValueOnly(subkey, "Version", regView) ?? "";
                        if (!string.IsNullOrWhiteSpace(v)) ver = v;
                    }
                }
                info.Version = ver;

                // Preferred: InstallLocation\ExeFileName; fallback DisplayIcon
                info.InstallLocation = QueryValueOnly(subkey, "InstallLocation", regView) ?? "";
                var candidate = CombineIfPresent(info.InstallLocation, exeFileName);
                if (!string.IsNullOrWhiteSpace(candidate) && File.Exists(candidate))
                    info.LaunchPath = candidate;
                else
                    info.LaunchPath = "";

                if (string.IsNullOrWhiteSpace(info.LaunchPath))
                {
                    var icon = QueryValueOnly(subkey, "DisplayIcon", regView) ?? "";
                    if (TryExtractExecutablePath(icon, out var iconExe) && !string.IsNullOrWhiteSpace(iconExe) && File.Exists(iconExe))
                        info.LaunchPath = iconExe;
                }
                return info;
            }
        }

        return info;
    }

    private AppInfo DetectFromAppPaths(string exeFileName)
    {
        var baseKey = @"HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths";
        var key = $"{baseKey}\\{exeFileName}";

        string? p64 = KeyExists(key, "64") ? TryDefaultValue(key, "64") : null;
        if (!string.IsNullOrWhiteSpace(p64) && File.Exists(p64))
        {
            var info = new AppInfo { Found = true, DisplayName = exeFileName, LaunchPath = p64 };
            var ver = GetExeProductVersion(p64);
            if (!string.IsNullOrWhiteSpace(ver)) info.Version = ver;
            return info;
        }

        string? p32 = KeyExists(key, "32") ? TryDefaultValue(key, "32") : null;
        if (!string.IsNullOrWhiteSpace(p32) && File.Exists(p32))
        {
            var info = new AppInfo { Found = true, DisplayName = exeFileName, LaunchPath = p32 };
            var ver = GetExeProductVersion(p32);
            if (!string.IsNullOrWhiteSpace(ver)) info.Version = ver;
            return info;
        }

        return new AppInfo();
    }

    private string? TryDefaultValue(string key, string? regView)
    {
        // Query default value via /ve and parse its data
        var output = QueryDefaultValue(key, regView);
        var lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var line in lines)
        {
            if (line.TrimStart().StartsWith(key, StringComparison.OrdinalIgnoreCase)) continue;
            var parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 3) return string.Join(" ", parts, 2, parts.Length - 2);
        }
        return null;
    }

    private AppInfo DetectFromKnownPaths(string exeFileName)
    {
        string Expand(string s) => Environment.ExpandEnvironmentVariables(s ?? string.Empty);
        var nameNoExt = TrimExe(exeFileName);

        var candidates = new[]
        {
            Expand($@"%LOCALAPPDATA%\Programs\Microsoft VS Code\{exeFileName}"),
            Expand($@"%ProgramFiles%\Microsoft VS Code\{exeFileName}"),
            Expand($@"%ProgramFiles(x86)%\Microsoft VS Code\{exeFileName}"),
            Expand($@"%ProgramFiles%\{nameNoExt}\{exeFileName}"),
            Expand($@"%ProgramFiles(x86)%\{nameNoExt}\{exeFileName}"),
            Expand($@"%LOCALAPPDATA%\{nameNoExt}\{exeFileName}")
        };

        foreach (var c in candidates)
        {
            if (!string.IsNullOrWhiteSpace(c) && File.Exists(c))
            {
                var info = new AppInfo { Found = true, DisplayName = exeFileName, LaunchPath = c };
                var ver = GetExeProductVersion(c);
                if (!string.IsNullOrWhiteSpace(ver)) info.Version = ver;
                return info;
            }
        }

        return new AppInfo();
    }

    private static string TrimExe(string exeFile) => Path.GetFileNameWithoutExtension(exeFile ?? string.Empty);

    private static List<string> ParseSubkeyPaths(string regQueryOutput, string parentKey)
    {
        var result = new List<string>();
        var lines = regQueryOutput.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var line in lines)
        {
            var s = line.Trim();
            if (s.StartsWith(parentKey, StringComparison.OrdinalIgnoreCase) && !s.Equals(parentKey, StringComparison.OrdinalIgnoreCase))
                result.Add(s);
        }
        return result;
    }

    private static bool TryExtractExecutablePath(string value, out string path)
    {
        path = "";
        if (string.IsNullOrWhiteSpace(value)) return false;
        var v = value.Trim().Trim('"');
        var idx = v.IndexOf(".exe", StringComparison.OrdinalIgnoreCase);
        if (idx >= 0)
        {
            path = v.Substring(0, idx + 4).Trim().Trim('"');
            return true;
        }
        return false;
    }

    private static string CombineIfPresent(string dir, string file)
    {
        if (string.IsNullOrWhiteSpace(dir)) return "";
        try { return Path.Combine(dir, file); } catch { return ""; }
    }

    private static bool WildcardMatch(string? text, string pattern)
    {
        text ??= "";
        return new Regex("^" + Regex.Escape(pattern).Replace("\\*", ".*").Replace("\\?", ".") + "$",
                         RegexOptions.IgnoreCase).IsMatch(text);
    }

    // ======================
    // Registry helpers
    // ======================
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
                return string.Join(" ", parts, 2, parts.Length - 2);
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

    private static void EnsureDemoKey()
    {
        RunReg($"add \"{DemoKey}\" /f");
        RunReg($"add \"{DemoKey}\" /ve /t REG_SZ /d \"LE Demo Root\" /f");
        RunReg($"add \"{DemoKey}\" /v \"DemoString\" /t REG_SZ /d \"Hello from Login Enterprise\" /f");
        RunReg($"add \"{DemoKey}\" /v \"DemoDWORD\" /t REG_DWORD /d 1 /f");
    }

    public static void EnsureKey(string key) => RunReg($"add \"{key}\" /f");
    public static void SetHkcuString(string key, string name, string data) => RunReg($"add \"{key}\" /v \"{name}\" /t REG_SZ /d \"{data ?? ""}\" /f");
    public static void SetHkcuDword(string key, string name, int data)   => RunReg($"add \"{key}\" /v \"{name}\" /t REG_DWORD /d {data} /f");

    public static void DeleteHkcuValue(string key, string name)
    {
        try { RunReg($"delete \"{key}\" /v \"{name}\" /f"); } catch { }
    }

    public static void SetHkcuEnvVar(string name, string value)
    {
        var envKey = @"HKCU\Environment";
        RunReg($"add \"{envKey}\" /v \"{name}\" /t REG_SZ /d \"{value ?? "unknown"}\" /f");
        try { Environment.SetEnvironmentVariable(name, value ?? "unknown", EnvironmentVariableTarget.Process); } catch { }
    }

    // ======================
    // Value & Version helpers
    // ======================
    public static bool ValueEqualsDword(string key, string name, int expected)
    {
        var data = QueryValueOnly(key, name);
        if (string.IsNullOrWhiteSpace(data)) return false;

        if (int.TryParse(data, out var dec) && dec == expected) return true;

        if (data.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            if (int.TryParse(data.Substring(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var hex)
                && hex == expected) return true;
        }
        return false;
    }

    public static bool ValueEqualsString(string key, string name, string expected, StringComparison cmp = StringComparison.Ordinal)
    {
        var data = QueryValueOnly(key, name);
        return data != null && string.Equals(data, expected, cmp);
    }

    private static string GetExeProductVersion(string path)
    {
        try
        {
            var vi = FileVersionInfo.GetVersionInfo(path);
            var v = vi.ProductVersion;
            if (string.IsNullOrWhiteSpace(v)) v = vi.FileVersion;
            return v ?? "";
        }
        catch { return ""; }
    }

    private static bool VersionEquals(string detected, string expected)
    {
        var a = ParseVersionSegments(detected);
        var b = ParseVersionSegments(expected);
        var max = Math.Max(a.Length, b.Length);
        a = Pad(a, max);
        b = Pad(b, max);
        for (int i = 0; i < max; i++)
            if (a[i] != b[i]) return false;
        return true;
    }

    private static int[] ParseVersionSegments(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return new[] { 0 };
        var segs = new List<int>();
        foreach (var part in Regex.Split(s, "[^0-9]+"))
            if (int.TryParse(part, out var n)) segs.Add(n);
        if (segs.Count == 0) segs.Add(0);
        return segs.Count > 4 ? segs.GetRange(0, 4).ToArray() : segs.ToArray();
    }

    private static int[] Pad(int[] arr, int len)
    {
        if (arr.Length >= len) return arr;
        var r = new int[len];
        Array.Copy(arr, r, arr.Length);
        return r; // remaining default to 0
    }
}
