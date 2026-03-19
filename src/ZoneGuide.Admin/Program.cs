using Blazored.LocalStorage;
using ZoneGuide.Admin.Services;
using MudBlazor.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor().AddCircuitOptions(options =>
{
    if (builder.Environment.IsDevelopment())
    {
        options.DetailedErrors = true;
    }
});

// Add MudBlazor
builder.Services.AddMudServices();

// Add Blazored LocalStorage
builder.Services.AddBlazoredLocalStorage();

// Get API base URL
var apiBaseUrl = builder.Configuration["ApiBaseUrl"] ?? "https://localhost:56040";

// Register AuthTokenHandler for automatic JWT token injection
builder.Services.AddScoped<AuthTokenHandler>();

// Register a single Scoped HttpClient for this circuit
builder.Services.AddScoped(sp =>
{
    var handler = sp.GetRequiredService<AuthTokenHandler>();
    // Ensure inner handler is set
    if (handler.InnerHandler == null)
    {
        handler.InnerHandler = new HttpClientHandler();
    }
    return new HttpClient(handler)
    {
        BaseAddress = new Uri(apiBaseUrl)
    };
});

// Configure IApiService to use the Scoped HttpClient
builder.Services.AddScoped<IApiService, ApiService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

app.Run();
