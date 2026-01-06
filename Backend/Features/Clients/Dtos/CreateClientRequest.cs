using System.ComponentModel.DataAnnotations;

namespace ActoEngine.WebApi.Features.Clients.Dtos;

public class CreateClientRequest
{
    [Required(ErrorMessage = "Client name is required")]
    [StringLength(100, MinimumLength = 3, ErrorMessage = "Client name must be between 3 and 100 characters")]
    public string ClientName { get; set; } = default!;
}

