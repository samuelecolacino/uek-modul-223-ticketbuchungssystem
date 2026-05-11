using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using FluentAssertions;
using NBomber.Contracts;
using NBomber.CSharp;
using TicketShop.Application.Auth;
using TicketShop.Tests.Infrastructure;
using Xunit.Abstractions;

namespace TicketShop.Tests;

public class TicketLoadTest
{
    private readonly ITestOutputHelper _output;

    public TicketLoadTest(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task BuyTicket_50ConcurrentUsersFor30Seconds_StaysStableWithoutLostUpdates()
    {
        const int seededTicketCount = 20_000;
        const int concurrentUsers = 50;
        var loadDuration = TimeSpan.FromSeconds(30);

        await using var factory = new TicketShopApiFactory();
        factory.SeedLoadTestData(seededTicketCount);

        using var httpClient = factory.CreateClient();

        var loginResponse = await httpClient.PostAsJsonAsync("/api/auth/login", new LoginRequest(factory.LoadTestUsername, factory.LoadTestPassword));
        loginResponse.EnsureSuccessStatusCode();
        var loginPayload = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();
        loginPayload.Should().NotBeNull();
        var token = loginPayload!.Token;

        var statusCounts = new System.Collections.Concurrent.ConcurrentDictionary<int, int>();

        var scenario = Scenario.Create("buy_ticket", async _ =>
        {
            var ticketId = Random.Shared.Next(1, seededTicketCount + 1);

            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/tickets/buy")
            {
                Content = new StringContent($"{{\"ticketId\":{ticketId}}}", Encoding.UTF8, "application/json")
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            using var response = await httpClient.SendAsync(request);
            var status = (int)response.StatusCode;
            statusCounts.AddOrUpdate(status, 1, (_, n) => n + 1);

            return status >= 500
                ? Response.Fail(statusCode: status.ToString(), message: $"server error {status}")
                : Response.Ok(statusCode: status.ToString());
        })
        .WithoutWarmUp()
        .WithLoadSimulations(Simulation.KeepConstant(copies: concurrentUsers, during: loadDuration));

        var nodeStats = NBomberRunner
            .RegisterScenarios(scenario)
            .Run();

        var stats = nodeStats.ScenarioStats.Single();
        var ok = stats.Ok.Request.Count;
        var ko = stats.Fail.Request.Count;
        var sold = factory.CountSoldTickets();
        var success200 = statusCounts.GetValueOrDefault(200, 0);

        _output.WriteLine($"Load test summary — total OK:{ok} fail:{ko}");
        foreach (var kvp in statusCounts.OrderBy(p => p.Key))
        {
            _output.WriteLine($"  HTTP {kvp.Key}: {kvp.Value}");
        }
        _output.WriteLine($"  Sold rows in DB: {sold}");
        _output.WriteLine($"  HTTP 200 responses: {success200}");

        // Stability: no 5xx server errors during 30s of load.
        ko.Should().Be(0, "the server must stay stable under load with no 5xx responses");

        // No lost updates: every "Success" HTTP response is backed by exactly one Sold row in the database.
        sold.Should().Be(success200, "every accepted purchase must persist exactly one IsSold=true row");
    }
}
