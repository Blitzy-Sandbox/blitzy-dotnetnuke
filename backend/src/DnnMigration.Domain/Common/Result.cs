namespace DnnMigration.Domain.Common;

// MIGRATION: New Clean/Onion scaffolding with NO direct legacy analog. The legacy DNN codebase
// signalled expected (non-exceptional) failures via thrown exceptions, boolean return values, or
// ByRef out-parameters. This Result wrapper conveys success/failure plus error messages explicitly,
// so Application services can return expected-failure outcomes without throwing.
/// <summary>
/// Represents the outcome of an operation that either succeeds or fails with one or more error
/// messages, without throwing for expected (for example, validation) failures.
/// </summary>
public class Result
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Result"/> class.
    /// </summary>
    /// <param name="isSuccess">Indicates whether the operation succeeded.</param>
    /// <param name="errors">The error messages associated with a failure, or <c>null</c> for success.</param>
    protected Result(bool isSuccess, IEnumerable<string>? errors)
    {
        IsSuccess = isSuccess;
        Errors = errors is null ? Array.Empty<string>() : errors.ToArray();
    }

    /// <summary>Gets a value indicating whether the operation succeeded.</summary>
    public bool IsSuccess { get; }

    /// <summary>Gets a value indicating whether the operation failed.</summary>
    public bool IsFailure => !IsSuccess;

    /// <summary>Gets the error messages for a failed operation; an empty list when successful.</summary>
    public IReadOnlyList<string> Errors { get; }

    /// <summary>Creates a successful <see cref="Result"/>.</summary>
    public static Result Success() => new(true, null);

    /// <summary>Creates a failed <see cref="Result"/> with the specified error messages.</summary>
    public static Result Failure(params string[] errors) => new(false, errors);

    /// <summary>Creates a failed <see cref="Result"/> with the specified error messages.</summary>
    public static Result Failure(IEnumerable<string> errors) => new(false, errors);
}

/// <summary>
/// Represents the outcome of an operation that produces a value of type <typeparamref name="T"/>
/// on success.
/// </summary>
/// <typeparam name="T">The type of the value produced when the operation succeeds.</typeparam>
public sealed class Result<T> : Result
{
    private readonly T? _value;

    private Result(bool isSuccess, T? value, IEnumerable<string>? errors)
        : base(isSuccess, errors)
    {
        _value = value;
    }

    /// <summary>
    /// Gets the value produced by a successful operation.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when accessed on a failed result.</exception>
    public T Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException("The value of a failed result cannot be accessed.");

    /// <summary>Creates a successful <see cref="Result{T}"/> wrapping the specified value.</summary>
    public static Result<T> Success(T value) => new(true, value, null);

    // MIGRATION: 'new' hides the non-generic base Failure factories (CS0108) so that callers using
    // Result<T>.Failure(...) receive a Result<T> rather than a base Result. The 'new' modifier is
    // REQUIRED here to compile cleanly under --warnaserror.
    /// <summary>Creates a failed <see cref="Result{T}"/> with the specified error messages.</summary>
    public static new Result<T> Failure(params string[] errors) => new(false, default, errors);

    /// <summary>Creates a failed <see cref="Result{T}"/> with the specified error messages.</summary>
    public static new Result<T> Failure(IEnumerable<string> errors) => new(false, default, errors);
}
