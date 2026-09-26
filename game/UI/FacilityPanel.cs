using Godot;
using RelayStation.Core.Events;
using RelayStation.Core.Facilities;
using RelayStation.Core.Tasks;
using RelayStation.Game.Autoloads;

namespace RelayStation.Game.UI;

/*****
Date: 2026-09-25
Name: FacilityPanel
Description: 设施面板 UI；点击设施后显示名称、状态（损坏/维修中/运行）、维修人力（n/Max，取自同目标未结任务的工作者数）与优先级选择（1~9 + 紧急），设施损坏时提供「修复」按钮创建修复任务（携带所选优先级）。作业中的设施另提供「中止」按钮（挂起任务、保留进度与设施状态、释放全部人员）与（非维修态的）「继续」按钮（恢复挂起任务）；维修态不需要「继续」——直接再点「修复」即恢复；因零件耗尽被动中止时在人力行注明原因。订阅任务/角色状态事件实时刷新人力显示。
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
    Date: 2026-09-25
    Name: _workersLabel
    Description: 维修人力标签（n/Max；无未结任务时隐藏该行内容）。
    *****/
    private Label? _workersLabel;

    /*****
    Date: 2026-09-25
    Name: _progressLabel
    Description: 作业进度文字标签（修复中/建造中/拆除中 + 百分比 + 已耗/总零件）。
    *****/
    private Label? _progressLabel;

    /*****
    Date: 2026-09-25
    Name: _progressBar
    Description: 作业进度条（0~100，取任务 ProgressFraction）；无未结任务时隐藏。
    *****/
    private ProgressBar? _progressBar;

    /*****
    Date: 2026-09-06
    Name: _repairButton
    Description: 修复按钮（仅设施损坏时可用）。
    *****/
    private Button? _repairButton;

    /*****
    Date: 2026-09-25
    Name: _prioritySelector
    Description: 任务优先级选择器（1~9 + 紧急；默认 5），对应《缺氧》式任务优先度数字档。
    *****/
    private OptionButton? _prioritySelector;

    /*****
    Date: 2026-09-26
    Name: _suspendButton
    Description: 中止按钮（设施上有未挂起的未结任务时可用）：挂起该任务。
    *****/
    private Button? _suspendButton;

    /*****
    Date: 2026-09-26
    Name: _resumeButton
    Description: 继续按钮（任务已挂起且**非维修态**时可见）：恢复挂起的任务重新排队；维修态的恢复入口复用「修复」按钮。
    *****/
    private Button? _resumeButton;

    /*****
    Date: 2026-09-25
    Name: RepairRequested
    Description: 玩家请求修复事件（参数：目标设施、所选任务优先级）。
    *****/
    public event Action<FacilitySim, TaskPriority>? RepairRequested;

    /*****
    Date: 2026-09-26
    Name: SuspendRequested
    Description: 玩家请求中止事件（参数：目标设施）；由装配方把该设施上的未结任务挂起。
    *****/
    public event Action<FacilitySim>? SuspendRequested;

    /*****
    Date: 2026-09-26
    Name: ResumeRequested
    Description: 玩家请求继续事件（参数：目标设施）；由装配方恢复该设施上已挂起的任务。
    *****/
    public event Action<FacilitySim>? ResumeRequested;

    /*****
    Date: 2026-09-25
    Name: CurrentFacility
    Description: 当前显示的设施；面板隐藏或未绑定时为 null（供装配方在设施被移除时判断是否需要收起面板）。
    *****/
    public FacilitySim? CurrentFacility => _facility;

    /*****
    Date: 2026-09-25
    Name: Setup
    Description: 构建面板控件（名称 + 状态 + 维修人力 + 优先级选择 + 修复/中止/继续按钮），订阅事件总线的任务/角色状态事件实时刷新，初始隐藏。
    *****/
    public void Setup()
    {
        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 6);
        AddChild(box);

        _nameLabel = new Label { Text = "设施" };
        _stateLabel = new Label { Text = "状态" };
        _workersLabel = new Label { Text = "" };
        _progressLabel = new Label { Text = "" };
        _progressBar = new ProgressBar
        {
            MinValue = 0,
            MaxValue = 100,
            ShowPercentage = false,
            CustomMinimumSize = new Vector2(180, 18),
            Visible = false,
        };
        _repairButton = new Button { Text = "修复", CustomMinimumSize = new Vector2(120, 32) };
        _repairButton.Pressed += OnRepairPressed;
        _suspendButton = new Button { Text = "中止", CustomMinimumSize = new Vector2(120, 32) };
        _suspendButton.Pressed += OnSuspendPressed;
        _resumeButton = new Button { Text = "继续", CustomMinimumSize = new Vector2(120, 32) };
        _resumeButton.Pressed += OnResumePressed;

        // 优先级选择行（《缺氧》式任务优先度数字档：1~9 + 紧急；条目下标 = 数值 - 1）
        var priorityRow = new HBoxContainer();
        priorityRow.AddThemeConstantOverride("separation", 8);
        priorityRow.AddChild(new Label { Text = "优先级", CustomMinimumSize = new Vector2(56, 0) });
        _prioritySelector = new OptionButton { CustomMinimumSize = new Vector2(100, 28) };
        for (int number = 1; number <= 9; number++)
        {
            _prioritySelector.AddItem(number.ToString());
        }
        _prioritySelector.AddItem("紧急");
        _prioritySelector.Selected = (int)TaskPriority.P5 - 1;
        priorityRow.AddChild(_prioritySelector);

        box.AddChild(_nameLabel);
        box.AddChild(_stateLabel);
        box.AddChild(_workersLabel);
        box.AddChild(_progressLabel);
        box.AddChild(_progressBar);
        box.AddChild(priorityRow);
        box.AddChild(_repairButton);
        box.AddChild(_suspendButton);
        box.AddChild(_resumeButton);

        if (GameRoot.Instance?.Simulation?.EventBus is { } bus)
        {
            bus.Subscribe<TaskStateChangedEvent>(OnTaskEvent);
            bus.Subscribe<CharacterStateChangedEvent>(OnCharacterEvent);
        }

        Visible = false;
    }

    /*****
    Date: 2026-09-25
    Name: OnTaskEvent
    Description: 任务状态变化（含工作者加入）时刷新人力显示。
    *****/
    private void OnTaskEvent(TaskStateChangedEvent e) => Refresh();

    /*****
    Date: 2026-09-25
    Name: OnCharacterEvent
    Description: 角色状态变化（含加入多人维修）时刷新人力显示。
    *****/
    private void OnCharacterEvent(CharacterStateChangedEvent e) => Refresh();

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
    Date: 2026-09-25
    Name: Refresh
    Description: 刷新设施名称、状态文字、作业人力（n/Max，取自同目标未结任务；任务挂起时显示「已中止」）、作业进度条与百分比（修复中/建造中/拆除中）+ 零件消耗，以及三个操作按钮的可用性：
    ①修复：设施损坏时可发起新修复；维修态下若存在**已挂起的修复任务**则也可用（再点「修复」即恢复，无需单独的「继续」）；②中止：设施上有未挂起的未结任务时可用；③继续：任务已挂起且非维修态时可见（维修态的恢复走「修复」按钮）。
    *****/
    private void Refresh()
    {
        if (_facility == null || _nameLabel == null || _stateLabel == null) return;

        _nameLabel.Text = _facility.Def?.DisplayName ?? "未知设施";
        _stateLabel.Text = _facility.State switch
        {
            FacilityState.Damaged => "状态：损坏",
            FacilityState.UnderRepair => "状态：维修中",
            FacilityState.UnderConstruction => "状态：建造中",
            FacilityState.Demolishing => "状态：拆除中",
            _ => "状态：运行",
        };

        ITask? task = FindActiveTask();
        bool suspended = task?.State == TaskState.Suspended;
        // 维修态的恢复入口是「修复」按钮（设施停在维修中且任务已挂起）
        bool suspendedRepair = suspended && _facility.State == FacilityState.UnderRepair;

        if (_workersLabel != null)
        {
            _workersLabel.Text = task switch
            {
                null => "",
                { State: TaskState.Suspended, SuspendedByPartsShortage: true } =>
                    $"作业人力：已中止（零件不足）　优先级：{PriorityName(task.Priority)}",
                { State: TaskState.Suspended } =>
                    $"作业人力：已中止　优先级：{PriorityName(task.Priority)}",
                _ => $"作业人力：{task.Workers.Count}/{task.MaxWorkers}  优先级：{PriorityName(task.Priority)}",
            };
        }
        if (_progressBar != null && _progressLabel != null)
        {
            _progressBar.Visible = task != null;
            _progressLabel.Visible = task != null;
            if (task != null)
            {
                _progressBar.Value = task.ProgressFraction * 100.0;
                string parts = task.TotalPartsCost > 0
                    ? $"　零件 {task.PartsConsumed}/{task.TotalPartsCost}"
                    : "";
                string prefix = suspended ? "已中止　" : "";
                _progressLabel.Text = $"{prefix}{WorkVerb(_facility.State)} {task.ProgressFraction * 100.0:0}%{parts}";
            }
        }
        if (_repairButton != null)
        {
            _repairButton.Disabled = _facility.State != FacilityState.Damaged && !suspendedRepair;
        }
        if (_suspendButton != null)
        {
            _suspendButton.Visible = task != null && !suspended;
        }
        if (_resumeButton != null)
        {
            _resumeButton.Visible = suspendedRepair == false && suspended;
        }
    }

    /*****
    Date: 2026-09-25
    Name: _Process
    Description: 面板可见且当前设施存在未结任务时每帧刷新——进度条百分比随游戏时间连续推进，仅靠状态事件不足以驱动。
    *****/
    public override void _Process(double delta)
    {
        if (!Visible || _facility == null) return;
        if (FindActiveTask() == null) return;
        Refresh();
    }

    /*****
    Date: 2026-09-25
    Name: WorkVerb
    Description: 按设施状态给出作业动词（建造中/拆除中/维修中）。
    *****/
    private static string WorkVerb(FacilityState state) => state switch
    {
        FacilityState.UnderConstruction => "建造中",
        FacilityState.Demolishing => "拆除中",
        _ => "维修中",
    };

    /*****
    Date: 2026-09-25
    Name: FindActiveTask
    Description: 查找当前设施的同目标未结任务（含已挂起者；用于人力/进度显示与按钮态）；无则返回 null。
    *****/
    private ITask? FindActiveTask()
    {
        if (_facility == null) return null;
        foreach (ITask task in GameRoot.Instance?.Simulation?.TaskBoard.Active ?? Array.Empty<ITask>())
        {
            if (ReferenceEquals(task.Target, _facility)
                && task.State is TaskState.Pending or TaskState.Assigned or TaskState.InProgress or TaskState.Suspended)
            {
                return task;
            }
        }
        return null;
    }

    /*****
    Date: 2026-09-26
    Name: PriorityName
    Description: 优先级显示名称：数字档显示 1~9，紧急显示「紧急」。
    *****/
    private static string PriorityName(TaskPriority priority)
        => priority == TaskPriority.Urgent ? "紧急" : ((int)priority).ToString();

    /*****
    Date: 2026-09-26
    Name: SelectedPriority
    Description: 优先级下拉当前选择 → 任务优先级（条目下标 0~8 对应 1~9，下标 9 对应紧急；越界回退 5 档）。
    *****/
    private TaskPriority SelectedPriority()
    {
        int index = _prioritySelector?.Selected ?? -1;
        return index switch
        {
            9 => TaskPriority.Urgent,
            >= 0 and <= 8 => (TaskPriority)(index + 1),
            _ => TaskPriority.P5,
        };
    }

    /*****
    Date: 2026-09-06
    Name: OnRepairPressed
    Description: 发出修复请求事件（携带所选优先级）；装配方对「已挂起的修复任务」走恢复路径，否则创建新修复任务。
    *****/
    private void OnRepairPressed()
    {
        if (_facility == null) return;
        RepairRequested?.Invoke(_facility, SelectedPriority());
    }

    /*****
    Date: 2026-09-26
    Name: OnSuspendPressed
    Description: 发出中止请求事件（装配方把该设施上的未结任务挂起：释放全部人员、保留进度与设施状态）。
    *****/
    private void OnSuspendPressed()
    {
        if (_facility == null) return;
        SuspendRequested?.Invoke(_facility);
    }

    /*****
    Date: 2026-09-26
    Name: OnResumePressed
    Description: 发出继续请求事件（装配方恢复该设施上已挂起的任务重新排队）。
    *****/
    private void OnResumePressed()
    {
        if (_facility == null) return;
        ResumeRequested?.Invoke(_facility);
    }

    /*****
    Date: 2026-09-06
    Name: _ExitTree
    Description: 节点退出时注销事件订阅，防止悬空回调。
    *****/
    public override void _ExitTree()
    {
        if (_facility != null) _facility.StateChanged -= OnStateChanged;
        if (GameRoot.Instance?.Simulation?.EventBus is { } bus)
        {
            bus.Unsubscribe<TaskStateChangedEvent>(OnTaskEvent);
            bus.Unsubscribe<CharacterStateChangedEvent>(OnCharacterEvent);
        }
    }
}
