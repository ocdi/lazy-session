using CustomSession;

var builder = WebApplication.CreateBuilder(args);

// Use an in-memory distributed cache for demonstration. Replace with Redis or SQL distributed cache in production.
builder.Services.AddDistributedMemoryCache();
builder.Services.Configure<SessionOptions>(opts =>
{
	opts.Cookie.Name = ".AspNetCore.Session";
	opts.IdleTimeout = TimeSpan.FromMinutes(20);
});

var app = builder.Build();

app.UseLazyDistributedSession();

app.MapGet("/", (HttpContext http) =>
{
	// example usage
	var session = http.Session;
	session.Set("now", System.Text.Encoding.UTF8.GetBytes(DateTime.UtcNow.ToString("o")));
	return "Hello World!";
});

app.Run();
