namespace ASMARA.API.Services.Email;

public record EmailMessage(
    IReadOnlyList<string> Recipients,
    string Subject,
    string HtmlBody);
