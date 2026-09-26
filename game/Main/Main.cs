using Godot;
using RelayStation.Core.Characters;
using RelayStation.Core.Common;
using RelayStation.Core.Facilities;
using RelayStation.Core.Items;
using RelayStation.Core.Tasks;
using RelayStation.Game.Audio;
using RelayStation.Game.Autoloads;
using RelayStation.Game.Characters;
using RelayStation.Game.Cutscenes;
using RelayStation.Game.Cutscenes.Scripts;
using RelayStation.Game.Facilities;
using RelayStation.Game.Input;
using RelayStation.Game.Items;
using RelayStation.Game.Map;
using RelayStation.Game.Save;
using RelayStation.Game.UI;
using MenuBar = RelayStation.Game.UI.MenuBar;

namespace RelayStation.Game.Main;

/*****
Date: 2026-09-06
Name: Main
Description: 主场景装配类；构建表现层各部件（MapView / FacilityView / CharacterAgent / InputPicker / 相机 / UI），并桥接玩家操作（选中设施、发起修复/中止/继续、中止选中人物、右键点地移动）到模拟层。
*****/
public partial class Main : Node2D
{
	/*****
	Date: 2026-09-06
	Name: _mapView
	Description: 格子地图视图。
	*****/
	private MapView? _mapView;

	/*****
	Date: 2026-09-06
	Name: _facilityPanel
	Description: 设施面板。
	*****/
	private FacilityPanel? _facilityPanel;

	/*****
	Date: 2026-09-06
	Name: _facilityViews
	Description: 设施视图列表（用于选中描边同步）。
	*****/
	private readonly List<FacilityView> _facilityViews = new();

	/*****
	Date: 2026-09-25
	Name: _characterPanel
	Description: 人物状态面板（与设施面板同区域互斥显示）。
	*****/
	private CharacterPanel? _characterPanel;

	/*****
	Date: 2026-09-25
	Name: _characterAgents
	Description: 角色表现层列表（选中圈同步）。
	*****/
	private readonly List<CharacterAgent> _characterAgents = new();

	/*****
	Date: 2026-09-26
	Name: _itemViews
	Description: 地面物品堆视图（以 ItemStack 实例为键，便于在「扣空回收」时精确移除对应视图）。
	*****/
	private readonly Dictionary<ItemStack, ItemStackView> _itemViews = new();

	/*****
	Date: 2026-09-26
	Name: RightColumnWidth
	Description: 右上角右侧栏（资源监测 + 选中材料）的统一宽度；两栏等宽更整齐，且贴合右边缘。
	*****/
	private const float RightColumnWidth = 220f;

	/*****
	Date: 2026-09-26
	Name: _selectedItems
	Description: 当前选中的地面物品堆集合（支持框选多选；主选为同格循环切换的基准）。
	*****/
	private readonly List<ItemStack> _selectedItems = new();

	/*****
	Date: 2026-09-26
	Name: _resourceMonitor
	Description: 资源监测栏（右上角常驻、可折叠）。
	*****/
	private ResourceMonitor? _resourceMonitor;

	/*****
	Date: 2026-09-26
	Name: _itemPanel
	Description: 材料信息栏（显示选中的地面物品，按种类整合）。
	*****/
	private ItemPanel? _itemPanel;

	/*****
	Date: 2026-09-25
	Name: _selectedCharacters
	Description: 当前选中的角色集合（支持框选多选；主选为列表中用于详情展示与循环切换基准的那一个）。
	*****/
	private readonly List<CharacterSim> _selectedCharacters = new();

	/*****
	Date: 2026-09-25
	Name: _selectionBoxView
	Description: 框选矩形可视化（世界坐标绘制，置于实体之上）。
	*****/
	private SelectionBoxView? _selectionBoxView;

	/*****
	Date: 2026-09-06
	Name: _entities
	Description: 实体节点容器（设施/角色视图的父节点）。
	*****/
	private Node2D? _entities;

	/*****
	Date: 2026-09-26
	Name: _placementPreview
	Description: 建造预览幽灵（挂实体层，与设施视图同一坐标系；层级见 FacilityPlacementPreview.ZOrder）。
	*****/
	private FacilityPlacementPreview? _placementPreview;

	/*****
	Date: 2026-09-26
	Name: _worldHint
	Description: 世界坐标短提示（挂 UI 层，建造被拒/拾取装不下/物品已被取走等场景在对应位置告知原因；全场同时只保留一条）。
	*****/
	private WorldHint? _worldHint;

	/*****
	Date: 2026-09-26
	Name: _worldContextMenu
	Description: 世界右键菜单（有选中人物且悬停命中物品时弹出：拾取 / 走到此处 /〔可装备时〕穿戴 / 预留扩展项）；条目在 BuildItemActions 里组装，扩展只改那里。
	*****/
	private WorldContextMenu? _worldContextMenu;

	/*****
	Date: 2026-09-06
	Name: _mapEditor
	Description: 地图编辑器。
	*****/
	private MapEditor? _mapEditor;

	/*****
	Date: 2026-09-06
	Name: _inputPicker
	Description: 输入拾取器（编辑模式下禁用）。
	*****/
	private InputPicker? _inputPicker;

	/*****
	Date: 2026-09-25
	Name: _camera
	Description: 主相机（剧情动画镜头控制引用）。
	*****/
	private Camera2D? _camera;

	/*****
	Date: 2026-09-25
	Name: _cameraController
	Description: 相机控制（剧情动画期间禁用）。
	*****/
	private CameraController? _cameraController;

	/*****
	Date: 2026-09-25
	Name: _dialogueBox
	Description: 底部对话框（剧情动画使用）。
	*****/
	private DialogueBox? _dialogueBox;

	/*****
	Date: 2026-09-25
	Name: _musicPlayer
	Description: 背景音乐播放器（曲目经音库配置）。
	*****/
	private AudioStreamPlayer? _musicPlayer;

	/*****
	Date: 2026-09-25
	Name: _cutsceneDirector
	Description: 剧情动画导演器。
	*****/
	private CutsceneDirector? _cutsceneDirector;

	/*****
	Date: 2026-09-25
	Name: _triggerRegistry
	Description: 剧情动画触发器注册表。
	*****/
	private CutsceneTriggerRegistry? _triggerRegistry;

	/*****
	Date: 2026-09-25
	Name: _pauseMenu
	Description: ESC 暂停菜单（继续/设置/保存/退出）。
	*****/
	private PauseMenu? _pauseMenu;

	/*****
	Date: 2026-09-06
	Name: _Ready
	Description: 装配表现层：MapView → 实体（设施/角色）→ 相机 → UI → 输入拾取。
	*****/
	public override void _Ready()
	{
		// 兜底：直接运行主场景（F6/F5 指定 Main）且无已构建会话时自动以正常模式开局；
		// 从标题界面进入时 Simulation 已构建，此分支不生效。
		if (GameRoot.Instance.Simulation == null)
			GameRoot.Instance.StartNewGame(devMode: false);

		Simulation simulation = GameRoot.Instance.Simulation
			?? throw new InvalidOperationException("模拟层未构建：无法装配主场景。");

		_mapView = new MapView { Name = "MapView" };
		AddChild(_mapView);

		_entities = new Node2D { Name = "Entities" };
		AddChild(_entities);
		BuildFacilityViews(_entities, simulation, _mapView);
		BuildCharacterAgents(_entities, simulation, _mapView);
		BuildItemViews(_entities, simulation, _mapView);
		simulation.Items.Changed += OnItemsChanged;

		// 建造预览幽灵：与设施视图同挂实体层（同一坐标系），层级由自身 ZIndex 定序
		_placementPreview = new FacilityPlacementPreview { Name = "PlacementPreview" };
		_entities.AddChild(_placementPreview);
		_placementPreview.Setup(simulation, _mapView);

		SetupCamera(simulation);
		BuildUi(simulation, _mapView);
	}

	/*****
	Date: 2026-09-06
	Name: BuildFacilityViews
	Description: 为模拟层每台设施创建 FacilityView 并绑定。
	*****/
	private void BuildFacilityViews(Node parent, Simulation simulation, MapView mapView)
	{
		foreach (FacilitySim facility in simulation.Facilities)
		{
			var view = new FacilityView { Name = $"Facility_{facility.Def?.Id ?? facility.OriginCell.ToString()}" };
			parent.AddChild(view);
			view.Setup(facility, mapView);
			_facilityViews.Add(view);
		}
	}

	/*****
	Date: 2026-09-06
	Name: BuildCharacterAgents
	Description: 为模拟层每名角色实例化 CharacterAgent.tscn 并绑定。
	*****/
	private void BuildCharacterAgents(Node parent, Simulation simulation, MapView mapView)
	{
		PackedScene? scene = GD.Load<PackedScene>("res://game/Characters/CharacterAgent.tscn");
		if (scene == null)
		{
			GD.PushError("无法加载角色场景：res://game/Characters/CharacterAgent.tscn");
			return;
		}

		foreach (Core.Characters.CharacterSim character in simulation.Characters)
		{
			if (scene.Instantiate() is not CharacterAgent agent) continue;
			agent.Name = $"Character_{character.Def?.Id ?? character.Cell.ToString()}";
			parent.AddChild(agent);
			agent.Setup(character, mapView);
			_characterAgents.Add(agent);
		}
	}

	/*****
	Date: 2026-09-26
	Name: BuildItemViews
	Description: 为模拟层现存的地面物品堆创建视图（开局散落或读档恢复的零件都由此在进场时一次性建出）。
	*****/
	private void BuildItemViews(Node parent, Simulation simulation, MapView mapView)
	{
		foreach (ItemStack stack in simulation.Items.Stacks)
		{
			AddItemView(parent, stack, mapView);
		}
	}

	/*****
	Date: 2026-09-26
	Name: AddItemView
	Description: 创建并登记单个物品堆视图。
	*****/
	private void AddItemView(Node parent, ItemStack stack, MapView mapView)
	{
		var view = new ItemStackView { Name = $"Item_{stack.Def.Id}_{stack.Cell.X}_{stack.Cell.Y}" };
		parent.AddChild(view);
		view.Setup(stack, mapView);
		_itemViews[stack] = view;
	}

	/*****
	Date: 2026-09-26
	Name: OnItemsChanged
	Description: 地面物品堆集合变化（开局散落 / 建造实时消耗 / 拆除返还）时同步视图：新增的建视图、消失的释放视图、其余刷新数量与位置。视图以 ItemStack 实例为键，故「扣空回收」能精确反映为对应视图移除；已消失的堆若正在被选中，一并从选择集剔除并刷新材料信息栏。本回调只在物品真发生变化时触发，不涉及逐帧开销。
	*****/
	private void OnItemsChanged()
	{
		if (_entities == null || _mapView == null) return;
		Simulation? simulation = GameRoot.Instance?.Simulation;
		if (simulation == null) return;

		HashSet<ItemStack> current = simulation.Items.Stacks.ToHashSet();
		foreach (ItemStack stack in current)
		{
			if (!_itemViews.ContainsKey(stack)) AddItemView(_entities, stack, _mapView);
		}

		foreach (KeyValuePair<ItemStack, ItemStackView> entry in _itemViews.ToArray())
		{
			if (current.Contains(entry.Key))
			{
				entry.Value.Refresh();
			}
			else
			{
				_itemViews.Remove(entry.Key);
				entry.Value.QueueFree();
			}
		}

		if (_selectedItems.RemoveAll(stack => !current.Contains(stack)) > 0)
		{
			_itemPanel?.ShowSelection(_selectedItems);
		}

		// 悬停对象若已被取空/消失，一并撤销蓝框（其余变化不清悬停，避免逐件转移时蓝框闪烁）
		if (_inputPicker?.HoveredItem is { } hovered && !current.Contains(hovered)) _inputPicker.ClearHover();
	}

	/*****
	Date: 2026-09-25
	Name: SetupCamera
	Description: 创建居中相机并设置初始缩放 = 「视野不超出地图」的缩放（两轴视口/世界比值取较大者），使地图铺满视口且不露出背景图以外的空白；后续缩放/平移由 CameraController 按同一约束夹紧。
	*****/
	private void SetupCamera(Simulation simulation)
	{
		var map = simulation.Map;
		float worldWidth = map.Width * MapView.TileSize;
		float worldHeight = map.Height * MapView.TileSize;
		Vector2 viewportSize = GetViewportRect().Size;
		float zoom = MathF.Max(viewportSize.X / worldWidth, viewportSize.Y / worldHeight);

		var camera = new Camera2D
		{
			Name = "MainCamera",
			Position = new Vector2(worldWidth / 2f, worldHeight / 2f),
			Zoom = new Vector2(zoom, zoom),
		};
		AddChild(camera);
		camera.MakeCurrent();
		_camera = camera;

		var cameraController = new CameraController { Name = "CameraController" };
		AddChild(cameraController);
		cameraController.Setup(camera, simulation);
		_cameraController = cameraController;
	}

	/*****
	Date: 2026-09-25
	Name: BuildUi
	Description: 构建 CanvasLayer 下的时间流速条（右上角）、设施面板（右侧居中）、调试浮层（左上）并接线输入拾取。
	*****/
	private void BuildUi(Simulation simulation, MapView mapView)
	{
		var canvas = new CanvasLayer { Name = "Ui" };
		AddChild(canvas);
		Vector2 viewportSize = GetViewportRect().Size;

		// 时间流速条固定右上角（与左上角菜单栏分列两侧互不挤占）：尺寸按内容最小化，
		// 内容变宽时向左生长（GrowHorizontal=Begin），始终贴住右边缘而不会溢出屏幕
		var timeBar = new TimeBar { Name = "TimeBar" };
		canvas.AddChild(timeBar);
		timeBar.Setup(simulation.Clock);
		timeBar.GrowHorizontal = Control.GrowDirection.Begin;
		timeBar.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.TopRight, Control.LayoutPresetMode.Minsize, 12);

		// 右侧信息栏（时间流速条正下方、贴右边缘、向左生长）：上为资源监测（可折叠），下为选中材料汇总；
		// 二者同列上下排布，故互不遮挡，也不会与中间偏右的设施/人物面板抢位置
		var rightColumn = new VBoxContainer { Name = "RightColumn" };
		canvas.AddChild(rightColumn);
		rightColumn.AddThemeConstantOverride("separation", 8);
		rightColumn.CustomMinimumSize = new Vector2(RightColumnWidth, 0);
		rightColumn.GrowHorizontal = Control.GrowDirection.Begin;
		rightColumn.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.TopRight, Control.LayoutPresetMode.Minsize, 12);
		rightColumn.OffsetTop += 31f; // 让开时间流速条（约 25 高 + 6 间距）
		rightColumn.OffsetBottom += 31f;

		_resourceMonitor = new ResourceMonitor { Name = "ResourceMonitor" };
		rightColumn.AddChild(_resourceMonitor);
		_resourceMonitor.Setup(GameRoot.Instance.ResourceStore, simulation, GameRoot.Instance.PartsItem?.Id);

		_itemPanel = new ItemPanel { Name = "ItemPanel" };
		rightColumn.AddChild(_itemPanel);
		_itemPanel.Setup();

		_facilityPanel = new FacilityPanel { Name = "FacilityPanel" };
		canvas.AddChild(_facilityPanel);
		_facilityPanel.Setup();
		_facilityPanel.CustomMinimumSize = new Vector2(220, 0);
		_facilityPanel.Position = new Vector2(viewportSize.X - 236f, viewportSize.Y / 2f - 60f);
		_facilityPanel.RepairRequested += OnRepairRequested;
		_facilityPanel.SuspendRequested += OnFacilitySuspendRequested;
		_facilityPanel.ResumeRequested += OnFacilityResumeRequested;

		// 人物状态面板（与设施面板同区域互斥显示）
		_characterPanel = new CharacterPanel { Name = "CharacterPanel" };
		canvas.AddChild(_characterPanel);
		_characterPanel.Setup(simulation);
		// 属性变多后面板变高：宽度固定（248），上边线与设施面板对齐，底边留 12px，超出内容由面板内滚动承接
		_characterPanel.CustomMinimumSize = new Vector2(248, 0);
		float characterPanelTop = viewportSize.Y / 2f - 60f;
		_characterPanel.AnchorLeft = 1f;
		_characterPanel.AnchorRight = 1f;
		_characterPanel.AnchorTop = characterPanelTop / viewportSize.Y;
		_characterPanel.AnchorBottom = 1f;
		_characterPanel.OffsetLeft = -260f;
		_characterPanel.OffsetRight = -12f;
		_characterPanel.OffsetTop = 0f;
		_characterPanel.OffsetBottom = -12f;
		_characterPanel.AbortRequested += AbortSelectedCharacters;

		var debugOverlay = new DebugOverlay { Name = "DebugOverlay" };
		canvas.AddChild(debugOverlay);
		debugOverlay.Setup(simulation, mapView);
		debugOverlay.Position = new Vector2(16f, 320f); // F3 调试浮层，避开左上菜单栏与面板区

		_inputPicker = new InputPicker { Name = "InputPicker" };
		AddChild(_inputPicker);
		_inputPicker.Setup(simulation, mapView);
		_inputPicker.FacilityPicked += OnFacilityPicked;
		_inputPicker.CharacterPicked += OnCharacterPicked;
		_inputPicker.ItemPicked += OnItemPicked;
		_inputPicker.RightClicked += OnRightClicked;
		_inputPicker.ItemHovered += OnItemHovered;

		// 框选可视化与多选提交（世界坐标，绘制在实体之上）；框选可同时圈中角色与地面物品
		_selectionBoxView = new SelectionBoxView { Name = "SelectionBoxView" };
		AddChild(_selectionBoxView);
		_inputPicker.BoxSelectChanged += rect => _selectionBoxView?.SetBox(rect);
		_inputPicker.BoxSelectionCommitted += (characters, items) =>
		{
			SetCharacterSelection(characters, characters.Count > 0 ? characters[0] : null);
			SetItemSelection(items, items.Count > 0 ? items[0] : null);
		};

		// 地图编辑器与编辑器面板
		_mapEditor = new MapEditor { Name = "MapEditor" };
		AddChild(_mapEditor);
		_mapEditor.Setup(simulation, mapView, _inputPicker);
		_mapEditor.FacilityPlaced += OnFacilityPlaced;
		_mapEditor.FacilityRemoved += OnFacilityRemoved;
		_mapEditor.FacilityBuildRequested += OnFacilityBuildRequested;
		_mapEditor.FacilityDemolishRequested += OnFacilityDemolishRequested;
		_mapEditor.PlacementRejected += OnPlacementRejected;
		if (_placementPreview != null) _mapEditor.SetPlacementPreview(_placementPreview);

		// 建造被拒提示文本（挂 UI 层：字号不受相机缩放影响；同时只保留一条，3 秒自行消失）
		_worldHint = new WorldHint { Name = "WorldHint" };
		canvas.AddChild(_worldHint);
		_worldHint.Setup(mapView);

		// 世界右键菜单（悬停物品时弹出：拾取 / 走到此处 /〔可装备时〕穿戴；嵌入式子窗口，随视口渲染在最上层）
		_worldContextMenu = new WorldContextMenu { Name = "WorldContextMenu" };
		AddChild(_worldContextMenu);
		_worldContextMenu.Setup();

		// ESC 暂停菜单（保存入口与设置/退出；存档对话框置顶浮于其上）
		_pauseMenu = new PauseMenu { Name = "PauseMenu" };
		canvas.AddChild(_pauseMenu);

		// 存档槽对话框（暂停菜单与主菜单读档共享三槽；确认后写入槽位）
		var saveDialog = new SaveSlotDialog { Name = "SaveSlotDialog" };
		saveDialog.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
		canvas.AddChild(saveDialog);
		saveDialog.SlotConfirmed += OnSaveSlotConfirmed;
		_pauseMenu.Setup(saveDialog);

		// 建造面板不再常驻：由左上角菜单「建造」经 PanelHost 按需开合（见 SetupMenus）

		// 底部对话框与背景音乐播放器
		_dialogueBox = new DialogueBox { Name = "DialogueBox" };
		canvas.AddChild(_dialogueBox);
		_dialogueBox.Setup();
		_dialogueBox.Position = new Vector2((viewportSize.X - 560f) / 2f, viewportSize.Y - 170f);

		// 存档对话框移至 UI 顶层，确保覆盖暂停菜单与建造面板
		canvas.MoveChild(saveDialog, canvas.GetChildCount() - 1);

		// 窗口注册：滚轮/中键等全局输入落在这些窗口上时只作用于窗口本身，不再缩放/平移地图
		// （相机在 _UnhandledInput 经 UiWindowRegistry 判定；世界提示 WorldHint 为鼠标穿透元素，刻意不注册）
		UiWindowRegistry.Register(timeBar);
		UiWindowRegistry.Register(rightColumn);
		UiWindowRegistry.Register(_facilityPanel);
		UiWindowRegistry.Register(_characterPanel);
		UiWindowRegistry.Register(debugOverlay);
		UiWindowRegistry.Register(_pauseMenu);
		UiWindowRegistry.Register(saveDialog);
		UiWindowRegistry.Register(_dialogueBox);

		// 左上角注册制菜单栏与互斥面板宿主（置于最后：依赖 _mapEditor 等全部组件就绪）
		SetupMenus(canvas, simulation);

		_musicPlayer = new AudioStreamPlayer { Name = "MusicPlayer" };
		AddChild(_musicPlayer);

		// 剧情动画导演器与触发器注册表
		var musicLibrary = GD.Load<MusicLibraryResource>("res://resources/music_library.tres");
		_cutsceneDirector = new CutsceneDirector { Name = "CutsceneDirector" };
		AddChild(_cutsceneDirector);
		var context = new CutsceneContext(
			camera: _camera ?? throw new InvalidOperationException("主相机未创建。"),
			mapView: mapView,
			dialogue: _dialogueBox,
			musicPlayer: _musicPlayer,
			musicLibrary: musicLibrary ?? new MusicLibraryResource(),
			findCharacter: id => simulation.Characters.FirstOrDefault(c => c.Def?.Id == id));
		_cutsceneDirector.Setup(context, _inputPicker, _cameraController!, _mapEditor);
		_triggerRegistry = new CutsceneTriggerRegistry(_cutsceneDirector);
		SetupCutsceneTriggers(_triggerRegistry);

		// 新游戏且未播过开场动画：标记已播并触发新游戏事件（读档 IntroPlayed=true 不触发）
		if (!GameRoot.Instance.IntroPlayed)
		{
			GameRoot.Instance.IntroPlayed = true;
			_triggerRegistry.FireNewGame();
		}
	}

	/*****
	Date: 2026-09-25
	Name: SetupCutsceneTriggers
	Description: 登记本局全部剧情动画触发器（新游戏开场/时钟/事件/独立音乐）；新增剧情在此追加一行注册即可。
	*****/
	private void SetupCutsceneTriggers(CutsceneTriggerRegistry registry)
	{
		registry.Register(new NewGameTrigger(new IntroCutscene()));
	}

	/*****
	Date: 2026-09-25
	Name: SetupMenus
	Description: 装配左上角菜单：注册制按钮栏（十项入口，图标按 menu_<id>.png 约定加载，缺失自动回退文字按钮）+ 互斥面板宿主。菜单结构演进只改本函数（新增入口 = 注册定义 + 面板工厂，零改框架）：建造=编辑面板（面板可见即建造模式，与 MapEditor 双向协调且带幂等守卫）、人物=乘员总览（行点击联动选中系统）、资源=真实零件余额、其余（氧气/水/食物/电力/健康与卫生/科学与技术/政治）=通用占位面板。
	*****/
	private void SetupMenus(CanvasLayer canvas, Simulation simulation)
	{
		var menuBar = new MenuBar { Name = "MenuBar" };
		canvas.AddChild(menuBar);
		menuBar.Position = new Vector2(16f, 8f);
		UiWindowRegistry.Register(menuBar);

		var panelHost = new PanelHost { Name = "PanelHost" };
		canvas.AddChild(panelHost);
		panelHost.Position = new Vector2(16f, 56f);

		// 面板注册（惰性工厂，首次打开才实例化）
		panelHost.RegisterPanel("build", () =>
		{
			var panel = new EditorPanel { Name = "EditorPanel", CustomMinimumSize = new Vector2(200, 0) };
			panel.Setup(_mapEditor!, GameRoot.Instance.FacilityCatalog);
			return panel;
		});
		panelHost.RegisterPanel("crew", () =>
		{
			var panel = new CrewPanel();
			panel.Setup(simulation);
			panel.CharacterSelected += OnCharacterPicked;
			return panel;
		});
		panelHost.RegisterPanel("resource", () =>
		{
			var panel = new ResourcePanel();
			panel.Setup();
			return panel;
		});
		// 其余入口暂为通用占位面板（功能落地时替换为真实面板类即可）
		panelHost.RegisterPanel("oxygen", () => new PlaceholderPanel("氧气"));
		panelHost.RegisterPanel("water", () => new PlaceholderPanel("水"));
		panelHost.RegisterPanel("food", () => new PlaceholderPanel("食物"));
		panelHost.RegisterPanel("electric", () => new PlaceholderPanel("电力"));
		panelHost.RegisterPanel("healthy_and_tidy", () => new PlaceholderPanel("健康与卫生"));
		panelHost.RegisterPanel("science_and_technology", () => new PlaceholderPanel("科学与技术"));
		panelHost.RegisterPanel("politics", () => new PlaceholderPanel("政治"));

		// 菜单项注册（SortOrder 决定排列顺序；Id 同时决定图标路径与本面板注册键）；建造入口 = 开合建造面板（面板可见性即建造模式）
		menuBar.RegisterItem(new MenuItemDef("build", "建造", 0, MenuIcon("build")), _ => panelHost.Toggle("build"));
		menuBar.RegisterItem(new MenuItemDef("crew", "人物", 1, MenuIcon("crew")), _ => panelHost.Toggle("crew"));
		menuBar.RegisterItem(new MenuItemDef("oxygen", "氧气", 2, MenuIcon("oxygen")), _ => panelHost.Toggle("oxygen"));
		menuBar.RegisterItem(new MenuItemDef("water", "水", 3, MenuIcon("water")), _ => panelHost.Toggle("water"));
		menuBar.RegisterItem(new MenuItemDef("food", "食物", 4, MenuIcon("food")), _ => panelHost.Toggle("food"));
		menuBar.RegisterItem(new MenuItemDef("electric", "电力", 5, MenuIcon("electric")), _ => panelHost.Toggle("electric"));
		menuBar.RegisterItem(new MenuItemDef("healthy_and_tidy", "健康与卫生", 6, MenuIcon("healthy_and_tidy")), _ => panelHost.Toggle("healthy_and_tidy"));
		menuBar.RegisterItem(new MenuItemDef("resource", "资源", 7, MenuIcon("resource")), _ => panelHost.Toggle("resource"));
		menuBar.RegisterItem(new MenuItemDef("science_and_technology", "科学与技术", 8, MenuIcon("science_and_technology")), _ => panelHost.Toggle("science_and_technology"));
		menuBar.RegisterItem(new MenuItemDef("politics", "政治", 9, MenuIcon("politics")), _ => panelHost.Toggle("politics"));

		/*****
		Date: 2026-09-25
		Name: MenuIcon
		Description: 本地函数；菜单图标资源路径（约定 res://game/Textures/ui/menu_<id>.png，文件缺失时 MenuBar 自动回退文字按钮）。
		*****/
		static string MenuIcon(string id) => $"res://game/Textures/ui/menu_{id}.png";

		// 建造面板可见性 ⇄ 编辑模式（Main 为唯一协调者；EnterEdit/ExitEdit 自身幂等，不会循环触发）
		panelHost.PanelVisibilityChanged += (id, visible) =>
		{
			menuBar.SetPressed(id, visible);
			if (id != "build") return;
			if (visible) _mapEditor?.EnterEdit();
			else _mapEditor?.ExitEdit();
		};
		// F12 等外部入口切换编辑模式时，同步开合建造面板与菜单钮按下态
		if (_mapEditor != null)
		{
			_mapEditor.EditModeChanged += editing =>
			{
				menuBar.SetPressed("build", editing);
				if (editing) panelHost.Open("build");
				else panelHost.CloseAll();
			};
		}
	}

	/*****
	Date: 2026-09-25
	Name: _ExitTree
	Description: 场景退出时注销剧情动画触发器订阅，防止悬空回调。
	*****/
	public override void _ExitTree()
	{
		_triggerRegistry?.UnregisterAll();
	}

	/*****
	Date: 2026-09-26
	Name: OnFacilityPicked
	Description: 点击选中/取消选中设施：同步设施视图描边与面板显示（点击空处取消选中）；选中设施时清空人物与材料选中（点击类选中互斥）。
	*****/
	private void OnFacilityPicked(FacilitySim? facility)
	{
		foreach (FacilityView view in _facilityViews)
		{
			view.Selected = view.Facility == facility;
		}
		_facilityPanel?.ShowFacility(facility);

		if (facility != null)
		{
			SetCharacterSelection(Array.Empty<CharacterSim>(), null);
			SetItemSelection(Array.Empty<ItemStack>(), null);
		}
	}

	/*****
	Date: 2026-09-26
	Name: OnCharacterPicked
	Description: 点击选中/取消选中角色：置为单选选择集并同步选中圈、层级与人物面板；选中角色时收起设施面板与材料选中（点击类选中互斥）。
	*****/
	private void OnCharacterPicked(CharacterSim? character)
	{
		if (character != null) SetItemSelection(Array.Empty<ItemStack>(), null);
		SetCharacterSelection(
			character == null ? Array.Empty<CharacterSim>() : new[] { character },
			character);
	}

	/*****
	Date: 2026-09-26
	Name: OnItemPicked
	Description: 点击选中/取消选中地面物品：置为单选选择集并刷新材料信息栏；选中物品时清空人物与设施选中（点击类选中互斥；而框选允许人与物资同时选中）。
	*****/
	private void OnItemPicked(ItemStack? stack)
	{
		SetItemSelection(stack == null ? Array.Empty<ItemStack>() : new[] { stack }, stack);
		if (stack == null) return;

		SetCharacterSelection(Array.Empty<CharacterSim>(), null);
		_facilityPanel?.ShowFacility(null);
		foreach (FacilityView view in _facilityViews) view.Selected = false;
	}

	/*****
	Date: 2026-09-26
	Name: SetItemSelection
	Description: 统一应用物品选择集（单选/同格循环/框选多选共用）：同步各物品视图的选中描边、刷新材料信息栏（按种类整合），并回写 InputPicker 的主选作为同格循环基准。物品层级固定不变（始终在人物之下），故不参与 ZIndex 提升；清空选择集时仅收起面板，不动人物/设施选中（框选可同时选中人与物资）。
	*****/
	private void SetItemSelection(IReadOnlyList<ItemStack> items, ItemStack? primary)
	{
		_selectedItems.Clear();
		foreach (ItemStack stack in items) _selectedItems.Add(stack);

		foreach (ItemStackView view in _itemViews.Values)
		{
			view.Selected = _selectedItems.Contains(view.Stack);
		}

		_itemPanel?.ShowSelection(_selectedItems);
		if (_inputPicker != null) _inputPicker.SelectedItem = primary;
	}

	/*****
	Date: 2026-09-25
	Name: SetCharacterSelection
	Description: 统一应用人物选择集（单选/叠放循环/框选多选共用）：同步选中圈与层级（选中者 ZIndex 提升到设施之上，主选移到实体层最后以保证在重叠人群中可见）、选中时收起设施面板（互斥）、刷新人物面板（主选详情 + 多人计数），并回写 InputPicker 的主选作为叠放循环基准。
	*****/
	private void SetCharacterSelection(IReadOnlyList<CharacterSim> characters, CharacterSim? primary)
	{
		_selectedCharacters.Clear();
		foreach (CharacterSim character in characters) _selectedCharacters.Add(character);

		// 选择集变化后重置物品悬停（悬停与「选中了谁」强相关，避免蓝框停留在旧选择的语境里）
		if (_inputPicker != null) _inputPicker.ClearHover();

		foreach (CharacterAgent agent in _characterAgents)
		{
			bool selected = _selectedCharacters.Contains(agent.Sim);
			agent.Selected = selected;
			// 层级带（用显式 ZIndex 定序，不依赖子节点顺序）：设施=0 ＜ 人物=1 ＜ 选中人物=2 ＜ 框选矩形=10。
			// 必须显式定序的原因：游戏内新造好的设施视图是运行时 AddChild 到实体层末尾的，若只靠子节点顺序，新设施会盖在人物之上。
			agent.ZIndex = selected ? 2 : 1;
		}

		// 主选置于实体层最后：重叠时保证可见
		if (primary != null && _entities != null && FindAgent(primary) is { } primaryAgent)
		{
			_entities.MoveChild(primaryAgent, _entities.GetChildCount() - 1);
		}

		if (_selectedCharacters.Count > 0)
		{
			_facilityPanel?.ShowFacility(null);
			foreach (FacilityView view in _facilityViews) view.Selected = false;
		}

		_characterPanel?.ShowSelection(_selectedCharacters, primary);
		if (_inputPicker != null) _inputPicker.SelectedCharacter = primary;
	}

	/*****
	Date: 2026-09-25
	Name: FindAgent
	Description: 查找指定角色对应的表现层代理；不存在返回 null。
	*****/
	private CharacterAgent? FindAgent(CharacterSim character)
	{
		foreach (CharacterAgent agent in _characterAgents)
		{
			if (ReferenceEquals(agent.Sim, character)) return agent;
		}
		return null;
	}

	/*****
	Date: 2026-09-25
	Name: OnRepairRequested
	Description: 玩家请求修复目标设施：若该设施上已存在**已挂起的修复任务**，本次请求即「恢复」它（进度、设施状态与施工警戒线保留），不新建任务；否则创建修复任务（耗时/零件/人力上限取设施定义，优先级取面板选择）并提交任务板。两条路径都先清除各角色对该设施的「暂时忽略」，使该任务重新参与优先级排队。提交即经任务提交事件触发空闲人力派工（当帧投入全部空闲人手）。正常模式注入备用零件物品定义——零件由工人**从自己背包**扣除，手里没料时会先去取最近的零件堆（备料前置）；开发者模式（或物品定义缺失）传 null 即免费。
	*****/
	private void OnRepairRequested(FacilitySim facility, TaskPriority priority)
	{
		Simulation simulation = GameRoot.Instance.Simulation;

		if (FindSuspendedTask(simulation, facility) is { } suspended)
		{
			simulation.ClearIgnoredTasksFor(facility);
			simulation.TaskBoard.Resume(suspended);
			return;
		}

		simulation.ClearIgnoredTasksFor(facility);
		var def = facility.Def;
		var task = new RepairTask(
			facility,
			def?.RepairGameMinutes ?? 30.0,
			def?.RepairPartsCost ?? 0,
			GameRoot.Instance.DevModeEnabled ? null : GameRoot.Instance.PartsItem,
			def?.MaxWorkers ?? 1,
			priority);
		simulation.TaskBoard.Submit(task);
	}

	/*****
	Date: 2026-09-26
	Name: OnFacilitySuspendRequested
	Description: 玩家在设施信息栏点击「中止」：把该设施上的未结任务挂起（Suspended）——任务板据此退出待处理队列并释放全部人员，任务自身保留进度、设施状态与施工警戒线（不触发回滚），可经「继续」或（维修态）再点「修复」恢复。
	*****/
	private void OnFacilitySuspendRequested(FacilitySim facility)
	{
		Simulation simulation = GameRoot.Instance.Simulation;
		foreach (ITask task in simulation.TaskBoard.Active.ToArray())
		{
			if (!ReferenceEquals(task.Target, facility)) continue;
			if (task.State is not (TaskState.Pending or TaskState.Assigned or TaskState.InProgress)) continue;
			task.Suspend();
			return;
		}
	}

	/*****
	Date: 2026-09-26
	Name: OnFacilityResumeRequested
	Description: 玩家在设施信息栏点击「继续」：恢复该设施上已挂起的任务（回到待处理队列并按紧急度重新排队，当帧派工）；同时清除各角色对该设施的「暂时忽略」。
	*****/
	private void OnFacilityResumeRequested(FacilitySim facility)
	{
		Simulation simulation = GameRoot.Instance.Simulation;
		if (FindSuspendedTask(simulation, facility) is not { } suspended) return;
		simulation.ClearIgnoredTasksFor(facility);
		simulation.TaskBoard.Resume(suspended);
	}

	/*****
	Date: 2026-09-26
	Name: FindSuspendedTask
	Description: 查找指定设施上已挂起的任务（用于「继续」按钮与「修复」按钮的恢复路径）；无则返回 null。
	*****/
	private static ITask? FindSuspendedTask(Simulation simulation, FacilitySim facility)
	{
		foreach (ITask task in simulation.TaskBoard.Active)
		{
			if (ReferenceEquals(task.Target, facility) && task.State == TaskState.Suspended) return task;
		}
		return null;
	}

	/*****
	Date: 2026-09-26
	Name: AbortSelectedCharacters
	Description: 「人物中止」（快捷键 F 与人物信息栏按钮共用）：对当前选择集内每个角色执行中止——停止当前工作、把手上任务列入本人忽略清单（紧急任务除外，其他角色不受影响），并立即转向任务列表的下一项。全部角色已忽略的任务仅在玩家对其设施重新下达指令时才会重新参与排队。
	*****/
	private void AbortSelectedCharacters()
	{
		Simulation? simulation = GameRoot.Instance?.Simulation;
		if (simulation == null || _selectedCharacters.Count == 0) return;

		foreach (CharacterSim character in _selectedCharacters.ToArray())
		{
			simulation.AbortCharacterWork(character, ignoreCurrent: true);
		}
	}

	/*****
	Date: 2026-09-26
	Name: OnRightClicked
	Description: 玩家右键点击地图：**鼠标悬停命中物品且有选中人物**时弹出世界右键菜单（拾取 / 走到此处 /〔可装备时〕穿戴 / 预留扩展项）；其他位置维持现状——若点击格可通行，令选择集内每人各前往一个落脚格（点击格与四邻依次分配，避免叠放），不可通行时以世界提示告知并忽略本次指令。移动会先打断当前工作（与「人物中止」共用释放逻辑，但不加入忽略清单——抵达后仍可回到原任务）。
	*****/
	private void OnRightClicked(Vector2 worldPosition)
	{
		if (_mapView == null || _selectedCharacters.Count == 0) return;
		Simulation? simulation = GameRoot.Instance?.Simulation;
		if (simulation == null) return;

		if (_inputPicker?.HoveredItem is { } hovered && _worldContextMenu != null)
		{
			_worldContextMenu.Open(_inputPicker.PointerViewportPosition, BuildItemActions(hovered));
			return;
		}

		OnMoveRequested(_mapView.LocalToMap(worldPosition));
	}

	/*****
	Date: 2026-09-26
	Name: OnItemHovered
	Description: 物品悬停变化：同步各物品视图的蓝色描边（只有当前悬停者显示）。悬停仅在有主选人物时由 InputPicker 发出。
	*****/
	private void OnItemHovered(ItemStack? stack)
	{
		foreach (ItemStackView view in _itemViews.Values)
		{
			view.Hovered = ReferenceEquals(view.Stack, stack);
		}
	}

	/*****
	Date: 2026-09-26
	Name: BuildItemActions
	Description: 组装悬停物品上的右键菜单条目（**注册式**：新增按钮在此追加一项即可，零改 WorldContextMenu）——「拾取」（逐件转移，背包满则置灰并说明原因）、「穿戴」（仅当物品可装备时出现）、「走到此处」（走到该物品所在格）。
	*****/
	private IReadOnlyList<WorldContextAction> BuildItemActions(ItemStack stack)
	{
		var actions = new List<WorldContextAction>();
		CharacterSim? actor = _selectedCharacters.Count > 0 ? _selectedCharacters[0] : null;
		if (actor == null) return actions;

		bool canPickup = actor.Inventory.MaxAddable(stack.Def) > 0;
		actions.Add(new WorldContextAction(
			"pickup", "拾取", canPickup, "背包已满，装不下更多",
			() => OnPickupRequested(actor, stack)));

		if (stack.Def.EquipSlot != EquipmentSlot.None)
		{
			actions.Add(new WorldContextAction(
				"wear", "穿戴", true, "",
				() => OnWearRequested(actor, stack)));
		}

		actions.Add(new WorldContextAction(
			"move", "走到此处", true, "",
			() => OnMoveRequested(stack.Cell)));

		return actions;
	}

	/*****
	Date: 2026-09-26
	Name: OnPickupRequested
	Description: 菜单「拾取」：让主选角色前往该物品并逐件搬入背包（`Simulation.OrderPickup`）；预检不过或任务途中收尾时，把原因以世界提示钉在物品所在格。
	*****/
	private void OnPickupRequested(CharacterSim actor, ItemStack stack)
	{
		Simulation? simulation = GameRoot.Instance?.Simulation;
		if (simulation == null) return;
		simulation.OrderPickup(actor, stack, outcome => ShowPickupOutcome(outcome, stack.Cell));
	}

	/*****
	Date: 2026-09-26
	Name: ShowPickupOutcome
	Description: 拾取结果 → 世界提示文案（`Transferred` 表示已转移（含部分），无需提示）。
	*****/
	private void ShowPickupOutcome(PickupOutcome outcome, Vector2I cell)
	{
		if (_worldHint == null || _mapView == null) return;
		string? text = outcome switch
		{
			PickupOutcome.NoCapacity => "背包装不下",
			PickupOutcome.Gone => "物品已被取走",
			PickupOutcome.Unreachable => "无法抵达该物品",
			_ => null,
		};
		if (text != null) _worldHint.ShowAt(text, _mapView.MapToLocal(cell));
	}

	/*****
	Date: 2026-09-26
	Name: OnWearRequested
	Description: 菜单「穿戴」：让主选角色前往该地面装备、取下并直接穿上（`Simulation.OrderWearFromGround`）；失败原因以世界提示告知。
	*****/
	private void OnWearRequested(CharacterSim actor, ItemStack stack)
	{
		Simulation? simulation = GameRoot.Instance?.Simulation;
		if (simulation == null) return;
		simulation.OrderWearFromGround(actor, stack, outcome => ShowEquipOutcome(outcome, stack.Cell));
	}

	/*****
	Date: 2026-09-26
	Name: ShowEquipOutcome
	Description: 穿戴结果 → 世界提示文案（`Worn` 成功不提示）。
	*****/
	private void ShowEquipOutcome(EquipOutcome outcome, Vector2I cell)
	{
		if (_worldHint == null || _mapView == null) return;
		string? text = outcome switch
		{
			EquipOutcome.Gone => "装备已被取走",
			EquipOutcome.Blocked => "这件装备换不下来（背包放不下）",
			_ => null,
		};
		if (text != null) _worldHint.ShowAt(text, _mapView.MapToLocal(cell));
	}

	/*****
	Date: 2026-09-26
	Name: OnMoveRequested
	Description: 「走到此处」（右键直接移动与菜单项共用）：目标格可通行则令选择集内每人各前往一个落脚格；不可通行时以世界提示告知并忽略本次指令。
	*****/
	private void OnMoveRequested(Vector2I cell)
	{
		Simulation? simulation = GameRoot.Instance?.Simulation;
		if (simulation == null || _mapView == null || _selectedCharacters.Count == 0) return;

		if (!simulation.CanMoveTo(cell))
		{
			_worldHint?.ShowAt("此处不可通行", _mapView.MapToLocal(cell));
			return;
		}

		simulation.OrderMoveGroup(_selectedCharacters.ToArray(), cell);
	}

	/*****
	Date: 2026-09-26
	Name: _UnhandledInput
	Description: 全局快捷键：F = 中止当前选中人物的当前工作（等价于人物信息栏的「中止」按钮）。仅在非编辑模式且拾取器启用（剧情动画/编辑模式中禁用）时生效；焦点在文本输入控件时不拦截。
	*****/
	public override void _UnhandledInput(InputEvent @event)
	{
		if (@event is not InputEventKey { Pressed: true, Echo: false, Keycode: Key.F }) return;
		if (_inputPicker is not { Enabled: true }) return;
		if (GetViewport().GuiGetFocusOwner() is LineEdit or TextEdit) return;

		AbortSelectedCharacters();
		GetViewport().SetInputAsHandled();
	}

	/*****
	Date: 2026-09-25
	Name: OnFacilityBuildRequested
	Description: 玩家下达建造指令：为已入场的「建造中」占位设施创建建造任务（耗时/零件/人力上限取设施定义，优先级普通）并提交任务板；正常模式注入备用零件物品定义（零件由工人从自己背包扣除，手里没料时会先去取最近的零件堆）；开发者模式不发出本请求，物品定义缺失时传 null 即免费。零件不足时建造转入被动中止（面板显示原因）、补足后自动继续；若任务被取消（无可用工人或作业格不可达）则移除该未完成设施。提交前清除各角色对该设施的「暂时忽略」（视为「任务再次被发布」）。
	*****/
	private void OnFacilityBuildRequested(FacilitySim facility)
	{
		Simulation simulation = GameRoot.Instance.Simulation;
		simulation.ClearIgnoredTasksFor(facility);
		var def = facility.Def;
		var task = new BuildTask(
			facility,
			def?.BuildGameMinutes ?? 30.0,
			def?.BuildCost ?? 0,
			GameRoot.Instance.DevModeEnabled ? null : GameRoot.Instance.PartsItem,
			def?.MaxWorkers ?? 1,
			TaskPriority.P5,
			onAbandoned: OnBuildAbandoned);
		simulation.TaskBoard.Submit(task);
	}

	/*****
	Date: 2026-09-25
	Name: OnBuildAbandoned
	Description: 建造任务被取消（无可用工人或作业格不可达）时的回调：提示并移除未完成的设施与其视图（已消耗零件不退还，沉没成本）。
	*****/
	private void OnBuildAbandoned(FacilitySim facility)
	{
		GD.PushWarning($"[Build] 「{facility.Def?.DisplayName ?? "设施"}」建造中断（无可用工人或作业格不可达），已撤销该建造。");
		GameRoot.Instance.Simulation.RemoveFacility(facility);
		OnFacilityRemoved(facility);
	}

	/*****
	Date: 2026-09-25
	Name: OnFacilityDemolishRequested
	Description: 玩家下达拆除指令：为设施创建拆除任务（耗时取定义、返还建造成本的 50%）并提交任务板；正常模式注入库存用于**完成时返还**（拆除本身不耗零件，故工人无须备料；开发者模式不发出本请求）。完成后由 OnDemolishCompleted 从模拟与表现层移除。提交前清除各角色对该设施的「暂时忽略」（视为「任务再次被发布」）。
	*****/
	private void OnFacilityDemolishRequested(FacilitySim facility)
	{
		Simulation simulation = GameRoot.Instance.Simulation;
		simulation.ClearIgnoredTasksFor(facility);
		var def = facility.Def;
		int refund = def != null ? Mathf.FloorToInt(def.BuildCost * 0.5f) : 0;
		var task = new DemolishTask(
			facility,
			def?.DemolishGameMinutes ?? 20.0,
			refund,
			GameRoot.Instance.DevModeEnabled ? null : GameRoot.Instance.ResourceStore,
			def?.MaxWorkers ?? 1,
			TaskPriority.P5,
			onRemoved: OnDemolishCompleted);
		simulation.TaskBoard.Submit(task);
	}

	/*****
	Date: 2026-09-25
	Name: OnDemolishCompleted
	Description: 拆除任务完成时的回调：从模拟移除设施并同步移除其视图（零件返还由任务经库存完成）。
	*****/
	private void OnDemolishCompleted(FacilitySim facility)
	{
		GameRoot.Instance.Simulation.RemoveFacility(facility);
		OnFacilityRemoved(facility);
	}

	/*****
	Date: 2026-09-25
	Name: OnSaveSlotConfirmed
	Description: 存档对话框确认槽位：将当前局面写入该存档槽。
	*****/
	private void OnSaveSlotConfirmed(int slot)
	{
		GameRoot.Instance.SaveGame(slot);
	}

	/*****
	Date: 2026-09-06
	Name: OnFacilityPlaced
	Description: 编辑器放置设施后创建对应 FacilityView 并加入列表。
	*****/
	private void OnFacilityPlaced(FacilitySim facility)
	{
		if (_mapView == null || _entities == null) return;
		var view = new FacilityView { Name = $"Facility_{facility.Def?.Id ?? facility.OriginCell.ToString()}" };
		_entities.AddChild(view);
		view.Setup(facility, _mapView);
		_facilityViews.Add(view);
	}

	/*****
	Date: 2026-09-25
	Name: OnFacilityRemoved
	Description: 编辑器拆除设施（或拆除任务完成/建造中断）后移除并释放对应 FacilityView；若设施面板正显示该设施则一并收起（避免面板停留于已移除设施）。
	*****/
	private void OnFacilityRemoved(FacilitySim facility)
	{
		if (ReferenceEquals(_facilityPanel?.CurrentFacility, facility)) _facilityPanel?.ShowFacility(null);

		FacilityView? view = _facilityViews.FirstOrDefault(v => v.Facility == facility);
		if (view == null) return;
		_facilityViews.Remove(view);
		view.QueueFree();
	}

	/*****
	Date: 2026-09-26
	Name: OnPlacementRejected
	Description: 建造被拒提示：把受阻原因组织成一句文案，钉在被点击的地图位置上。重复点击不会堆积——WorldHint 全场只有一条，新提示直接覆盖旧的并重置计时。
	*****/
	private void OnPlacementRejected(FacilityDef def, Vector2I origin, PlacementObstacle obstacle)
	{
		if (_worldHint == null || _mapView == null) return;
		_worldHint.ShowAt($"{DescribeObstacle(obstacle)}，无法建造「{def.DisplayName}」", _mapView.MapToLocal(origin));
	}

	/*****
	Date: 2026-09-26
	Name: DescribeObstacle
	Description: 受阻原因 → 玩家可读文案。材料不足**不在其列**：零件短缺只让建造进度停滞（补料后自动继续），并不阻止放置。
	*****/
	private static string DescribeObstacle(PlacementObstacle obstacle) => obstacle switch
	{
		PlacementObstacle.NotFloor => "此处不是可建造的地板",
		PlacementObstacle.FacilityOccupied => "此处已被其他设施占用",
		PlacementObstacle.ItemsPresent => "此处有物品阻挡，请先清理",
		_ => "此处无法放置",
	};
}
