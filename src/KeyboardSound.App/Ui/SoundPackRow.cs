namespace KeyboardSound.App.Ui;

/// <summary>Plain snapshot for data-binding a soundpack row in the list. The list is fully
/// rebuilt on every relevant change (packs are few, this is cheap) so no INotifyPropertyChanged
/// plumbing is needed here.</summary>
public sealed class SoundPackRow
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Description { get; init; }
    public required int SampleCount { get; init; }
    public required bool IsActive { get; init; }
    public required bool IsFavorite { get; init; }
}
