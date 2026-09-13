namespace ProcurementSystem.Auth.Keycloak;

public sealed class KeycloakOptions
{
    public const string Section = "Keycloak";

    public string Authority { get; set; } = "http://localhost:8088/realms/procurement";
    public string AdminBaseUrl { get; set; } = "http://localhost:8088";
    public string AdminRealm { get; set; } = "master";
    public string AdminClientId { get; set; } = "admin-cli";
    public string AdminUsername { get; set; } = "admin";
    public string AdminPassword { get; set; } = "admin";
    public string Realm { get; set; } = "procurement";
    public string PublicClientId { get; set; } = "procurement-api";
    public string DefaultRole { get; set; } = "viewer";
}
