using Godot;
using RelayStation.Core.Facilities;

namespace RelayStation.Game.UI;

/*****
Date: 2026-09-06
Name: FacilityPanel
Description: 设施面板 UI；点击设施后显示名称与状态（损坏/维修中/运行），设施损坏时提供「修复」按钮创建修复任务（对应计划 §5.1/§7 设施面板组件）。
*****/
public partial class FacilityPanel : PanelContainer
{
    /*****
    Date: 2026-09-06
    Name: _facility
    Description: 当前显示的设施；面板隐藏时为 null。
    *****/
    private FacilitySim? _facility;

    /*****
    Date: 2026-09-06
    Name: _nameLabel
    Description: 设施名称标签。
    *****/
    private Label? _nameLabel;

    /*****
    Date: 2026-09-06
    Name: _stateLabel
    Description: 设施状态标签。
    *****/
    private Label? _stateLabel;

    /*****
    Date: 2026-09-06
    Name: _repairButton
    Description: 修复按钮（仅设施损坏时可用）。
    *****/
    private Button? _repairButton;

    /*****
    Date: 2026-09-06
    Name: RepairRequested
    Description: 玩家请求修复事件（参数：目标设施）。
    *****/
    public event Action<FacilitySim>? RepairRequested;

    /*****
    Date: 2026-09-06
    Name: Setup
    Description: 构建面板控件（名称 + 状态 + 修复按钮）并初始隐藏。
    *****/
    public void Setup()
    {
        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 6);
        AddChild(box);

        _nameLabel = new Label { Text = "设施" };
        _stateLabel = new Label { Text = "状态" };
        _repairButton = new Button { Text = "修复", CustomMinimumSize = new Vector2(120, 32) };
        _repairButton.Pressed += OnRepairPressed;

        box.AddChild(_nameLabel);
        box.AddChild(_stateLabel);
        box.AddChild(_repairButton);

        Visible = false;
    }

    /*****
    Date: 2026-09-06
    Name: ShowFacility
    Description: 显示指定设施的面板（订阅其状态变更）；传 null 时隐藏面板并注销订阅。
    *****/
    public void ShowFacility(FacilitySim? facility)
    {
        if (_facility != null) _facility.StateChanged -= OnStateChanged;
        _facility = facility;

        if (facility == null)
        {
            Visible = false;
            return;
        }

        facility.StateChanged += OnStateChanged;
        Visible = true;
        Refresh();
    }

    /*****
    Date: 2026-09-06
    Name: OnStateChanged
    Description: 设施状态变化时刷新面板显示。
    *****/
    private void OnStateChanged(FacilitySim facility, FacilityState newState) => Refresh();

    /*****
    Date: 2026-09-06
    Name: Refresh
    Description: 刷新设施名称、状态文字与修复按钮可用性（仅损坏状态可修复）。
    *****/
    private void Refresh()
    {
        if (_facility == null || _nameLabel == null || _stateLabel == null) return;

        _nameLabel.Text = _facility.Def?.DisplayName ?? "未知设施";
        _stateLabel.Text = _facility.State switch
        {
            FacilityState.Damaged => "状态：损坏",
            FacilityState.UnderRepair => "状态：维修中",
            _ => "状态：运行",
        };
        if (_repairButton != null)
        {
            _repairButton.Disabled = _facility.State != FacilityState.Damaged;
        }
    }

    /*****
    Date: 2026-09-06
    Name: OnRepairPressed
    Description: 发出修复请求事件。
    *****/
    private void OnRepairPressed()
    {
        if (_facility != null) RepairRequested?.Invoke(_facility);
    }

    /*****
    Date: 2026-09-06
    Name: _ExitTree
    Description: 节点退出时注销事件订阅，防止悬空回调。
    *****/
    public override void _ExitTree()
    {
        if (_facility != null) _facility.StateChanged -= OnStateChanged;
    }
}
