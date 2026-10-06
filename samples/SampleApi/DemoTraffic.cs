namespace SampleApi;

/// <summary>Demo only. Calls the sample's own endpoints so errors show up without anyone clicking.</summary>
public sealed class DemoTraffic(IConfiguration configuration) : BackgroundService
{
    private static readonly TimeSpan Warmup = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan Pause = TimeSpan.FromSeconds(2);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var http = new HttpClient { BaseAddress = new Uri(configuration["Demo:BaseUrl"] ?? "http://localhost:8080") };
        await Task.Delay(Warmup, stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var response = await SendAsync(http, Random.Shared.Next(5), stoppingToken);
            }
            catch (HttpRequestException)
            {
            }

            await Task.Delay(Pause, stoppingToken);
        }
    }

    private static Task<HttpResponseMessage> SendAsync(HttpClient http, int choice, CancellationToken cancellationToken) => choice switch
    {
        0 => http.GetAsync($"/orders/{Random.Shared.Next(1, 500) * 2}", cancellationToken),
        1 => http.GetAsync($"/orders/{Random.Shared.Next(1, 500) * 2 + 1}?expand=items", cancellationToken),
        2 => http.GetAsync($"/orders/{Random.Shared.Next(1, 500)}/invoice", cancellationToken),
        3 => http.PostAsJsonAsync("/login?returnUrl=%2Forders&session_token=demo-session", new { email = "demo@example.com", password = "not-a-real-password" }, cancellationToken),
        _ => http.PostAsJsonAsync("/payments", new { orderId = 42, cardNumber = "4111 1111 1111 1111" }, cancellationToken),
    };
}
