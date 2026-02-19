using WorkflowFramework.Dashboard.Web.Services;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddHttpClient<DashboardApiClient>(client =>
    client.BaseAddress = new Uri("https+http://dashboard-api"));

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseStaticFiles();
app.UseAntiforgery();

app.MapDefaultEndpoints();

app.MapRazorComponents<WorkflowFramework.Dashboard.Web.Components.App>()
    .AddInteractiveServerRenderMode();

app.Run();
