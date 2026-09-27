using System;
using Evolit.Core;
using Evolit.Save;
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

    public GameViewSaveState ToSaveState() => new()
    {
        Shape = (int)Shape,
        FlatCameraX = CameraPosition.X,
        FlatCameraY = CameraPosition.Y,
        FlatZoom = Zoom,
        PlanetOrbitX = PlanetOrbit.X,
        PlanetOrbitY = PlanetOrbit.Y,
        PlanetOrbitZ = PlanetOrbit.Z,
        PlanetDistance = PlanetDistance,
        SelectedEntityId = SelectedEntityId,
        SelectedCellId = SelectedCellId
    };

    public static GameViewState? FromSaveState(GameViewSaveState? state)
    {
        if (state is null)
            return null;

        var shape = state.Shape is >= 0 and <= byte.MaxValue && Enum.IsDefined((WorldShape)state.Shape)
            ? (WorldShape)state.Shape
            : WorldShape.Flat;

        return shape == WorldShape.Planet
            ? Planet(
                new Vector3(state.PlanetOrbitX, state.PlanetOrbitY, state.PlanetOrbitZ),
                state.PlanetDistance,
                state.SelectedEntityId,
                state.SelectedCellId.HasValue ? new CellId(state.SelectedCellId.Value) : null)
            : Flat(
                new Vector2(state.FlatCameraX, state.FlatCameraY),
                state.FlatZoom <= 0f ? 1f : state.FlatZoom,
                state.SelectedEntityId);
    }
}
