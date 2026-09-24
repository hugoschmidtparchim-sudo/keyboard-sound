namespace KeyboardSound.App.Ui;

/// <summary>Plain snapshot for data-binding an individual sound row. Like <see cref="SoundPackRow"/>,
/// the list is fully rebuilt on every relevant change rather than using INotifyPropertyChanged.</summary>
public sealed class SoundRow
{
    public required string Id { get; init; }
    public required string DisplayName { get; init; }
    public required bool IsFavorite { get; init; }
    public required bool IsSelected { get; init; }
}
