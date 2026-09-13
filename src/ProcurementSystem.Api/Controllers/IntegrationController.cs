using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProcurementSystem.Domain.Integration;

namespace ProcurementSystem.Api.Controllers;

/// <summary>Статус внешних интеграций (Этап 8) — для экрана «Настройки».</summary>
[ApiController]
[Route("api/integration")]
[Authorize(Policy = "read")]
public class IntegrationController(IAccountingGateway accounting, IWarehouseGateway warehouse) : ControllerBase
{
    public record IntegrationStatusDto(string System, string Kind, bool Available);

    [HttpGet("status")]
    public async Task<IReadOnlyList<IntegrationStatusDto>> Status(CancellationToken ct) =>
    [
        new(accounting.SystemName, "accounting", await accounting.IsAvailableAsync(ct)),
        new(warehouse.SystemName, "warehouse", await warehouse.IsAvailableAsync(ct)),
    ];
}
