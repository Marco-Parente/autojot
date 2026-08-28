using Projects;

var builder = DistributedApplication.CreateBuilder(args);

var worker = builder.AddProject<AutoJot_Worker>("worker");

// The vault path is the one setting the host can usefully own: it is machine-specific rather than
// secret. Tokens and API keys stay in user secrets, which the worker reads for itself.
if (builder.Configuration["AutoJot:RootPath"] is { Length: > 0 } rootPath)
{
    worker.WithEnvironment("AutoJot__RootPath", rootPath);
}

builder.Build().Run();
