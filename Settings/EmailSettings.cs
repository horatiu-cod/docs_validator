namespace DocsValidator.Settings;

public record EmailSettings
{
    public string Provider { get; init; } = "Smtp";
    public SmtpConfig Smtp { get; init; } = new();
    public int Retries { get; init; } = 3;
    public int RetryDelaySeconds { get; init; } = 60;
    public int TimeoutSeconds { get; init; } = 30;
}

public record SmtpConfig
{
    public string Host { get; init; } = string.Empty;
    public int Port { get; init; } = 587;
    public bool UseTls { get; init; } = true;
    public string Username { get; init; } = string.Empty;
    public string Password { get; init; } = string.Empty;
    public string FromAddress { get; init; } = string.Empty;
    public string FromName { get; init; } = string.Empty;
}
