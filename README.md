# Registry & App Assertion Suite for Login Enterprise

## 1. Overview
This script is a **Login Enterprise Virtual User** workload that validates Windows registry keys, application presence, and application versions **before running any other tests**.

While this example focuses on **app version detection and conditional start/stop**, the **methods included** can be reused for a wide range of registry-based logic.  
Think of this as a **starter kit** for building **your own registry-driven workload logic**.

Examples of what you could do beyond version checks:
- Verify feature flags or configuration toggles are set before a test.
- Detect if certain security policies are enabled (`HKLM\Software\Policies\...`).
- Track performance-impacting registry changes over time.
- Write test-specific markers to the registry that later workloads can read.
- Configure environment variables dynamically for downstream workloads.
- Assert values in HKCU/HKLM to validate compliance or baseline image readiness.

---

## 2. Features
- **Registry value checks** with pass/fail logging.
- **Multi-location app detection**:
  - HKCU/HKLM uninstall keys (64-bit and 32-bit views).
  - `App Paths`.
  - Known installation folders.
- **Version assertion** with optional script abort.
- **Demo key setup** to confirm registry operations work in your test environment.
- **Environment variable setting** for downstream workload use.
- **Safe timers** for reporting in Login Enterprise analytics.
- **Conditional app launch and shutdown** for basic smoke testing.
- **User-friendly logging** in the virtual user console output.
- **Reusable helper methods** for registry interaction and value comparison.

---

## 3. Prerequisites
- Login Enterprise Virtual Appliance with scripting enabled.
- Virtual user account with:
  - Registry read/write access in `HKCU`
  - Read access in `HKLM`
- Target application must be discoverable via:
  - Windows uninstall registry keys
  - App Paths
  - Or a known installation folder
- `reg.exe` available in `PATH` (default on Windows).

---

## 4. Configuration
All user-editable variables are grouped at the top of the script:

| Variable | Description |
|----------|-------------|
| `AppDisplayNamePattern` | Wildcard to match the app’s `DisplayName` (e.g. `Visual Studio Code*`). |
| `ExeFileName` | Primary executable name (e.g. `Code.exe`). |
| `TargetProcessName` | Process name for START/STOP checks. |
| `AbortIfAppMissing` | `true` to stop script if app not found. |
| `ExpectedVersion` | Version string to check against. |
| `AbortIfVersionMismatch` | `true` to stop if detected version doesn’t match. |
| `TargetWindowTitle` / `TargetWindowClass` | Optional hints for START() to detect app window. |
| `StartTimeoutSeconds` / `StopTimeoutSeconds` | Timeouts for start and stop actions. |

---

## 5. How It Works
The `Execute()` method runs in four phases:

1. **Demo Registry Key Setup**
   - Creates `HKCU\Software\LoginVSI\Demo` with sample values.
   - Runs assertions to verify registry read/write works.

2. **App Detection**
   - Searches uninstall keys in HKCU and HKLM (both 64-bit and 32-bit views).
   - Falls back to App Paths or known install folders.
   - Attempts to detect `DisplayVersion` or product version from EXE metadata.

3. **Status Logging**
   - Writes installed status, version, and launch path to `HKCU\Software\LoginVSI\AppStatus`.
   - Sets an environment variable (`LOGINVSI_APP_VERSION`).
   - Creates PASS/FAIL events for detection and version checks.

4. **Conditional Start/Stop**
   - If the app is found and a valid executable path exists, STARTs and STOPs the app.
   - Logs timer results and creates an event.

---

## 6. Version Assertion
- The detected version is compared to `ExpectedVersion` using a segment-by-segment match.
- On mismatch:
  - Creates a FAIL event.
  - Aborts the script if `AbortIfVersionMismatch` is `true`.
- On match:
  - Creates a PASS event and continues.

---

## 7. Events & Timers
- **Events** are created for every registry assertion, app detection, version check, and START/STOP action.
- **Timers** (with safe names for Login Enterprise reporting) measure success or failure for each stage.
- PASS events/timers show green in the UI; FAILs are easy to spot and can trigger automation.

---

## 8. Example Output
**Login Enterprise Event Log**:  
*(Example screenshot to be inserted — showing PASS/FAIL for registry checks, app detection, and version match)*

**Virtual User Console**:
== Login Enterprise: Registry & App Assertion Suite ==
[1/4] Preparing demo registry data...
[PASS] Demo: QueryKey - Demo key should list values
[2/4] Detecting target app from registry...
Version filled from EXE metadata: '1.103.0'
[PASS] AppDetected - Found 'Visual Studio Code' v1.103.0
...
== Suite complete ==

---

## 9. Limitations & Troubleshooting
- If the application doesn’t write version info to the registry, EXE metadata is used — which may not match the marketing version number.
- If the app is installed per-machine (HKLM) but the virtual user has no permission to read it, detection may fail.
- App detection relies on either:
  - A matching uninstall key with `DisplayName` and `DisplayVersion`.
  - A resolvable App Path entry.
  - A known install path.
- When using `AbortIfAppMissing` or `AbortIfVersionMismatch`, note that these will end the workload early.

---

## 10. Method Reference
Here’s what some of the key helper methods do:

| Method | Purpose |
|--------|---------|
| `QueryKey` | Lists subkeys and values under a registry key. |
| `QueryValueOnly` | Reads the value data for a single named value. |
| `EnsureKey` | Creates a registry key if it doesn’t exist. |
| `SetHkcuString` | Writes a string value to HKCU. |
| `SetHkcuEnvVar` | Creates/updates a user-scoped environment variable. |
| `ValueEqualsString` | Compares a registry string value to an expected string. |
| `ValueEqualsDword` | Compares a registry DWORD to an expected integer. |
| `GetExeProductVersion` | Reads product/file version from an executable. |
| `VersionEquals` | Compares two version strings segment-by-segment. |
| `TimerPass` / `TimerFail` | Sets timers for PASS/FAIL reporting in Login Enterprise. |
| `CreateEvent` | Logs a named event in Login Enterprise UI. |

---

## 11. Author
**Author:** Michael Kent, CPO/CTO at Login VSI & Joshua Kennedy, Technical Product Manager 
**License:** MIT

---

## 12. Support / Contact  
For questions, feedback, or to share creative uses of this workload, join the **#workspace-weekly** channel in our customer Slack:  
[Join the Login VSI Customer Slack](https://join.slack.com/t/lvsi-customers/shared_invite/zt-3acoc4xmq-NcLJT33APZwrZrcppl8YQw)  

---

## 13. Disclaimer  
This script interacts directly with the Windows Registry.  
- **Test in a non-production environment first.**  
- Ensure you understand the changes being made before running in production.  
- Login VSI is not responsible for unintended effects of running modified versions of this script.  