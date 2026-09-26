using Godot;

namespace RelayStation.Core.Items;

/*****
Date: 2026-09-27
Name: IItemSource
Description: 物品来源抽象（拾取类任务的取货口径）；把「从哪儿取」与「怎么取」解耦，使拾取任务既能面向地面物品堆（GroundItemSource），也能面向后续的容器设施（柜子等，实现本接口即可接入，任务侧零改动）。
约定：来源是有限且可数的——AvailableCount 即此刻可取件数；TryTake 必须原子地扣减来源（数量不足或失败时不产生任何改动，taken 为 0）；Return 用于转移失败时把已取出的物品原样放回。
*****/
public interface IItemSource
{
    /*****
    Date: 2026-09-27
    Name: Def
    Description: 来源内物品的静态定义（同一来源内的物品同种）。
    *****/
    IItemDef Def { get; }

    /*****
    Date: 2026-09-27
    Name: Cell
    Description: 来源所在格（拾取任务的作业格：走到这里取货）。
    *****/
    Vector2I Cell { get; }

    /*****
    Date: 2026-09-27
    Name: AvailableCount
    Description: 此刻可取的件数；为 0 表示来源已空（拾取任务据此判定失效）。
    *****/
    int AvailableCount { get; }

    /*****
    Date: 2026-09-27
    Name: TryTake
    Description: 从来源取出至多 count 件；实际取出量经 taken 返回。数量非法、来源已空或扣减失败时不产生任何改动并返回 false。
    *****/
    bool TryTake(int count, out int taken);

    /*****
    Date: 2026-09-27
    Name: Return
    Description: 把 count 件物品放回来源（原格原位；供「取出后装入背包失败」的回退路径，保证物品不凭空消失）；count ≤ 0 时忽略。
    *****/
    void Return(int count);
}