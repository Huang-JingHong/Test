using Godot;

namespace RelayStation.Game.Save;

/*****
Date: 2026-09-25
Name: SaveGameService
Description: 存档读写服务；管理 3 个存档槽（user://saves/slot_1..3.tres）的存在性检查、写入与读取。user:// 在编辑器运行时位于 %APPDATA%\Godot\app_userdata\Online_AI_Town\。
*****/
public static class SaveGameService
{
    /*****
    Date: 2026-09-25
    Name: SlotCount
    Description: 存档槽总数。
    *****/
    public const int SlotCount = 3;

    /*****
    Date: 2026-09-25
    Name: SaveDir
    Description: 存档目录（user:// 协议）。
    *****/
    private const string SaveDir = "user://saves";

    /*****
    Date: 2026-09-25
    Name: SlotPath
    Description: 指定槽位的存档文件路径。
    *****/
    public static string SlotPath(int slot) => $"{SaveDir}/slot_{slot}.tres";

    /*****
    Date: 2026-09-25
    Name: SlotExists
    Description: 判断指定槽位是否已有存档。
    *****/
    public static bool SlotExists(int slot) => ResourceLoader.Exists(SlotPath(slot));

    /*****
    Date: 2026-09-25
    Name: SaveToSlot
    Description: 将存档资源写入指定槽位；自动创建存档目录。
    *****/
    public static void SaveToSlot(int slot, SaveGameResource save)
    {
        DirAccess.MakeDirRecursiveAbsolute(SaveDir);
        Error err = ResourceSaver.Save(save, SlotPath(slot));
        if (err != Error.Ok) GD.PushError($"[Save] 保存到槽 {slot} 失败：{err}");
        else GD.Print($"[Save] 已保存到槽 {slot}。");
    }

    /*****
    Date: 2026-09-25
    Name: LoadFromSlot
    Description: 从指定槽位读取存档；槽位不存在或文件损坏时返回 null。
    *****/
    public static SaveGameResource? LoadFromSlot(int slot)
    {
        if (!SlotExists(slot)) return null;
        var save = GD.Load<SaveGameResource>(SlotPath(slot));
        if (save == null) GD.PushError($"[Save] 读取槽 {slot} 失败（文件损坏或类型不符）。");
        return save;
    }
}
