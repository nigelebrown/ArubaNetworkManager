using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Xunit;

namespace Jcf.NetworkManager.Tests;

public sealed class PilotIntegrationTests : IClassFixture<PilotIntegrationTests.AppFactory>
{
    private readonly AppFactory factory;
    public PilotIntegrationTests(AppFactory factory) => this.factory=factory;

    public sealed class AppFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseSetting("Jcf:DemoMode","true");
            builder.UseSetting("Jcf:EnableDeviceWrites","false");
            builder.UseEnvironment("Development");
        }
    }

    [Fact]
    public async Task Enterprise_dashboard_serves_simulated_inventory_without_live_writes()
    {
        using var client=factory.CreateClient();
        var response=await client.GetAsync("/api/v1/dashboard/summary");
        Assert.Equal(HttpStatusCode.OK,response.StatusCode);
        var body=await response.Content.ReadAsStringAsync();
        Assert.Contains("pendingRecommendations",body);
        Assert.Contains("devices",body);
    }

    [Fact]
    public async Task New_operations_and_topology_pages_render()
    {
        using var client=factory.CreateClient();
        Assert.Equal(HttpStatusCode.OK,(await client.GetAsync("/operations")).StatusCode);
        Assert.Equal(HttpStatusCode.OK,(await client.GetAsync("/topology")).StatusCode);
    }

    [Fact]
    public async Task Health_endpoint_reports_demo()
    {
        using var client=factory.CreateClient();
        var response=await client.GetAsync("/healthz");
        Assert.Equal(HttpStatusCode.OK,response.StatusCode);
        var data=await response.Content.ReadAsStringAsync();
        Assert.Contains("demo",data,StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Device_inventory_returns_simulated_devices()
    {
        using var client=factory.CreateClient();
        var data=await client.GetStringAsync("/api/v1/devices");
        Assert.Contains("Demo AP-505",data);
        Assert.Contains("Demo CX 6100",data);
    }

    [Fact]
    public async Task Device_execution_is_disabled_even_when_called_as_authenticated_demo_operator()
    {
        using var client=factory.CreateClient();
        var response=await client.PostAsync("/api/v1/execution-jobs",new StringContent("{}"));
        Assert.Equal(HttpStatusCode.NotImplemented,response.StatusCode);
    }

    [Fact]
    public async Task Approved_width_policy_is_visible_and_read_only()
    {
        using var client=factory.CreateClient();
        var data=await client.GetStringAsync("/api/v1/radio-policy");
        Assert.Contains("20",data);Assert.Contains("40",data);
        Assert.Contains("52",data);Assert.Contains("144",data);
    }

    [Fact]
    public async Task Blazor_navigation_pages_are_served()
    {
        using var client=factory.CreateClient();
        foreach(var path in new[]{"/","/devices","/locations","/wireless","/switches","/recommendations","/audit","/configuration","/changes","/groups","/capabilities","/alerts","/client-experience","/kiosk"})
        {
            var response=await client.GetAsync(path);
            Assert.Equal(HttpStatusCode.OK,response.StatusCode);
        }
    }
}
