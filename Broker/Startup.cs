using Broker.Services;
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

               });
          }
     }
}