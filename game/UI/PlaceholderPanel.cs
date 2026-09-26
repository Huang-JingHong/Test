using Godot;

namespace RelayStation.Game.UI;

/*****
Date: 2026-09-25
Name: PlaceholderPanel
Description: 通用占位面板；构造传入标题，正文显示「未实现」——未来菜单入口（氧气/政治等）共用，非一类一个类。功能落地时替换为真实面板类并改装配处工厂即可。
*****/
public partial class PlaceholderPanel : PanelContainer
{
    /*****
    Date: 2026-09-25
    Name: PlaceholderPanel
    Description: 构造函数；按标题构建标题栏与「未实现」正文。
    *****/
    public PlaceholderPanel(string title)
    {
        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 6);
        AddChild(box);

        var titleLabel = new Label { Text = title };
        titleLabel.AddThemeFontSizeOverride("font_size", 18);
        box.AddChild(titleLabel);

        box.AddChild(new Label
        {
            Text = "未实现",
            CustomMinimumSize = new Vector2(180, 40),
        });
    }
}
