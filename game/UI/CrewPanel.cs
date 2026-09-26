using Godot;
using RelayStation.Core.Characters;
using RelayStation.Core.Common;
using RelayStation.Core.Events;
using RelayStation.Core.Tasks;
using RelayStation.Game.Autoloads;

namespace RelayStation.Game.UI;

/*****
Date: 2026-09-25
Name: CrewPanel
Description: 人物总览面板；遍历模拟层角色列表动态生成行（数量天然随登场角色自适应），每行显示姓名/状态/当前任务，点击行选中该角色（联动 CharacterPanel 与选中圈）。订阅角色/任务状态事件实时重绘。
*****/
public partial class CrewPanel : PanelContainer
{
    /*****
    Date: 2026-09-25
    Name: CharacterSelected
    Description: 点击行选中角色事件（参数：目标角色）。
    *****/
    public event Action<CharacterSim>? CharacterSelected;

    /*****
    Date: 2026-09-25
    Name: _simulation
    Description: 模拟核心（读取角色列表）。
    *****/
    private Simulation? _simulation;

    /*****
    Date: 2026-09-25
    Name: _rows
    Description: 角色行容器。
    *****/
    private VBoxContainer? _rows;

    /*****
    Date: 2026-09-25
    Name: Setup
    Description: 构建标题与行容器，订阅事件总线角色/任务状态事件实时重绘。
    *****/
    public void Setup(Simulation simulation)
    {
        _simulation = simulation;

        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 4);
        AddChild(box);

        var titleLabel = new Label { Text = "乘员总览" };
        titleLabel.AddThemeFontSizeOverride("font_size", 18);
        box.AddChild(titleLabel);

        _rows = new VBoxContainer();
        _rows.AddThemeConstantOverride("separation", 4);
        box.AddChild(_rows);

        if (GameRoot.Instance?.Simulation?.EventBus is { } bus)
        {
            bus.Subscribe<CharacterStateChangedEvent>(OnCharacterEvent);
            bus.Subscribe<TaskStateChangedEvent>(OnTaskEvent);
        }

        RebuildRows();
    }

    /*****
    Date: 2026-09-25
    Name: OnCharacterEvent
    Description: 角色状态变化时重绘行。
    *****/
    private void OnCharacterEvent(CharacterStateChangedEvent e) => RebuildRows();

    /*****
    Date: 2026-09-25
    Name: OnTaskEvent
    Description: 任务状态变化时重绘行。
    *****/
    private void OnTaskEvent(TaskStateChangedEvent e) => RebuildRows();

    /*****
    Date: 2026-09-25
    Name: RebuildRows
    Description: 按当前角色列表全量重建行（姓名[队长★] ｜ 状态 ｜ 任务目标）。
    *****/
    private void RebuildRows()
    {
        if (_simulation == null || _rows == null) return;

        foreach (Node child in _rows.GetChildren())
        {
            _rows.RemoveChild(child);
            child.QueueFree();
        }

        foreach (CharacterSim character in _simulation.Characters)
        {
            CharacterSim captured = character;
            string name = character.Def?.DisplayName ?? "未知角色";
            if (character.Def?.IsCaptain == true) name += "★";
            string state = character.State switch
            {
                CharacterState.Idle => "待机",
                CharacterState.Moving => "移动",
                CharacterState.Working => "维修",
                CharacterState.Interrupted => "打断",
                _ => "—",
            };
            string target = character.CurrentTask?.Target?.Def?.DisplayName ?? "—";

            var rowButton = new Button
            {
                Text = $"{name} ｜ {state} ｜ {target}",
                CustomMinimumSize = new Vector2(220, 30),
            };
            rowButton.Pressed += () => CharacterSelected?.Invoke(captured);
            _rows.AddChild(rowButton);
        }
    }

    /*****
    Date: 2026-09-25
    Name: _ExitTree
    Description: 节点退出时注销事件订阅，防止悬空回调。
    *****/
    public override void _ExitTree()
    {
        if (GameRoot.Instance?.Simulation?.EventBus is { } bus)
        {
            bus.Unsubscribe<CharacterStateChangedEvent>(OnCharacterEvent);
            bus.Unsubscribe<TaskStateChangedEvent>(OnTaskEvent);
        }
    }
}
