using Godot;
using RelayStation.Core.Common;
using RelayStation.Core.Facilities;
using RelayStation.Core.Tasks;
using RelayStation.Game.Autoloads;
using RelayStation.Game.Characters;
using RelayStation.Game.Facilities;
using RelayStation.Game.Input;
using RelayStation.Game.Map;
using RelayStation.Game.UI;

namespace RelayStation.Game.Main;

/*****
Date: 2026-09-06
Name: Main
Description: 主场景装配类；构建表现层各部件（MapView / FacilityView / CharacterAgent / InputPicker / 相机 / UI），并桥接玩家操作（选中设施、发起修复）到模拟层任务板。
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
    Date: 2026-09-06
    Name: _entities
    Description: 实体节点容器（设施/角色视图的父节点）。
    *****/
    private Node2D? _entities;

    /*****
    Date: 2026-09-06
    Name: _mapEditor
    Description: 地图编辑器。
    *****/
    private MapEditor? _mapEditor;

    /*****
    Date: 2026-09-06
    Name: _editorPanel
    Description: 编辑器 UI 面板。
    *****/
    private EditorPanel? _editorPanel;

    /*****
    Date: 2026-09-06
    Name: _inputPicker
    Description: 输入拾取器（编辑模式下禁用）。
    *****/
    private InputPicker? _inputPicker;

    /*****
    Date: 2026-09-06
    Name: _Ready
    Description: 装配表现层：MapView → 实体（设施/角色）→ 相机 → UI → 输入拾取。
    *****/
    public override void _Ready()
    {
        Simulation simulation = GameRoot.Instance.Simulation;

        _mapView = new MapView { Name = "MapView" };
        AddChild(_mapView);

        _entities = new Node2D { Name = "Entities" };
        AddChild(_entities);
        BuildFacilityViews(_entities, simulation, _mapView);
        BuildCharacterAgents(_entities, simulation, _mapView);

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
        }
    }

    /*****
    Date: 2026-09-06
    Name: SetupCamera
    Description: 创建居中相机并按视口尺寸计算缩放，使整张环形基地可见。
    *****/
    private void SetupCamera(Simulation simulation)
    {
        var map = simulation.Map;
        float worldWidth = map.Width * MapView.TileSize;
        float worldHeight = map.Height * MapView.TileSize;
        Vector2 viewportSize = GetViewportRect().Size;
        float zoom = MathF.Min(viewportSize.X / worldWidth, viewportSize.Y / worldHeight) * 0.95f;

        var camera = new Camera2D
        {
            Name = "MainCamera",
            Position = new Vector2(worldWidth / 2f, worldHeight / 2f),
            Zoom = new Vector2(zoom, zoom),
        };
        AddChild(camera);
        camera.MakeCurrent();

        var cameraController = new CameraController { Name = "CameraController" };
        AddChild(cameraController);
        cameraController.Setup(camera, simulation);
    }

    /*****
    Date: 2026-09-06
    Name: BuildUi
    Description: 构建 CanvasLayer 下的时间条（顶部居中）、设施面板（右侧居中）、调试浮层（左上）并接线输入拾取。
    *****/
    private void BuildUi(Simulation simulation, MapView mapView)
    {
        var canvas = new CanvasLayer { Name = "Ui" };
        AddChild(canvas);
        Vector2 viewportSize = GetViewportRect().Size;

        var timeBar = new TimeBar { Name = "TimeBar" };
        canvas.AddChild(timeBar);
        timeBar.Setup(simulation.Clock);
        timeBar.CustomMinimumSize = new Vector2(420, 40);
        timeBar.Position = new Vector2((viewportSize.X - 420f) / 2f, 8f);

        _facilityPanel = new FacilityPanel { Name = "FacilityPanel" };
        canvas.AddChild(_facilityPanel);
        _facilityPanel.Setup();
        _facilityPanel.CustomMinimumSize = new Vector2(220, 0);
        _facilityPanel.Position = new Vector2(viewportSize.X - 236f, viewportSize.Y / 2f - 60f);
        _facilityPanel.RepairRequested += OnRepairRequested;

        var debugOverlay = new DebugOverlay { Name = "DebugOverlay" };
        canvas.AddChild(debugOverlay);
        debugOverlay.Setup(simulation, mapView);
        debugOverlay.Position = new Vector2(16f, 56f);

        _inputPicker = new InputPicker { Name = "InputPicker" };
        AddChild(_inputPicker);
        _inputPicker.Setup(simulation, mapView);
        _inputPicker.FacilityPicked += OnFacilityPicked;

        // 地图编辑器与编辑器面板
        _mapEditor = new MapEditor { Name = "MapEditor" };
        AddChild(_mapEditor);
        _mapEditor.Setup(simulation, mapView, _inputPicker);
        _mapEditor.FacilityPlaced += OnFacilityPlaced;
        _mapEditor.FacilityRemoved += OnFacilityRemoved;

        _editorPanel = new EditorPanel { Name = "EditorPanel" };
        canvas.AddChild(_editorPanel);
        _editorPanel.Setup(_mapEditor, GameRoot.Instance.FacilityTemplates);
        _editorPanel.CustomMinimumSize = new Vector2(200, 0);
        _editorPanel.Position = new Vector2(16f, viewportSize.Y - 280f);
    }

    /*****
    Date: 2026-09-06
    Name: OnFacilityPicked
    Description: 点击选中/取消选中设施：同步设施视图描边与面板显示（点击空处取消选中）。
    *****/
    private void OnFacilityPicked(FacilitySim? facility)
    {
        foreach (FacilityView view in _facilityViews)
        {
            view.Selected = view.Facility == facility;
        }
        _facilityPanel?.ShowFacility(facility);
    }

    /*****
    Date: 2026-09-06
    Name: OnRepairRequested
    Description: 玩家请求修复目标设施：创建修复任务（耗时取设施定义）并提交任务板。
    *****/
    private void OnRepairRequested(FacilitySim facility)
    {
        var task = new RepairTask(facility, facility.Def?.RepairGameMinutes ?? 30.0);
        GameRoot.Instance.Simulation.TaskBoard.Submit(task);
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
    Date: 2026-09-06
    Name: OnFacilityRemoved
    Description: 编辑器拆除设施后移除并释放对应 FacilityView。
    *****/
    private void OnFacilityRemoved(FacilitySim facility)
    {
        FacilityView? view = _facilityViews.FirstOrDefault(v => v.Facility == facility);
        if (view == null) return;
        _facilityViews.Remove(view);
        view.QueueFree();
    }
}
