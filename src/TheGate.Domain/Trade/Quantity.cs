namespace TheGate.Domain.Trade;

public sealed record Quantity
{
    public Quantity(decimal value, string unitCode)
    {
        if (value <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(value), "Quantity must be greater than zero.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(unitCode);
        if (decimal.Round(value, 3) != value)
        {
            throw new ArgumentOutOfRangeException(nameof(value), "Quantity supports at most three decimal places.");
        }

        if (unitCode.Trim().Length > 16)
        {
            throw new ArgumentOutOfRangeException(nameof(unitCode), "Unit code cannot exceed 16 characters.");
        }

        Value = value;
        UnitCode = unitCode.Trim().ToUpperInvariant();
    }

    public decimal Value { get; }

    public string UnitCode { get; }

    public bool HasSameUnit(Quantity other) =>
        string.Equals(UnitCode, other.UnitCode, StringComparison.Ordinal);
}
