using Godot;
using RelayStation.Core.Characters;
using RelayStation.Core.Common;
using RelayStation.Core.Events;
using RelayStation.Core.Facilities;
using RelayStation.Core.Map;
using RelayStation.Core.Tasks;
using RelayStation.Game.Autoloads;
using RelayStation.Game.Map;

namespace RelayStation.Game.UI;

/*****
Date: 2026-09-06
Name: DebugOverlay
Description: 开发期调试浮层；显示鼠标格子坐标、地形与功能区归属，订阅事件总线滚动打印任务/设施/角色关键事件日志（仅开发期，验收后可隐藏）。
*****/
public partial class DebugOverlay : VBoxContainer
{
    /*****
    Date: 2026-09-06
    Name: MaxLogLines
    Description: 事件日志最多保留行数。
    *****/
    private const int MaxLogLines = 8;

    /*****
    Date: 2026-09-06
    Name: _logs
    Description: 事件日志队列。
    *****/
    private readonly Queue<string> _logs = new();

    /*****
    Date: 2026-09-06
    Name: _mouseLabel
    Description: 鼠标信息标签。
    *****/
    private Label? _mouseLabel;

    /*****
    Date: 2026-09-06
    Name: _logLabel
    Description: 事件日志标签（多行）。
    *****/
    private Label? _logLabel;

    /*****
    Date: 2026-09-06
    Name: Setup
    Description: 构建标签控件并接线：地图悬停事件 + 事件总线的任务/设施/角色事件。
    *****/
    public void Setup(Simulation simulation, MapView mapView)
    {
        _mouseLabel = new Label();
        _logLabel = new Label();
        AddChild(_mouseLabel);
        AddChild(_logLabel);

        mapView.HoverCellChanged += OnHoverCellChanged;
        simulation.EventBus.Subscribe<TaskStateChangedEvent>(OnTaskStateChanged);
        simulation.EventBus.Subscribe<TaskSubmittedEvent>(OnTaskSubmitted);
        simulation.EventBus.Subscribe<FacilityStateChangedEvent>(OnFacilityStateChanged);
        simulation.EventBus.Subscribe<CharacterStateChangedEvent>(OnCharacterStateChanged);
    }

    /*****
    Date: 2026-09-06
    Name: OnHoverCellChanged
    Description: 更新鼠标格子坐标、地形与功能区显示。
    *****/
    private void OnHoverCellChanged(Vector2I cell, bool valid)
    {
        if (_mouseLabel == null) return;
        if (!valid)
        {
            _mouseLabel.Text = "鼠标：界外";
            return;
        }

        Simulation? simulation = GameRoot.Instance?.Simulation;
        ZoneId? zone = simulation?.Map.GetZone(cell);
        CellKind kind = simulation != null ? simulation.Map.GetCell(cell) : CellKind.Vacuum;
        _mouseLabel.Text = $"鼠标：({cell.X}, {cell.Y})  地形:{kind}  区:{zone?.ToString() ?? "—"}";
    }

    /*****
    Date: 2026-09-06
    Name: OnTaskSubmitted
    Description: 任务提交 → 追加日志。
    *****/
    private void OnTaskSubmitted(TaskSubmittedEvent e)
        => AppendLog($"[任务] 提交：{DescribeTarget(e.Task)}");

    /*****
    Date: 2026-09-06
    Name: OnTaskStateChanged
    Description: 任务状态变更 → 追加日志。
    *****/
    private void OnTaskStateChanged(TaskStateChangedEvent e)
        => AppendLog($"[任务] {DescribeTarget(e.Task)} → {e.NewState}");

    /*****
    Date: 2026-09-06
    Name: OnFacilityStateChanged
    Description: 设施状态变更 → 追加日志。
    *****/
    private void OnFacilityStateChanged(FacilityStateChangedEvent e)
        => AppendLog($"[设施] {e.Facility.Def?.DisplayName ?? "?"} → {e.NewState}");

    /*****
    Date: 2026-09-06
    Name: OnCharacterStateChanged
    Description: 角色状态变更 → 追加日志。
    *****/
    private void OnCharacterStateChanged(CharacterStateChangedEvent e)
        => AppendLog($"[角色] {e.Character.Def?.DisplayName ?? "?"} → {e.NewState}");

    /*****
    Date: 2026-09-06
    Name: DescribeTarget
    Description: 任务目标设施名称；无目标时显示「任务」。
    *****/
    private static string DescribeTarget(ITask task)
        => task.Target?.Def?.DisplayName ?? "任务";

    /*****
    Date: 2026-09-06
    Name: AppendLog
    Description: 追加一行日志并裁剪到最多 MaxLogLines 行。
    *****/
    private void AppendLog(string line)
    {
        _logs.Enqueue(line);
        while (_logs.Count > MaxLogLines) _logs.Dequeue();
        if (_logLabel != null) _logLabel.Text = string.Join("\n", _logs);
    }
}
