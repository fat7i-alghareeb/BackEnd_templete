namespace Taxi.Domain.Common.Results;

public readonly record struct Error
{
    private Error(string code, string description, ErrorKind type, object[]? args = null, string? propertyName = null)
    {
        this.Code = code;
        this.Description = description;
        this.Type = type;
        this.Args = args;
        this.PropertyName = propertyName;
    }

    /// <summary>
    /// Gets the localization key (a <c>LocalizationKeys.X</c> constant).
    /// Always used as the lookup key into <c>SharedResource.{lang}.json</c>.
    /// </summary>
    public string Code { get; }

    /// <summary>
    /// Gets english fallback text, used when the localizer cannot resolve <see cref="Code"/>.
    /// </summary>
    public string Description { get; }

    public ErrorKind Type { get; }

    /// <summary>
    /// Gets optional format arguments for parametrized localization messages.
    /// Used with IStringLocalizer: localizer[code, Args].
    /// </summary>
    public object[]? Args { get; init; }

    /// <summary>
    /// Gets for validation errors bound to a request property (produced by FluentValidation
    /// or DataAnnotations), this carries the offending field name. Used as the key in
    /// the RFC 7807 <c>errors</c> dictionary. Null for non-property errors.
    /// </summary>
    public string? PropertyName { get; init; }

    public static Error Failure(string code = nameof(Failure), string description = "General failure.", params object[] args)
        => new(code, description, ErrorKind.Failure, args.Length > 0 ? args : null);

    public static Error Unexpected(string code = nameof(Unexpected), string description = "Unexpected error.", params object[] args)
        => new(code, description, ErrorKind.Unexpected, args.Length > 0 ? args : null);

    public static Error Validation(string code = nameof(Validation), string description = "Validation error", params object[] args)
        => new(code, description, ErrorKind.Validation, args.Length > 0 ? args : null);

    /// <summary>
    /// Validation error bound to a specific request property.
    /// Used by <c>ValidationBehavior</c> to relay FluentValidation failures.
    /// </summary>
    /// <returns></returns>
    public static Error ValidationForProperty(string propertyName, string code, params object[] args)
        => new(code, code, ErrorKind.Validation, args.Length > 0 ? args : null, propertyName);

    public static Error Conflict(string code = nameof(Conflict), string description = "Conflict error", params object[] args)
        => new(code, description, ErrorKind.Conflict, args.Length > 0 ? args : null);

    public static Error NotFound(string code = nameof(NotFound), string description = "Not found error", params object[] args)
        => new(code, description, ErrorKind.NotFound, args.Length > 0 ? args : null);

    public static Error Unauthorized(string code = nameof(Unauthorized), string description = "Unauthorized error", params object[] args)
        => new(code, description, ErrorKind.Unauthorized, args.Length > 0 ? args : null);

    public static Error Forbidden(string code = nameof(Forbidden), string description = "Forbidden error", params object[] args)
        => new(code, description, ErrorKind.Forbidden, args.Length > 0 ? args : null);

    public static Error Create(int type, string code, string description)
        => new(code, description, (ErrorKind)type);
}