using System;

namespace ExpenseHub.Api.Domain;

/// <summary>
/// Raised when an operation is incompatible with the current state of a resource
/// (invalid or repeated transition, or a concurrent modification).
/// </summary>
internal sealed class StateConflictException : Exception
{
    public StateConflictException()
    {
    }

    public StateConflictException(string message)
        : base(message)
    {
    }

    public StateConflictException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
