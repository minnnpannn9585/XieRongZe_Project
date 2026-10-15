# 清溪治理手记

俯视角环保调查游戏：走访现场、收集证据，在 3 点调查权限内判断污染源。

## 打开与运行

使用 **Unity 6000.3.18f1** 打开项目，从 `Assets/Scenes/MainMenu.unity` 开始 Play。Build Settings 已按菜单、河镇、水库、山溪排序。

- `MainMenu.unity`：插画封面、三关选择、解锁与继续调查。
- `RiverTown.unity`：河流、养殖场、鱼塘、回收站、居民社区。
- `Reservoir.unity`：库区、网箱、化肥厂、湖滨餐馆、取水社区。
- `MountainCreek.unity`：山溪、农家乐、采石场、废弃矿硐、灌溉区。

**场景已保存完整物体，打开即可编辑，不需要先运行生成。** 每个关卡也可单独打开并 Play；编辑器直接运行关卡不受菜单解锁限制。

## 在场景中修改

世界根节点下按地貌植被、调查地点、边界、玩家、相机组织。选中调查点可以修改位置、图案、入口、碰撞体及互动范围；选中玩家或镜头可以调整速度和缩放。

- `PlayerMovement`：挂在玩家上，处理输入、物理移动和前后遮挡顺序。
- `PlayerWalkAnimation`：根据真实物理位移驱动四帧 Animator，控制左右朝向、步频和待机。
- `CameraFollow`：挂在相机上，处理跟随、任务总览与地图边界。
- `InvestigationSite`：挂在各调查点上，保存区域索引、入口、插画和标牌。
- `WorldDepthSorting`：按地标位置处理遮挡顺序。
- `WaterArea`：标记插画对应的水域及已保存的实体水面碰撞。
- `WalkableBridge`：保存桥梁或栈道的桥面尺寸、通行范围和两端接近点。
- `TownExplorer`：协调现成玩家、相机和五个调查点，不创建地图物体。
- `QingxiUiAction`：挂在按钮上，在 Inspector 配置动作与关卡/选项索引。
- `UiPress`：按钮悬停和按压反馈。
- `QingxiGame`：绑定场景 UI 引用，管理调查流程及场景切换。
- `CleanupGame`：挂在各关的 `Canvas/Cleanup` 面板上，管理形状拼图、管道的规则和反馈。
- `CleanupPiece`：挂在每个可拖放材料上，处理拖动、吸附、错误落点退回及管道点击旋转。
- `CleanupShape` / `CleanupFlow`：绘制纯色交互设备和水流反馈，不生成图片或游戏物体。

可复用玩家、调查点和树木 Prefab 保存在 `Assets/Prefabs/World`。UI 的 Canvas 与所有面板也已保存在各自场景中。

## 水面与桥梁

三关河流、水库、山溪以及鱼塘、网箱、灌溉渠等地点插画中的水面，均有已保存的非 Trigger 水域碰撞，角色无法直接涉水。河镇和山溪各有一座木桥，水库网箱旁有一段带尽头护栏的调查栈道；临水采样入口对齐桥面。桥面开口和岸线碰撞在编辑阶段烘焙，运行时不生成碰撞或桥梁。

桥梁使用纯色矢量木板、栏杆和立柱，与现有插画配色一致。三种桥梁/栈道的预制体在 `Assets/Prefabs/World`，网格和材质在 `Assets/Art/Bridges`。水域和栏杆使用无摩擦、无弹性的 2D 物理材质，方便沿岸或栏杆滑动。

移动桥梁或水域插画后，执行 `Qingxi / Water / Rebake Collision in Current Scene`，按当前桥位重新烘焙碰撞，再保存场景。该工具保留桥位，同时检查调查入口及桥梁两端的可达性。`Qingxi / Water / Update Water and Bridges` 则恢复三关默认水域与桥梁布局，保留其余物体和界面。

场景验证使用略大于角色半径的 0.34 单位净空检查，从出生点搜索到每个实际入口，并对搜索路径做连续圆形扫掠；还通过真实 Rigidbody2D 模拟检查岸边阻挡、桥梁双向通过和护栏阻挡。水域阻挡允许玩家在木桥或栈道上跨越水面。

`QingxiCase.cs` 保留三关文本与调查规则；`InvestigationSave.cs` 处理存档校验。界面排版代码位于仅编辑器编译的 `QingxiGame.Editor.cs`，玩家构建不会包含它。

## 角色四帧动画

`Assets/Art/Characters/player_walk.png` 是沿用现有调查员造型、用内置 imagegen 绘制的四帧透明图集。使用统一的竖向切片范围和按帽子位置校准的横向轴心，保持角色高度与脚底基准稳定。`walk_manifest.json` 记录完整提示词、图像尺寸、切片和轴心。

`PlayerWalk.controller` 使用原生 Unity Animator 的 `Idle` / `Walk` 状态；`PlayerWalk.anim` 按 10 fps 循环四帧，最后一个关键帧仅用于闭合循环。待机保持第二帧的收腿姿势。动画只修改 SpriteRenderer 的图片，没有位移、缩放或 Root Motion 曲线，已移除原来的正弦上下漂浮。

玩家根物体的 `PlayerWalkAnimation` 根据真实位移和碰撞后的速度设置 `IsWalking`，并按实际速度调节 `MoveSpeed`。停下、被水面/护栏挡住或调查面板锁定移动时回到待机；左右移动镜像角色，上下移动保持最近的左右朝向。

玩家 Prefab 和三关场景已绑定动画。需要重新导入图集或更新预制体时，执行 `Qingxi / Player / Import Four Frame Walk Animation`；全量场景重建也会接入现有 Animator。验证包含四帧真实播放、移动/停下、左右朝向以及视觉位置与比例稳定性。

## 插画素材

`Assets/Art/Illustrations` 包含用内置 imagegen 绘制的 4 张图、24 个 Sprite：15 处调查地标、调查员、4 类植被岩石、3 张地貌图和菜单全景。

材质 `IllustrationUnlit.mat` 使用 `Qingxi/Flat Illustration`，把原图轻微色调变化统一为有限调色板中的纯色块。`Flatness = 1` 为纯色表现，设为 0 可查看原画；`Palette Smoothness` 控制选色前的细噪点平滑。SpriteRenderer 可直接更换图案。

`illustration_manifest.json` 保存完整生成提示词、尺寸与切片坐标。图集分隔并非严格等宽，使用 `QingxiArtImporter` 的已校准切片；导入器不会修改源 PNG。背景是地貌插画，建筑与角色是独立可移动物体。

字体为 Noto Sans CJK SC Regular，已随项目存入 `Assets/Resources/Fonts/QingxiUI.otf`；许可证见同目录 `OFL.txt`，来源为 [Noto CJK](https://github.com/notofonts/noto-cjk)。

## 中英双语

使用 Unity 官方 **Localization 1.5.13**（`com.unity.localization`）。主菜单右上角的语言下拉菜单提供「简体中文」和「English」，选择后立即刷新文字。首次运行默认中文，后续通过 `Qingxi.Language` 保存并恢复选择；语言在场景间保持一致，不改变调查进度或已有存档。

`Assets/Localization` 包含 Localization Settings、`zh-Hans` / `en` Locale 和 `Qingxi` String Table Collection。界面、三关任务、地点名称、全部线索、笔记、结论选项及成功/失败反馈都有英文翻译。固定界面文字通过物体上的 `QingxiLocalizedText` 组件绑定 Unity `LocalizedString`；动态文本从同一套官方 String Tables 读取，由 `QingxiLocaleController` 在初始化与语言切换后刷新。

中文原文作为 String Table 的条目键，关卡定义和存档继续保留固定中文标识。`Translations.json` 保存中英对照，`Qingxi / Localization / Import Bilingual String Tables` 可重新导入（会覆盖同键的表内翻译）。日常也可在 Unity 的 Localization Tables 窗口直接编辑表；如需保留到后续重新导入，请同时更新 JSON。含 `{0}` 等占位符的条目需在两种语言中保留相同占位符。

`Qingxi / Localization / Configure Authored Scenes` 为已有四个场景补齐本地化组件与下拉菜单，不重建地图。全量场景重建也会保留双语功能。Addressables 已配置为随 Player Build 自动构建，导出游戏时会一起打包语言资源。

## 操作与存档

- **WASD / 方向键**移动，靠近场所按 **E** 调查，再按 E 离开。
- **N** 展开/收起调查笔记，推理阶段也可查看。
- **Esc** 依次关闭笔记/场所详情、从推理返回调查，其他游戏页面返回菜单。
- 表面观察免费；深入核查消耗 1 点，同一调查不能重复消费。点数耗尽后仍可免费观察。
- 每次获得线索自动保存，返回菜单后可继续；答对结论后完成治理小游戏，才会通关并解锁下一关。错误结论可重新调查。

各关使用独立的 `PlayerPrefs` 中 `Qingxi.*` 键。恢复时校验并重放合法记录，拒绝损坏、重复、越界和超预算记录。答对结论保留调查记录，直到治理完成一起清除；调查续玩从角色入口开始，治理续玩直接进入对应面板。

## 答题后的治理互动

三关分别为「隔板拦截」「溢流水引流」「修复矿硐挡墙」。在正确结论页点击「开始治理」进入原场景内的近景操作面板：

- 河镇：从五种不同轮廓的碎片中挑选三块，按形状补齐排污沟的缺口；两块是干扰项。
- 水库：拖入四段弯管，点击顺时针旋转，按浅色轮廓接通溢流口和收集池，再启动抽水泵。
- 山溪：同样从五种轮廓中选择三块补齐挡墙，两块是干扰项；已移除支撑杆交互，拼齐即可确认治理成果。

第一、三关没有编号、颜色或碎片到安装位的预设绑定；所有候选碎片同色，依照实际轮廓与落点判断是否匹配。每轮随机打乱五块碎片在材料区的位置，重进关卡保留该排列。第二关继续使用原来的管道编号、旋转与抽水玩法。

不设倒计时或失败惩罚；错误形状、干扰项或错误落点不会消耗材料，可通过「重新摆放」重置本次操作。治理期间锁定角色与调查笔记输入。每次安装或旋转即保存到 `Qingxi.Cleanup.0/1/2`（版本 2 的 JSON），包括缺口中的碎片、材料区排列和管道方向。旧版本 1 存档会保留已安装数量和管道方向，并移除第三关已废弃的支撑要求；Esc /「保存并返回」可退出，菜单显示「继续治理」。重置和切换语言不影响调查结论。

只有三个缺口全部拼好（第二关为四段管道均安装且方向正确）并确认后，才播放简短水流控制/收集动画，记录通关并解锁下一关。反馈说明污染源已控制、后续仍需处理，不表现为水体瞬间恢复。

三关 `GameFlow/Canvas/Cleanup` 下的插画、材料、安装位置、固定管道、按钮和水流均是保存的可编辑场景物体，运行时不创建界面或设备。`CleanupShape` 的纯色矢量图形用于拼图轮廓，`CleanupPuzzle` 保存各关形状与旧存档迁移规则。`Qingxi / Install or Update Treatment Minigames` 更新这三个面板；`Qingxi / Update Shape Matching Puzzles (First and Third)` 只更新第一、三关，保留第二关及已有地图、桥梁、玩家与其他 UI。全量场景重建也会接入治理面板。

最终插画是 `Assets/Art/Cleanup/*_simple.png`，由内置 imagegen 生成。按简化要求去掉建筑、植被与装饰细节，以排口、设备和水流为主体；`FlatCleanupUI.mat` 用固定八色色板消除渐变。`cleanup_manifest.json` 保存简化版与初稿的提示词。所有新增界面与反馈共用原生 Unity Localization 中英表。

## 编辑器工具与验证

日常直接修改并保存场景即可。`Qingxi / Rebuild Authored Scenes` 是**显式重建工具，会覆盖四个场景与生成的 Prefab**；仅在需要按默认布局重新制作时使用。没有自动启动或导入时重建逻辑。

`Qingxi / Import Illustration Sprites` 更新图集切片并保留同名 Sprite ID。`Qingxi / Validate Authored Scenes` 检查持久化引用、唯一镜头/事件系统、按钮动作、Prefab 及五个调查入口的可达性。

完整批处理验证（先关闭目标项目实例，或使用隔离项目副本）：

```powershell
& 'C:/Program Files/Unity/Hub/Editor/6000.3.18f1/Editor/Unity.exe' `
  -batchmode -projectPath '<项目绝对路径>' `
  -executeMethod Qingxi.Editor.PolishChecks.Run `
  -logFile '<项目绝对路径>/Logs/polish-checks.log'
```

不要添加 `-quit`，验证器会等待 Play Mode 完成后退出。测试覆盖全部双语条目和格式占位符、实际下拉菜单选择、语言保存与恢复选择器、跨语言存档、打开笔记时切换语言、调查预算、四场景跳转、笔记输入锁、失败重试和英文三关通关；治理检查还执行真实拖放事件与 UI 射线检测、形状匹配、两块干扰项在全部缺口均被拒绝、错误落点、材料区乱序保存、旧存档迁移、管道旋转、提前确认阻止、重置、退出菜单续玩、中英切换和完成后解锁。同时比较编辑状态与运行状态的物体数量，确保没有生成或销毁场景结构（Unity 下拉菜单展开时的临时选项由 UGUI 自行管理）。带图形设备运行会在 `Logs` 保存中英文菜单、语言下拉列表、三关地图、笔记、治理和结算截图；加入 `-nographics` 可跳过截图。

验证器备份并恢复涉及的存档键。建议使用隔离副本并修改副本 Product Name，避免影响实际游戏存档。

Unity 6000.3.18f1 在隔离副本中可能报告 `UnityEditor.Search.SearchDatabase` 启动索引异常；验证器会单独记录并排除此编辑器异常，其他 Error、Exception、Assert 均视为失败。
