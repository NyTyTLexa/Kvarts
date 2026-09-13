using System.Net.Http.Headers;

namespace ProcurementSystem.Services.Notifications.Neighbors;

/// <summary>
/// Фоновый Sync идёт без пользователя: если во входящем запросе есть Authorization — пробросить,
/// иначе подставить кэшированный service token (password grant).
/// </summary>
public sealed class NeighborAuthHandler(IHttpContextAccessor accessor, ServiceTokenProvider tokens) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (request.Headers.Authorization is null)
        {
            var incoming = accessor.HttpContext?.Request.Headers.Authorization.ToString();
            if (!string.IsNullOrWhiteSpace(incoming))
            {
                if (AuthenticationHeaderValue.TryParse(incoming, out var header))
                    request.Headers.Authorization = header;
                else
                    request.Headers.TryAddWithoutValidation("Authorization", incoming);
            }
            else
            {
                var token = await tokens.GetTokenAsync(cancellationToken);
                if (!string.IsNullOrWhiteSpace(token))
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }
        }

        return await base.SendAsync(request, cancellationToken);
    }
}
