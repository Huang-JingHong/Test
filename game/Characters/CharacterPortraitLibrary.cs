using Godot;
using RelayStation.Core.Characters;

namespace RelayStation.Game.Characters;

/*****
Date: 2026-09-25
Name: CharacterPortraitLibrary
Description: 角色头像贴图解析（表现层）；按「CharacterDef.PortraitTexturePath + _表情后缀」约定取图（如 char_field_portrait.png + smile → char_field_portrait_smile.png），结果缓存；表情贴图缺失时回退默认（平静）头像，未配置头像路径时返回 null 由调用方回退占位图。
*****/
public static class CharacterPortraitLibrary
{
    /*****
    Date: 2026-09-25
    Name: _cache
    Description: 解析结果缓存（路径 → 贴图，缺失路径缓存 null 避免重复探测）。
    *****/
    private static readonly Dictionary<string, Texture2D?> _cache = new();

    /*****
    Date: 2026-09-25
    Name: Get
    Description: 取指定角色的指定表情头像；未配置头像路径返回 null，表情贴图缺失回退默认表情。
    *****/
    public static Texture2D? Get(CharacterDef? def, PortraitExpression expression)
    {
        if (def == null || string.IsNullOrEmpty(def.PortraitTexturePath)) return null;

        string path = ResolvePath(def.PortraitTexturePath, expression);
        if (_cache.TryGetValue(path, out Texture2D? cached)) return cached;

        Texture2D? texture = ResourceLoader.Exists(path) ? GD.Load<Texture2D>(path) : null;
        _cache[path] = texture;

        // 表情贴图缺失：回退默认（平静）头像；默认头像也缺失时由调用方回退占位图
        if (texture == null && expression != PortraitExpression.Calm)
            return Get(def, PortraitExpression.Calm);

        return texture;
    }

    /*****
    Date: 2026-09-25
    Name: ResolvePath
    Description: 按约定解析表情贴图路径：默认表情原样返回，其余在扩展名前插入「_表情后缀」（如 ..._portrait.png → ..._portrait_smile.png）。
    *****/
    private static string ResolvePath(string portraitPath, PortraitExpression expression)
    {
        string suffix = Suffix(expression);
        if (suffix.Length == 0) return portraitPath;

        int dot = portraitPath.LastIndexOf('.');
        return dot < 0
            ? portraitPath + "_" + suffix
            : portraitPath[..dot] + "_" + suffix + portraitPath[dot..];
    }

    /*****
    Date: 2026-09-25
    Name: Suffix
    Description: 表情对应的文件名后缀；默认（平静）表情返回空串表示不加后缀。
    *****/
    private static string Suffix(PortraitExpression expression) => expression switch
    {
        PortraitExpression.Smile => "smile",
        PortraitExpression.Angry => "angry",
        PortraitExpression.Cry => "cry",
        PortraitExpression.Hurt => "hurt",
        PortraitExpression.Panic => "panic",
        _ => "",
    };
}
