using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace OctopusUMC.Api.Services;

public class EmailSettings
{
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 587;
    public bool UseSsl { get; set; } = true;
    public string UserName { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string FromAddress { get; set; } = "noreply@octopuserp.com";
    public string FromName { get; set; } = "OctopusERP";
}

public class SmtpEmailService : IEmailService
{
    private readonly EmailSettings _cfg;

    public SmtpEmailService(IOptions<EmailSettings> options) => _cfg = options.Value;

    public async Task SendAsync(string toAddress, string toName, string subject, string htmlBody)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(_cfg.FromName, _cfg.FromAddress));
        message.To.Add(new MailboxAddress(toName, toAddress));
        message.Subject = subject;
        message.Body = new TextPart("html") { Text = htmlBody };

        using var client = new SmtpClient();
        var socketOption = _cfg.UseSsl ? SecureSocketOptions.StartTls : SecureSocketOptions.None;
        await client.ConnectAsync(_cfg.Host, _cfg.Port, socketOption);
        if (!string.IsNullOrEmpty(_cfg.UserName))
            await client.AuthenticateAsync(_cfg.UserName, _cfg.Password);
        await client.SendAsync(message);
        await client.DisconnectAsync(true);
    }
}
