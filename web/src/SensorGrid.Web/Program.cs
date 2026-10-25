// What this file is for: starts the SensorGrid web console. For now it only answers that the console is being built;
// the full start-up (sign-in, layers, security) replaces this file once the pages exist.
// How it fits in: Web project entry point.
var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.MapGet("/", () => "SensorGrid web console: under construction.");
app.Run();