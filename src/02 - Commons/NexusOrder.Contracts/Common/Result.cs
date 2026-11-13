namespace NexusOrder.Contracts.Common;

/// <summary>
/// Represents a domain or application error with a machine-readable code.
/// </summary>
public sealed record OperationError(string Code, string Description)
{
    public static readonly OperationError None = new(string.Empty, string.Empty);
    public static OperationError Validation(string description) => new("Validation.Error", description);
    public static OperationError NotFound(string description) => new("Resource.NotFound", description);
    public static OperationError Conflict(string description) => new("Concurrency.Conflict", description);
    public static OperationError DomainRule(string description) => new("Domain.RuleViolation", description);
}

/// <summary>
/// Encapsulates the outcome of an operation without throwing exceptions for expected failures.
/// </summary>
public class Result
{
    protected Result(bool isSuccess, OperationError error)
    {
        if ((isSuccess && error != OperationError.None) || (!isSuccess && error == OperationError.None))
        {
            throw new InvalidOperationException("Invalid error configuration for Result.");
        }

        IsSuccess = isSuccess;
        Error = error;
    }

    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public OperationError Error { get; }

    public static Result Success() => new(true, OperationError.None);
    public static Result Failure(OperationError error) => new(false, error);
    public static Result<TValue> Success<TValue>(TValue value) => new(value, true, OperationError.None);
    public static Result<TValue> Failure<TValue>(OperationError error) => new(default, false, error);
}

public sealed class Result<TValue> : Result
{
    private readonly TValue? _value;

    internal Result(TValue? value, bool isSuccess, OperationError error) : base(isSuccess, error)
    {
        _value = value;
    }

    public TValue Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException("Cannot access Value of a failed Result.");
}