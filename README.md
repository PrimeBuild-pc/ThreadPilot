# 🚀 ThreadPilot

<p align="center">
  <img src="generated-icon.png" alt="ThreadPilot Logo" width="200" height="200">
</p>

## 📋 Overview

ThreadPilot is a powerful Windows 11 desktop application designed to help you manage and optimize your system's CPU thread allocation, process affinity, and power profiles. With its intuitive interface, you can monitor system performance, apply optimization profiles automatically, and fine-tune your system settings for improved performance.

## ✨ Features

### 🧠 CPU Core Management
- **Dynamic Thread Allocation**: Intelligently distribute application workloads across CPU cores
- **Process Affinity Control**: Fine-tune which cores specific applications can use
- **Multi-threading Optimization**: Maximize performance for both single and multi-threaded applications

### ⚡ Power Profile Management
- **Pre-configured Power Profiles**: Choose from bundled optimization profiles for different use cases
- **Profile Import/Export**: Share your custom power profiles with others
- **Real-time Switching**: Seamlessly switch between profiles based on your current tasks

### 📊 System Monitoring
- **Resource Usage Tracking**: Monitor CPU, memory, and disk usage in real-time
- **Process Prioritization**: Easily adjust process priorities to allocate resources where needed
- **Performance Analytics**: Track system performance over time with detailed graphs

### 🔧 Customization
- **Application Rules**: Create custom rules for specific applications
- **Automated Optimization**: Schedule profile changes based on running applications
- **Light and Dark Themes**: Choose the visual theme that matches your preference

## 🖼️ Screenshots

*Coming soon*

## 🔍 System Requirements

- Windows 11 (64-bit)
- .NET 6.0 or later
- Administrator privileges (for modifying system settings)
- 4GB RAM minimum
- 50MB of free disk space

## 📥 Installation

1. Download the latest release from the [Releases](https://github.com/PrimeBuild-pc/ThreadPilot/releases) page
2. Run the installer and follow the on-screen instructions
3. Launch ThreadPilot from your Start menu
4. Grant administrator privileges when prompted

## 🔄 How It Works

ThreadPilot integrates with Windows' built-in power management and process scheduling capabilities, providing a user-friendly interface to access and control these features. It uses the Windows Management Instrumentation (WMI) and PowerCfg utilities to modify system settings safely and effectively.

### Power Profiles

Power profiles (.pow files) contain configurations for Windows power management settings. ThreadPilot allows you to:

- Import existing .pow files
- Create custom profiles tailored to your needs
- Export and share your optimized configurations
- Switch between profiles with a single click

### Process Management

The application monitors running processes and allows you to:

- Set processor affinity (which cores a process can use)
- Adjust process priorities
- Create persistent rules for applications

## 🛡️ Security and Privacy

ThreadPilot operates entirely on your local system and does not:
- Collect or transmit any personal data
- Require internet connectivity to function
- Install any third-party software or services

## 🤝 Contributing

Contributions are welcome! If you'd like to help improve ThreadPilot:

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/amazing-feature`)
3. Commit your changes (`git commit -m 'Add some amazing feature'`)
4. Push to the branch (`git push origin feature/amazing-feature`)
5. Open a Pull Request

## 📜 License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## 🙏 Acknowledgments

- Thanks to all contributors who have helped shape this project
- Special appreciation to the open-source community for their invaluable resources and tools

## 📞 Support

For support, feature requests, or bug reports, please [open an issue](https://github.com/PrimeBuild-pc/ThreadPilot/issues) on GitHub.

---

<p align="center">
  <i>Made with ❤️ by the ThreadPilot Team</i>
</p>