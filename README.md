# NeonSuit.RSSReader

A modern, lightweight RSS/Atom feed reader for Windows, part of the NeonSuit productivity suite.

![NeonSuit](https://img.shields.io/badge/NeonSuit-RSS%20Reader-blue)
![.NET](https://img.shields.io/badge/.NET-8.0-purple)
![WPF](https://img.shields.io/badge/WPF-Windows-blue)
![License](https://img.shields.io/badge/license-MIT-green)

## 🌟 About NeonSuit

NeonSuit is a suite of productivity applications designed to enhance your workflow. NeonSuit.RSSReader is part of this ecosystem, providing a clean and efficient way to stay updated with your favorite content sources.

## ✨ Features

- 📰 **Multi-feed support** - Subscribe to unlimited RSS/Atom feeds
- 📁 **Smart organization** - Organize feeds into categories
- ⭐ **Favorites** - Star important articles for later
- 🔍 **Advanced search** - Find articles across all your feeds
- 🎨 **Clean interface** - Modern WPF design with intuitive navigation
- 💾 **Local storage** - SQLite database for fast, offline access
- 🔄 **Auto-refresh** - Configurable update intervals
- 🌐 **Offline reading** - Read articles without internet connection

## 🚀 Getting Started

### Prerequisites

- Windows 10/11
- .NET 8.0 Runtime

### Installation

1. Download the latest release from [Releases](https://github.com/yourusername/NeonSuit.RSSReader/releases)
2. Extract the ZIP file
3. Run `NeonSuit.RSSReader.Desktop.exe`

### Building from Source

```bash
# Clone the repository
git clone https://github.com/yourusername/NeonSuit.RSSReader.git

# Navigate to the project
cd NeonSuit.RSSReader

# Restore dependencies
dotnet restore

# Build the solution
dotnet build

# Run the application
cd NeonSuit.RSSReader.Desktop/bin/Debug/net8.0-windows
./NeonSuit.RSSReader.Desktop.exe
```

## 🏗️ Architecture

NeonSuit.RSSReader follows a clean architecture pattern with clear separation of concerns:

```
NeonSuit.RSSReader/
├── NeonSuit.RSSReader.Core        # Domain models, interfaces, enums
├── NeonSuit.RSSReader.Data        # Data access layer (SQLite)
├── NeonSuit.RSSReader.Services    # Business logic and RSS parsing
└── NeonSuit.RSSReader.Desktop     # WPF user interface (MVVM)
```

### Technologies

- **Frontend**: WPF (Windows Presentation Foundation)
- **Pattern**: MVVM (Model-View-ViewModel)
- **Database**: SQLite with sqlite-net-pcl
- **RSS Parser**: CodeHollow.FeedReader
- **DI**: Microsoft.Extensions.DependencyInjection
- **MVVM Toolkit**: CommunityToolkit.Mvvm

## 📖 Usage

### Adding a Feed

1. Click the **"Add Feed"** button
2. Enter the RSS/Atom feed URL
3. (Optional) Assign to a category
4. Click **"Subscribe"**

### Managing Categories

1. Right-click on the sidebar
2. Select **"New Category"**
3. Organize feeds by dragging and dropping

### Keyboard Shortcuts

- `Ctrl + N` - Add new feed
- `Ctrl + F` - Search articles
- `Ctrl + R` - Refresh all feeds
- `Space` - Mark as read
- `S` - Star/Unstar article

## 🤝 Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

1. Fork the repository
2. Create your feature branch (`git checkout -b feature/AmazingFeature`)
3. Commit your changes (`git commit -m 'Add some AmazingFeature'`)
4. Push to the branch (`git push origin feature/AmazingFeature`)
5. Open a Pull Request

## 📝 License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## 🙏 Acknowledgments

- [CodeHollow.FeedReader](https://github.com/codehollow/FeedReader) - Excellent RSS/Atom parser
- [sqlite-net-pcl](https://github.com/praeclarum/sqlite-net) - SQLite ORM
- [CommunityToolkit.Mvvm](https://github.com/CommunityToolkit/dotnet) - MVVM helpers

## 📬 Contact

- **Project**: [NeonSuit.RSSReader](https://github.com/yourusername/NeonSuit.RSSReader)
- **Issues**: [Report a bug](https://github.com/yourusername/NeonSuit.RSSReader/issues)
- **NeonSuit Website**: [Coming Soon]

---

**Part of NeonSuit** - Your productivity suite
