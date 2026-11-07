# Big Old Guy

[日本語](README.ja.md) · [下载 Windows 版](https://github.com/Resuty/big_old_guy/releases/latest)

用 Unity 和 C# 制作的单人卡牌对战 Demo，玩家与 AI 对战。使用一副 54 张扑克牌，在牌型比较之外加入卡牌效果和生命值：要不起会受伤，生命也可以用来购买额外手牌。

这是我的个人学习与求职展示项目。我想尝试把熟悉的扑克牌规则做成一套有资源取舍的对战，并通过这个项目学习 Unity 的界面、输入和游戏状态管理。

![Windows 版对战画面](Docs/screenshots/gameplay-zh.jpg)

## 试玩

在 [Releases](https://github.com/Resuty/big_old_guy/releases) 下载 `BigOldGuy-v0.1.0-Windows-x64.zip`，解压后运行 `BigOldGuy.exe`。请保留同目录下的 Data 文件夹和 DLL。无需安装 Unity。

发布平台为 Windows 64 位。设置中可切换中文、日语、英语，分别调整音乐和音效音量，也可调整 AI 难度和行动速度。

| 操作 | 功能 |
| --- | --- |
| 左键点击手牌 | 选中或取消选中 |
| 长按卡牌 | 查看牌效说明 |
| 出牌 / 要不起 | 提交选牌或放弃回应 |
| 提示 | 选中一组可出的牌，仍需自己确认 |
| 右上角齿轮 / Esc | 打开设置 |
| 设置 → 对局 → 新开一局 | 重新开始 |

## 规则

- 双方初始生命为 3，各持 8 张牌；猜拳决定先手。
- 用相同牌型、更高主点数回应上一手；炸弹和王炸有额外的压制规则。当前支持 11 种牌型，不包含飞机。
- 要不起会损失生命，并开始新的一墩。生命低于 0 时失败；出完手牌并不是直接获胜。
- 普通行动通常免费抽 1 张。允许购买时，可花 1 点生命额外抽 4 张；首次先手行动不能购买，也没有免费抽牌。
- 卡牌效果按出牌前、出牌后、进入弃牌堆后三个时机处理，包括复制点数、抽牌、回手和调整牌库顺序等。
- 弃牌回收、低手牌补牌和受伤后的补偿抽牌，让对战可以继续进行。

具体规则和牌效说明可以在游戏内的“设置 → 对局 → 规则手册”查看。

## 实现

规则层使用普通 C#，界面使用 uGUI。卡牌实体保留固定 ID；手牌、牌库和弃牌堆存放 ID，图片只负责显示。这样可以单独检查规则，也方便更换界面。

| 入口 | 内容 |
| --- | --- |
| [GameEngine.cs](Assets/CardDuel/Core/GameEngine.cs) | 抽牌、出牌、回合推进、效果结算和胜负 |
| [Model.cs](Assets/CardDuel/Core/Model.cs) | 卡牌、状态、出牌命令与快照 |
| [HandEvaluator.cs](Assets/CardDuel/Core/HandEvaluator.cs) | 牌型识别和大小比较 |
| [AiController.cs](Assets/CardDuel/Core/AiController.cs) | 基于可见信息选择行动 |
| [TeachingGameScreen.cs](Assets/CardDuel/LearningView/TeachingGameScreen.cs) | 对战界面和输入到规则的连接 |
| [TeachingSettings.cs](Assets/CardDuel/LearningView/TeachingSettings.cs) | 设置保存、语言、音效和 BGM |
| [RuleChecks.cs](Assets/CardDuel/Verification/RuleChecks.cs) | 42 组规则检查 |

出牌先预览是否合法，再提交状态变更；复制点数写入本次出牌快照，不覆盖卡牌原始点数。AI 通过观察数据取得自己的手牌和公开信息，不直接读取对手暗牌或牌库顺序。

## 打开工程与构建

使用 Unity **6000.6.0f1**，主要依赖 URP 2D、uGUI、Input System。资源使用 Git LFS，克隆后需取回大文件：

```powershell
git clone https://github.com/Resuty/big_old_guy.git
cd big_old_guy
git lfs pull
```

在 Unity Hub 添加项目目录，打开 `Assets/Scenes/Game.unity` 后运行。`Card Duel → Build Windows Release` 会先运行规则检查和 5 个固定开局的自动对局，再构建 Windows 版；输出位于 `Builds/v0.1.0`。详细调用链见 [中文技术手册](Docs/CardDuel完整对局技术讲解手册.pdf)。

## 开发方式与当前限制

游戏规则与玩法方向由我提出，代码在 Codex 辅助下完成，包含较多辅助生成的实现。这个仓库也保留了我学习 C#、重建 uGUI 界面的过程；`Docs` 中的旧实现备份用于对照学习，不参与游戏编译。

目前是本地单人 Demo，没有联网对战。上一手区域最多显示前 8 张牌，完整张数与出牌记录保留在日志中。界面动效较少，尚未覆盖多种电脑和显示器的兼容性测试。

## 素材

卡牌和界面图片、音效来自 Kenney，字体为 Noto Sans CJK；完整来源见 [素材说明](Assets/CardDuel/Licenses/ASSET-SOURCES.md) 和 [图片来源](Assets/CardDuel/Licenses/PUBLIC-ART-SOURCES.json)。BGM 已获项目作者确认可随游戏公开分发，不属于 Kenney 的 CC0 素材，也不因此授予独立提取和再利用的许可。仓库公开供阅读和作品展示；项目自有代码目前未另行声明开源许可证。
