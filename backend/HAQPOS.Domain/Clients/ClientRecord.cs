namespace HAQPOS.Domain.Clients;

public sealed class ClientRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string RegistrationNumber { get; set; } = "";
    public string FullName { get; set; } = "";
    public string Category { get; set; } = "NORMAL";
    public string? Office { get; set; }
    public bool IsExternal { get; set; }
    public Guid CreatedById { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public Guid Version { get; set; } = Guid.NewGuid();
}
