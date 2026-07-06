using System.ComponentModel.DataAnnotations.Schema;

namespace UniVerein.DAL.Entities;

[Table("MailSettings")]
public class MailSettingsEntity : BaseEntity
{
    [Column("smtp_server")] 
    public string SmtpServer { get; set; } = string.Empty;

    [Column("port")] 
    public int Port { get; set; }

    [Column("username")]
    public string Username { get; set; } = string.Empty;

    [Column("password")] 
    public string Password { get; set; } = string.Empty;

    [Column("from_mail")] 
    public string FromMail { get; set; } = string.Empty;

    [Column("enable_ssl")] 
    public bool EnableSsl { get; set; } = true;

    [Column("imap_server")] 
    public string ImapServer { get; set; } = string.Empty;

    [Column("imap_port")]
    public int ImapPort { get; set; } = 993;
}