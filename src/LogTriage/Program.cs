using LogTriage;
using LogTriage.Api;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddLogTriage(builder.Configuration);

var app = builder.Build();
app.MapGet("/healthz", static () => Results.Ok(new { status = "ok" }));
app.MapGroupsApi();
app.Run();
