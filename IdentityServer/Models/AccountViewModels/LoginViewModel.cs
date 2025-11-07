using System.ComponentModel.DataAnnotations;

namespace IdentityServer.Models.AccountViewModels;

public class LoginViewModel
{
    [Required]
    public string Username { get; set; } = string.Empty;

    [Required]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    [Display(Name = "Remember me?")]
    public bool RememberLogin { get; set; }

    public string? ReturnUrl { get; set; }

    public string? Button { get; set; }
}