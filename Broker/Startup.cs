using Broker.Services;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Broker.Services.Interfaces;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Broker
{
     public class Startup
     {
          public void ConfigureServices(IServiceCollection services)
          {
               services.AddGrpc();

               services.AddSingleton<IMessageStorageService, MessageStorageService>();
               services.AddSingleton<IConnectionStorageService, ConnectionStorageService>();
               // PersistenceWorker este inregistrat primul: restaureaza backup-ul inainte de livrare si se opreste ultimul (backup final).
               services.AddSingleton<PersistenceWorker>();
               services.AddHostedService(provider => provider.GetRequiredService<PersistenceWorker>());
               services.AddHostedService<SenderWorker>();

               services.AddHealthChecks()
                    .AddCheck<ReadinessHealthCheck>("persistence", tags: new[] { "ready" });
          }

          public void Configure(
              IApplicationBuilder app,
              IWebHostEnvironment env)
          {
               if (env.IsDevelopment())
               {
                    app.UseDeveloperExceptionPage();
               }

               app.UseRouting();

               app.UseEndpoints(endpoints =>
               {
                    endpoints.MapGrpcService<Services.PublisherService>();
                    endpoints.MapGrpcService<Services.SubscriberService>();

                    // live: procesul raspunde; ready: backup-ul a fost restaurat si Broker-ul poate primi trafic.
                    endpoints.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
                    endpoints.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = x => x.Tags.Contains("ready") });

               });
          }
     }
}