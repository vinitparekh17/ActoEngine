namespace ActoX.Domain.Entities;
public class User
{
    public Guid Id { get; private set; }
    public string Email { get; private set; }
    public string FirstName { get; private set; }
    public string LastName { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    // Constructor for creating new users
    public User(string email, string firstName, string lastName)
    {
        Id = Guid.NewGuid();
        Email = email ?? throw new ArgumentNullException(nameof(email));
        FirstName = firstName ?? throw new ArgumentNullException(nameof(firstName));
        LastName = lastName ?? throw new ArgumentNullException(nameof(lastName));
        CreatedAt = DateTime.UtcNow;
    }

    // Constructor for database hydration
    public User(Guid id, string email, string firstName, string lastName, DateTime createdAt, DateTime? updatedAt = null)
    {
        Id = id;
        Email = email;
        FirstName = firstName;
        LastName = lastName;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
    }

    public void UpdateProfile(string firstName, string lastName)
    {
        FirstName = firstName ?? throw new ArgumentNullException(nameof(firstName));
        LastName = lastName ?? throw new ArgumentNullException(nameof(lastName));
        UpdatedAt = DateTime.UtcNow;
    }
}