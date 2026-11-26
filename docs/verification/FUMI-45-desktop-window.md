# FUMI-45 · 桌面窗口 Platform 接入验证

> 分支：`feat/FUMI-45-desktop-window-platform`  
> 目标：把原型 `E:\xiaosu-pet` 的透明置顶、点击穿透、真实窗口顶边跟随/落回接进主仓 `Xiaosu.Platform`。  
> 原型：<https://github.com/secret-star-handsome/pet>

## 1. 本轮范围

- `Xiaosu.Platform`（`noEngineReferences` 纯 C#）新增：
  - `IDesktopWindowOverlay`：在现有 `IDesktopWindow` 上增加 `Initialize/Tick/IsAttached/StateChanged`。
  - `DesktopWindowSettings`、`DesktopWindowEvent`、`WindowInfo`、`DesktopWindowGeometry`、`DesktopWindowMetrics`、`DesktopWindowFactory`。
  - `Windows/`：`NativeMethods`、`WindowTracker`、`WindowsDesktopInput`、`AttachmentController`、`WindowsDesktopWindow`。
  - `Mac/MacDesktopWindow`：窗口配置/点击穿透/圆盘命中；**顶边跟随仍 TODO，未编译/未实测**。
- `Xiaosu.Bootstrap`：`DesktopPetHost` + 独立 `DesktopPetDemo` 场景（占位橙色圆盘）；不修改 `BaselineDemo`。
- `Xiaosu.Platform.Editor`：`DesktopPetBuild`，只在构建时设置 D3D11/flip model/窗口化，并恢复原设置；不影响 FUMI-42 默认构建。
- `Assets/Settings/PC_RPAsset.asset`：启用 `m_AllowPostProcessAlphaOutput`（Alpha Processing）。
- macOS 原生源码：`Native~/macOS/{PetMac.h,PetMac.mm,build-macos.sh}`；`Assets/Plugins/macOS/README.md`；**不提交 `.bundle`**。

## 2. 打开/构建

- Unity **6000.3.28f1**（changeset `8f082d2fe476`），URP **17.3.0**。
- 首次打开会恢复 URP 包；等待编译完成，Console 0 error。
- 生成 Demo 场景（若仓库未带场景或需要修复）：

```powershell
$unity = 'C:\path\to\6000.3.28f1\Editor\Unity.exe'
$root = 'E:\xiaosu-main-fumi45'

& $unity -batchmode -quit -projectPath $root `
  -executeMethod Xiaosu.Platform.Editor.DesktopPetBuild.CreateDemoScene `
  -logFile "$root\Logs\FUMI-45-scene.log"
```

- 构建 Windows 宠物 Demo（自动设置 D3D11、flip model=0、Windowed 200 DIP、Mono，构建后恢复）：

```powershell
& $unity -batchmode -quit -projectPath $root `
  -executeMethod Xiaosu.Platform.Editor.DesktopPetBuild.BuildWindows `
  -logFile "$root\Logs\FUMI-45-build.log"
```

产物：`Build\Win64\XiaosuDesktopPet.exe`。

> 不要用普通 `-buildWindows64Player` 验证透明：主仓默认 flip model=1、Windows 图形 API 自动，可能黑屏。必须走 `DesktopPetBuild`。

## 3. Windows 手工检查表

| # | 操作 | 通过标准 | 结果 |
|---|---|---|---|
| 1 | 启动 exe，等 Splash 结束 | 右下角橙色圆盘；背景透明、无边框/任务栏项；置顶 | 通过（本机 Unity 6.3） |
| 2 | 记事本前台输入 `abc`，点宠物，再输入 `def` | `def` 仍进记事本；`GetForegroundWindow` 不变 | 通过（前台 HWND 不变） |
| 3 | 点宠物旁边透明空白（覆盖记事本） | 记事本响应；`WindowFromPoint` 返回下层窗口，不是 `UnityWndClass` | 通过（透明角返回 Edge） |
| 4 | 拖宠物到记事本顶边松手 | 圆盘底边贴住顶边；日志 `attached to Notepad ...` | 通过（日志 Attached） |
| 5 | 上下左右拖动记事本 5 秒 | 宠物逐帧跟随，无分离 | 通过（+100/+50 同步） |
| 6 | 横向缩放记事本 | 宠物仍在顶边 | 通过 |
| 7 | 最小化记事本 | 平滑落回桌面，日志 `FallStarted`/`FallCompleted` | 通过 |
| 8 | 恢复/重新吸附后关闭记事本 | 再次落回，无报错 | 通过 |
| 9 | 落回后点击宠物 | 占位圆盘闪烁/小跳；前台不变 | 通过（点击不抢焦点；Flash 视觉） |
| 10 | `Ctrl+Alt+Q` | 进程退出，无 `UnityCrashHandler64` 残留 | 通过 |
| 11 | 125% 缩放 | 命中/吸附无偏移 | 通过 |
| 12 | 150% / 200% / 100% | 手动切缩放后重启复测 | 待测（需手动切缩放） |
| 13 | 双显示器 | 本机无第二屏 → 阻塞 | 阻塞 |
| 14 | macOS | 无 Mac → 阻塞；源码已附 | 阻塞 |

### 本次实测记录（Windows 11 26200 / 125% / Unity 6000.3.28f1 / URP 17.3.0）

- 构建：`DesktopPetBuild.BuildWindows` 成功，产物 `Build\Win64\XiaosuDesktopPet.exe`。
- 窗口：`style=0x94000000/ex=0x8000088`，`topmost/noactivate/toolwindow`；圆形 region；`DwmExtendFrameIntoClientArea` + URP Alpha Processing；橙色占位圆盘透明显示。
- 穿透/焦点：透明角 `WindowFromPoint` 返回 Edge；圆盘中心返回宠物；点击宠物/透明角前后 `GetForegroundWindow` 不变。
- 吸附/跟随：拖到 Notepad 顶边，日志 `Attached: attached to Notepad 37832`；Notepad +100/+50，宠物窗口同步 +100/+50。
- 落回：最小化/关闭 Notepad，日志 `FallStarted`/`FallCompleted`；落回后点击宠物仍不抢焦点。
- 退出：`Ctrl+Alt+Q`，进程退出。
- 备注：首次启动时原型 `xiaosu-pet.exe` 还占着 `Ctrl+Alt+Q`，主工程 `RegisterHotKey` 返回 1409；停掉原型并重启主工程后正常。测试时请确保只运行一个宠物实例。
- `.meta`/场景/材质由 Unity 6.3 生成：`DesktopPetDemo.unity`、`DesktopPetPlaceholder.mat` 及所有新增脚本/文件夹 `.meta` 一并提交。

### FUMI-42 回归

```powershell
$unity = 'E:\Unity\6000.3.28f1\Editor\Unity.exe'
$root = 'E:\xiaosu-main-fumi45'

# EditMode
& $unity -batchmode -projectPath $root -runTests -testPlatform EditMode `
  -testResults "$root\Logs\EditMode.xml" -logFile "$root\Logs\EditMode.log"

# FUMI-42 Windows baseline build
& $unity -batchmode -quit -projectPath $root `
  -buildWindows64Player "$root\Build\Win64\XiaosuMockDemo.exe" `
  -logFile "$root\Logs\FUMI-42-build.log"
```

- 结果：**EditMode 8/8 通过**（FUMI-42 3/3 + DesktopWindow 5/5）；`XiaosuMockDemo.exe` 构建成功。
- `BaselineDemo` 场景/相机未修改；URP `PC_RPAsset` 只改 `m_AllowPostProcessAlphaOutput: 1`；FUMI-42 测试与构建均通过。
- `DesktopPetBuild` 临时设置 D3D11/flip model=0/Windowed/200 DIP/Mono，构建后恢复原 PlayerSettings。

## 4. 原型复现（Windows 2022.3.62f3c1，已实测）

仓库：<https://github.com/secret-star-handsome/pet>

1. 用 Unity 2022.3.62f3c1 构建 `Build\Windows\xiaosu-pet.exe`，或直接运行已构建产物；构建/操作说明见该仓库 `README.md`。
2. 打开记事本，输入文字；点击宠物，输入焦点不丢。
3. 点圆盘外透明区，点击穿透到记事本。
4. 拖宠物到记事本顶边；拖动/缩放/最小化/关闭记事本，观察跟随/落回。
5. 落回后点击宠物；`Ctrl+Alt+Q` 退出。

原型证据：`E:\xiaosu-pet\docs\TEST_MATRIX.md`、`docs\DEMO_SCRIPT.md`。  
注意：原型是 Built-in/D3D11/Gamma；主仓是 URP/Linear，透明效果必须在主仓重新验证。

## 5. 已知风险/未测项

- **本机 Unity 6.3 导入/构建**：见本次日志；如果包恢复或 URP Alpha 失败，按 Console 修复。
- **URP Alpha Processing**：`PC_RPAsset` 已启用 `m_AllowPostProcessAlphaOutput`；若黑底/整窗透明，检查 URP prefiltering、相机后处理、HDR/MSAA、D3D11、flip model。
- **Mono/IL2CPP**：`DesktopPetBuild` 固定 Mono；IL2CPP 的 WndProc 反向 P/Invoke 需要另做原生 DLL/AOT。
- **Unity 6 窗口类名**：默认 `UnityWndClass`；找不到时后端回退同 PID 最大可见窗口。
- **DPI/多屏**：125% 已测；150/200/100 待手动切；双屏无硬件。
- **macOS**：源码未编译，顶边跟随 TODO；`.bundle` 未提交，需 Mac 队友构建并补录屏。
- **BaselineDemo 回归**：未修改场景/相机；URP Alpha 资产改动后需跑 FUMI-42 EditMode 3/3 + Windows 构建。
- **UI 视觉**：FUMI-45 用占位橙色圆盘；正式 VRM/Live2D 由 FUMI-44/M1 接。

## 6. 协作

- PR 从 `secret-star-handsome/xiaosuchongban:feat/FUMI-45-desktop-window-platform` 提到 `ninjaz0/xiaosuchongban:main`。
- 队友若需直接修 PR 分支：把队友加为 fork collaborator，或在 PR 评论给 patch/Console log，由作者代改。
- 不在本 PR 合并/发布；Multica 最终状态人工验收。
