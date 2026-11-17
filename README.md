# 晓苏宠伴

面向桌面的 AI 陪伴项目，目标支持 Windows/macOS、VRM 与 Live2D 角色、语音交互和本地记忆。

当前仓库处于工程初始化阶段；这次提交提供协作规范与 Git 配置，尚无可运行 Unity 工程或安装包。能力以实际构建与验收证据为准。

## 开始协作

- [贡献与 PR 流程](CONTRIBUTING.md)
- [架构边界](docs/architecture.md)
- [阶段与验收](docs/roadmap.md)
- [Agent 工作规则](AGENTS.md)

Multica 是任务、负责人、阶段与验收记录的入口，GitHub 是代码与 PR 的入口。用 FUMI 任务编号关联二者，不重复维护两套任务列表。

工程采用 Unity/C# 进程内核心，具体 Unity LTS patch、URP 和 SDK 版本由首轮兼容验证后锁定。云端服务可以用于早期联调；完整离线能力仍需单独通过断网验收。
