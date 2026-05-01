# VoidOS

![Version](https://img.shields.io/badge/version-2.0.0-9cf?style=flat-square&label=Gen3)
![License](https://img.shields.io/badge/license-MIT-green?style=flat-square&label=License&color=blue)
![Architecture](https://img.shields.io/badge/architecture-CLI_%26_Networking-orange?style=flat-square)

A lightweight, modular operating system built from the ground up on **Cosmos Gen3**. VoidOS has transitioned from a GUI-focused environment into a robust, server-oriented architecture, emphasizing low-level networking, custom TCP protocols, and a highly extensible command-line interface.

## 📊 Progress
### **Current Status: Gen3 Alpha v2.0.0**
- **Overall Progress**:  [██████░░░░░░░░░░░░░░] 30%
- **Focus**: Core Kernel, Networking Stack, Remote Administration

## ⚙️ Core Architecture

VoidOS Gen3 is built around a clean separation of concerns, making it incredibly easy to expand without touching core kernel logic.

### 🧩 Modular Command System
Gone are the days of massive `switch` statements. Commands are now fully object-oriented. 
* **`ICommand` Interface:** Every command is a class.
* **`CommandManager`:** A dynamic registry that handles routing, argument parsing (including quoted strings), and aliases.
* **1-Line Registration:** Adding a new feature to the OS is as simple as writing a class and adding `CommandManager.Register(new YourCommand());`.

### 🌐 Low-Level Networking
Built directly on top of Cosmos Gen3's raw network stack, VoidOS handles the complexities of OS-level networking:
* **Dynamic DHCP:** Automatically negotiates IP addresses and gateway routing on boot.
* **Raw TCP Control:** Bypasses standard .NET buffering to handle Gen3's specific virtual hardware quirks (Nagle's algorithm, TIME_WAIT states, SLIRP starvation).
* **Custom Protocol Design:** Uses specific delimiters (e.g., `\u001E`) to frame messages cleanly over raw TCP streams.

## 🖥️ Features
| Feature | Status | Description |
|---------|--------|-------------|
| **Command Parser** | ✅ | Object-oriented routing with alias support and argument parsing |
| **Network Stack** | ✅ | Cosmos Gen3 DHCP, TCP/IP initialization |
| **Void Remote Shell (VRS)** | ✅ | Custom TCP daemon (Port 23) for remote administration |
| **Background Services** | ✅ | Multi-threaded async execution (`RunAsync`, `RunServiceAsync`) |
| **Cosmos Gen3 Kernel** | ✅ | x64 architecture, hardware-level text mode output |

## 💻 Void Remote Shell (VRS)
VRS is VoidOS's built-in remote administration daemon. It listens on Port 23 and allows external clients to connect, execute commands, and receive output seamlessly. It was engineered specifically to survive the quirks of virtual network buffers in QEMU/Cosmos.

## 🛠️ Example: Adding a Command
Because of the new architecture, extending the OS is trivial. Here is how you add a fully functional `Echo` command:

```csharp
public class EchoCommand : BaseCommand
{
    public override string Name => "echo";
    public override string Description => "Echo text back";

    public override string Execute(string[] args)
    {
        if (args.Length == 0) return "Usage: echo <text>";
        return string.Join(" ", args);
    }
}
```
Then, in your Kernel initialization, simply register it:
```csharp
CommandManager.Register(new EchoCommand());
```

## 🚀 Getting Started

### Prerequisites
* Visual Studio 2026 or VS Code (with `.NET 10.0`)
* [Cosmos SDK](https://valentinbreiz.github.io/nativeaot-patcher/articles/install.html) (Gen3 Install Docs)
* [Cosmos Tools](https://valentinbreiz.github.io/nativeaot-patcher/articles/install.html)

### Building & Running
1. Clone the repository:
   ```bash
   git clone https://github.com/VoidbornGames/VoidOS.git
   ```
2. Run command ```cosmos build``` then ```cosmos run```.

## 📜 License
### [MIT](LICENSE) © Alireza Janaki
