var builder = DistributedApplication.CreateBuilder(args);

var api = builder.AddProject<Projects.WorkflowFramework_Dashboard_Api>("dashboard-api");
var dashboardUsePersistence = builder.Configuration["Dashboard:UsePersistence"];
if (!string.IsNullOrWhiteSpace(dashboardUsePersistence))
{
    api = api.WithEnvironment("Dashboard__UsePersistence", dashboardUsePersistence);
}

var web = builder.AddProject<Projects.WorkflowFramework_Dashboard_Web>("dashboard-web")
    .WithReference(api)
    .WaitFor(api);

builder.Build().Run();
