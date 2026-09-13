namespace ProcurementSystem.Services.Notifications.Auth;

/// <summary>
/// В Docker браузер ходит на localhost:8088, а контейнер должен тянуть JWKS с keycloak:8080.
/// </summary>
public sealed class KeycloakBackchannelHandler : DelegatingHandler
{
    readonly string _internalHost;
    readonly int _internalPort;
    readonly int _publicPort;

    public KeycloakBackchannelHandler(string internalHost, int internalPort, int publicPort = 8088)
        : base(new HttpClientHandler())
    {
        _internalHost = internalHost;
        _internalPort = internalPort;
        _publicPort = publicPort;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (request.RequestUri is { } uri
            && (uri.Host is "localhost" or "127.0.0.1")
            && uri.Port == _publicPort)
        {
            request.RequestUri = new UriBuilder(uri) { Host = _internalHost, Port = _internalPort }.Uri;
        }
        return base.SendAsync(request, cancellationToken);
    }
}
