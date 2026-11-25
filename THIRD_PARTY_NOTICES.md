# Third-party notices

Empiro Network Scanner source is GPL-3.0-only, copyright 2026 Vineet Kumar and contributors. Dependencies retain their original licenses. This file summarizes direct dependencies and is not a complete transitive software bill of materials.

| Package | Version |
|---|---|
| Microsoft.Extensions.Hosting | 10.0.13 |
| Microsoft.Data.Sqlite | 10.0.13 |
| System.Security.Cryptography.ProtectedData | 10.0.13 |
| Serilog.Extensions.Hosting | 8.0.0 |
| Serilog.Sinks.File | 6.0.0 |
| Microsoft.NET.Test.Sdk | 17.11.1 |
| xunit | 2.9.2 |
| xunit.runner.visualstudio | 2.8.2 |

The .NET/WPF runtime, SQLite native components and WiX packaging tools have their own applicable notices. Restore retrieves packages separately; this repository does not vendor their binaries. Inspect the exact restored package metadata before distributing binaries. The historical packaging script collects runtime notices and package license expressions for its output.

No camera models, third-party firmware, credentials, production databases or SDK distributions are included. GPL applies to this project's source; it does not remove obligations attached to dependencies or grant rights to unrelated trademarks.

## License expressions from restored direct-package metadata

| Package | Version | License expression |
|---|---|---|
| Microsoft.Data.Sqlite | 10.0.13 | MIT |
| Microsoft.Extensions.Hosting | 10.0.13 | MIT |
| Microsoft.NET.Test.Sdk | 17.11.1 | MIT |
| Serilog.Extensions.Hosting | 8.0.0 | Apache-2.0 |
| Serilog.Sinks.File | 6.0.0 | Apache-2.0 |
| System.Security.Cryptography.ProtectedData | 10.0.13 | MIT |
| xunit | 2.9.2 | Apache-2.0 |
| xunit.runner.visualstudio | 2.8.2 | Apache-2.0 |
