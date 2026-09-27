using Evolit.Core;
using Godot;

namespace Evolit.UI.Game;

public readonly record struct GameViewState(
    WorldShape Shape,
    Vector2 CameraPosition,
    float Zoom,
    Vector3 PlanetOrbit,
    float PlanetDistance,
    string? SelectedEntityId,
    long? SelectedCellId)
{
    public static GameViewState Flat(Vector2 cameraPosition, float zoom, string? selectedEntityId) =>
        new(WorldShape.Flat, cameraPosition, zoom, Vector3.Zero, 0f, selectedEntityId, null);

    public static GameViewState Planet(
        Vector3 orbit,
        float distance,
        string? selectedEntityId,
        CellId? selectedCellId) =>
        new(
            WorldShape.Planet,
            Vector2.Zero,
            1f,
            orbit,
            distance,
            selectedEntityId,
            selectedCellId?.Value);
}
