using Godot;

namespace RelayStation.Game.Input;

/*****
Date: 2026-09-25
Name: SelectionBoxView
Description: 框选矩形可视化（世界坐标绘制）；由 InputPicker 的框选事件驱动更新，宽高为 0 时不绘制。置于角色/设施之上（ZIndex=10）。
*****/
public partial class SelectionBoxView : Node2D
{
    /*****
    Date: 2026-09-25
    Name: FillColor
    Description: 框内填充色（半透明）。
    *****/
    private static readonly Color FillColor = new(1f, 0.85f, 0.2f, 0.12f);

    /*****
    Date: 2026-09-25
    Name: BorderColor
    Description: 边框颜色。
    *****/
    private static readonly Color BorderColor = new(1f, 0.85f, 0.2f, 0.9f);

    /*****
    Date: 2026-09-25
    Name: _rect
    Description: 当前框选矩形（世界坐标）。
    *****/
    private Rect2 _rect;

    /*****
    Date: 2026-09-25
    Name: _active
    Description: 是否有有效框选矩形（宽或高大于阈值）。
    *****/
    private bool _active;

    /*****
    Date: 2026-09-25
    Name: SelectionBoxView
    Description: 构造函数；提升绘制层级到角色/设施之上。
    *****/
    public SelectionBoxView()
    {
        ZIndex = 10;
    }

    /*****
    Date: 2026-09-25
    Name: SetBox
    Description: 更新框选矩形（世界坐标）；宽高为 0（或极小）视为无框选并清除显示。
    *****/
    public void SetBox(Rect2 rect)
    {
        _rect = rect;
        _active = rect.Size.X > 0.5f || rect.Size.Y > 0.5f;
        QueueRedraw();
    }

    /*****
    Date: 2026-09-25
    Name: _Draw
    Description: 绘制框选矩形（半透明填充 + 边框）。
    *****/
    public override void _Draw()
    {
        if (!_active) return;
        DrawRect(_rect, FillColor, true);
        DrawRect(_rect, BorderColor, false, 1.5f);
    }
}
