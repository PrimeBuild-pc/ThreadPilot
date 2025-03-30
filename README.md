# ThreadPilot

ThreadPilot is a Windows desktop application designed to provide advanced system performance optimization through intuitive core affinity and process management tools.

## Features

- **CPU Core Optimization**: Fine-tune which CPU cores your applications use for maximum performance
- **Power Profile Management**: Import, export, and apply power profiles to optimize your system
- **Process Priority Control**: Adjust process priorities to ensure your important applications get the resources they need
- **Real-time System Monitoring**: View detailed information about your system's performance
- **Automatic Optimization**: Apply optimizations automatically based on process type and system state

## Requirements

- Windows 10 or Windows 11
- .NET 6.0 or later
- Administrator privileges (required for modifying process affinities and power profiles)

## Installation

1. Download the latest release from the Releases section
2. Run the installer and follow the prompts
3. Launch ThreadPilot from the Start menu

## Building from Source

1. Clone the repository
2. Open the solution in Visual Studio 2022 or later
3. Build the solution
4. Run the application

```bash
git clone https://github.com/PrimeBuild-pc/ThreadPilot.git
cd ThreadPilot
dotnet build
dotnet run --project ThreadPilot
```

## Usage

1. Launch ThreadPilot with administrator privileges
2. The Dashboard shows an overview of your system's performance
3. Navigate to the Processes tab to view and manage running processes
4. Use the CPU Cores tab to view core usage and set affinity rules
5. Power Profiles allow you to import, export, and apply Windows power profiles

## License

This project is licensed under the MIT License - see the LICENSE file for details.