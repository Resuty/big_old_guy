# Card Duel 完整对局技术讲解手册

> 对应教学项目：`D:\unitylearning\big_old_guy`，Unity 6000.6.0f1。更新于 2026-10-31。本文以教学项目当前代码为准。原 Demo 的核心规则代码已复制到教学项目；可视界面按你的选择使用 uGUI、`CardVisual` Prefab 和公开素材重新接线。原 Demo 本身没有被修改。

## 0. 先运行，再带着问题读代码

1. 在 Unity Hub 打开 `D:\unitylearning\big_old_guy`。
2. 在 **Project** 窗口双击 `Assets/Scenes/Game.unity`。这是教学版完整对局的入口场景，也是 Build Settings 中第一项。
3. 看 **Hierarchy**：`GameRoot` 管理游戏；`Canvas/HandRoot` 管理人类手牌；`EventSystem` 转发鼠标等 UI 输入；`Main Camera` 与 `TableBackgorund` 构成画面。`CardVisual` 在 Project 的 Prefabs 文件夹里，是资产模板，不需要预先摆 54 个场景对象。
4. 点击顶部 **Play**。在 **Game** 页签操作实际运行的画面；**Scene** 页签是编辑场景和摆放对象的工作区。运行时脚本创建的 `Teaching UI` 可以在 Play 模式的 Hierarchy 中展开查看，停止 Play 后会消失。**Inspector** 显示当前所选对象的组件和字段。**Console** 用来查看错误、警告和日志。
5. 猜拳，选择是否买牌，点击手牌选中，再点“出牌”；有多种出法时在弹窗中选一种。需要回应上一手时可点“要不起”。部分牌会出现出牌后/弃牌后效果。直到一方 HP 低于 0。

阅读顺序建议：先看下文第 1～6 节，边玩边在代码中搜索同名函数；看完一条交互链再继续。打开 C# 文件不需要把全部函数背下来，先回答“谁调用谁、哪个状态改变、牌从哪里到哪里”。

### 文件地图

| 文件 | 在本局中的职责 |
| --- | --- |
| [Model.cs](../Assets/CardDuel/Core/Model.cs) | 卡牌、配置、枚举、命令、快照、状态、AI 可见信息 |
| [GameEngine.cs](../Assets/CardDuel/Core/GameEngine.cs) | 唯一修改规则状态的核心：建牌、抽牌、出牌、效果、回合、胜负 |
| [HandEvaluator.cs](../Assets/CardDuel/Core/HandEvaluator.cs) | 识别牌型、比较压制、生成基础候选 |
| [AiController.cs](../Assets/CardDuel/Core/AiController.cs) | 根据可见信息选择 AI 出牌及效果 |
| [GameBootstrap.cs](../Assets/CardDuel/LearningView/GameBootstrap.cs) | Unity 场景入口，创建 `GameEngine` |
| [TeachingGameScreen.cs](../Assets/CardDuel/LearningView/TeachingGameScreen.cs) | uGUI 界面、按钮、弹窗、AI 行动调度 |
| [TeachingSettings.cs](../Assets/CardDuel/LearningView/TeachingSettings.cs) | 教学版设置界面、语言切换、存档、BGM 与音效播放 |
| [UserSettings.cs](../Assets/CardDuel/Presentation/UserSettings.cs) | 设置数据及合法范围校验 |
| [Localization.cs](../Assets/CardDuel/Presentation/Localization.cs) | 中文原文到日语、英语的界面和规则文本映射 |
| [Rulebook.cs](../Assets/CardDuel/Presentation/Rulebook.cs) | 三种语言的规则手册正文 |
| [HandView.cs](../Assets/CardDuel/LearningView/HandView.cs) | 把玩家手牌映射为卡牌图片，管理选中 ID |
| [CardVisual.cs](../Assets/CardDuel/LearningView/CardVisual.cs) | 单张卡牌 Prefab 的图片、徽章、点击和长按 |
| [RuleChecks.cs](../Assets/CardDuel/Verification/RuleChecks.cs) | 规则回归检查和模拟对局 |
| [RulesTests.cs](../Assets/CardDuel/EditModeTests/RulesTests.cs) | Unity EditMode Test Runner 入口 |

依赖方向：`CardVisual/HandView/TeachingGameScreen → GameEngine → Model/HandEvaluator`；AI 通过 `GameObservation` 使用规则。**界面读状态、发命令；规则引擎才移动牌或扣血。**原 Demo 的 Core 与教学项目中的这几份 Core `.cs` 文件内容一致，教学版 UI 则是独立实现。

## 1. 一局游戏的总流程

```text
打开 Game 场景
  → GameBootstrap.Awake：new GameEngine(seed)
  → GameEngine.CreateDeck：生成 54 张实体牌、分配效果、洗牌
  → GameEngine.Draw：双方各发 8 张；Phase = Rps
  → GameBootstrap.Start：添加 TeachingGameScreen 组件
  → TeachingGameScreen.Start：建 uGUI、订阅 Changed、显示猜拳
  → Guess：平局重猜；胜者 BeginTurn
  → Draw：决定免费抽牌/是否花 HP 买额外 4 张
  → Select：选牌、预览、提交，或回应上一手时 Pass
  → Post：选择/自动触发本次出牌的一个出牌后效果
  → 把本次出牌从手牌移到弃牌堆
  → Discard：选择/自动触发一个弃牌后效果
  → BeginTurn(另一方)，回到 Draw
  → 有人 Pass：扣 HP、清除上一手、Trick +1、安排补偿抽牌
  → HP < 0：GameOver，显示胜者；可以再来一局
```

`TurnPhase` 是流程状态（phase）。它让按钮和规则都知道现在允许什么操作；例如 `Commit` 只能在 `Select` 调用，`ResolveEffect` 只接受 `Post` 或 `Discard`。`Guard(actor, phase)` 同时检查**玩家编号、当前行动者和阶段**。玩家是 `0`，AI 是 `1`，不是任意卡牌 ID。

### “一回合”和“一墩”不是一回事

- `State.Turn`：每次 `BeginTurn(actor)` 自增一次。玩家行动一次、AI 行动一次，分别算回合。
- `State.Trick`：从 1 开始，仅在 `ResolvePass` 中自增；上一手被清空，开启新的一墩争夺。
- `State.LastPlay`：当前需要被压过的上一手。它可以跨多个玩家行动存在；Pass 后清空。由此，`Turn` 变化不意味着“上一手”自动消失。

例如玩家出单张 7：`LastPlay=7`；AI 的下一个 Turn 要出更高单张、炸弹或王炸，或者 Pass。若 AI Pass，HP 减 1，`Trick++`，`LastPlay=null`，AI 获得新墩的先手行动。

## 2. 卡牌身份、点数、花色、效果

`CardInstance` 有四个只读属性：

```csharp
public int Id { get; }
public int Rank { get; }
public int Suit { get; }
public EffectId Effect { get; }
```

- `Id` 是 0～53 的**实体身份**。两张牌即使暂时都按“黑桃 7”计算，ID 仍不同。所有区域存的是 ID，再用 `State.Cards[id]` 找到牌。
- `Rank` 是**印刷点数**：3～10、J=11、Q=12、K=13、A=14、2=15、小王=16、大王=17。
- `Suit`：0=黑桃、1=红心、2=梅花、3=方块；王没有花色，使用 -1。花色用于牌面与身份；现有牌型比较主要看点数。
- `Effect` 是实体牌绑定的效果。普通牌可能是白板；两张王中一张固定是 `ExileJoker`，另一张白板。大小王用不同 Sprite 显示。

`EffectId`、`TurnPhase`、`HandType` 等是 **enum（枚举）**：把有限的合法取值用名字表示。`sealed class` 表示类不允许再被继承。`{ get; }` 是只有 getter 的属性：构造函数赋值之后，外部可以读取而不能改写。复制点数效果不修改 `CardInstance.Rank`；`Preview` 建一个仅供本次出牌使用的 `ranks` 数组，再把逻辑点数存进 `PlaySnapshot.Ranks`。这避免“印刷牌面被永久改掉”。

### 54 张牌如何生成

看 `GameEngine.CreateDeck()`：

1. 双循环 `r=3..15`、`s=0..3`，产生 `13 × 4 = 52` 张普通牌。
2. 追加点数 16 和 17、花色 -1 的两张王。
3. 普通牌 ID 0～31 属于 3～10 的低点数组，32～51 属于 J～2 的高点数组。默认分别留 7、12 张白板，再从打乱的效果袋分配其余普通牌效果；配额总和由 `GameBalanceConfig.Validate()` 检查必须等于 52。
4. ID 52、53 两张王中随机选一张赋予 `ExileJoker`。
5. `State.cards` 按 ID 存实体对象；`State.draw` 先放 0～53，再用 `SeededRandom.Shuffle` 洗牌。

**随机性**由同一个 `SeededRandom(seed)` 负责。给同样 seed、执行同样命令序列，会得到可重现的牌库与效果。`Shuffle` 用 Fisher–Yates：从末尾往前，将当前位置与 `[0,i]` 中随机位置交换。`Next(max)` 返回 `[0,max)`；内部 xorshift32 更新 32 位无符号状态，拒绝采样处理取余偏差。这里用 `uint` 是代码中明确写出的类型。注意它是**可复现伪随机**，不是密码学随机。

## 3. 牌在哪里：状态、牌库、手牌、弃牌、除外

在 `GameState` 中，内部可变区域是：

```csharp
internal readonly List<int> draw = new List<int>(),
    discard = new List<int>(), exile = new List<int>();
internal readonly List<int>[] hands =
    { new List<int>(), new List<int>() };
```

`draw` 是牌库，**索引 0 是牌顶**；`discard` 是弃牌堆，新进入的牌通常追加到末尾，界面显示最后一张；`exile` 是移出本局的牌；`hands[0]` 和 `hands[1]` 是双方手牌。`readonly` 限制的是**字段引用重新赋值**，不妨碍 `List.Add/Remove` 修改列表内容。

对界面公开的是 `DrawPile`、`DiscardPile`、`ExileZone`、`Hand(actor)` 等只读视图。`State.Cards[id]` 用于“ID → 实体内容”。规则引擎内部直接修改列表；UI 不修改它们。

还有两个阶段性集合：

- `pending`：本次已经提交出牌，但为等待 POST 效果，**实体仍暂留在行动者手牌中**的 ID。
- `enteredDiscard`：本次从 `pending` 真正移入弃牌堆的 ID，只有这些牌可作为 DISCARD 效果来源。

因此 `pending`、`enteredDiscard` 不是第五、第六个实体存放区；它们是指向现有实体的**阶段索引**。`CheckInvariant()` 把牌库、弃牌、除外、双方手牌拼起来，要求总数和不同 ID 数都恰好是 54。每次成功命令最后由 `Done()` 调用它。若有人重复添加一张 ID、丢失牌或移动错区域，就会抛 `InvalidOperationException`。

## 4. 抽牌：定义在哪里、谁调用、从哪里抽

一开局 `GameEngine` 构造函数先 `CreateDeck()`，然后：

```csharp
Draw(0, config.InitialHand, "初始发牌");
Draw(1, config.InitialHand, "初始发牌");
```

默认 `InitialHand=8`，所以各 8 张，若无其他移动，牌库剩 38。实际抽牌都经过 `GameEngine.Draw(actor, count, source, effectSource)`。核心循环是：

```csharp
for (int i = 0; i < count; i++)
{
    if (State.draw.Count == 0)
        Recycle("抽牌不足自动回收", effectSource);
    if (State.draw.Count == 0)
        break;
    int id = State.draw[0];       // 读牌顶
    State.draw.RemoveAt(0);      // 从牌库删除这张实体
    State.hands[actor].Add(id);  // 放进指定玩家手牌
    actual++;
}
```

假设牌库 `[12,31,7]`，玩家手牌 `[4]`，请求抽 2 张：第一次移动 ID 12，第二次移动 ID 31；结果牌库 `[7]`、手牌 `[4,12,31]`、`actual=2`。`RemoveAt(0)` 是移除**索引 0 处的元素**，不是移除“数值为 0 的卡”。此处 `List<T>` 头部移除需要搬移后续元素，约 O(n)；牌总数固定 54，足够简单。若未来海量对象、频繁从队头取，可考虑 `Queue<T>.Dequeue()` 或用“牌顶索引”而不删元素；但重洗牌与插入牌顶/底会影响选择。

如果牌库空，`Draw` 先调用 `Recycle`：把弃牌堆加入牌库、清空弃牌堆、洗牌；仍空才 `break`，所以实际张数可以小于请求张数。`AvailableToDraw` 是当前牌库数加弃牌堆数；处于手牌或除外区的牌不计入可抽数。

### 正常回合抽牌与买牌

默认配置：普通免费抽 1；首次猜拳胜者免费抽 0、不能购买；可购买时花 1 HP 额外抽 4。`BeginTurn` 设 `Phase=Draw`、本次免费抽数、购买标记等。教学 UI 的购买/不购买按钮调用 `ChooseDraw(0, true/false)`：

- 先检查当前是否真在该玩家的 Draw 阶段；若想购买，还要 `CanBuy`。
- 购买时 `ApplyPurchase` **先** `ApplyFreeDraw`，然后扣 HP、额外 `Draw(...,4)`。`freeDrawDone` 防止重复免费抽牌。
- `CompleteDraw` 再调用一次 `ApplyFreeDraw`，已抽过则直接返回；设置 `firstTurn=false`、`Phase=Select`。
- 不购买则只走免费抽牌。AI 的 `StepAi` 使用 `BuyExtra`/`FinishDraw`；两条路径也复用上述底层操作。

HP 可以正好变成 0，胜负判定是 `<0`，不是 `<=0`。抽牌不足也不会凭空生成卡。某些牌效（如“抽两张”“换牌”）同样调用底层 `Draw`，但不经过回合购买按钮。
## 5. 猜拳与开始行动：谁先走、什么时候换人

猜拳按钮在 `TeachingGameScreen.DrawRps()` 创建。点击按钮，uGUI `Button.onClick` 运行回调，最终调用 `game.Guess(choice)`。`0=石头、1=剪刀、2=布`。`Guess` 用 `random.Next(3)` 取 AI 手势；相同则仍处于 `Rps`，让你重新点；不同则按 `(gesture + 1) % 3 == ai` 判定玩家是否胜利，然后 `BeginTurn(winner)`。

`BeginTurn` 执行顺序：

1. 设置 `State.Actor`，`Turn++`，`Phase=Draw`。
2. 把 `purchased`、`freeDrawDone` 清回 false，清理上次行动的 `pending`、`enteredDiscard`。
3. 免费抽牌数设为：第一次行动 0，否则默认 `NormalDraw=1`。
4. 如果有 Pass 产生的 `grants` 补偿队列，取队首覆盖本回合免费抽牌数，并检查队首确实属于当前行动者。
5. 写日志。这里**尚未真正把牌从牌库移走**；抽牌发生在 Draw 阶段的选择命令中。

正常出牌并完成效果后，`AdvanceEffectStage` 选择 `next = 1 - State.Actor`，调用 `BeginTurn(next)`。发生 Pass 时是另一条路：`ResolvePass(actor)` 清空上一手并让 Pass 的人立刻成为新墩的行动者，随后按队列在之后行动中补偿抽牌。两种换人路径不要混为一谈。

## 6. 选牌、识别牌型与压制：先预览，再提交

### 6.1 牌型识别

`HandEvaluator.Recognize(ranks)` 接收本次出牌的**逻辑点数**，排序并按点数分组，可能返回零个或多个 `HandPattern(Type, Count, MainRank)`。本游戏当前支持：

| `HandType` | 成立条件概要 | 主点数 |
| --- | --- | --- |
| `Single` | 1 张 | 该牌 |
| `Pair` | 2 张同点数 | 对子点数 |
| `Triple` | 3 张同点数 | 三张点数 |
| `TripleSingle` | 3+1，共 4 张 | 三张点数 |
| `TriplePair` | 3+2，共 5 张 | 三张点数 |
| `Straight` | 至少 5 张，不重复、连续，最大不超过 A | 最大点数 |
| `PairRun` | 至少 3 对，每点数 2 张、点数连续，最大不超过 A | 最大点数 |
| `FourSingles` | 4+2 张，共 6 张 | 四张点数 |
| `FourPairs` | 4+2 对，共 8 张 | 四张点数 |
| `Bomb` | 4 张同点数 | 四张点数 |
| `Rocket` | 一张小王 + 一张大王 | 大王点数 17 |

**顺子为什么不会把 3,4,5,7,8 算成连续？**因为条件同时要求无重复、最大值减最小值等于 `n-1`。五张互异牌若首尾差是 4，就恰好占满中间四个间隔；3 到 8 差 5，因此失败。连对同理，还要求每组恰好两张。2 和王不能进顺子/连对，最大点数必须 `<=14`。由于“复制点数”可临时制造超过四张同点数，函数先拒绝任一组数量大于 4。

`HandEvaluator.Beats(play, previous)` 负责压制：没有上一手则任何已识别牌型都可出；王炸不可被压过；王炸压所有其他牌；炸弹压非炸弹；其余要求**类型相同、张数相同、主点数更高**。炸弹与炸弹比较主点数。类型是 `enum`，不是根据图片或文字猜测。

### 6.2 从鼠标选牌到规则命令

`HandView` 按 `State.Hand(0)` 创建/复用 `CardVisual`，每张绑定唯一的 `id`。点击卡牌时：

```text
CardVisual.OnPointerClick
  → Bind 时传入的 click 回调
  → HandView.ToggleSelection(id, view)
  → selectedIds(HashSet<int>) 增删 ID
  → SelectionChanged 事件
  → TeachingGameScreen.RefreshControls 更新按钮可用状态
```

选中的是实体 ID，避免同点数、同牌面或复制点数造成“到底点了哪张”的歧义。`HashSet<int>` 保证不重复。长按 `0.45` 秒触发详情回调；移动超过 12 像素或松开/离开时结束长按，减少滚动手牌时误点。

点“出牌”后的路径：

```text
TeachingGameScreen.ShowPlayOptions
  → game.PlayOptions(0, selectedIds)
      → 无 PRE / 每种可用 PRE 来源 / 复制的每个有效目标
      → HandEvaluator.Recognize
      → game.Preview 过滤不合法方式
  → 0 种：用 PreviewSelection 的 Reason 提示
  → 1 种：直接 game.Commit
  → 多种：弹出牌型与 PRE 效果列表，玩家选一个，再 Commit
```

`PlayCommand` 带 `Cards`（ID 数组）、`Type`（想按哪种牌型出）、`PreSource`（是否选用牌前效果）、`CopyTarget`（复制点数时目标 ID）。`-1` 表示未选来源/目标。多个实体都有 PRE 时，这次**只选其中一个**；另一个不会自动一起发动。

`Preview` 验证当前阶段、归属、无重复、PRE 来源和目标、牌型以及能否压过上一手，并返回 `PreviewResult`。它不从任何区域移动牌，也不消耗随机数，所以可以反复用于界面提示。`Commit` **再次调用 Preview**，合法后才写入 `LastPlay`、`pending`、出牌累计数并切到 `Post`。再次检查很重要：界面预览后状态可能变化，最终提交必须以当前状态为准。

### 6.3 印刷点数和逻辑点数分开

假设实体 ID 42 印着 J、效果是 `CopyRank`；本次同选一张印着 7 的实体 ID 20。`Preview` 在临时数组中把 ID 42 对应位置设为 7，以此识别牌型；`CardInstance(42).Rank` 仍是 J。`Commit` 记录：

- `LastPlay.CardIds`：真正打出的实体 ID，仍是 42、20；
- `LastPlay.Ranks`：这一次用于压制的点数，例如 7、7；
- `LastPlay.Pattern`：按逻辑点数得到的对子 7。

下次比较上一手，用的是快照中的逻辑结果。之后若实体 42 回到手牌，印刷点数和效果仍保持原样。`PlaySnapshot` 在构造时用 `Array.AsReadOnly(ids.ToArray())` 复制数组，防止外部后来改写同一个数组使历史出牌被篡改。

## 7. 一次出牌的效果顺序：PRE → POST → DISCARD

`EffectCatalog.Phase(effectId)` 告诉引擎一个效果所属阶段。`None` 是无效果，`CopyRank/ForceRandomDiscard/LowerPreviousPlay` 是 PRE，`ExileJoker/DrawTwo` 是 POST，其余非空效果是 DISCARD。

### 7.1 PRE：提交出牌时

PRE 随 `PlayCommand.PreSource` 选定。`Preview` 先验证该效果是否可用；`Commit` 在通过验证后执行。三种效果：

| 效果 | 核心实现 |
| --- | --- |
| `CopyRank` 复制点数 | 改临时逻辑点数，不修改实体印刷点数；当前牌型和快照按逻辑点数算 |
| `ForceRandomDiscard` 随机弃敌牌 | 用引擎的随机数从对手手牌选一个 ID，手牌移到弃牌堆；该被迫弃掉的牌**不触发它自己的牌效** |
| `LowerPreviousPlay` 降低上手 | `HandEvaluator.Lower(previous)` 将上一手快照中每个逻辑点数减 1，维持同牌型再比较；上一手含 3 或王、没有上一手或降低后牌型不成立则不能用 |

“降低上手”不会永久修改 `State.LastPlay`，只是这次 `Preview` 用降后的 `previous` 做比较。出牌成功后 `LastPlay` 被本次出牌快照取代。

### 7.2 POST：牌仍在手牌，但已被本次出牌预定

`Commit` 把本次 ID 加到 `pending`，设置 `Phase=Post`。此时 `Candidates(actor)` 只看 `pending` 中仍在行动者手牌里的实体，并筛出 POST 效果。`ResolveEffect` 可选择一个合法来源；`Source=-1` 表示跳过本阶段。允许的 POST：

- `ExileJoker`：将这张带该效果的王从手牌移到 `exile`；它不会随后进弃牌堆。小王或大王中只有被分到这个效果的那张能发动。
- `DrawTwo`：调用 `Draw(actor,2,...)`，最多两张；牌库空则按底层抽牌逻辑回收弃牌。

结束 POST 时，`AdvanceEffectStage` 遍历 `pending`，把仍在手牌里的本次牌移到弃牌堆，并将这些 ID 记入 `enteredDiscard`，切到 `Discard`。已被除外的王不再被移过去。**这就是“已出牌但暂留手牌”设计要解决的问题：POST 的王除外必须发生在普通弃牌移动之前，同时维持 54 张实体守恒。**

### 7.3 DISCARD：牌已进入弃牌堆

现在 `Candidates` 只看 `enteredDiscard` 中还在弃牌堆的实体，并筛出 DISCARD 效果；同样只选一个或跳过。默认限制与实现如下：

| 效果 | 实际区域变动或信息 |
| --- | --- |
| `ReturnThisCard` 本牌回手 | 该来源牌从弃牌堆回到本方手牌；每位玩家整局最多 3 次 |
| `ReshuffleDiscard` 洗回弃牌 | 将**当前整个弃牌堆**加入牌库，清空弃牌堆并洗牌 |
| `PeekOpponentTwo` 看敌两张 | 人类选两个不同的**对手手牌背面位置索引**；显示该两张实体。AI 不可用；牌不移动 |
| `SwapTwoInHand` 换手牌两张 | 选本方两张不同手牌 ID，移入牌库并洗牌，再从牌库抽 2 张；抽回原牌也有可能 |
| `ReorderTopFive` 牌顶排序 | 预览牌库前最多 5 张，必须各分配一次到“顶”或“底”。顶组依点击顺序放在牌库前，底组依点击顺序追加牌库末尾；此效果不额外回收弃牌 |

`ResolveEffect` 先验证来源是否属于**当前阶段、本次候选**，再验证目标：例如换牌必须是两张不同的本方手牌；偷看目标是背面位置，不是让界面提前知道隐藏牌的 ID；排序必须完整覆盖预览牌集合且不能重复。验证通过才 `Use` 记效果使用次数、执行区域移动。完成后 `AdvanceEffectStage`：若刚完成 DISCARD，先 `CheckEnd`，再检查低手牌事件，最后开始另一方行动。

**选择规则与当前教学界面**：每阶段至多发动一个效果。若候选为零，`TeachingGameScreen.Update` 自动跳过；若恰有一个且无需选目标，它自动发动；候选多个或需要选择目标时才弹窗。核心规则本身允许 `Source=-1` 主动跳过，但当前 UI 对“唯一且无需目标”的效果默认自动发动。想对照效果列表，可搜索 `AutomaticEffect`、`DrawEffectChoices`、`SelectEffect`。

### 效果顺序实例

假设本次打出两张实体牌：A 带“抽两张”（POST），B 带“本牌回手”（DISCARD）。`Commit` 先把 A/B 记入 `pending`；POST 选 A，调用 `Draw(2)`，抽到的两张直接加入本方手牌；然后 `AdvanceEffectStage` 把 A/B 从手牌移到弃牌堆，记录到 `enteredDiscard`；DISCARD 选 B，B 从弃牌堆回手；最后切到对手 Draw。A 最终在弃牌堆，B 在本方手牌，新抽的两张也在本方手牌。该例是规则路径示意，具体抽到哪张由 seed 和此前操作决定。

## 8. Pass、补偿、低手牌事件与胜负

`Pass(actor)` 只在 `Select`、存在上一手且上一手属于对手时成立；**没有上一手的先手不能 Pass**。`ResolvePass` 扣默认 1 HP，清空 `LastPlay`，`Trick++`，清掉旧补偿队列，再按顺序放进三个 `DrawGrant`：

1. Pass 的玩家下一次行动免费抽 1；
2. 上一手玩家之后行动免费抽 3；
3. Pass 的玩家再之后行动免费抽 3。

这里的 1、3、3 来自 `CompensationFirst/Second/Third`，是**覆盖各次行动的普通免费抽牌数**，不是同一刻立即抽 7 张。`BeginTurn` 每次取队首，并核对归属。Pass 后若还没结束，先检查低手牌事件，再让 Pass 的玩家开始新墩行动。若 HP 从 0 扣成 -1，`CheckEnd` 当场设置 `Winner`、`Phase=GameOver`，不会再开下一回合。

`ResolveLowHands`：只要任一玩家手牌数 `<= LowThreshold`（默认 2）且未被锁定，就触发一次：合并弃牌、洗牌，然后循环最多 4 轮，每轮先给玩家抽 1、再给 AI 抽 1；可能因无可抽牌而不足。`lowLocked` 防止手牌仍低时在连续阶段反复触发；双方重新高于阈值时解除锁定。这个事件可以让牌从弃牌堆重新流入牌库，是与“牌库为空时自动回收”不同的触发条件。

每方累计打出的**实体张数**每满 10 张，`Commit` 计算跨过多少个十张门槛，最多回复到初始 HP 上限 3。`State.played[actor]` 是累计数，不会因回手或洗牌而减少；再次打出同一实体也会再次计入“出牌张数”。`CheckEnd` 的条件是**任一方 HP < 0**，不是“手牌抽空”或“牌库抽空”。胜者为未跌破 0 的一方；当前实现不存在两方在一个检查点同时跌破 0 的正常路径。

## 9. AI：怎样行动、它能看到什么

`TeachingGameScreen.Update()` 每隔约 0.5 秒在 AI 回合调用 `game.StepAi(1)`。`StepAi` 根据阶段执行：

- Draw：满足配置的 HP、手牌和购买条件时 `BuyExtra`，否则 `FinishDraw`；
- Select：`AiController.LegalPlays` 生成候选并排序，找不到合法牌则 `Pass`，否则 `Commit`；
- Post/Discard：`AiController.ChooseEffect` 选效果并交给 `ResolveEffect`。

`GameEngine.Observe(actor)` 创建 `GameObservation`：包括自己的牌、对手手牌**数量**、公开弃牌/除外、牌库**数量**、上一手和当前可发动效果。普通情况下不提供对手实体 ID 或牌库顺序；仅牌顶排序效果可用时提供 `VisibleTop`。`AiController` 接收的是这个观察对象，而不是完整 `GameState`。搜索 `LegalPlays` 会看到“按点数分组生成候选 → 加上 PRE 变体 → 按牌型/点数/炸弹成本排序 → 截取候选上限”的过程。`HandEvaluator.Generate` 明确避免遍历手牌全部 `2^n` 子集；`AiCandidateLimit=384` 控制计算规模。AI 是启发式，不保证每步最优。
## 10. Unity 交互链：场景、组件、Prefab、uGUI

### 10.1 Scene 中有什么，Project 中有什么

`Assets/Scenes/Game.unity` 保存 `GameRoot`、`Canvas/HandRoot`、`EventSystem`、相机与背景。`GameRoot` 的 **Inspector → GameBootstrap** 有 seed（当前场景值 20261022）。`HandRoot` 的 **Inspector → HandView** 已连接 `GameBootstrap`、`CardVisual` Prefab、大小王 Sprite。若画面缺卡，先点击 **Hierarchy → Canvas → HandRoot** 检查这些字段是否为 None。

`Assets/CardDuel/Prefabs/CardVisual.prefab` 是一张卡牌的预制体（Prefab），包含卡面 `Image`、效果徽章、文字、描边、`Button`、`CardVisual` 脚本。它是 Project 资产，不是开局就存在的 54 个 UI 对象。`HandView.Render` 只为玩家当前持有的实体牌创建或复用视图：

```csharp
views.Add(Instantiate(cardPrefab, content));
view.CardId = id;
view.Bind(SpriteFor(card), badge, Color.white, "", selected,
          selectable, click, show, hide);
```

`Instantiate(prefab, parent)` 克隆 Prefab 并将实例设为 `content` 的子对象。`Bind` 填图片、徽章、选中状态和回调；**它不生成规则牌，也不改变手牌列表**。牌变少时多余视图 `SetActive(false)`，下次可复用。`ScrollRect`、`RectMask2D`、`HorizontalLayoutGroup` 让牌横排且可滚动。

素材位于 `Assets/CardDuel/Resources/Art/Cards`、`Art/UI`；来源与授权见 [ASSET-SOURCES.md](../Assets/CardDuel/Licenses/ASSET-SOURCES.md)。`HandView.SpriteFor` 按名字调用 `Resources.Load<Sprite>("Art/Cards/…")`；路径从 `Resources` 内部算起，不写扩展名。加载失败返回 null。大小王优先用 Inspector 中单独连接的 Sprite；普通牌 Sprite 用字典缓存。Sprite 数与规则实体牌数是两回事。

### 10.2 生命周期函数在这里做什么

- `GameBootstrap.Awake()`：`Engine = new GameEngine(seed)`。GameEngine 是普通 C# 对象，不挂在 GameObject。
- `GameBootstrap.Start()`：`gameObject.AddComponent<TeachingGameScreen>()`，运行时为 GameRoot 加界面组件。
- `HandView.Awake()`：设置 HandRoot 内部滚动容器和布局。
- `TeachingGameScreen.Start()`：找到 Bootstrap、HandView、Canvas；创建 HUD/按钮/弹窗；订阅引擎事件并首次刷新。
- `TeachingGameScreen.Update()`：到时间推进 AI；没有可选项时自动推进 Draw/效果阶段。它不会每帧重新发牌。
- `OnDestroy()`：取消事件订阅，避免已销毁 UI 继续收到旧引擎通知。

这些 `Awake/Start/Update/OnDestroy` 是 Unity 调用的 `MonoBehaviour` 生命周期函数。`GameEngine` 无 Unity 生命周期，可以在普通 .NET 环境直接验证。`Restart(newSeed)` 新建引擎后，界面重新订阅它并回到猜拳。

### 10.3 一个按钮如何走到规则代码

动态按钮由 `TeachingGameScreen.UiButton` 创建：新 GameObject 加 `RectTransform`、`Image`、`Button` 和文字，再通过 `button.onClick.AddListener(() => click())` 接入回调。购买按钮传入 `() => Act(() => game.ChooseDraw(0,true))`。`() =>` 是 lambda，表示**点击时才运行**，创建按钮时不购买。

```text
EventSystem + InputSystemUIInputModule 接收点击
→ Button.onClick 执行回调
→ TeachingGameScreen.Act
→ GameEngine.ChooseDraw(0,true)
→ ApplyPurchase → ApplyFreeDraw/Draw → 扣 HP → Draw(额外 4)
→ CompleteDraw → Phase=Select
→ Done → CheckInvariant → Changed?.Invoke()
→ TeachingGameScreen.Refresh → HandView.Render → 更新 HUD/卡图/按钮
```

`Act` 接收 `Func<bool>` 命令，负责成功/失败反馈和统一刷新；它不直接扣血或移动牌。`Done` 的 `Changed` 是 C# 事件（event），`Action` 是无参数无返回值的委托；`?.Invoke()` 是有订阅者才调用。执行命令期间，`executing` 避免事件中间重复刷新；命令返回后统一 `Refresh`。`OnDestroy` 退订事件，防止旧引用残留。

### 10.4 Unity 各窗口看什么

| 窗口 | 本项目中的用途 |
| --- | --- |
| **Scene** | 编辑背景、相机、Canvas、HandRoot 的位置与层级；不是最终玩家画面 |
| **Game** | 玩家的实际显示与点击区域；在这里检查按钮、弹窗、卡牌是否遮挡 |
| **Hierarchy** | 查看场景对象；Play 时展开 `Canvas/Teaching UI` 可见运行时创建的 HUD/弹窗，停止 Play 后消失是正常的 |
| **Project** | 查看 `.unity` 场景、`.prefab` 模板、`.cs` 脚本和 Sprite 资产 |
| **Inspector** | 查看所选对象组件与字段引用；尤其核对 HandView 与 CardVisual 的连接 |
| **Console** | 查看编译错误、运行异常、警告；先处理第一条红色错误 |

`CanvasScaler` 运行时用 1600×900 参考分辨率、宽高折中系数 0.5；实际 Game 视图比例不同，布局可能与 Scene 中的尺子视觉不同。`CardVisual` 实现 `IPointerClickHandler` 等接口以接收细分的按下、抬起、移出事件；长按约 0.45 秒显示详情，移动超过 12 像素视为滚动而不点击。

## 11. 五条实战调用链

### A. 开局

`GameBootstrap.Awake → GameEngine 构造 → CreateDeck → Draw(0,8) → Draw(1,8) → Phase=Rps`。正常初值：双方 HP 3、手牌各 8、牌库 38、弃牌 0、除外 0、Turn 0、Trick 1。若不符，先看 `GameBalanceConfig/CreateDeck/Draw/CheckInvariant`。

### B. 普通抽牌

在非首次行动且无 Pass 补偿时，`BeginTurn` 设置免费 1。点“不购买” → `ChooseDraw(0,false)` → `CompleteDraw` → `ApplyFreeDraw` → `Draw(0,1)` → `Phase=Select`。若牌库原本空了，可能先洗回弃牌；此时不要只凭牌库数量变化推断实际抽牌数。

### C. 买牌

点“购买” → `ChooseDraw(0,true)`。默认花 1 HP，免费 1，再额外最多 4；牌足量时手牌 +5、牌库 -5。`CanBuy` 同时看阶段、首次行动、是否买过、HP 是否足够。若按钮不出现，先查这些状态，别绕过规则直接改按钮。

### D. 打出白板单张

点卡牌 → `HandView.selectedIds` 收实体 ID → 点“出牌” → `PlayOptions` 产生单张命令 → `Commit` 写 `LastPlay/pending` 并切 `Post` → 无 POST 效果则自动跳过 → 牌从手牌进弃牌，切 `Discard` → 无 DISCARD 效果则自动跳过 → 下一方 `BeginTurn`。白板也走完整状态机。

### E. 要不起

面对对手上一手，点“要不起” → `Pass` → `ResolvePass` 扣 HP → `LastPlay=null`、`Trick++`、补偿队列 1/3/3 → 若 HP<0，立即 GameOver；否则检查低手牌并由刚 Pass 的玩家开始新墩行动。新墩先手不能再 Pass。

## 12. 常用 C#、Unity API：本项目中的具体作用

| 写法/API | 当前代码的作用 | 再次使用的场景 |
| --- | --- | --- |
| `List<T>`、`Add/Remove/RemoveAt` | `GameState` 存有顺序的牌 ID | 动态有序集合；`RemoveAt(i)` 按索引删 |
| `Dictionary<TKey,TValue>` | `HandView` 缓存 Sprite；牌型/AI 按键查对象 | 通过键快速定位 |
| `HashSet<T>` | `selectedIds` 避免选中重复 ID | 唯一成员集合 |
| `Queue<T>`、`Enqueue/Dequeue` | Pass 后的 1/3/3 抽牌补偿 | 先进先出事件 |
| `IReadOnlyList<T>`、`AsReadOnly` | 对外只给状态读取入口 | 调用方无需修改；注意视图仍随底层变化 |
| `ToArray`、`Array.AsReadOnly` | `PlaySnapshot` 复制并封存历史 | 保存不可被调用方改写的快照 |
| `Select/Where/OrderBy` | 映射 ID、筛效果、排手牌 | 集合投影/过滤/排序，需 `using System.Linq;` |
| `GroupBy/All/Any/Distinct` | 识别牌型、验证目标 | 分类、全称/存在判断、去重 |
| `FirstOrDefault/Take/SequenceEqual` | 取首个候选、截取、核对排序结果 | 查找/限制/顺序比较 |
| `() => expression` | UI 点击回调、LINQ 谓词 | 把“以后执行的函数”传出去 |
| `event Action`、`?.Invoke()` | 引擎通知 UI 更新 | 事件通知；销毁时退订 |
| `Func<bool>` | `Act` 接受返回成功或失败的命令 | 把有返回值的函数作为参数 |
| `[SerializeField]` | 私有 Prefab/Sprite 字段在 Inspector 接线 | 私有字段仍可序列化 |
| `GetComponent<T>()`、`AddComponent<T>()` | 获取/添加同一 GameObject 的组件 | 组件化场景逻辑 |
| `FindAnyObjectByType<T>()` | 启动时找到唯一 HandView/Canvas | 少量场景查找；找到后缓存 |
| `Instantiate(prefab,parent)` | 克隆手牌 UI 模板 | 运行时创建视图实例 |
| `Resources.Load<T>(path)` | 根据资源路径加载牌面/按钮 | 动态加载 Resources；路径错返回 null |
| `Button.onClick.AddListener` | 动态按钮连接回调 | uGUI 交互 |
| `IPointerClickHandler` 等 | `CardVisual` 区分点击、长按、移出 | 需要比普通 Button 更细的输入 |
| `Time.unscaledTime` | 长按时间、AI 行动间隔 | 不受 `Time.timeScale` 影响的计时 |
| `throw new ...` | 配置非法、54 张实体守恒失败 | 程序内部不变量被破坏；普通用户操作非法用 `Reject` 返回 false |

Core 与 Unity 界面分开的实际收益是：原 Demo 用 OnGUI，我们选 uGUI 和公开素材，但规则 Core 无需改写。牌型、回合和效果可用普通 C# 重播和验证；目前不需要再为模式而加空接口层。

## 13. 验证与排错

### 在 Unity 里实际检查

1. 打开 `Game.unity`，Console 无编译错误后点击 Play。初值应是 HP 3/3、手牌 8/8、牌库 38。
2. 猜拳；平局会重猜。首次先手免费抽 0 且不能买。之后分别试“不购买”和“购买”，对照 HP、手牌、牌库及日志。
3. 选白板出牌，观察上一手、弃牌和换人；面对对手上一手时试“要不起”，看 HP、Trick、补偿抽牌。
4. 遇到效果牌时，观察 PRE 出牌方式和 POST/DISCARD 效果选择。目标有时是实体 ID（换牌），有时是背面位置索引（偷看）。
5. 运行到一方 HP<0，确认胜者与“再来一局”。固定开局要在 `GameRoot → GameBootstrap → Seed` 用同一整数，且操作序列也一致。

### 已完成的验证与边界

`RuleChecks.Run()` 有 42 组规则检查，覆盖牌型、效果、区域守恒和固定 seed 重播。教学项目中的 Core 已通过独立 .NET 编译并运行该检查：**42 groups passed**；另有 5 个不同 seed 的完整自动对局结束并通过 `CheckInvariant()`。我还在 Unity 编辑器实际操作了猜拳、普通抽牌、花 HP 买牌、出牌、带目标的效果及 AI 推进，未见游戏逻辑错误。`RulesTests.cs` 是 Unity EditMode Test Runner 的入口，但这次尚未单独在 Test Runner 中执行，不能把 .NET 检查说成 Unity Test Runner 已通过。

### 出问题时追哪条链

- **无法 Play**：先看 Console 第一条红色编译错误的文件和行号。检查 `CardDuel.Core.asmdef`、类名、括号。
- **开局画面为空**：确认打开 `Game.unity`；`GameRoot` 有 Bootstrap，`Canvas/HandRoot` 有 HandView，`EventSystem` 存在。`TeachingGameScreen.Start` 有明确的缺组件报错。
- **卡面空白**：查 HandView 的 Prefab/Joker 引用、`SpriteFor` 拼出的 Resources 路径、Sprite 的导入类型。先追一张具体实体的 `Rank/Suit/Face`。
- **点击无反应**：查是否轮到玩家且 `Phase=Select`；看 `Button.interactable`、`EventSystem`、`InputSystemUIInputModule`、是否有覆盖 UI 挡住射线。
- **出牌失败**：读界面反馈/`game.Error`，追 `ShowPlayOptions → PlayOptions → Preview`；区分牌型不成立、压不过、PRE 来源/目标非法。
- **抽牌数量不符**：查 `FreeDraw/drawSource/AvailableToDraw/freeDrawDone`，再看 `Draw` 是否进入 `Recycle`。牌库数量单独不能证明实际抽牌数。
- **实体牌丢失或重复**：在 `CheckInvariant` 断点看牌库、弃牌、除外、双方手牌的 ID；同一 ID 应恰在一个区域。
- **效果时机不对**：查 `Phase/pending/enteredDiscard`，再追 `Candidates → EffectBlockedReason → ResolveEffect → AdvanceEffectStage`。
- **怀疑 AI 看到暗牌**：看 `Observe` 构建的 `GameObservation`，普通回合只有对手张数，不应提供对手实体或牌库顺序。

给 `Draw`、`Preview`、`Commit`、`AdvanceEffectStage`、`ResolvePass` 下断点，Unity Play 时附加 C# 调试器，逐步记录“调用前状态 → 函数 → 调用后状态”。一次只追一条命令，比通读全部代码更容易定位问题。

## 14. 当前范围与后续扩展

此次完成的是**核心完整对局规则和可操作的教学版 uGUI**。核心文件与原 Demo 一致；新界面不是原 OnGUI 画面的逐像素复制。教学版有猜拳、买牌、选牌、提示、出牌、Pass、效果目标、日志、胜负、重开，以及中日英界面、设置、音效和循环 BGM。部分演出仍与原版不同。当前上一手 UI 最多展示前 8 张，完整 ID/张数仍在 `LastPlay` 与日志中。将来若需要在 Inspector 直接摆每个 HUD 元素，可把 `BuildUi` 创建的对象逐步改成 Prefab，不需要更改引擎命令。

## 15. 用自己的话解释

看完后不翻手册，试答：

1. 为什么牌库是 `List<int>`，而不是 54 个 `CardVisual`？抽出 `draw[0]` 后实体 ID 如何移动？
2. 为什么先 `Preview` 后 `Commit`？复制点数为何写进出牌快照而不改 `CardInstance.Rank`？
3. `Turn` 与 `Trick` 分别在哪个函数增加？Pass 后 1/3/3 何时真正抽？
4. 为什么 POST 时已出牌的实体还在手牌？`pending` 会不会让总牌数变成 55？
5. 点击“购买”怎样走到 `GameEngine.Draw`？成功后谁通知 UI 更新？
6. AI 普通回合能看到对手哪几张牌？`GameObservation` 的边界有什么用？

能沿代码讲清这六条，才算掌握了主干。后续可以一次挑一个具体场景，例如“牌库耗尽时买牌”，先自己画状态变化，再与代码和运行结果核对。

## 16. 多语言、设置、音频：它们怎样接到同一局游戏

这三个功能主要在 `TeachingSettings.cs`，它与 `TeachingGameScreen.cs` 声明同一个 `partial class TeachingGameScreen`。`partial` 只把一个类拆成两个源文件；运行时仍是 **GameRoot 上的一个 MonoBehaviour 组件**，两份文件可以共用 `game`、`dialog`、`modalContent` 等字段。这里拆文件是为了让对局界面和设置代码容易分别阅读，没有新增一个控制游戏规则的对象。数据类 `UserSettings`、字典 `Localization`、规则正文 `Rulebook` 在 `Presentation` 程序集中，依赖 `CardDuel.Core`；教学版界面也引用这个程序集。

### 16.1 从 Unity 界面进入设置

1. **Project → Assets/CardDuel/LearningView** 找到 `TeachingGameScreen.cs` 和 `TeachingSettings.cs`。`TeachingSettings.cs` 是部分类脚本，**不要单独拖到 GameObject 上**；只要 `GameRoot` 有 `GameBootstrap`，后者在运行时添加 `TeachingGameScreen`，两份文件的逻辑都会生效。
2. **Game** 页签右上角黄色齿轮是 `BuildUi()` 创建的 `Button`。点击调用 `ToggleSettings()`；按 Esc 也是同一个入口。设置浮层优先显示，并记住原来的 `dialog`，返回时恢复猜拳或正在进行的效果弹窗。
3. **常规**页有语言、窗口分辨率、音乐音量、音效音量；**对局**页有 AI 强度、行动速度、新开一局、规则手册、对战日志。玩家界面不显示或编辑随机种子。桌面构建中有退出按钮，Editor Play 中隐藏退出按钮。
4. 在 **Hierarchy → GameRoot** 选择运行中的对象，Inspector 可看到动态增加的 `TeachingGameScreen` 与两个 `AudioSource`。在 **Hierarchy → Canvas → Teaching UI → Modal Shade → Modal Panel → Modal Content** 可看到当前页动态生成的 `Slider`、`InputField`、按钮和标签；切页时销毁旧页对象、建立新页对象。停止 Play 后这些对象消失，不代表设置丢失。

按钮的链路以切换日语为例：

```text
按钮 onClick → ChangeLanguage(1)
→ settings.Language = 1 → SaveSettings()
→ Refresh() → HUD、手牌徽章、当前弹窗用日语重绘
```

新开一局的链路：按钮调用 `Restart()` → 取得新种子并传入 `RestartWithSeed` → `GameBootstrap.Restart(seed)` 新建引擎 → `Attach` 退订旧引擎、订阅新引擎 → `Refresh`。种子是重播和调试所需的内部数据；要复现问题，可在 Unity 编辑器的 `GameRoot → GameBootstrap → Seed` 指定整数，再 Play，并重复相同操作序列。这是开发入口，游戏画面不展示该值。

### 16.2 设置数据保存在哪里

`UserSettings` 是 `[Serializable]` 普通 C# 类，不是场景组件。它保存：`Language`（0 中文、1 日语、2 英语）、`Difficulty`（0 简单、1 标准）、`Resolution`（分辨率预设下标）、`Music` 和 `Sound`（各 0～1）、`Fast`（AI 快速）。`Sanitize()` 把越界数值夹回范围，遇到 NaN/无穷音量时使用默认值。默认音乐是 0.35，音效是 0.65。

```csharp
settings.Music = next;                  // Slider 的 0～1 数值
musicSource.volume = settings.Music;    // 立即影响 BGM
PlayerPrefs.SetString(SettingsKey, JsonUtility.ToJson(settings));
```

这是 `VolumeSlider` 的监听器经 `SaveSettings()` 走到的关键三步。`JsonUtility.ToJson` 把字段写成 JSON 字符串，`PlayerPrefs.SetString` 更新该键的值，`PlayerPrefs.Save()` 才明确要求写入持久存储。滑块连续拖动会频繁触发监听器，所以 `SaveSettings` 只标记 `settingsDirty` 并把保存时间定在 `Time.unscaledTime + 0.5`；`Update → TickSettingsSave` 到时刷新磁盘。`OnApplicationPause(true)` 和 `OnApplicationQuit()` 立即 `FlushSettings()`，避免刚拖动就退出时丢失最后一次更改。`Time.unscaledTime` 不受游戏暂停时间缩放影响。

启动时 `InitializeSettingsAudio()` 通过 `PlayerPrefs.GetString` 读取 JSON，再 `JsonUtility.FromJson<UserSettings>` 和 `Sanitize()`。教学项目使用键 `CardDuel.Teaching.Settings.v1`，与原 Demo 的键不同。分辨率按钮只显示当前显示器可容纳的预设；在 Unity Editor 中不调用 `Screen.SetResolution` 改编辑器窗口，桌面构建才用窗口模式设置尺寸。`AI强度` 会传进 `game.StepAi(1, difficulty)`；`AI速度` 只调整界面调用 AI 的等待时间（正常 0.5 秒、快速 0.12 秒），不改变规则引擎的随机数或牌序。

**常用 API**：`PlayerPrefs.GetString/SetString/Save` 适合少量本机偏好；`JsonUtility.ToJson/FromJson` 负责 Unity 可序列化字段；`Mathf.Clamp` 和 `Sanitize` 防止非法 UI 值；`Screen.SetResolution` 只影响运行窗口。若将来需要复杂存档、多个存档槽或跨设备同步，应再评估文件/云端存储，而不是把整局牌状态塞进这个设置键。

### 16.3 为什么切语言不改 GameEngine

`GameEngine` 的 `BattleLogEntry.Text` 以中文原文记录规则事件；UI 显示时再调用 `L(...)`。`L` 先查 `Localization.Text` 的固定字符串与带 `{0}` 的格式模式，再查教学版新增按钮的 `UiWords`。例如原始事件“牌库 → 手牌 2 张（卡牌效果）”可根据模式转成英语；数字 2 会保留，“卡牌效果”也递归翻译。动态 HUD 用 `F(中文格式, 日语格式, 英语格式, values)` 明确选择一种句式，然后 `string.Format` 插入 HP、回合等数值。牌面花色和点数图片无需切语言；牌效徽章由 `HandView.BadgeText` 指向 `Badge(EffectId)`，重绘手牌时选择对应语言。

这条边界有两个实际作用：固定 seed 的规则日志与重播不会因界面语言改变；语言切换只改变显示，不会重新洗牌、换牌或消耗一次随机数。`Rulebook.Text(CurrentLanguage)` 提供三语规则正文；牌效页逐个读取 `EffectCatalog.Name/Description` 并翻译。`LogLine` 将 `BattleLogEntry` 的行动者、回合和文本组合成可读行。新增规则提示时，要同时检查它是否能被 `Localization` 或 `UiWords` 翻译；找不到的字符串会保留原文，这是明确的回退行为，不等于已完成该句的翻译。

**常用语法/API**：`enum Language` 比任意字符串更容易限制合法语言；`Dictionary<string,string[]>` 通过中文键找日英文案；`string.Format` 处理带变量的文案；`Regex` 在现有 `Localization` 中匹配带占位符的原文。一般新界面文案优先用固定键和格式参数，只有需要兼容引擎现存动态文本时才使用正则匹配。

### 16.4 音频资源从哪里加载，什么时候播放

素材位于 **Project → Assets/CardDuel/Resources/Audio**：`bgm.ogg` 和 `click/draw/play/shuffle/return/effect/damage/heal/exile.ogg`。`Resources.Load<AudioClip>("Audio/bgm")` 的路径从 `Resources` 的内部开始，省略扩展名；名称或目录错误会返回 null。初始化时如缺音频会向 Console 报错。BGM 来自原项目保留的用户提供素材；音效是 Kenney CC0，许可说明仍在教学项目素材目录。

`InitializeSettingsAudio()` 在 **GameRoot** 上创建两个 `AudioSource`：`musicSource` 挂 `bgm`、`loop=true`、调用 `Play()`；`soundSource` 用 `PlayOneShot(clip)` 叠加短音效。两者都设 `spatialBlend=0`，即 2D 音频，不随摄像机距离衰减；音量分别来自 `settings.Music` 与 `settings.Sound`。场景已有 `Main Camera` 的 `AudioListener` 时不再额外添加，避免多个监听器警告。

```text
玩家点按钮 → UiButton 的 onClick → PlaySound("click")
规则命令成功 → Act 比较执行前后阶段、日志和 HP → PlayActionAudio(...)
→ 按抽牌 / 出牌 / 洗牌 / 牌效 / 受伤 / 回复选一个或多个短音效
```

声音属于界面反馈：`Act` **先执行规则命令**，成功后才根据新 `BattleLogEntry` 和 HP 差异选音效；失败命令不会假装抽牌成功。`PlayActionAudio` 只读引擎状态，不调用规则随机数。卡牌点击通过 `HandView.CardClicked` 播放 click。音效音量为 0 时 `PlaySound` 直接返回；BGM 仍可单独播放或静音。一个动作可能同时有“出牌效果”和“受伤”两类声音；`PlayOneShot` 允许短音效重叠。结束状态的提示音沿用 `heal` 素材，这是当前的映射，不代表游戏结束一定是回血。

**常用 API**：`AudioSource.Play` 适合循环音乐；`PlayOneShot` 适合一次性短音效；`AudioSource.volume` 单独控制声道；`AudioListener` 是听者；`Resources.Load<AudioClip>` 加载音频资产。需要更多层级混音时可考虑 `AudioMixer`，当前只有音乐和音效两路，两个 `AudioSource` 已满足需求。

### 16.5 在 Unity 中验收和排错

1. 打开 `Game.unity`，点 Play、齿轮。常规页切换中文、日语、英语，确认 HUD、按钮、规则手册、牌效说明也切换，而手牌 ID、HP、牌库数量不变。
2. 把音乐和音效分别拉到 0%，检查两个百分比；立即停止 Play 再运行，检查 0% 仍被读取。最后调回希望的音量。桌面构建还应关闭进程并新开进程验证持久化；Editor 的两次 Play 只证明当前编辑器会话内读回。
3. 对局页切 AI 简单/标准、速度正常/快速；猜拳后观察 AI 行动间隔。点“新开一局”应回到新局猜拳，界面上没有种子输入框。开发调试固定局面时，停止 Play 后在 GameRoot 的 GameBootstrap Inspector 指定 seed，再启动。
4. 在 Play 模式的 Hierarchy 点 `GameRoot`，Inspector 检查两个 `AudioSource`：第一路 Clip 为 bgm、Loop 勾选、音量跟音乐滑块一致；第二路用 `PlayOneShot`，无需预先指定 Clip。现场听一遍 BGM、按钮、抽牌和受伤音效；只有截图或 Inspector 不能证明扬声器实际出声。
5. 看 Console。若缺素材，先查 `Resources/Audio` 下文件名与导入状态；若按钮可按却没音，查音效音量、`AudioListener`、设备静音。若语言没变，追 `ChangeLanguage → SaveSettings → Refresh → L/F`；若重开后丢设置，追 `SettingsKey → PlayerPrefs.SetString → FlushSettings → GetString`。黄色的 Unity Account API 网络警告与本游戏的设置或音频逻辑无关。

本次已在 Unity Editor 实际检查了三语设置界面与 HUD、规则手册、静音滑块显示和立即停止 Play 后的设置读回；脚本编译通过。尚未用桌面构建的全新进程验证分辨率和设置持久化，也没有自动化方式确认真实扬声器输出。上述桌面与听觉检查应作为发布前验收，而不把当前 Editor 证据说成已覆盖它们。
