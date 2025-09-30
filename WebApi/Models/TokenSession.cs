namespace ActoEngine.Domain.Entities
{
    public class TokenSession
    {
        public int Id { get; set; }
        public int UserID { get; set; }
        public string SessionToken { get; set; } = default!;
        public string RefreshToken { get; set; } = default!;
        public DateTime SessionExpiresAt { get; set; }
        public DateTime RefreshExpiresAt { get; set; }
    }
}