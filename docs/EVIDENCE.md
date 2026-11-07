# Evidence status

Record every boundary independently. Do not turn a static code review or a simulated test scenario into Unity runtime evidence.

| Boundary | Status at initial PR | Required evidence |
| --- | --- | --- |
| GitHub commit + file readback | Pending after upload | Commit SHA, root tree, fetch exact files. |
| Wolfram math reference | Verified in Wolfram kernel, 2026-11-07 | `Floor[Floor[100*(10000+220*10)/10000]*(10000+2500)/10000] = 152`, and `Floor[Sqrt[2^63-1]]=3037000499`. |
| .NET shared C# compile/test | Not locally available | Actual `dotnet run --project Tests/CoreVerification/CoreVerification.csproj -c Release` CI logs + SHA. |
| Unity Editor compilation | Unverified | Unity console import no errors on pinned editor revision. |
| Play-Mode movement/combat/loot | Unverified | Real editor/game log plus human observation or captured play-mode screenshots. |
| Android APK | Unverified | Successful Unity BuildReport, artifact ID/digest and installation proof. |
| AMD performance | Unverified | Device/driver/GPU identifier and Unity profiler receipts. |

The original AI-Studio-provided text was **review material**, not ground truth about Blizzard internals. No proprietary source, SNO catalog or engine code was imported.
