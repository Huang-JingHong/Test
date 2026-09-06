# Attention !!!
you must update this document when you make a change.

template:
```
change no.1
change description:
2026-01-01
```

# Development History

change no.1
change description: 新增《中继站》第一阶段「基础验证」开发计划（docs/development_plans/中继站开发计划_第一阶段_v0.1.md），包含模拟/表现分层架构约定、格子地图与三人角色架构、任务与设施最小闭环、模块接口契约草案及 W1~W3 任务拆解
2026-09-06

change no.2
change description: 修订第一阶段开发计划：移除预算与人力估算等非开发内容（预算口径段落、任务规模列、日历日期、预算类风险项），文档聚焦开发本身；同步更新本历史文件
2026-09-06

change no.3
change description: 为第一阶段开发计划 §8 所有接口/类/方法/属性/事件/枚举添加统一注释头（Date/Name/Description，中文优先），并在 §2.5 与 §8 新增代码注释规范
2026-09-06

change no.4
change description: 补充代码注释位置约束：注释只在实现处（优先）或声明处编写，不在调用处编写
2026-09-06

change no.5
change description: 实施第一阶段「基础验证」全部开发（W1~W3）：①工程骨架（Online_AI_Town.csproj/sln、Autoload 注册 GameRoot、core/Scenes 与 core/Textures 迁入 game/）；②模拟层 core/（EventBus、GameClock「1现实秒=1游戏分钟」、RingMapGenerator 48×48 环形基地五功能区、自写纯 C# A* 寻路〔经确认替代 AStarGrid2D，后者引擎外构造崩溃〕、CharacterSim/NeedSystem、TaskBoard/RepairTask、FacilitySim 状态机、Simulation 组装驱动）；③表现层 game/（MapView 程序化灰盒 TileMapLayer、CharacterAgent 头顶任务标签、FacilityView 状态变色、点击拾取、时间条/设施面板/DebugOverlay）；④resources/ 数据定义（角色×3、设施×2〔制氧机+通信阵列〕、demo_config.tres 默认 1 人可扩 3 人）；⑤tests/ xUnit 22 项测试全部通过（时钟换算、任务板流转、寻路、修复闭环集成、五区连通性）；⑥验证：dotnet build 0 错误、Godot --headless --import 无报错、600 帧冒烟运行无错误；⑦计划文档追加实施修订记录（寻路方案、§8 接口补充、目录调整、.tres 经验）
2026-09-06

change no.6
change description: 实现游戏内地图编辑器与 .tres 地图持久化：①材质整理（texture_temp/ 14 个材质移入 game/Textures/，删除原文件夹）；②核心数据层（IGridMap 补 SetCell/SetZone/ClearZone 写入接口，GridMap 补 ClearZone 实现，Simulation 补 RemoveFacility 拆除设施恢复地板）；③新增持久化数据载体（FacilityPlacement 设施放置条目 Resource、GridMapData 地图数据 Resource 含格子/功能区/设施列表的 FromGridMap/ToGridMap 序列化）；④重写 GameRoot（启动优先加载 res://resources/maps/base_map.tres 含设施放置列表，缺失时生成环形基地并落盘；暴露 FacilityTemplates 与 SaveMap() 供编辑器调用；FindSpawnCells 从 GridMap 查找生活区出生格）；⑤MapView 补 RefreshCell/RefreshAll 支持编辑后即时重绘；InputPicker 补 Enabled 开关编辑模式禁用拾取；⑥新建 MapEditor（地形画笔/功能区画笔/设施放置/拆除四工具，左键涂抹拖拽、右键擦除，进入编辑自动暂停时钟并禁用 InputPicker，设施放置检查占地格全为地板，拆除调用 Simulation.RemoveFacility；F12 键盘切换编辑模式〔键位待定〕）；⑦新建 EditorPanel（进出编辑按钮、工具/地形/功能区/设施选择按当前工具动态显隐、保存地图按钮调用 GameRoot.SaveMap）；⑧Main 集成编辑器（创建 MapEditor+EditorPanel、FacilityPlaced/Removed 事件同步增删 FacilityView）；⑨验证：dotnet build 0 错误、dotnet test 全部通过
2026-09-06

change no.7
change description: 表现层全面接入实际贴图材质：①新增宇宙背景图层（universe_background.png 作为 MapView 最底层 Sprite2D，按地图世界尺寸 1536×1536 缩放铺满，真空格改为全透明以透出背景）；②MapView TileSet 由灰盒纯色改为材质贴图（墙/走廊/门/五功能区地板/设施占位从 game/Textures/ 加载）——修复关键缺陷：原 Image.LoadFromFile 直接读盘后 BlitRect 拼贴，无 Alpha 通道的 RGB 材质（wall/door/各功能区地板等 colortype=2）因格式不匹配静默失败导致大面积透明，改为经 Godot 资源系统 GD.Load + GetImage 加载并统一 Convert(Rgba8) 后拼贴（corridor_floor.png 为 RGBA 故此前唯一可见）；③FacilityDef 新增 IconTexturePath/BrokenTexturePath 贴图路径字段，facility_oxygen/facility_comm 两个 .tres 配置正常/破损贴图（oxygen_generator[_broken]、communication_array[_broken]，1296×1296 RGBA）；④FacilityView 优先绘制设施贴图（损坏与维修中共用破损图、运行用正常图，DrawTextureRect 缩放铺满 2×2 占地，未配置贴图回退状态色块），保留名称标签/占地描边/选中黄框；⑤验证：dotnet build 0 错误、dotnet test 22 项全部通过
2026-09-06

change no.8
change description: 地图编辑器撤销与视角控制：①MapEditor 新增撤销栈（EditorAction 记录单元保存受影响格子先前地形/功能区状态与设施增删；画笔按「一笔画」聚合成单条记录〔按下开始、拖拽累积、释放入栈〕，设施放置/拆除为单发记录；同格多次修改只保留最早状态；容量上限 200 条丢弃最旧；值无变化的涂抹不入栈），Ctrl+Z 与 EditorPanel「撤销 (Ctrl+Z)」按钮触发；②撤销恢复逻辑：逆置设施增删（撤销放置→Simulation.RemoveFacility、撤销拆除→占地恢复 FacilitySlot 并重新加入）并经 FacilityPlaced/Removed 事件重建/移除 FacilityView，格子恢复先前地形与功能区并逐格 RefreshCell；③Simulation 设施事件订阅改为 _facilityHandlers 映射表管理（AddFacility 幂等、RemoveFacility 注销订阅），修复撤销重复加入导致的状态事件重复发布；④MapEditor 编辑模式仅消费鼠标左/右键（滚轮与中键放行给相机），退出编辑自动结算未完成笔画；⑤新建 CameraController（滚轮缩放朝光标世界点锚定、上下限 0.2~4，中键拖拽平移按 Relative/Zoom 换算世界位移反向移动相机，相机中心夹紧在地图世界区域内），Main.SetupCamera 挂接；⑥验证：dotnet build 0 错误、dotnet test 22 项全部通过
2026-09-06

change no.9
change description: 修复撤销功能无效（change no.8 遗留缺陷）：①根因——实施 no.8 时对 MapEditor.cs 同文件并行发起两个编辑（画笔记录重构 + 设施操作重构），「画笔记录重构」编辑的写入被另一编辑基于过期版本的写入覆盖丢失，导致 ApplyToolAtMouse/ApplyEraseAtMouse 回退为直接 SetCell/SetZone 的旧版（不调用 BeginAction/CaptureCell，PaintTerrain/PaintZone 方法缺失）；旧代码自身合法故构建 0 错误未暴露，但画笔涂抹从不写入撤销栈、Ctrl+Z 时栈恒为空〔设施放置/拆除的撤销未受影响〕；②修复——串行重发该编辑：恢复 PaintTerrain/PaintZone（先查值变化、CaptureCell 记录先前状态、再写格并 RefreshCell），ApplyToolAtMouse/ApplyEraseAtMouse 的地形/功能区分支改经二者执行；③顺带修复右键擦除笔画不结算的粒度缺陷——右键为单发操作，执行后立即 FinalizeStroke 使其独立成为一条撤销记录（原实现不结算会与下一次左键笔画合并）；④验证：dotnet build 0 错误、dotnet test 22 项全部通过，并 grep 逐项确认撤销链路（记录/入栈/快捷键/按钮/恢复）全部在磁盘；教训：同一文件的多次修改必须串行执行，构建通过不代表编辑未丢失（无悬空引用时不报错），关键改动需 grep 复核
2026-09-06
