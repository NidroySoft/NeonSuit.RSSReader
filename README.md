# NeonSuit.RSSReader

<div align="center">

![NeonSuit](https://img.shields.io/badge/NeonSuit-RSS%20Reader-8A2BE2)
![.NET](https://img.shields.io/badge/Backend-.NET%2010-512BD4)
![WPF](https://img.shields.io/badge/WPF-Frontend%20in%20development-0078D6)
![License](https://img.shields.io/badge/license-MIT-green)
[![Backend .NET 10 CI](https://github.com/NidroySoft/NeonSuit.RSSReader/actions/workflows/dotnet.yml/badge.svg)](https://github.com/NidroySoft/NeonSuit.RSSReader/actions/workflows/dotnet.yml)

**An RSS/Atom reader backend for a Windows desktop client — part of the NeonSuit productivity suite.**

[Features](#features) • [Getting Started](#getting-started) • [Architecture](#architecture) • [Testing](#testing) • [Documentation](#documentation) • [Contributing](#-contributing)

</div>

## Project Status

The backend has been migrated to **.NET 10** and integrated into `master` through [PR #1](https://github.com/NidroySoft/NeonSuit.RSSReader/pull/1). It is a set of local libraries, with SQLite persistence and background synchronization, ready to serve as the foundation for the new WPF client.

**The existing WPF project is incomplete and was outside the backend migration.** The commands below build and test the backend; they do not launch a finished desktop application. WPF integration, UI behavior and long-running workload validation remain development work.

## Features

| Area | Backend capabilities |
| --- | --- |
| Feeds | RSS/Atom parsing, feed updates and article deduplication |
| Organization | Categories and hierarchy, tags, favorites, read states, search and pagination |
| Rules | Persistent article actions, tagging and highlighting |
| Synchronization | Scoped background tasks, execution history, cancellation and statistics |
| Persistence | EF Core SQLite migrations, compatible legacy-schema adoption and online backups |
| Interchange | OPML import/export and preference import/export |
| Presentation integration | Shared notification and rule-action events for the future WPF client |

Sound playback and desktop notifications require a presentation-layer consumer. See the [backend guide](docs/BACKEND_NET10_ES.md) for the validated contracts and remaining limitations.

## Getting Started

### Prerequisites

- A stable **.NET 10 SDK**. `global.json` selects the latest installed feature band in the 10.0 family.
- Git.
- Windows or Linux for the validated backend build. The future WPF client will require Windows and target `net10.0-windows`.

### Build the Backend

```sh
git clone https://github.com/NidroySoft/NeonSuit.RSSReader.git
cd NeonSuit.RSSReader

dotnet restore NeonSuit.RSSReader.Backend.slnx -p:RestoreDisableParallel=true
dotnet build NeonSuit.RSSReader.Backend.slnx --no-restore -c Release -m:1
```

Use `NeonSuit.RSSReader.Backend.slnx` explicitly: the original full solution also contains projects outside the validated backend scope.

### Connect a Client

Reference `NeonSuit.RSSReader.Setup` and `NeonSuit.RSSReader.Core`. Register services with `AddNeonSuitBackend(databasePath)`, initialize the database with `UseNeonSuitDatabaseAsync()`, subscribe to `IBackendEvents`, then start `ISyncCoordinatorService`.

Resolve scoped services inside a service scope and dispose the root provider asynchronously when the application closes. The [backend guide](docs/BACKEND_NET10_ES.md#uso-desde-el-futuro-wpf) includes a complete example and WPF integration guidance.

**Before opening an existing SQLite database, preserve a backup and test migration on a copy.** Code rollback and database restoration are separate operations; follow the [recovery guide](docs/ROLLBACK_NET10_ES.md).

## Architecture

| Project or directory | Responsibility |
| --- | --- |
| `src/Core/NeonSuit.RSSReader.Core` | Models, DTOs, contracts and mapping profiles |
| `src/Core/NeonSuit.RSSReader.Data` | EF Core SQLite context, migrations and repositories |
| `src/Core/NeonSuit.RSSReader.Services` | Services, RSS parsing, rules and synchronization |
| `src/Core/NeonSuit.RSSReader.Setup` | Dependency injection and database initialization |
| `src/UI/NeonSuit.RSSReader.Desktop` | Existing WPF project, outside the migrated backend solution |
| `tests/Core` | Active backend unit and integration suites |
| `tests/LegacyContracts` | Preserved historical tests with incompatible contracts, excluded from active suites |

### Technology Stack

| Component | Technology |
| --- | --- |
| Backend | .NET 10 / C# |
| Persistence | Entity Framework Core 10 and Microsoft.Data.Sqlite |
| RSS/Atom parsing | CodeHollow.FeedReader |
| HTML processing | AngleSharp |
| Mapping | AutoMapper |
| Dependency injection | Microsoft.Extensions.DependencyInjection |
| Logging | Serilog |
| Tests | xUnit, SQLite fixtures and local HTTP fixtures |
| Planned desktop client | WPF / MVVM |

## Testing

[**View backend CI runs and test results**](https://github.com/NidroySoft/NeonSuit.RSSReader/actions/workflows/dotnet.yml)

[**Successful validation before integration**](https://github.com/NidroySoft/NeonSuit.RSSReader/actions/runs/37333873924): Release build and tests passed on **Linux and Windows**, with **241 tests passed, 0 failed and 4 historical benchmarks skipped per platform**. Builds reported **0 warnings and 0 errors**. This result applies to the tested migration commit; the badge above tracks the workflow's current status.

The workflow restores, builds and tests the backend on both platforms and uploads TRX results as `backend-tests-ubuntu-latest` and `backend-tests-windows-latest` artifacts.

After the Release build above, run:

```sh
dotnet test NeonSuit.RSSReader.Backend.slnx --no-build -c Release -m:1 --logger trx --results-directory TestResults
```

There are 245 discovered cases in the validated suites. The 33 historical files under `tests/LegacyContracts` are preserved outside the active test projects and are not counted as passing. Functional tests do not establish performance under sustained or large workloads.

## Documentation

- [Backend migration, validation scope and WPF integration (Spanish)](docs/BACKEND_NET10_ES.md)
- [Publication evidence and original-to-published commit mapping (Spanish)](docs/PUBLICACION_NET10_ES.md)
- [Code rollback and SQLite recovery (Spanish)](docs/ROLLBACK_NET10_ES.md)
- [Migration pull request and exact merge-reversal command](https://github.com/NidroySoft/NeonSuit.RSSReader/pull/1)

---

## 🤝 Contributing

Contributions are welcome! Here's how you can help:

1. **Fork** the repository
2. **Create a branch** (`git checkout -b feature/amazing-feature`)
3. **Commit** your changes (`git commit -m 'feat: add amazing feature'`)
4. **Push** (`git push origin feature/amazing-feature`)
5. **Open a Pull Request**

### Guidelines

- Follow existing code style
- Add tests for new features
- Update documentation when needed
- Use semantic commits (`feat:`, `fix:`, `docs:`, etc.)

---

## 💛 Support the Project

If this application helps you stay organized and productive, consider supporting its development. Your contributions help maintain and improve the project.

### Crypto Donations

```
$ USDT (TRC-20): TXYH9Q6s2uvaDwhuzjYMfzdb19WziPFRDa
₿ Bitcoin (BTC): 1DPYXBoHuD2HTemqEbWC8mEqd7ryx3Zoso
⟠ Ethereum (ERC-20): 0xfce098d84397e20c92ed94832b99276722994c06

```

### Why Support?

- ⏱️ **Countless hours** of development
- 🚀 **Regular updates** and improvements
- 💡 **Feature requests** prioritized for supporters
- 📚 **Quality documentation** and support

Every contribution, no matter the size, helps keep this project alive and growing. Thank you! ❤️

---

## 📝 License

This project is licensed under the **MIT License**.

```
Copyright (c) 2025 NeonSuit

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files...
```

✔️ Free for personal and commercial use
✔️ Modify and distribute freely
✔️ Include in your own projects

---

## 🙏 Acknowledgments

- [CodeHollow.FeedReader](https://github.com/codehollow/FeedReader) – Excellent RSS/Atom parser
- [Entity Framework Core](https://github.com/dotnet/efcore) – SQLite persistence and migrations
- [CommunityToolkit.Mvvm](https://github.com/CommunityToolkit/dotnet) – MVVM helpers and utilities

---

## 📬 Contact


- **Email**: nidroysoft@gmail.com

---

<div align="center">
  <sub>Built with ❤️ by an independent developer</sub>
  <br/>
  <sub>🇨🇺 From Cuba, for the world</sub>
  <br/>
  <sub>Part of <strong>NeonSuit</strong> – Your productivity suite</sub>
</div>