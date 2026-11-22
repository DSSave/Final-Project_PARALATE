/// <summary>
/// ชื่อ Scene ทั้งเกม: ค่าจริงอยู่ใน Assets/Resources/SceneFlow.asset (ปรับใน Inspector ได้)
/// โค้ดยังเรียก SceneNames.Tutorial / SceneNames.Base / ... ได้เหมือนเดิม
/// </summary>
public static class SceneNames
{
    public static string MainMenu => SceneFlow.Current.mainMenu;
    public static string Base => SceneFlow.Current.hub;

    /// <summary>Scene ที่ New Game พาไป</summary>
    public static string Tutorial => SceneFlow.Current.newGame;

    /// <summary>Scene หน้า Options ที่โหลดแบบ Additive ซ้อนทับ MainMenu</summary>
    public static string Options => SceneFlow.Current.options;

    /// <summary>ด่านที่เลือกเล่นได้จากฮับ เรียงตาม index ที่ LevelManager.LoadLevel ใช้</summary>
    public static string[] Levels => SceneFlow.Levels;
}
