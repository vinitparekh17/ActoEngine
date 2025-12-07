using System.ComponentModel.DataAnnotations;

namespace ActoEngine.WebApi.Models
{
    public class Client
    {
        /// <summary>
        /// Initializes a new Client instance with default property values.
        /// </summary>
        /// <remarks>
        /// Default values: ClientName is an empty string; IsActive is true. Other properties (ClientId, CreatedAt, CreatedBy, UpdatedAt, UpdatedBy) remain unset or at their type defaults.
        /// </remarks>
        public Client()
        {
        }

        /// <summary>
        /// Initializes a new Client with the specified name, creator, and creation time, and marks the client as active.
        /// </summary>
        /// <param name="clientName">The client name.</param>
        /// <param name="createdAt">The date and time when the client was created.</param>
        /// <param name="createdBy">The identifier of the user who created the client.</param>
        public Client(string clientName, DateTime createdAt, int createdBy)
        {
            ClientName = clientName;
            CreatedAt = createdAt;
            CreatedBy = createdBy;
            IsActive = true;
        }

        public int ClientId { get; set; }
        public string ClientName { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; }
        public int CreatedBy { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public int? UpdatedBy { get; set; }
    }

    public class CreateClientRequest
    {
        [Required(ErrorMessage = "Client name is required")]
        [StringLength(100, MinimumLength = 3, ErrorMessage = "Client name must be between 3 and 100 characters")]
        public string ClientName { get; set; } = default!;
    }

    public class UpdateClientRequest
    {
        [Required(ErrorMessage = "Client name is required")]
        [StringLength(100, MinimumLength = 3, ErrorMessage = "Client name must be between 3 and 100 characters")]
        public string ClientName { get; set; } = default!;

        public bool? IsActive { get; set; }
    }

    public class ClientResponse
    {
        public int ClientId { get; set; }
        public string ClientName { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public int CreatedBy { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public int? UpdatedBy { get; set; }
    }
}