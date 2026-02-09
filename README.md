# RkDevelopTool C# Implementation

[![Target Framework](https://img.shields.io/badge/.NET-10.0-blue)](https://dotnet.microsoft.com/download)

这是一个基于 C# 重写的 Rockchip 瑞芯微设备开发工具。它将原本的 C++ `rkdeveloptool` 迁移到 .NET 平台，提供了更现代化、可移植且易于维护的代码实现，用于通过 USB 与 Rockchip 设备（如 RK3588, RK3568 等）进行底层交互。

## 核心功能

- **设备管理**: 自动扫描并识别处于 Loader、Maskrom等状态的 Rockchip 设备。
- **固件操作**:
  - 支持解析 Rockchip 固件镜像 (`.img`) 及 Boot 文件。
  - 下载 Boot 到 RAM 执行。
- **Flash 交互**:
  - 扇区级读写 (Read/Write LBA)。
  - 全盘擦除 (Erase All Blocks)。
  - 获取 Flash ID 及容量信息。
- **现代特性**:
  - 基于 C# 标准 XML 文档，代码逻辑清晰。
  - 内置完善的单元测试。

## 技术栈

- **Runtime**: .NET 10.0
- **USB Protocol**: [LibUsbDotNet](https://github.com/LibUsbDotNet/LibUsbDotNet) (底层 Bulk 传输)

### 前置条件

1. 安装 [.NET 10.0 SDK](https://dotnet.microsoft.com/download)。
2. (Linux/macOS) 确保已安装 `libusb` 并配置好相应的 `udev` 规则。

### 编译

```powershell
dotnet build rkdeveloptool_cs/RkDevelopTool/RkDevelopTool.csproj
```

### 运行测试

```powershell
dotnet test rkdeveloptool_cs/RkDevelopTool.Tests/RkDevelopTool.Tests.csproj
```

### 常用命令示例

目前 CLI 支持以下操作：

- **列出设备**: `dotnet run -- ld`
- **读取 Flash 信息**: `dotnet run -- rfi`
- **下载 Boot**: `dotnet run -- db <path_to_boot>`
- **写入 LBA**: `dotnet run -- wl <start_lba> <file_path>`
- **擦除 Flash**: `dotnet run -- ef`
- **其他类似参数TBD......**

*免责声明：本工具涉及底层 Flash 操作，使用不当可能导致设备变砖，请在操作前确保已备份重要数据。*
