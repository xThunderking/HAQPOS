using System.Text.Json.Serialization;
using HAQPOS.Domain.Areas;

namespace HAQPOS.Application.Areas;

public sealed record CreateAreaRequest(
    [property: JsonRequired] string Name,
    [property: JsonRequired] string Description);

public sealed record AreaView(Guid Id, string Name, string Description, DateTimeOffset CreatedAt, Guid Version)
{
    public static AreaView From(AreaRecord area) => new(area.Id, area.Name, area.Description, area.CreatedAt, area.Version);
}

public sealed record AreaPage(IReadOnlyList<AreaView> Items, int Total, int Page, int PageSize);
public sealed record AreaOption(Guid Id, string Name);
