namespace TheGate.Domain.Trade;

public sealed class ProductOffer
{
    private readonly Dictionary<Guid, Quantity> _allocations = [];
    private readonly object _allocationLock = new();

    public ProductOffer(
        Guid id,
        Guid producerOrganizationId,
        string productDescription,
        Quantity declaredQuantity,
        Quantity minimumDirectTradeQuantity,
        Uri? externalContactUri = null)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Offer ID cannot be empty.", nameof(id));
        }

        if (producerOrganizationId == Guid.Empty)
        {
            throw new ArgumentException("Producer organization ID cannot be empty.", nameof(producerOrganizationId));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(productDescription);
        ArgumentNullException.ThrowIfNull(declaredQuantity);
        ArgumentNullException.ThrowIfNull(minimumDirectTradeQuantity);

        EnsureSameUnit(declaredQuantity, minimumDirectTradeQuantity);
        if (minimumDirectTradeQuantity.Value > declaredQuantity.Value)
        {
            throw new ArgumentException("Minimum direct trade quantity cannot exceed the declared quantity.", nameof(minimumDirectTradeQuantity));
        }

        if (externalContactUri is not null &&
            (!externalContactUri.IsAbsoluteUri ||
             !string.Equals(externalContactUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)))
        {
            throw new ArgumentException("External contact links must use HTTPS.", nameof(externalContactUri));
        }

        Id = id;
        ProducerOrganizationId = producerOrganizationId;
        ProductDescription = productDescription.Trim();
        DeclaredQuantity = declaredQuantity;
        MinimumDirectTradeQuantity = minimumDirectTradeQuantity;
        ExternalContactUri = externalContactUri;
    }

    public Guid Id { get; }

    public Guid ProducerOrganizationId { get; }

    public string ProductDescription { get; }

    public Quantity DeclaredQuantity { get; }

    public Quantity MinimumDirectTradeQuantity { get; }

    public Uri? ExternalContactUri { get; }

    public decimal AllocatedQuantity
    {
        get
        {
            lock (_allocationLock)
            {
                return _allocations.Values.Sum(quantity => quantity.Value);
            }
        }
    }

    public decimal RemainingQuantity => DeclaredQuantity.Value - AllocatedQuantity;

    public void Allocate(Guid tradeRecordId, Quantity quantity)
    {
        if (tradeRecordId == Guid.Empty)
        {
            throw new ArgumentException("Trade record ID cannot be empty.", nameof(tradeRecordId));
        }

        ArgumentNullException.ThrowIfNull(quantity);
        EnsureSameUnit(DeclaredQuantity, quantity);

        if (quantity.Value < MinimumDirectTradeQuantity.Value)
        {
            throw new InvalidOperationException("Allocation is below the minimum direct trade quantity.");
        }

        lock (_allocationLock)
        {
            if (_allocations.TryGetValue(tradeRecordId, out var existing))
            {
                if (existing == quantity)
                {
                    return;
                }

                throw new InvalidOperationException("An existing trade allocation cannot be silently changed.");
            }

            var allocated = _allocations.Values.Sum(existingQuantity => existingQuantity.Value);
            if (quantity.Value > DeclaredQuantity.Value - allocated)
            {
                throw new InvalidOperationException("Allocation exceeds the producer's declared available quantity.");
            }

            _allocations.Add(tradeRecordId, quantity);
        }
    }

    public bool ReleaseAllocation(Guid tradeRecordId)
    {
        if (tradeRecordId == Guid.Empty)
        {
            throw new ArgumentException("Trade record ID cannot be empty.", nameof(tradeRecordId));
        }

        lock (_allocationLock)
        {
            return _allocations.Remove(tradeRecordId);
        }
    }

    private static void EnsureSameUnit(Quantity left, Quantity right)
    {
        if (!left.HasSameUnit(right))
        {
            throw new ArgumentException("Quantities must use the same unit.");
        }
    }
}
