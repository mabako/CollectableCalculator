namespace CollectableCalculator.Model;

internal sealed class Collectability
{
    public required ushort MinimumQuality { get; init; }
    public required int Quantity1 { get; init; }
    public required int Quantity2 { get; init; }
}
