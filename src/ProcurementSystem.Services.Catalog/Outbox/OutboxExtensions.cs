using System.Text.Json;
using ProcurementSystem.Domain.Outbox;
using ProcurementSystem.Services.Catalog.Persistence;

namespace ProcurementSystem.Services.Catalog.Outbox;

public static class OutboxExtensions
{
    /// <summary>
    /// Положить доменное событие в Outbox. Запись добавляется в тот же DbContext, что и бизнес-данные,
    /// и сохраняется одной транзакцией при SaveChanges — событие не потеряется и не уйдёт раньше коммита.
    /// </summary>
    public static void EnqueueEvent<T>(this CatalogDbContext db, T evt) where T : class
    {
        db.OutboxMessages.Add(new OutboxMessage
        {
            Type = typeof(T).Name,
            Payload = JsonSerializer.Serialize(evt)
        });
    }
}
