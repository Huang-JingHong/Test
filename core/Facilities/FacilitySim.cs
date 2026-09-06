using Godot;

namespace RelayStation.Core.Facilities;

/*****
Date: 2026-09-06
Name: FacilitySim
Description: 设施模拟对象；持有设施定义、状态与占地信息，提供占地格查询与状态迁移（含状态变更事件）。阶段二挂点：OnOperationalTick（供电/制氧等运行效果）。
*****/
public sealed class FacilitySim
{
    /*****
    Date: 2026-09-06
    Name: Def
    Description: 设施的静态定义（名称、占地格、修复耗时等）；引擎外单元测试等场景可为 null。
    *****/
    public FacilityDef? Def { get; }

    /*****
    Date: 2026-09-06
    Name: State
    Description: 设施当前状态（初始状态取自 Def，Def 缺省为 Damaged）。
    *****/
    public FacilityState State { get; private set; }

    /*****
    Date: 2026-09-06
    Name: OriginCell
    Description: 设施占地范围的原点格子坐标（左上角）。
    *****/
    public Vector2I OriginCell { get; }

    /*****
    Date: 2026-09-06
    Name: Size
    Description: 占地尺寸（取自 Def；Def 缺省为 2×2）。
    *****/
    public Vector2I Size => Def?.Size ?? new Vector2I(2, 2);

    /*****
    Date: 2026-09-06
    Name: StateChanged
    Description: 设施状态变更时触发的事件（参数：设施本体、新状态）。
    *****/
    public event Action<FacilitySim, FacilityState>? StateChanged;

    /*****
    Date: 2026-09-06
    Name: FacilitySim
    Description: 构造函数；以指定定义与占地原点创建设施。
    *****/
    public FacilitySim(FacilityDef? def, Vector2I originCell)
    {
        Def = def;
        OriginCell = originCell;
        State = def?.InitialState ?? FacilityState.Damaged;
    }

    /*****
    Date: 2026-09-06
    Name: SetState
    Description: 迁移设施状态；状态实际变化时触发 StateChanged 事件。
    *****/
    public void SetState(FacilityState newState)
    {
        if (State == newState) return;
        State = newState;
        StateChanged?.Invoke(this, newState);
    }

    /*****
    Date: 2026-09-06
    Name: OccupiedCells
    Description: 枚举设施占地覆盖的所有格子。
    *****/
    public IEnumerable<Vector2I> OccupiedCells()
    {
        Vector2I size = Size;
        for (int dy = 0; dy < size.Y; dy++)
        {
            for (int dx = 0; dx < size.X; dx++)
            {
                yield return new Vector2I(OriginCell.X + dx, OriginCell.Y + dy);
            }
        }
    }

    /*****
    Date: 2026-09-06
    Name: Occupies
    Description: 判断指定格子是否被本设施占地覆盖。
    *****/
    public bool Occupies(Vector2I cell)
    {
        Vector2I size = Size;
        return cell.X >= OriginCell.X && cell.X < OriginCell.X + size.X
            && cell.Y >= OriginCell.Y && cell.Y < OriginCell.Y + size.Y;
    }
}
