using Microsoft.AspNetCore.Http;

namespace ProcurementSystem.Services.Matching.Clients;

/// <summary>Пробрасывает Authorization текущего запроса в исходящий HttpClient к Catalog и Quoting.</summary>
public class AuthForwardHandler(IHttpContextAccessor httpContextAccessor) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var auth = httpContextAccessor.HttpContext?.Request.Headers.Authorization.ToString();
        if (!string.IsNullOrWhiteSpace(auth))
        {
            request.Headers.Remove("Authorization");
            request.Headers.TryAddWithoutValidation("Authorization", auth);
        }

        return base.SendAsync(request, cancellationToken);
    }
}
