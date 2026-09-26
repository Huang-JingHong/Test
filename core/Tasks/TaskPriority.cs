namespace RelayStation.Core.Tasks;

/*****
Date: 2026-09-25
Name: TaskPriority
Description: 任务优先级（对齐《缺氧》/《环世界》的任务优先度）；用于 TaskBoard.PickFor 在「新任务认领」与「加入进行中任务」之间统一择优——数值高者先被认领，同数值按提交先后（先到先得）。
枚举值即排序数值：P1~P9 对应玩家可见的 1~9 档（默认 P5），Urgent（紧急）单列为最高档且数值取 10 以保持「数值越大越优先」的单调性。
「紧急」语义：不受「人物中止」的忽略清单影响——小人只要没死就得干（将来接入需求惩罚时同样 bypass），见 TaskBoard.PickFor。
*****/
public enum TaskPriority
{
    /*****
    Date: 2026-09-26
    Name: P1
    Description: 优先级 1（最低，可长期延后）。
    *****/
    P1 = 1,

    /*****
    Date: 2026-09-26
    Name: P2
    Description: 优先级 2。
    *****/
    P2 = 2,

    /*****
    Date: 2026-09-26
    Name: P3
    Description: 优先级 3。
    *****/
    P3 = 3,

    /*****
    Date: 2026-09-26
    Name: P4
    Description: 优先级 4。
    *****/
    P4 = 4,

    /*****
    Date: 2026-09-26
    Name: P5
    Description: 优先级 5（玩家下达指令的默认档）。
    *****/
    P5 = 5,

    /*****
    Date: 2026-09-26
    Name: P6
    Description: 优先级 6。
    *****/
    P6 = 6,

    /*****
    Date: 2026-09-26
    Name: P7
    Description: 优先级 7。
    *****/
    P7 = 7,

    /*****
    Date: 2026-09-26
    Name: P8
    Description: 优先级 8。
    *****/
    P8 = 8,

    /*****
    Date: 2026-09-26
    Name: P9
    Description: 优先级 9（数字档中的最高档）。
    *****/
    P9 = 9,

    /*****
    Date: 2026-09-26
    Name: Urgent
    Description: 紧急（最高优先，必然被处理）；显示为「紧急」而非数字，且不受「人物中止」忽略清单影响。
    *****/
    Urgent = 10,
}
