namespace ASMARA.API.Services.Email;

public class EmailOptions
{
    public const string SectionName = "EmailSettings";

    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 25;
    public bool UseSSL { get; set; } = false;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string SenderEmail { get; set; } = "no-reply@asmara.local";
    public string SenderName { get; set; } = "ASMARA Ticketing System";
}
