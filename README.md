# VoidOS

![Version](https://img.shields.io/badge/version-3.0.49-9cf?style=flat-square&label=Gen3)
![License](https://img.shields.io/badge/license-MIT-green?style=flat-square&label=License&color=blue)
![Architecture](https://img.shields.io/badge/architecture-CLI_%26_Networking-orange?style=flat-square)

A lightweight, modular operating system built from the ground up on [**Cosmos Gen3**](https://github.com/valentinbreiz/nativeaot-patcher) using the Native AOT patcher. VoidOS has transitioned from a GUI-focused environment into a robust, server-oriented architecture, low-level networking, custom TCP protocols, and a highly extensible command-line interface.

## Progress
### **Current Status: Beta v2.0.0**
- **Overall Progress**:  [████████░░░░░░░░░░░░] 40%
- **Focus**: Core Kernel, Safe Architecture (No Panic), Networking Stack, Remote Administration

### Low-Level Networking
Built directly on top of the Cosmos Gen3 raw network stack via the `nativeaot-patcher`:
* **Dynamic DHCP:** Automatically negotiates IP addresses and gateway routing on boot, with automatic fallback to static IP configuration.
* **Inbound TCP Control:** Bypasses standard .NET buffering to handle Gen3's specific virtual hardware bugs.
* **Custom Protocol Design:** Uses specific delimiters (`\u001E`) to frame messages cleanly over raw TCP streams for the VRS.
* **Bound Sockets:** Local `TcpClient` endpoints must be explicitly defined (`new IPEndPoint(IPAddress.Any, 0)`) to prevent crashes during connections.

## Features
| Feature | Status | Description |
|---------|--------|-------------|
| **Safe Command Parser** | ✅ | Object-oriented routing with alias support and quote-safe argument parsing |
| **Network Stack** | ✅ | Cosmos Gen3 DHCP, static fallback, TCP/IP initialization |
| **Void Remote Shell (VRS)** | ✅ | Custom TCP daemon (Port 23) with brute-force protection |
| **Network Utilities** | ✅ | `ifconfig`, `nslookup`, DNS configuration |
| **System Utilities** | ✅ | `sysinfo`, `tasks` (thread listing), `mem` |
| **Background Services** | ✅ | Multi-threaded async execution |
| **File System (Stubbed)** | 🚧 | Commands written but disabled pending CosmosVFS mapping |

## 💻 Void Remote Shell (VRS)
VRS is VoidOS's built-in remote administration daemon. It listens on Port 23 and allows external clients to connect, execute commands, and receive output seamlessly. 
* **Security:** Features login attempt limiting to prevent brute-force attacks over the network.
* **Safe:** Engineered specifically to survive the bugs of virtual network buffers in QEMU/Cosmos without crashing the kernel.

## Example: Adding a Command
Because of the modularity, extending the OS requires following specific patterns to avoid compiler crashes. Here is how to add a fully functional command:

```csharp
public class ExampleCommand : BaseCommand
{
    public override string Name { get { return "example"; } }
    public override string[] Aliases { get { "ex", "test" } }
    public override string Description { get { return "An example"; } }

    public override string Execute(string[] args)
    {
        return "Executed successfully!";
    }
}
```
Then, in your Kernel initialization, simply register it:
```csharp
CommandManager.Register(new ExampleCommand());
```

## Getting Started

### Prerequisites
* Visual Studio 2022+ or VS Code (with `.NET 10.0`)
* [Cosmos SDK & Native AOT Patcher](https://valentinbreiz.github.io/nativeaot-patcher/articles/install.html)
* QEMU (Emulator)

### Building & Running
1. Clone the repository:
   ```bash
   git clone https://github.com/VoidbornGames/VoidOS.git
   ```
2. Open the project folder in your terminal.
3. Run command `cosmos build` followed by `cosmos run`.

## 📜 License
### [MIT](LICENSE) © Alireza Janaki
