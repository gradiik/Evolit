using Godot;

namespace Evolit.UI;

public static class EvolitIcons
{
    private const string Root = "res://assets/ui/icons/";

    public static Texture2D? Load(string relativePath)
    {
        return GD.Load<Texture2D>(Root + relativePath);
    }
}
