namespace NexusOrder.Contracts.Common;

/// <summary>
/// Represents a domain or application error with a machine-readable code.
/// </summary>
public sealed record Error(string Code, string Description)
{
    public static readonly Error None = new(string.Empty, string.Empty);
    public static Error Validation(string description) => new("Validation.Error", description);
    public static Error NotFound(string description) => new("Resource.NotFound", description);
    public static Error Conflict(string description) => new("Concurrency.Conflict", description);
    public static Error DomainRule(string description) => new("Domain.RuleViolation", description);
}

/// <summary>
/// Encapsulates the outcome of an operation without throwing exceptions for expected failures.
/// </summary>
public class Result
{
    protected Result(bool isSuccess, Error error)
    {
        if ((isSuccess && error != Error.None) || (!isSuccess && error == Error.None))
        {
            throw new InvalidOperationException("Invalid error configuration for Result.");
        }

        IsSuccess = isSuccess;
        Error = error;
    }

    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public Error Error { get; }

    public static Result Success() => new(true, Error.None);
    public static Result Failure(Error error) => new(false, error);
    public static Result<TValue> Success<TValue>(TValue value) => new(value, true, Error.None);
    public static Result<TValue> Failure<TValue>(Error error) => new(default, false, error);
}

public sealed class Result<TValue> : Result
{
    private readonly TValue? _value;

    internal Result(TValue? value, bool isSuccess, Error error) : base(isSuccess, error)
    {
        _value = value;
    }

    public TValue Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException("Cannot access Value of a failed Result.");
}