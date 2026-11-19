
using System.ComponentModel.DataAnnotations;

namespace MdmBilling.ViewModels;

public class ServerFormModel
{
    public int Id { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Select a group.")]
    [Display(Name = "Group")]
    public int? GroupId { get; set; }

    [Required]
    [StringLength(100)]
    [Display(Name = "Server name")]
    public string Name { get; set; } = "";

    [Required]
    [StringLength(100)]
    [Display(Name = "IP address")]
    public string IpAddress { get; set; } = "";

    [Required]
    [Display(Name = "Database engine")]
    public string DbEngine { get; set; } = "";

    [Required]
    public string Host { get; set; } = "";

    [Range(0, 65535)]
    public int Port { get; set; }

    [Required]
    [Display(Name = "Database name")]
    public string DatabaseName { get; set; } = "";

    [Required]
    [Display(Name = "Read-only username")]
    public string Username { get; set; } = "";

    // Required when creating a server.
    // When editing, blank means keep the existing password.
    [DataType(DataType.Password)]
    public string? Password { get; set; }
}
