# 清溪治理手记

一款俯视角环保调查游戏。走访现场、收集证据，在有限的调查权限内识别主要污染源。

## 运行

- 使用 **Unity 6000.3.18f1** 打开项目，等待 Package Manager 导入完成。
- 打开 `Assets/Scenes/SampleScene.unity`，点击 Play。
- 界面与地图由脚本在运行时生成，无须手动挂载组件。构建场景已加入 Build Settings。
- 建议使用横屏 16:9 窗口；界面按参考分辨率 1920 × 1080 缩放。

## 操作与规则

- **WASD / 方向键**：移动；靠近场所后按 **E** 调查，再按 E 离开。
- **N**：展开或收起调查笔记，推理阶段也可以查看。
- **Esc**：优先关闭笔记或场所详情；推理阶段返回调查；其他游戏页面返回菜单。
- 表面观察免费；深入核查每次消耗 1 点，每关共 3 点。同一项调查不能重复消费。
- 行动点用完后仍能免费观察和查看笔记。
- 提交正确结论解锁下一关，共三关：河流浊水、水库藻华、山溪赤浊。

## 存档

每次获得线索后自动保存。返回菜单或重启游戏后，选择标记为“继续调查”的关卡可恢复线索及剩余行动点；角色从地图入口开始。各关独立保存，提交结论会结束该次调查，重新调查会从零开始。通关记录单独保留。

存档使用 PlayerPrefs 的 `Qingxi.*` 键。恢复时重放合法调查记录，重新计算点数；损坏、重复操作、越界区域和超预算存档不会被加载。

## 代码位置

- `QingxiCase.cs`：三关内容、调查规则与证据记录。
- `InvestigationSave.cs`：存档序列化、校验与恢复。
- `QingxiGame.cs`：运行时界面与关卡流程。
- `QingxiGame.Journal.cs`：展开笔记和快捷键。
- `TownExplorer.cs`：地图、移动、互动和摄影机。
- `QingxiPoster.cs`：主菜单地图展示。
- `Assets/Editor/PolishChecks.cs`：预算、存档和实际 Play Mode 流程验证。

## 验证

先关闭用于验证的项目实例，再运行以下命令；也可以复制项目到临时目录验证，以保留当前编辑器状态。

```powershell
& 'C:/Program Files/Unity/Hub/Editor/6000.3.18f1/Editor/Unity.exe' `
  -batchmode -nographics -projectPath '<项目绝对路径>' `
  -executeMethod Qingxi.Editor.PolishChecks.Run `
  -logFile '<项目绝对路径>/Logs/polish-checks.log'
```

验证器自动退出，成功退出码为 0；不要加 `-quit`，因为需要等待 Play Mode 流程。测试前备份、结束时恢复涉及的存档键。建议使用临时副本并修改副本的 Product Name，以隔离测试存档。省略 `-nographics` 可生成截图至副本的 `Logs` 目录。

Unity 6000.3.18f1 在临时副本中出现过 `UnityEditor.Search.SearchDatabase` 索引异常。验证器会单独记录这一编辑器异常，不计入游戏运行错误；其他 Error / Exception / Assert 仍会使验证失败。

## 中文字体

优先加载 `Assets/Resources/Fonts/QingxiUI.ttf`（也支持 Unity 可识别的其他字体扩展名）。没有随包字体时，依次尝试微软雅黑、苹方、Noto Sans CJK SC、文泉驿等系统字体。

目前仓库不包含可分发的中文字体。向其他设备分发前，请添加有相应分发许可的中文字体，并在目标平台检查字形；系统字体回退不保证覆盖所有平台。
