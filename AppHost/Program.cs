using Projects;

var builder = DistributedApplication.CreateBuilder(args);

var worker = builder.AddProject<AutoJot_Worker>("worker");

builder.Build().Run();
