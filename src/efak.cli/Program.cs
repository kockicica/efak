
using Microsoft.Extensions.DependencyInjection;

using Typin;

await new CliApplicationBuilder()
      .UseStartup<CliStartup>()
      .Build()
      .RunAsync(args);



public class CliStartup : ICliStartup {

    public void ConfigureServices(IServiceCollection services) {
    }

    public void Configure(CliApplicationBuilder app) {
        app.AddCommandsFromThisAssembly();
    }
}