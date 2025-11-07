namespace BffServer.Models;

public class UserViewModel
{
    public bool IsAuthenticated { get; set; }
    public string? Username { get; set; }
    public string? Email { get; set; }
    public Dictionary<string, string> Claims { get; set; } = new();
}