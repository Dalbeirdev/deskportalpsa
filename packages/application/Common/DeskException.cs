namespace Desk.Application.Common;

/// <summary>Base for domain/application errors that map to well-defined HTTP problem responses.</summary>
public abstract class DeskException(string message) : Exception(message)
{
    public abstract int StatusCode { get; }
    public abstract string ErrorCode { get; }
}

public sealed class NotFoundException(string what) : DeskException($"{what} was not found.")
{
    public override int StatusCode => 404;
    public override string ErrorCode => "not_found";
}

public sealed class ValidationFailedException(string message) : DeskException(message)
{
    public override int StatusCode => 400;
    public override string ErrorCode => "validation_failed";
}

public sealed class ForbiddenException(string message) : DeskException(message)
{
    public override int StatusCode => 403;
    public override string ErrorCode => "forbidden";
}

/// <summary>Thrown when an operation is attempted without an established tenant scope.</summary>
public sealed class TenantScopeMissingException() : DeskException("No tenant scope is established for this operation.")
{
    public override int StatusCode => 403;
    public override string ErrorCode => "tenant_scope_missing";
}
