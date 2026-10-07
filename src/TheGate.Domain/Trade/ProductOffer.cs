namespace TheGate.Domain.Trade;

public sealed class ProductOffer
{
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
        if (productDescription.Trim().Length > 300)
        {
            throw new ArgumentOutOfRangeException(nameof(productDescription), "Product description cannot exceed 300 characters.");
        }

        ArgumentNullException.ThrowIfNull(declaredQuantity);
        ArgumentNullException.ThrowIfNull(minimumDirectTradeQuantity);

        EnsureSameUnit(declaredQuantity, minimumDirectTradeQuantity);
        if (minimumDirectTradeQuantity.Value > declaredQuantity.Value)
        {
            throw new ArgumentException("Minimum direct trade quantity cannot exceed the declared quantity.", nameof(minimumDirectTradeQuantity));
        }

        if (externalContactUri is not null &&
            (!externalContactUri.IsAbsoluteUri ||
             !string.Equals(externalContactUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) ||
             externalContactUri.AbsoluteUri.Length > 2048))
        {
            throw new ArgumentException("External contact links must be absolute HTTPS URIs no longer than 2048 characters.", nameof(externalContactUri));
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

    private static void EnsureSameUnit(Quantity left, Quantity right)
    {
        if (!left.HasSameUnit(right))
        {
            throw new ArgumentException("Quantities must use the same unit.");
        }
    }
}
