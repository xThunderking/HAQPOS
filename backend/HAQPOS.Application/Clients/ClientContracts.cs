using System.Text.Json.Serialization;
using HAQPOS.Domain.Clients;

namespace HAQPOS.Application.Clients;

public sealed record CreateClientRequest(
    [property: JsonRequired] string RegistrationNumber,
    [property: JsonRequired] string FullName,
    [property: JsonRequired] string Category,
    [property: JsonRequired] bool IsExternal,
    [property: JsonRequired] string? Office);

public sealed record UpdateClientRequest(
    [property: JsonRequired] string RegistrationNumber,
    [property: JsonRequired] string FullName,
    [property: JsonRequired] string Category,
    [property: JsonRequired] bool IsExternal,
    [property: JsonRequired] string? Office,
    [property: JsonRequired] Guid Version);

public sealed record DeleteClientRequest([property: JsonRequired] Guid Version);

public sealed record ClientView(Guid Id, string RegistrationNumber, string FullName, string Category,
    bool IsExternal, string? Office, DateTimeOffset CreatedAt, Guid Version)
{
    public static ClientView From(ClientRecord client) => new(client.Id, client.RegistrationNumber,
        client.FullName, client.Category, client.IsExternal, client.Office, client.CreatedAt, client.Version);
}

public sealed record ClientPage(IReadOnlyList<ClientView> Items, int Total, int Page, int PageSize);
