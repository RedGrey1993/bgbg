# GEMINI 项目上下文：BGBG

## 项目概览

这是一个使用 **Unity 6000.2.2f1** 开发的 2D 多人游戏项目，项目名称为 "BGBG"。

核心架构围绕灵活的网络模型构建。它具有一个自定义的网络抽象层 (`INetworkLayer.cs`)，允许游戏在不同的网络后端之间切换。目前，它支持：

*   **Steam P2P 网络：** 使用 `Steamworks.NET` 进行大厅创建、匹配和点对点数据传输。
*   **本地 UDP 网络：** 一个简单的基于 UDP 的层，用于本地网络测试，这对于不依赖 Steam 的开发非常有用。

游戏逻辑遵循 **主机权威** 模型。大厅的主机负责处理玩家输入、更新游戏状态并将其广播给所有客户端。游戏模拟是基于 Tick 的。

该项目利用 **通用渲染管线 (URP)** 进行渲染，并使用新的 **Unity 输入系统** 处理玩家控制。

## 构建和运行

### 从编辑器运行

1.  在 **Unity 编辑器（版本 6000.2.2f1 或兼容版本）** 中打开项目。
2.  打开位于 `Assets/Scenes/MainScene.unity` 的主场景文件。
3.  点击编辑器中的 **播放** 按钮以运行游戏。

### 网络配置

网络模式可以直接在 Unity 编辑器中配置：

*   在 `MainScene` 中选择 `NetworkManager` 游戏对象。
*   在 Inspector 窗口中，您会找到一个 **Network Mode** 下拉菜单。
*   您可以选择 `Steam` 或 `LocalUDP`。
    *   **`Steam` 模式：** 需要运行 Steam 且用户已登录。此模式使用 Steam 的后端进行大厅和 P2P 网络。
    *   **`LocalUDP` 模式：** 可用于在本地网络上进行测试，无需任何外部依赖。

### 构建项目

标准的 Unity 构建过程：

1.  在 Unity 编辑器中，转到 `File > Build Settings...`。
2.  选择您的目标平台（例如，Windows、macOS、Linux）。
3.  点击 **Build** 创建一个独立的可执行文件。

## 开发约定

*   **网络抽象：** 网络逻辑的核心是 `INetworkLayer` 接口。所有与网络相关的交互都应理想地通过此接口进行，以保持在不同网络后端之间切换的能力。
*   **管理器单例：** 项目对全局管理器类（如 `NetworkManager`、`LobbyNetworkManager` 和 `GameManager`）使用单例模式。
*   **事件驱动通信：** 网络层使用 C# 事件（例如 `OnLobbyCreated`、`OnPacketReceived`）实现网络层和游戏逻辑之间的解耦通信。
*   **数据序列化：** 网络消息在通过网络发送之前被序列化为 JSON。
*   **输入处理：** 玩家输入通过 Unity 输入系统 (`InputActionReference`) 进行管理。`PlayerController` 脚本将输入数据发送到 `LobbyNetworkManager` 以在主机上进行处理。
*   **代码风格：** 代码通常结构良好并带有注释。注释以英文和中文两种语言存在，表明这是一个多语言开发环境。
*   **场景结构：** 主要的游戏逻辑和 UI 似乎从 `MainScene.unity` 启动。玩家预制件位于 `Assets/Prefabs`。