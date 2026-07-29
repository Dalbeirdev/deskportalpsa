using System.Net;
using Desk.Connectors.Autotask;
using Desk.PsaCore.Contracts;

namespace Desk.Tests.Unit.Certification;

/// <summary>
/// Runs the shared certification suite against the REAL AutotaskConnector, driven by an in-memory
/// fake Autotask server. This certifies request construction, JSON parsing, and HTTP error mapping
/// end-to-end. Same tests as the mock — proving cross-provider contract compliance.
/// </summary>
public sealed class AutotaskConnectorCertificationTests : ConnectorCertificationSuite
{
    private const string Secret = "at-webhook-secret";

    private AutotaskConnector Build(FakeAutotaskServer server)
    {
        var http = new HttpClient(server) { BaseAddress = new Uri("https://webservices.local/atservicesrest/") };
        var config = new AutotaskConnectorConfig
        {
            BaseUrl = "https://webservices.local/atservicesrest/",
            Credentials = new AutotaskCredentials("code", "user", "secret"),
            WebhookSecret = Secret,
        };
        return new AutotaskConnector(http, config, Clock);
    }

    protected override IServiceManagementConnector CreateConnector() => Build(new FakeAutotaskServer(Clock));

    protected override IServiceManagementConnector CreateFailingConnector(ConnectorFailureKind kind)
    {
        var status = kind switch
        {
            ConnectorFailureKind.Authentication => HttpStatusCode.Unauthorized,
            ConnectorFailureKind.PermissionDenied => HttpStatusCode.Forbidden,
            ConnectorFailureKind.RateLimited => (HttpStatusCode)429,
            _ => HttpStatusCode.InternalServerError,
        };
        return Build(new FakeAutotaskServer(Clock) { ForceStatus = status });
    }

    protected override string SeededOrganizationId => "1";
    protected override string WebhookSecret => Secret;
}
