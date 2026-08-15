using System;

namespace DeskPet.Models;

/// <summary>Animation action for the pet. Mirrors PetAction in the macOS app.</summary>
public enum PetAction
{
    Idle,
    Walk,
    Sleep,
    Eat,
    Happy,
    Hurt,
    Drag,
    Music,
    Climb,
    Hang,
    Fall,
    Dizzy,
    Yawn,
    Home,
}

public static class PetActionExtensions
{
    /// <summary>Folder name used on disk (matches PET.md).</summary>
    public static string FolderName(this PetAction action) => action.ToString().ToLowerInvariant();

    public static double DefaultFps(this PetAction action) => action switch
    {
        PetAction.Walk => 12,
        PetAction.Music or PetAction.Drag => 14,
        PetAction.Climb => 10,
        PetAction.Hang => 5,
        PetAction.Fall => 20,
        PetAction.Dizzy or PetAction.Yawn => 8,
        PetAction.Idle or PetAction.Sleep => 6,
        _ => 10,
    };

    public static bool LoopsByDefault(this PetAction action) => action switch
    {
        PetAction.Eat or PetAction.Fall or PetAction.Happy or PetAction.Hurt or PetAction.Yawn => false,
        _ => true,
    };
}

public enum PetLifeState
{
    Roaming,
    AtHome,
}

public enum PetMovementMode
{
    Floor,
    ApproachingEdge,
    Climbing,
    Hanging,
    Falling,
    GoingToDock,
    DockCrawl,
    GoingToWindow,
    WindowClimb,
    WindowHang,
}
