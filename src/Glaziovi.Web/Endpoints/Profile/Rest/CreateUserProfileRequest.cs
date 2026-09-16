using System.ComponentModel.DataAnnotations;

namespace Glaziovi.Web.Endpoints.Profile.Rest;

public sealed record CreateUserProfileRequest(
    [Required] [StringLength(60)] string FirstName,
    [Required] [StringLength(60)] string LastName,
    [Required] [EmailAddress] string Email,
    [Required]
    [StringLength(25, MinimumLength = 3)]
    [RegularExpression(
        @"^[a-zA-Z0-9](?:[a-zA-Z0-9.]*[a-zA-Z0-9])?$",
        ErrorMessage = "Username cannot start or end with '.' and must contain only letters, numbers, and '.'"
    )]
    string Username,
    [Required]
    [StringLength(16, MinimumLength = 6)]
    [RegularExpression(
        @"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[\$#%!_])[A-Za-z\d\$#%!_]+$",
        ErrorMessage = "Password must contain upper, lower, number and one of $#%!_"
    )]
    string Password
);
