using CartCompare.Entities;

namespace CartCompare.Services.Models;

public class AuthResult
{
    public bool Succeeded { get; set; }

    public ApplicationUser? User { get; set; }

    public List<string> Errors { get; set; } = new List<string>();
}