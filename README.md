# Login Enterprise Registry Helpers

Example workloads for [Login Enterprise](https://www.loginvsi.com/login-enterprise) that demonstrate reusable, non-admin-safe registry methods.

## Purpose
These examples make it easy to query, check, and write registry values from Login Enterprise workloads, even when running as a standard (non-admin) user.  
They focus on the `HKCU` hive for safe, repeatable tests.

## Features
- Query full keys, single values, or just value data.
- Check for key or value existence.
- Write REG_SZ and REG_DWORD values to HKCU.
- Create demo keys automatically for testing.
- Designed for one-line invocation in the `Execute()` method.

## Getting Started
1. Import `RegistryExamples.cs` into your Login Enterprise environment as a workload.
2. Run it in a test targeting `dummy.exe`.
3. Check the session Events and Logs to see output from each helper.

## License
MIT License — see [LICENSE](LICENSE) for details.

---

© 2025 Login VSI. Login Enterprise is proprietary software. These examples are provided as-is with no warranty.
