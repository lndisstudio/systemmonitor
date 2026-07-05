# System Monitor

Hem Windows hem Linux için çalışan, sistem bileşenlerini izleyen modern bir masaüstü uygulaması.

![System Monitor](https://img.shields.io/badge/.NET-9.0-purple.svg)
![Avalonia](https://img.shields.io/badge/Avalonia-12.0-2391F7.svg)
![Platform](https://img.shields.io/badge/platform-Windows%20%7C%20Linux-lightgrey.svg)

## Features

- **CPU Monitoring**: Usage percentage, temperature, frequency, core count
- **Memory (RAM) Monitoring**: Usage percentage, speed, type, slot count
- **GPU Monitoring**: Usage, temperature, VRAM, fan speed, power consumption (NVIDIA/AMD support)
- **Disk Monitoring**: Usage percentage, temperature, model, file system
- **Motherboard Monitoring**: Model, manufacturer, BIOS version, temperature
- **PSU (Power Supply) Monitoring**: Capacity, load percentage, current draw, efficiency
- **Health Status**: Health indicators for all components
- **Real-time Updates**: 2-second refresh interval
- **Modern UI**: Catppuccin Mocha dark theme

## Tech Stack

- **Framework**: .NET 9.0
- **UI Framework**: Avalonia UI 12.0
- **MVVM**: CommunityToolkit.Mvvm
- **Language**: C#

## Requirements

### Windows
- Windows 10 or later
- .NET Desktop Runtime (or use self-contained exe)

### Linux
- Linux distribution (Ubuntu, Fedora, Debian, etc.)
- .NET Runtime 9.0
- Optional: `nvidia-smi` (for NVIDIA GPU), `dmidecode` (for detailed hardware info)

## Installation

### Windows
1. Download `publish/SystemMonitor.exe`
2. Double-click to run

### Linux
```bash
# Install .NET Runtime
sudo apt install dotnet-runtime-9.0  # Ubuntu/Debian

# Run the application
dotnet SystemMonitor.dll
```

## Building from Source

```bash
# Clone the repository
git clone <repository-url>
cd SystemMonitor

# Run the application
dotnet run

# Or build it
dotnet build
```

## Packaging

### Windows EXE:
```bash
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o ./publish
```

### Linux:
```bash
dotnet publish -c Release -r linux-x64 --self-contained true -p:PublishSingleFile=true -o ./publish
```

## Project Structure

```
SystemMonitor/
├── Services/
│   ├── ISystemMonitorService.cs      # Interface
│   ├── WindowsSystemMonitorService.cs # Windows implementation
│   ├── LinuxSystemMonitorService.cs   # Linux implementation
│   └── SystemMonitorServiceFactory.cs # Cross-platform factory
├── ViewModels/
│   └── MainWindowViewModel.cs         # MVVM ViewModel
├── Views/
│   └── MainWindow.axaml               # UI interface
├── Models/                            # Data models
└── publish/                           # Compiled executable
```

## Platform-Specific Notes

### Windows
- Uses WMI (Windows Management Instrumentation)
- Performance Counters for CPU/RAM usage
- Supports `nvidia-smi` for NVIDIA GPU

### Linux
- Reads `/proc` and `/sys` file systems
- Uses shell commands (`df`, `lsblk`, `lspci`)
- Supports `nvidia-smi` for NVIDIA GPU, `rocm-smi` for AMD GPU
- Uses `dmidecode` for detailed information

## Development

```bash
# Run in development mode
dotnet run

# Run tests
dotnet test

# Release build
dotnet build -c Release
```

## License

MIT License

## Contributing

Contributions are welcome! Please feel free to submit a pull request or open an issue.

## Future Features

- [ ] Graphical history display
- [ ] Warning/email notifications
- [ ] System logs
- [ ] Multiple disk/GPU support
- [ ] Tray icon support
- [ ] Auto-start on boot
- [ ] Custom themes
- [ ] Export reports to CSV/PDF
