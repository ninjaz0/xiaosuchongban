# 架构边界

## 基线

Unity/C# 主进程承载核心编排，云端和外部本地推理通过 provider 适配。VRM、Live2D 面向同一角色抽象；不让 AI、UI 或平台层依赖具体模型 SDK。

推荐目录在真实 Unity 工程初始化时建立：

```text
Assets/_Project/
  Bootstrap/
  Core/
  Platform/
  Avatar/Abstractions/
  Avatar/Vrm3D/
  Avatar/Live2D/
  Avatar/LipSync/
  CompanionAI/LLM/
  CompanionAI/Speech/
  CompanionAI/Memory/
  CompanionAI/Orchestrator/
  UI/
```

接口以 IAvatarAdapter、ILLMProvider、ITTSProvider、IMemoryStore 为边界，字段、线程约束与取消/释放语义由首轮契约任务确定。VContainer/UniTask/R3 等依赖按经验证的需求逐步引入。

## 先验证的集成风险

- 所选 Unity LTS patch、URP、UniVRM 与 Cubism 的真实兼容组合。
- Windows/macOS 的透明、穿透、焦点、多屏与全屏行为；Unity 编辑器结果不替代打包验证。
- 目标平台架构下 ONNX Runtime、SQLite、sqlite-vec/FTS5 与序列化方案的打包和运行。
- 不同 LLM 的协议差异；OpenAI 兼容接口需实际验证，不把所有原生 API 当成同一协议。
- SDK、模型、字体、动作资源与可选插件的适用授权。

## 生命周期和数据流

文字/语音输入进入对话状态机，流式内容解析为显示文字、受控情绪事件和 TTS 队列；口型分析实际播放音频。打断或换模型时统一取消旧任务，避免旧 token/音频/动作污染新会话。

记忆分为短期窗口、核心事实与长期摘要。提供查看、纠正和删除；推测不得写成已确认事实，删除需同步清理索引。只有在目标设备上通过断网检查，才能声称离线运行。

## 暂不锁死的选择

初期先做一个角色的完整闭环，再加入另一管线与热切换。进阶物理、额外着色器、Animancer、MagicaCloth、独立 Tauri 设置窗口及具身设备由需求和测量决定，不是初始化的强制依赖。
