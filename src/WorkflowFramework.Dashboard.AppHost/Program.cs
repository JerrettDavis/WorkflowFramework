var builder = DistributedApplication.CreateBuilder(args);

var api = builder.AddProject<Projects.WorkflowFramework_Dashboard_Api>("dashboard-api");

var web = builder.AddProject<Projects.WorkflowFramework_Dashboard_Web>("dashboard-web")
    .WithReference(api)
    .WaitFor(api);

builder.Build().Run();
