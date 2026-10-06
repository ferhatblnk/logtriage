using SampleApi;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.AddProvider(new DemoLokiLoggerProvider(builder.Configuration));
if (builder.Configuration.GetValue<bool>("Demo:Traffic"))
    builder.Services.AddHostedService<DemoTraffic>();

var app = builder.Build();
app.UseMiddleware<ErrorLoggingMiddleware>();
app.MapGet("/", static () => "orders-api demo");
app.MapGet("/orders/{id:int}", DemoEndpoints.GetOrder);
app.MapGet("/orders/{id:int}/invoice", DemoEndpoints.GetInvoice);
app.MapPost("/login", DemoEndpoints.Login);
app.MapPost("/payments", DemoEndpoints.Pay);
app.Run();
