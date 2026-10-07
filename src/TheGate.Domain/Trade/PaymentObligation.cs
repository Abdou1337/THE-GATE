namespace TheGate.Domain.Trade;

public enum PaymentObligationStatus
{
    Pending,
    PartnerReportedSettled
}

public sealed record PaymentObligation
{
    public PaymentObligation(
        Guid id,
        Guid tradeRecordId,
        Guid payerOrganizationId,
        Guid beneficiaryOrganizationId,
        Guid paymentPartnerOrganizationId,
        decimal amount,
        string currencyCode,
        string providerName,
        string providerReference,
        PaymentObligationStatus status = PaymentObligationStatus.Pending,
        DateTimeOffset? partnerReportedSettledAtUtc = null)
    {
        if (id == Guid.Empty || tradeRecordId == Guid.Empty ||
            payerOrganizationId == Guid.Empty || beneficiaryOrganizationId == Guid.Empty ||
            paymentPartnerOrganizationId == Guid.Empty ||
            payerOrganizationId == beneficiaryOrganizationId ||
            paymentPartnerOrganizationId == payerOrganizationId ||
            paymentPartnerOrganizationId == beneficiaryOrganizationId)
        {
            throw new ArgumentException("Payment, trade, payer, beneficiary, and independent payment partner IDs are required.");
        }

        if (amount <= 0 || decimal.Round(amount, 2) != amount)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), "Amount must be positive and use at most two decimal places.");
        }

        ValidateText(currencyCode, nameof(currencyCode), 3);
        ValidateText(providerName, nameof(providerName), 120);
        ValidateText(providerReference, nameof(providerReference), 200);

        if (currencyCode.Trim().Length != 3)
        {
            throw new ArgumentException("Currency code must contain three characters.", nameof(currencyCode));
        }

        if (!Enum.IsDefined(status) ||
            (status == PaymentObligationStatus.PartnerReportedSettled) != (partnerReportedSettledAtUtc is not null) ||
            (partnerReportedSettledAtUtc is not null && partnerReportedSettledAtUtc.Value.Offset != TimeSpan.Zero))
        {
            throw new ArgumentException("Payment status and UTC partner report time are inconsistent.", nameof(status));
        }

        Id = id;
        TradeRecordId = tradeRecordId;
        PayerOrganizationId = payerOrganizationId;
        BeneficiaryOrganizationId = beneficiaryOrganizationId;
        PaymentPartnerOrganizationId = paymentPartnerOrganizationId;
        Amount = amount;
        CurrencyCode = currencyCode.Trim().ToUpperInvariant();
        ProviderName = providerName.Trim();
        ProviderReference = providerReference.Trim();
        Status = status;
        PartnerReportedSettledAtUtc = partnerReportedSettledAtUtc;
    }

    public Guid Id { get; }

    public Guid TradeRecordId { get; }

    public Guid PayerOrganizationId { get; }

    public Guid BeneficiaryOrganizationId { get; }

    public Guid PaymentPartnerOrganizationId { get; }

    public decimal Amount { get; }

    public string CurrencyCode { get; }

    public string ProviderName { get; }

    public string ProviderReference { get; }

    public PaymentObligationStatus Status { get; private set; }

    public DateTimeOffset? PartnerReportedSettledAtUtc { get; private set; }

    public void RecordProviderSettlement(DateTimeOffset reportedAtUtc)
    {
        if (Status != PaymentObligationStatus.Pending)
        {
            throw new InvalidOperationException("Payment obligation has already been reported as settled.");
        }

        if (reportedAtUtc.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("Settlement report time must be UTC.", nameof(reportedAtUtc));
        }

        Status = PaymentObligationStatus.PartnerReportedSettled;
        PartnerReportedSettledAtUtc = reportedAtUtc;
    }

    private static void ValidateText(string value, string parameterName, int maxLength)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        if (value.Trim().Length > maxLength)
        {
            throw new ArgumentOutOfRangeException(parameterName, $"Value cannot exceed {maxLength} characters.");
        }
    }
}
