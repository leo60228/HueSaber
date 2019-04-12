using CustomUI.MenuButton;

namespace HueSaber
{
    public class HueUI
    {
        public static bool Created { get; protected set; } = false;
        public static void CreateUI()
        {
            if (Created) return;
            Created = true;

            MenuButtonUI.AddButton("Sync Hue", () => Plugin.SyncHue());
        }
    }
}
