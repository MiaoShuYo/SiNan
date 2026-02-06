namespace SiNan.Server.Auth;

public sealed class AuthResult
{
    private AuthResult(bool allowed, string actor, string? code, string? message, int? statusCode)
    {
        Allowed = allowed;
        Actor = actor;
        Code = code;
        Message = message;
        StatusCode = statusCode;
    }

    public bool Allowed { get; }
    public string Actor { get; }
    public string? Code { get; }
    public string? Message { get; }
    public int? StatusCode { get; }

    public static AuthResult Allow(string actor) => new(true, actor, null, null, null);

    public static AuthResult Deny(string code, string message, int statusCode) => new(false, "", code, message, statusCode);
}
