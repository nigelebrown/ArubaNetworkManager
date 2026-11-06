# JCF Intelligent Network Manager — release 0.6 evidence

This is source-code evidence, **not production acceptance**.

| Test | Outcome |
|---|---|
| Python source-safety checks | 24 PASS, 0 FAIL |
| C# lexer and balanced delimiters | PASS on 29 C# files |
| Linux install script shell syntax | PASS (`bash -n`) |
| JSON appsettings parse | PASS |
| ZIP archive integrity | Verified after packaging |
| Real .NET build / xUnit / TestServer | NOT RUN (SDK unavailable) |
| SQL Server migrations / integration | NOT RUN (SQL/SDK unavailable) |
| AP-505/AP-535/CX6100/CX6200 live connection | NOT RUN (hardware unavailable) |
| Penetration, load, resiliency, HA/DR | NOT RUN |

**Status:** No production certification. Default device writes OFF. See `PRODUCTION_READINESS.md` and `SRS_IMPLEMENTATION_MATRIX.md` for module gaps.

Use the provided Windows .NET 10 test runner to obtain compilation and xUnit evidence. Do not turn on equipment writes without model/firmware certification, signed approval and restore validation.
