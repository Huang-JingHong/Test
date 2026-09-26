using Godot;
using RelayStation.Core.Characters;
using RelayStation.Core.Common;
using RelayStation.Core.Events;
using RelayStation.Core.Items;
using RelayStation.Core.Tasks;

namespace RelayStation.Game.UI;

/*****
Date: 2026-09-26
Name: CharacterPanel
Description: 人物状态面板 UI；点击角色后显示姓名（队长后缀 ★）、选择集提示、当前行为状态、专长、年龄/性别，以及四个分区——「生存」（呼吸/进食/饮水/排泄/排尿/休息）、「身心」（卫生/社交/娱乐/生命值）、「装备」（头部/身体/背部/手部/脚部五槽 + 装备效果摘要，可卸下）、「背包」（负重 xx/yy kg + 物品行，可装备的可穿戴物品带「装备」按钮），底部为背景介绍 Bio 与「中止 (F)」按钮。
属性变多后采用紧凑单列 + 分区标题 + ScrollContainer 限高滚动；订阅 NeedsChangedEvent 按游戏分钟刷新数值、订阅 TaskStateChangedEvent 在本人的个人任务（穿脱/取用/拾取）终态后刷新装备与背包，选中/状态变化后全量刷新；与设施面板同区域互斥显示。**「装备」与「卸下」都是任务化指令**（经 Simulation 门面下耗时任务，不再瞬时写入）。
*****/
public partial class CharacterPanel : PanelContainer
{
    /*****
    Date: 2026-09-26
    Name: SurvivalNeeds
    Description: 「生存」分区显示顺序（生理需求：排泄与排尿相邻显示，便于对照「吃↔排、喝↔尿」的独立来源）。
    *****/
    private static readonly NeedId[] SurvivalNeeds =
    {
        NeedId.Oxygen, NeedId.Food, NeedId.Water, NeedId.Excretion, NeedId.Urination, NeedId.Rest,
    };

    /*****
    Date: 2026-09-26
    Name: MindNeeds
    Description: 「身心」分区显示顺序（心理需求；生命值紧随其后单独一行）。
    *****/
    private static readonly NeedId[] MindNeeds =
    {
        NeedId.Hygiene, NeedId.Social, NeedId.Entertainment,
    };

    /*****
    Date: 2026-09-26
    Name: EquipSlots
    Description: 「装备」分区展示的槽位顺序（不含 None）。
    *****/
    private static readonly EquipmentSlot[] EquipSlots =
    {
        EquipmentSlot.Head, EquipmentSlot.Body, EquipmentSlot.Back, EquipmentSlot.Hands, EquipmentSlot.Feet,
    };

    /*****
    Date: 2026-09-26
    Name: HealthyColor
    Description: 指标条健康色（衰减型 ≥60 / 增长型 <40）。
    *****/
    private static readonly Color HealthyColor = new(0.35f, 0.75f, 0.45f);

    /*****
    Date: 2026-09-26
    Name: WarnColor
    Description: 指标条警告色（衰减型 30~59 / 增长型 40~69）。
    *****/
    private static readonly Color WarnColor = new(0.9f, 0.72f, 0.3f);

    /*****
    Date: 2026-09-26
    Name: DangerColor
    Description: 指标条危险色（衰减型 <30 / 增长型 ≥70）。
    *****/
    private static readonly Color DangerColor = new(0.85f, 0.32f, 0.28f);

    /*****
    Date: 2026-09-26
    Name: HealthBarColor
    Description: 生命值条颜色（恒定红色，与需求条区分）。
    *****/
    private static readonly Color HealthBarColor = new(0.78f, 0.3f, 0.32f);

    /*****
    Date: 2026-09-26
    Name: BarBackgroundColor
    Description: 指标条底槽颜色。
    *****/
    private static readonly Color BarBackgroundColor = new(0.16f, 0.17f, 0.2f);

    /*****
    Date: 2026-09-26
    Name: SectionColor
    Description: 分区标题与占位文案颜色（暗色小字）。
    *****/
    private static readonly Color SectionColor = new(0.6f, 0.66f, 0.74f);

    /*****
    Date: 2026-09-25
    Name: _simulation
    Description: 模拟核心（读取需求系统、执行装备/卸下写操作）。
    *****/
    private Simulation? _simulation;

    /*****
    Date: 2026-09-25
    Name: _character
    Description: 当前显示的角色；面板隐藏时为 null。
    *****/
    private CharacterSim? _character;

    /*****
    Date: 2026-09-26
    Name: _box
    Description: 内容容器（ScrollContainer 内的纵向盒子，全部分区与指标行都挂在此）。
    *****/
    private VBoxContainer? _box;

    /*****
    Date: 2026-09-25
    Name: _selectionLabel
    Description: 选择集提示标签（多人框选时显示「已选 N 人」；单人时不显示）。
    *****/
    private Label? _selectionLabel;

    /*****
    Date: 2026-09-25
    Name: _nameLabel
    Description: 姓名标签（队长追加 ★队长）。
    *****/
    private Label? _nameLabel;

    /*****
    Date: 2026-09-25
    Name: _stateLabel
    Description: 行为状态标签。
    *****/
    private Label? _stateLabel;

    /*****
    Date: 2026-09-25
    Name: _specialtyLabel
    Description: 专长标签。
    *****/
    private Label? _specialtyLabel;

    /*****
    Date: 2026-09-25
    Name: _profileLabel
    Description: 年龄与性别标签。
    *****/
    private Label? _profileLabel;

    /*****
    Date: 2026-09-25
    Name: _bioLabel
    Description: 背景介绍标签（自动换行多行）。
    *****/
    private Label? _bioLabel;

    /*****
    Date: 2026-09-26
    Name: _needRows
    Description: 需求指标行（需求 → 细条 / 数值标签 / 填充样式），按 NeedCatalog 逐项刷新。
    *****/
    private readonly Dictionary<NeedId, (ProgressBar Bar, Label Value, StyleBoxFlat Fill)> _needRows = new();

    /*****
    Date: 2026-09-26
    Name: _healthBar
    Description: 生命值细条。
    *****/
    private ProgressBar? _healthBar;

    /*****
    Date: 2026-09-26
    Name: _healthValue
    Description: 生命值数值标签（如「100/100」）。
    *****/
    private Label? _healthValue;

    /*****
    Date: 2026-09-26
    Name: _equipRows
    Description: 装备槽行（槽位 → 物品名标签 / 卸下按钮）。
    *****/
    private readonly Dictionary<EquipmentSlot, (Label Value, Button Unequip)> _equipRows = new();

    /*****
    Date: 2026-09-26
    Name: _weightLabel
    Description: 背包负重标签（「负重 12.5/35.0 kg」；超重时红字）。
    *****/
    private Label? _weightLabel;

    /*****
    Date: 2026-09-26
    Name: _bagRows
    Description: 背包物品行容器（每次刷新整体重建）。
    *****/
    private VBoxContainer? _bagRows;

    /*****
    Date: 2026-09-26
    Name: _bagEmptyLabel
    Description: 空背包提示标签。
    *****/
    private Label? _bagEmptyLabel;

    /*****
    Date: 2026-09-26
    Name: _effectLabel
    Description: 装备效果摘要行（「效率 +10%　移速 +20%　容量 +20.0 kg」；无加成时显示「无」）。
    *****/
    private Label? _effectLabel;

    /*****
    Date: 2026-09-26
    Name: _abortButton
    Description: 中止按钮（快捷键 F 的等价入口）：让当前选择集停止当前工作、暂时忽略手上的任务并转做下一项。
    *****/
    private Button? _abortButton;

    /*****
    Date: 2026-09-26
    Name: AbortRequested
    Description: 玩家请求中止当前选择集事件（无参数；由装配方对选择集内全部角色执行中止）。
    *****/
    public event Action? AbortRequested;

    /*****
    Date: 2026-09-26
    Name: Setup
    Description: 构建面板控件（头部信息 + 生存/身心指标行 + 装备槽 + 背包 + Bio，整体置于限高滚动容器内）并初始隐藏；订阅需求推进事件刷新数值。
    *****/
    public void Setup(Simulation simulation)
    {
        _simulation = simulation;

        var scroll = new ScrollContainer
        {
            Name = "Scroll",
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        AddChild(scroll);

        _box = new VBoxContainer { Name = "Box", SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _box.AddThemeConstantOverride("separation", 6);
        scroll.AddChild(_box);

        _selectionLabel = new Label { Text = "", Visible = false };
        _selectionLabel.AddThemeFontSizeOverride("font_size", 13);
        _selectionLabel.AddThemeColorOverride("font_color", new Color(1f, 0.85f, 0.2f));

        _nameLabel = new Label { Text = "角色" };
        _nameLabel.AddThemeFontSizeOverride("font_size", 18);
        _stateLabel = new Label { Text = "状态" };
        _specialtyLabel = new Label { Text = "专长" };
        _profileLabel = new Label { Text = "年龄 / 性别" };
        _box.AddChild(_selectionLabel);
        _box.AddChild(_nameLabel);
        _box.AddChild(_stateLabel);

        // 中止按钮（与快捷键 F 等价；对当前选择集生效）
        _abortButton = new Button { Text = "中止 (F)", CustomMinimumSize = new Vector2(120, 30) };
        _abortButton.Pressed += () => AbortRequested?.Invoke();
        _box.AddChild(_abortButton);

        _box.AddChild(_specialtyLabel);
        _box.AddChild(_profileLabel);

        // 生存分区（生理需求）
        AddSectionHeader("生存");
        foreach (NeedId id in SurvivalNeeds) _needRows[id] = BuildMetricRow(NeedName(id), HealthyColor);

        // 身心分区（心理需求 + 生命值）
        AddSectionHeader("身心");
        foreach (NeedId id in MindNeeds) _needRows[id] = BuildMetricRow(NeedName(id), HealthyColor);
        (ProgressBar Bar, Label Value, StyleBoxFlat Fill) health = BuildMetricRow("生命值", HealthBarColor);
        _healthBar = health.Bar;
        _healthValue = health.Value;

        // 装备分区（五个槽位：头部/身体/背部/手部/脚部；末行为装备效果摘要）
        AddSectionHeader("装备");
        foreach (EquipmentSlot slot in EquipSlots) _equipRows[slot] = BuildEquipmentRow(slot);
        _effectLabel = new Label { Name = "EffectSummary", Text = "装备效果：无" };
        _effectLabel.AddThemeFontSizeOverride("font_size", 11);
        _effectLabel.AddThemeColorOverride("font_color", new Color(0.6f, 0.85f, 1f));
        _box.AddChild(_effectLabel);

        // 背包分区（负重 + 物品行 + 空背包提示）
        AddSectionHeader("背包");
        _weightLabel = new Label { Name = "WeightLabel", Text = "负重 —" };
        _weightLabel.AddThemeFontSizeOverride("font_size", 12);
        _box.AddChild(_weightLabel);

        _bagRows = new VBoxContainer { Name = "BagRows", SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _bagRows.AddThemeConstantOverride("separation", 3);
        _box.AddChild(_bagRows);

        _bagEmptyLabel = new Label { Name = "BagEmpty", Text = "（空）" };
        _bagEmptyLabel.AddThemeFontSizeOverride("font_size", 12);
        _bagEmptyLabel.AddThemeColorOverride("font_color", SectionColor);
        _box.AddChild(_bagEmptyLabel);

        _bioLabel = new Label
        {
            Text = "",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            CustomMinimumSize = new Vector2(200, 0),
        };
        _bioLabel.AddThemeFontSizeOverride("font_size", 13);
        _bioLabel.AddThemeColorOverride("font_color", new Color(0.75f, 0.78f, 0.82f));
        _box.AddChild(_bioLabel);

        Visible = false;

        simulation.EventBus.Subscribe<NeedsChangedEvent>(OnNeedsChanged);
        simulation.EventBus.Subscribe<TaskStateChangedEvent>(OnTaskStateChanged);
    }

    /*****
    Date: 2026-09-25
    Name: ShowSelection
    Description: 显示选择集：主选角色显示详情（多人时额外显示「已选 N 人」）；空选择则隐藏面板。
    *****/
    public void ShowSelection(IReadOnlyList<CharacterSim> selection, CharacterSim? primary)
    {
        ShowCharacter(primary);

        if (_selectionLabel != null)
        {
            _selectionLabel.Visible = selection.Count > 1;
            _selectionLabel.Text = selection.Count > 1 ? $"已选 {selection.Count} 人（显示主选）" : "";
        }
    }

    /*****
    Date: 2026-09-25
    Name: ShowCharacter
    Description: 显示指定角色的面板（订阅其状态变更）；传 null 时隐藏面板并注销订阅。
    *****/
    public void ShowCharacter(CharacterSim? character)
    {
        if (_character != null) _character.StateChanged -= OnStateChanged;
        _character = character;

        if (character == null)
        {
            Visible = false;
            return;
        }

        character.StateChanged += OnStateChanged;
        Visible = true;
        Refresh();
    }

    /*****
    Date: 2026-09-25
    Name: OnStateChanged
    Description: 角色状态变化时刷新面板显示。
    *****/
    private void OnStateChanged(CharacterSim character, CharacterState newState) => Refresh();

    /*****
    Date: 2026-09-26
    Name: OnNeedsChanged
    Description: 需求推进事件回调：仅刷新当前显示角色的指标行（每游戏分钟一次；不重建控件）。
    *****/
    private void OnNeedsChanged(NeedsChangedEvent e)
    {
        if (_character == null || !ReferenceEquals(e.Character, _character)) return;
        RefreshNeeds(e.Needs);
    }

    /*****
    Date: 2026-09-26
    Name: OnTaskStateChanged
    Description: 任务状态变更回调：只关心**本人**的个人任务（穿戴 / 卸下 / 取用 / 拾取）走到终态时的刷新——这些任务的产物（装备槽与背包内容）不经过需求事件，需在此补一次全量刷新，否则「装备/卸下」要等下次状态变化才显形。
    *****/
    private void OnTaskStateChanged(TaskStateChangedEvent e)
    {
        if (_character == null || !ReferenceEquals(e.Task.Owner, _character)) return;
        if (e.NewState is not (TaskState.Done or TaskState.Cancelled)) return;
        Refresh();
    }

    /*****
    Date: 2026-09-26
    Name: Refresh
    Description: 刷新姓名（队长后缀）、行为状态与当前任务、专长、年龄/性别、八项需求与生命值指标、装备槽与背包、背景介绍。
    *****/
    private void Refresh()
    {
        if (_character == null || _nameLabel == null || _stateLabel == null
            || _specialtyLabel == null || _profileLabel == null) return;

        string name = _character.Def?.DisplayName ?? "未知角色";
        if (_character.Def?.IsCaptain == true) name += " ★队长";
        _nameLabel.Text = name;

        _stateLabel.Text = _character.State switch
        {
            CharacterState.Idle => "状态：待机",
            CharacterState.Moving => $"状态：前往 {_character.CurrentTask?.Target?.Def?.DisplayName ?? "目标"}",
            CharacterState.Working => $"状态：维修 {_character.CurrentTask?.Target?.Def?.DisplayName ?? "目标"}",
            CharacterState.Interrupted => "状态：被打断",
            _ => "状态：待机",
        };

        _specialtyLabel.Text = $"专长：{SpecialtyName(_character.Def?.Specialty)}";
        _profileLabel.Text = $"年龄：{_character.Def?.Age ?? 0}　性别：{GenderName(_character.Def?.Gender)}";

        RefreshNeeds(_simulation?.NeedSystem.Read(_character) ?? NeedSnapshot.Full);
        RefreshEquipment();

        if (_bioLabel != null) _bioLabel.Text = _character.Def?.Bio ?? "";
    }

    /*****
    Date: 2026-09-26
    Name: RefreshNeeds
    Description: 按快照刷新八项需求指标行（数值文本与颜色带）与生命值行；生命值取自当前角色。
    *****/
    private void RefreshNeeds(NeedSnapshot needs)
    {
        foreach (KeyValuePair<NeedId, (ProgressBar Bar, Label Value, StyleBoxFlat Fill)> entry in _needRows)
        {
            float value = needs.Get(entry.Key);
            (ProgressBar bar, Label label, StyleBoxFlat fill) = entry.Value;
            bar.Value = value;
            label.Text = FormatValue(value);
            fill.BgColor = ColorFor(entry.Key, value);
        }

        if (_healthBar != null && _character != null)
        {
            _healthBar.Value = _character.Health;
            if (_healthValue != null) _healthValue.Text = FormatValue(_character.Health);
        }
    }

    /*****
    Date: 2026-09-26
    Name: RefreshEquipment
    Description: 刷新装备槽、装备效果摘要与背包分区：逐槽显示已装备物品名（空槽显示「—」并隐藏卸下按钮）、效果摘要（效率/移速/额外容量）、负重「当前/上限 kg」（超重红字）与背包物品行。
    *****/
    private void RefreshEquipment()
    {
        if (_character == null) return;
        CharacterInventory inventory = _character.Inventory;

        foreach (KeyValuePair<EquipmentSlot, (Label Value, Button Unequip)> entry in _equipRows)
        {
            IItemDef? equipped = inventory.GetEquipped(entry.Key);
            (Label value, Button unequip) = entry.Value;
            value.Text = equipped?.DisplayName ?? "—";
            value.AddThemeColorOverride("font_color", equipped != null ? Colors.White : SectionColor);
            unequip.Visible = equipped != null;
        }

        if (_weightLabel != null)
        {
            _weightLabel.Text = $"负重 {inventory.TotalWeightKg:0.0}/{inventory.CapacityKg:0.0} kg";
            _weightLabel.AddThemeColorOverride("font_color", inventory.IsOverloaded ? DangerColor : Colors.White);
        }

        RefreshEffectSummary(inventory);
        RefreshBagRows(inventory);
    }

    /*****
    Date: 2026-09-26
    Name: RefreshEffectSummary
    Description: 刷新装备效果摘要行：把已装备物品提供的加成汇总为「效率 +10%　移速 +20%　容量 +20.0 kg」（无任何加成时显示「无」）。容量一项只算**装备额外提供**的部分（基础负重不计入）。
    *****/
    private void RefreshEffectSummary(CharacterInventory inventory)
    {
        if (_effectLabel == null) return;

        var parts = new List<string>();
        float work = inventory.WorkSpeedBonusPercent;
        float move = inventory.MoveSpeedBonusPercent;
        float extraCapacity = inventory.CapacityKg - inventory.BaseCapacityKg;
        if (work > 0f) parts.Add($"效率 +{work * 100f:0.#}%");
        if (move > 0f) parts.Add($"移速 +{move * 100f:0.#}%");
        if (extraCapacity > 0f) parts.Add($"容量 +{extraCapacity:0.#} kg");

        _effectLabel.Text = parts.Count > 0 ? $"装备效果：{string.Join("　", parts)}" : "装备效果：无";
    }

    /*****
    Date: 2026-09-26
    Name: RefreshBagRows
    Description: 整体重建背包物品行：每行「名称 ×数量（重量 kg）」，可穿戴物品额外带「装备」按钮（点击经模拟层装备并刷新）；无物品时显示「（空）」提示。
    *****/
    private void RefreshBagRows(CharacterInventory inventory)
    {
        if (_bagRows == null) return;

        foreach (Node child in _bagRows.GetChildren())
        {
            _bagRows.RemoveChild(child);
            child.QueueFree();
        }

        bool any = false;
        foreach (InventoryEntry entry in inventory.Entries)
        {
            any = true;
            var row = new HBoxContainer { Name = $"Bag_{entry.Def.Id}", SizeFlagsHorizontal = SizeFlags.ExpandFill };
            row.AddThemeConstantOverride("separation", 6);

            var label = new Label
            {
                Text = $"{entry.Def.DisplayName} ×{entry.Count}（{MathF.Max(0f, entry.Def.WeightKg) * entry.Count:0.0}kg）",
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                VerticalAlignment = VerticalAlignment.Center,
            };
            label.AddThemeFontSizeOverride("font_size", 12);
            row.AddChild(label);

            if (entry.Def.EquipSlot != EquipmentSlot.None)
            {
                IItemDef def = entry.Def;
                var equip = new Button { Text = "装备", CustomMinimumSize = new Vector2(46, 20) };
                equip.AddThemeFontSizeOverride("font_size", 11);
                equip.TooltipText = "穿戴为角色行为，需耗时（生效后本行显示会变化）";
                equip.Pressed += () => EquipItem(def);
                row.AddChild(equip);
            }

            _bagRows.AddChild(row);
        }

        if (_bagEmptyLabel != null) _bagEmptyLabel.Visible = !any;
    }

    /*****
    Date: 2026-09-26
    Name: EquipItem
    Description: 请求装备指定物品：**任务化**——经模拟层门面 `OrderEquip` 下一条个人穿戴任务（就地耗时后才生效），生效/取消时由任务状态事件触发刷新；不再瞬时写入。
    *****/
    private void EquipItem(IItemDef def)
    {
        if (_simulation == null || _character == null) return;
        _simulation.OrderEquip(_character, def);
    }

    /*****
    Date: 2026-09-26
    Name: UnequipItem
    Description: 请求卸下指定槽位：**任务化**——经模拟层门面 `OrderUnequip` 下一条个人卸下任务（就地耗时后才生效；背包放不下时装备就地落地），完成/取消时由任务状态事件触发刷新。
    *****/
    private void UnequipItem(EquipmentSlot slot)
    {
        if (_simulation == null || _character == null) return;
        _simulation.OrderUnequip(_character, slot);
    }

    /*****
    Date: 2026-09-26
    Name: BuildEquipmentRow
    Description: 构建一条装备槽行（「槽名 + 物品名 + 卸下按钮」）并挂到内容容器；返回物品名标签与卸下按钮（供刷新时改写文本与显隐）。
    *****/
    private (Label Value, Button Unequip) BuildEquipmentRow(EquipmentSlot slot)
    {
        var row = new HBoxContainer { Name = $"Equip_{slot}" };
        row.AddThemeConstantOverride("separation", 6);

        row.AddChild(new Label
        {
            Text = SlotName(slot),
            CustomMinimumSize = new Vector2(52, 18),
            VerticalAlignment = VerticalAlignment.Center,
        });

        var value = new Label
        {
            Text = "—",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            VerticalAlignment = VerticalAlignment.Center,
        };
        value.AddThemeFontSizeOverride("font_size", 12);
        row.AddChild(value);

        EquipmentSlot captured = slot;
        var unequip = new Button { Text = "卸下", CustomMinimumSize = new Vector2(46, 20), Visible = false };
        unequip.AddThemeFontSizeOverride("font_size", 11);
        unequip.TooltipText = "卸下为角色行为，需耗时；背包放不下时会就地落地";
        unequip.Pressed += () => UnequipItem(captured);
        row.AddChild(unequip);

        _box?.AddChild(row);
        return (value, unequip);
    }

    /*****
    Date: 2026-09-26
    Name: BuildMetricRow
    Description: 构建一条指标行（「名称 + 细条 + 数值」）并挂到内容容器；返回细条、数值标签与其填充样式（供刷新时改数值与颜色）。细条内容边距清零，否则主题默认边距会把条撑高。
    *****/
    private (ProgressBar Bar, Label Value, StyleBoxFlat Fill) BuildMetricRow(string name, Color fillColor)
    {
        var row = new HBoxContainer { Name = $"Row_{name}" };
        row.AddThemeConstantOverride("separation", 6);

        row.AddChild(new Label
        {
            Text = name,
            CustomMinimumSize = new Vector2(52, 18),
            VerticalAlignment = VerticalAlignment.Center,
        });

        var fill = new StyleBoxFlat { BgColor = fillColor };
        var background = new StyleBoxFlat { BgColor = BarBackgroundColor };
        ClearMargins(fill);
        ClearMargins(background);

        var bar = new ProgressBar
        {
            MinValue = 0,
            MaxValue = NeedSnapshot.Max,
            Value = NeedSnapshot.Max,
            ShowPercentage = false,
            CustomMinimumSize = new Vector2(78, 8),
            SizeFlagsVertical = SizeFlags.ShrinkCenter,
        };
        bar.AddThemeStyleboxOverride("background", background);
        bar.AddThemeStyleboxOverride("fill", fill);
        row.AddChild(bar);

        var value = new Label
        {
            Text = FormatValue(NeedSnapshot.Max),
            CustomMinimumSize = new Vector2(58, 18),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
        };
        value.AddThemeFontSizeOverride("font_size", 12);
        row.AddChild(value);

        _box?.AddChild(row);
        return (bar, value, fill);
    }

    /*****
    Date: 2026-09-26
    Name: AddSectionHeader
    Description: 添加一条分区标题（暗色小字，用于分组提示）；节点名显式取「Section_标题」，便于查找与自动化验证（代码创建节点的自动名不可依赖）。
    *****/
    private void AddSectionHeader(string title)
    {
        var label = new Label { Name = $"Section_{title}", Text = title, CustomMinimumSize = new Vector2(0, 16) };
        label.AddThemeFontSizeOverride("font_size", 12);
        label.AddThemeColorOverride("font_color", SectionColor);
        _box?.AddChild(label);
    }

    /*****
    Date: 2026-09-26
    Name: ClearMargins
    Description: 清空 StyleBoxFlat 的内容边距（细条专用：避免主题默认边距把 8px 的条撑高）。
    *****/
    private static void ClearMargins(StyleBoxFlat style)
    {
        style.ContentMarginLeft = 0f;
        style.ContentMarginRight = 0f;
        style.ContentMarginTop = 0f;
        style.ContentMarginBottom = 0f;
    }

    /*****
    Date: 2026-09-26
    Name: FormatValue
    Description: 数值显示文本（取整 + 上限，如「80/100」）。
    *****/
    private static string FormatValue(float value) => $"{(int)value}/{NeedSnapshot.Max:0}";

    /*****
    Date: 2026-09-26
    Name: ColorFor
    Description: 按需求方向取颜色带：衰减型（越大越好）<30 红 / 30~59 黄 / ≥60 绿；增长型（排泄，越小越好）反向 ≥70 红 / 40~69 黄 / <40 绿。
    *****/
    private static Color ColorFor(NeedId id, float value)
    {
        if (NeedCatalog.Direction(id) == NeedDirection.Increasing)
        {
            if (value >= 70f) return DangerColor;
            return value >= 40f ? WarnColor : HealthyColor;
        }

        if (value < 30f) return DangerColor;
        return value < 60f ? WarnColor : HealthyColor;
    }

    /*****
    Date: 2026-09-26
    Name: NeedName
    Description: 需求显示名称（表现层文案）。
    *****/
    private static string NeedName(NeedId id) => id switch
    {
        NeedId.Oxygen => "呼吸",
        NeedId.Food => "进食",
        NeedId.Rest => "休息",
        NeedId.Water => "饮水",
        NeedId.Excretion => "排泄",
        NeedId.Urination => "排尿",
        NeedId.Hygiene => "卫生",
        NeedId.Social => "社交",
        _ => "娱乐",
    };

    /*****
    Date: 2026-09-26
    Name: SlotName
    Description: 装备槽显示名称（表现层文案）。
    *****/
    private static string SlotName(EquipmentSlot slot) => slot switch
    {
        EquipmentSlot.Head => "头部",
        EquipmentSlot.Body => "身体",
        EquipmentSlot.Back => "背部",
        EquipmentSlot.Hands => "手部",
        EquipmentSlot.Feet => "脚部",
        _ => "—",
    };

    /*****
    Date: 2026-09-25
    Name: SpecialtyName
    Description: 专长显示名称；定义缺失时显示「—」。
    *****/
    private static string SpecialtyName(Specialty? specialty) => specialty switch
    {
        Specialty.Maintenance => "维修",
        Specialty.SystemOperation => "系统操作",
        Specialty.FieldWork => "外勤",
        _ => "—",
    };

    /*****
    Date: 2026-09-25
    Name: GenderName
    Description: 性别显示名称；定义缺失时显示「—」。
    *****/
    private static string GenderName(Gender? gender) => gender switch
    {
        Gender.Male => "男",
        Gender.Female => "女",
        _ => "—",
    };

    /*****
    Date: 2026-09-26
    Name: _ExitTree
    Description: 节点退出时注销事件订阅（角色状态变更 + 需求推进 + 任务状态变更），防止悬空回调。
    *****/
    public override void _ExitTree()
    {
        if (_character != null) _character.StateChanged -= OnStateChanged;
        _simulation?.EventBus.Unsubscribe<NeedsChangedEvent>(OnNeedsChanged);
        _simulation?.EventBus.Unsubscribe<TaskStateChangedEvent>(OnTaskStateChanged);
    }
}