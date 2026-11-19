using System.Net;
using MailKit;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace MdmBilling.Services;

public class EmailOptions
{
    public bool Enabled { get; set; }
    public string Host { get; set; } = "";
    public int Port { get; set; } = 587;
    // None, Auto, StartTls, SslOnConnect
    public string Security { get; set; } = "StartTls";
    public string? Username { get; set; }
    public string? Password { get; set; }
    public string FromAddress { get; set; } = "";
    public string FromName { get; set; } = "MDM Billing Monitor";
}

public class EmailService
{
    private readonly EmailOptions _opt;
    private readonly ILogger<EmailService> _log;

    public EmailService(IOptions<EmailOptions> options, ILogger<EmailService> log)
    {
        _opt = options.Value;
        _log = log;
    }

    public async Task<(bool Ok, string? Error)> SendAsync(string to, string subject, string htmlBody, CancellationToken ct = default)
    {
        if (!_opt.Enabled) return (false, "Email is turned off in settings.");

        try
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(_opt.FromName, _opt.FromAddress));
            message.To.Add(MailboxAddress.Parse(to));
            message.Subject = subject;
            message.Body = new BodyBuilder { HtmlBody = htmlBody }.ToMessageBody();

            var security = Enum.TryParse<SecureSocketOptions>(_opt.Security, true, out var s)
                ? s : SecureSocketOptions.Auto;

            using var client = new SmtpClient();
            await client.ConnectAsync(_opt.Host, _opt.Port, security, ct);
            if (!string.IsNullOrWhiteSpace(_opt.Username))
#pragma warning disable CS8604 // Possible null reference argument.
                await client.AuthenticateAsync(_opt.Username, _opt.Password, ct);
#pragma warning restore CS8604 // Possible null reference argument.
            await client.SendAsync(message, ct);
            await client.DisconnectAsync(true, ct);

            return (true, null);
        }
        catch (Exception ex)
        {
            // Never log the message body: it contains a password.
            _log.LogError(ex, "Email to {To} failed", to);
            return (false, ex.Message);
        }
    }

    // Sends the login details to a new user, or after a password reset.
    public Task<(bool Ok, string? Error)> SendCredentialsAsync(
        string to, string fullName, string loginUrl, string tempPassword, bool isReset)
    {
        string E(string v) => WebUtility.HtmlEncode(v);

        var subject = isReset
            ? "Your MDM Billing Monitor password was reset"
            : "Your MDM Billing Monitor account";

        var intro = isReset
            ? "Your password was reset by an administrator."
            : "An account was created for you in MDM Billing Monitor.";

        var body = $@"
<div style=""font-family:Arial,sans-serif;font-size:14px;color:#101828"">
  <p>Hello {E(fullName)},</p>
  <p>{intro}</p>
  <table cellpadding=""6"" style=""border-collapse:collapse"">
    <tr><td><b>Login page</b></td><td><a href=""{E(loginUrl)}"">{E(loginUrl)}</a></td></tr>
    <tr><td><b>Email</b></td><td>{E(to)}</td></tr>
    <tr><td><b>Temporary password</b></td><td><code>{E(tempPassword)}</code></td></tr>
  </table>
  <p>You must change this password when you log in for the first time.</p>
  <p style=""color:#667085"">If you did not expect this email, ignore it and tell your administrator.</p>
</div>";

        return SendAsync(to, subject, body);
    }
}