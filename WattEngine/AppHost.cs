var builder = DistributedApplication.CreateBuilder(args);

builder.AddProject<Projects.WattEngine_Ideask>("ideask");

builder.Build().Run();