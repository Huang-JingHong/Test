namespace RelayStation.Game.Characters;

/*****
Date: 2026-09-25
Name: PortraitExpression
Description: 头像表情枚举（表现层概念）；Calm 为默认平静表情（对应角色默认头像文件），其余表情按「默认头像路径 + _后缀」约定解析对应贴图，缺失时自动回退默认头像。
*****/
public enum PortraitExpression { Calm, Smile, Angry, Cry, Hurt, Panic }
