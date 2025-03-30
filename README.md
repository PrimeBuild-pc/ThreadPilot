# ThreadPilot

ThreadPilot is a Windows desktop application designed to provide advanced system performance optimization through intuitive core affinity and process management tools.

<p align="center">
  <img src="ThreadPilot/Resources/Images/logo.svg" width="150" />
</p>

## Features

- **Core Affinity Management**: Easily assign specific CPU cores to applications for optimal performance
- **Process Priority Control**: Adjust process priorities to ensure critical applications get the resources they need
- **Power Profile Management**: Apply optimized power profiles for different usage scenarios (gaming, productivity, power saving)
- **Automation Rules**: Create automated rules to apply specific settings when certain applications are launched
- **System Tray Integration**: Runs in the background with minimal resource usage
- **Modern UI**: Clean, modern interface with light and dark theme support

## System Requirements

- Windows 10 (1903 or later) or Windows 11
- .NET 6.0 or later
- Admin rights for certain operations (changing process priorities, applying power profiles)

## Installation

1. Download the latest release from the [releases page](https://github.com/PrimeBuild-pc/ThreadPilot/releases)
2. Run the installer and follow the installation wizard
3. Launch ThreadPilot from the Start menu or desktop shortcut

## Development

ThreadPilot is built using:
- C# (.NET 6+)
- WPF (Windows Presentation Foundation)
- MVVM architectural pattern

To build from source:
1. Clone the repository: `git clone https://github.com/PrimeBuild-pc/ThreadPilot.git`
2. Open `ThreadPilot.sln` in Visual Studio 2022
3. Build the solution (F6 or Build > Build Solution)

## Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

1. Fork the repository
2. Create your feature branch: `git checkout -b feature/amazing-feature`
3. Commit your changes: `git commit -m 'Add some amazing feature'`
4. Push to the branch: `git push origin feature/amazing-feature`
5. Open a Pull Request

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## Acknowledgments

- All bundled power profiles are provided for optimization purposes
- Special thanks to the open-source community for various libraries and tools used in this project