# NeonSuit.RSSReader

<div align="center">

![NeonSuit](https://img.shields.io/badge/NeonSuit-RSS%20Reader-8A2BE2)
![.NET](https://img.shields.io/badge/.NET-8.0-512BD4)
![WPF](https://img.shields.io/badge/WPF-Windows-0078D6)
![License](https://img.shields.io/badge/license-MIT-green)

**A modern, lightweight RSS/Atom feed reader for Windows – part of the NeonSuit productivity suite.**

[Features](#features) •
[Getting Started](#getting-started) •
[Architecture](#architecture) •
[Usage](#usage) •
[Contributing](#contributing) •
[Support](#support)

</div>

---

## 📋 About NeonSuit

NeonSuit is a suite of productivity applications designed to enhance your workflow. **NeonSuit.RSSReader** is part of this ecosystem, providing a clean and efficient way to stay updated with your favorite content sources.

Built with **.NET 8** and **WPF**, this reader offers a modern interface, offline support, and smart organization features.

---

## ✨ Features

| Category | Features |
|----------|----------|
| **Feed Management** | 📰 Multi-feed support, 📁 Smart categories, 🔄 Auto-refresh |
| **Reading Experience** | 🌐 Offline reading, ⭐ Favorites, 🔍 Advanced search |
| **Interface** | 🎨 Clean WPF design, ⌨️ Keyboard shortcuts, 📱 Responsive layout |
| **Performance** | 💾 SQLite local storage, ⚡ Fast indexing, 🔌 Low memory footprint |

---

## 🚀 Getting Started

### Prerequisites

- Windows 10 or 11
- [.NET 8.0 Runtime](https://dotnet.microsoft.com/download/dotnet/8.0)

### Installation

#### Option 1: Download Release (Recommended)

```bash
# 1. Download the latest release from the Releases page
# 2. Extract the ZIP file
# 3. Run NeonSuit.RSSReader.Desktop.exe
```

#### Option 2: Build from Source

```bash
# Clone the repository
git clone https://github.com/yourusername/NeonSuit.RSSReader.git
cd NeonSuit.RSSReader

# Restore dependencies
dotnet restore

# Build the solution
dotnet build

# Run the application
dotnet run --project NeonSuit.RSSReader.Desktop
```

---

## 🏗️ Architecture

The project follows **Clean Architecture** principles with clear separation of concerns:

```
NeonSuit.RSSReader/
├── 📁 Core/              # Domain models, interfaces, enums
├── 📁 Data/              # Data access layer (SQLite)
├── 📁 Services/          # Business logic and RSS parsing
└── 📁 Desktop/           # WPF user interface (MVVM)
```

### Technology Stack

| Layer | Technology |
|-------|------------|
| **Frontend** | WPF (Windows Presentation Foundation) |
| **Pattern** | MVVM (Model-View-ViewModel) |
| **Database** | SQLite with sqlite-net-pcl |
| **RSS Parser** | CodeHollow.FeedReader |
| **DI** | Microsoft.Extensions.DependencyInjection |
| **MVVM Toolkit** | CommunityToolkit.Mvvm |

---

## 📖 Usage

### Adding a Feed

1. Click the **"Add Feed"** button in the toolbar
2. Enter the RSS/Atom feed URL
3. (Optional) Assign to an existing category
4. Click **"Subscribe"**

### Managing Categories

1. Right-click on the categories panel
2. Select **"New Category"**
3. Drag and drop feeds to organize them

### Keyboard Shortcuts

| Shortcut | Action |
|----------|--------|
| `Ctrl + N` | Add new feed |
| `Ctrl + F` | Search articles |
| `Ctrl + R` | Refresh all feeds |
| `Space` | Mark current article as read |
| `S` | Star/Unstar article |
| `Ctrl + ,` | Open settings |

---

## 🧪 Testing

```bash
# Run unit tests
dotnet test tests/NeonSuit.RSSReader.Tests.Unit

# Run integration tests
dotnet test tests/NeonSuit.RSSReader.Tests.Integration

# Run all tests
dotnet test
```

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
- [sqlite-net-pcl](https://github.com/praeclarum/sqlite-net) – Lightweight SQLite ORM
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