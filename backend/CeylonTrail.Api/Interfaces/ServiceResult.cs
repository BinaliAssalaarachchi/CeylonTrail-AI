namespace CeylonTrail.Api.Interfaces;

public enum ServiceErrorCode
{
    None,
    NotFound,
    Forbidden,
    Conflict,
    Validation
}

public sealed record ServiceResult<T>(
    bool Succeeded,
    T? Value,
    string? Error,
    ServiceErrorCode ErrorCode)
{
    public static ServiceResult<T> Success(T value) => new(true, value, null, ServiceErrorCode.None);

    public static ServiceResult<T> Failure(string error, ServiceErrorCode errorCode) =>
        new(false, default, error, errorCode);
}
