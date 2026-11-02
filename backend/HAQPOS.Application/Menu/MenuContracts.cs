using System.Text.Json.Serialization;
using HAQPOS.Domain.Menu;

namespace HAQPOS.Application.Menu;

public sealed record CreateMenuProductRequest(
    [property: JsonRequired] string Code,
    [property: JsonRequired] string Name,
    [property: JsonRequired] string Category,
    [property: JsonRequired] string? Description,
    [property: JsonRequired] decimal Price);

public sealed record UpdateMenuProductRequest(
    [property: JsonRequired] string Code,
    [property: JsonRequired] string Name,
    [property: JsonRequired] string Category,
    [property: JsonRequired] string? Description,
    [property: JsonRequired] decimal Price,
    [property: JsonRequired] Guid Version);

public sealed record SetMenuProductStatusRequest(
    [property: JsonRequired] bool IsActive,
    [property: JsonRequired] Guid Version);

public sealed record MenuProductView(Guid Id, string Code, string Name, string Category,
    string? Description, decimal Price, bool IsActive, DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt, Guid Version)
{
    public static MenuProductView From(MenuProduct product) => new(product.Id, product.Code,
        product.Name, product.Category, product.Description, product.Price, product.IsActive,
        product.CreatedAt, product.UpdatedAt, product.Version);
}

public sealed record MenuProductPage(IReadOnlyList<MenuProductView> Items, int Total, int Page, int PageSize);
