using PayCalc24.Contracts.Identity;

namespace PayCalc24.Contracts.Operations;

public interface ICorrelationContext
{
    string CorrelationId { get; }
    string? IdempotencyKey { get; }
}

public enum IdempotencyRegistrationStatus
{
    Registered,
    Existing,
    Conflict
}

public sealed record IdempotencyRegistration(
    IdempotencyRegistrationStatus Status,
    string OperationName,
    string RequestFingerprint,
    string? ResultReference);

public interface IIdempotencyStore
{
    ValueTask<IdempotencyRegistration> RegisterAsync(
        CompanyId companyId,
        string operationName,
        string idempotencyKey,
        string requestFingerprint,
        CancellationToken cancellationToken = default);

    ValueTask CompleteAsync(
        CompanyId companyId,
        string operationName,
        string idempotencyKey,
        string resultReference,
        CancellationToken cancellationToken = default);
}

public sealed record AuditEntry(
    CompanyId CompanyId,
    UserId UserId,
    string ActionCode,
    string EntityType,
    string? EntityId,
    string CorrelationId,
    DateTimeOffset OccurredAtUtc,
    IReadOnlyDictionary<string, object?> Details);

public interface IAuditWriter
{
    ValueTask WriteAsync(AuditEntry entry, CancellationToken cancellationToken = default);
}
