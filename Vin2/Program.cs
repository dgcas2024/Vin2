using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using Vin2Api;
using System.IO;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;

namespace Vin2
{
    public class Program
    {
        internal class Lazier<T>(IServiceProvider provider) : Lazy<T>(() => provider.GetRequiredService<T>()) where T : class
        {
        }

        public static WebApplication WebApplication { get; private set; }

        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);
            builder.Logging.ClearProviders();
            builder.Logging.AddSimpleConsole(option =>
            {
                option.TimestampFormat = "HH:mm:ss.fff dd/MM/yyyy - ";
                option.SingleLine = true;
            });
            if (args.Length > 0)
            {
                builder.Configuration.AddJsonFile($"{args[0]}/appsettings.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: false);
            }

            var mvc = builder.Services.AddControllersWithViews();
            mvc.AddJsonOptions(options => options.JsonSerializerOptions.PropertyNamingPolicy = null);
            builder.Services.AddSignalR(options => options.MaximumReceiveMessageSize = 256000).AddJsonProtocol(options => options.PayloadSerializerOptions.PropertyNamingPolicy = null);
            builder.Services.AddSingleton<Vin2Request, Vin2Request>();
            builder.Services.AddSingleton<Vin2Booking, Vin2Booking>();
            builder.Services.AddSingleton<IVin2Message, Vin2Message>();
            builder.Services.AddSingleton<Vin2BookingData, Vin2BookingData>();
            builder.Services.AddSingleton<Vin2Account, Vin2Account>();
            builder.Services.AddSingleton<Vin2Messenger, Vin2Messenger>();

            builder.Services.AddSingleton(typeof(Lazy<>), typeof(Lazier<>));

            WebApplication = builder.Build();
            _ = WebApplication.Services.GetService<Vin2BookingData>().ToString();
            _ = WebApplication.Services.GetService<Vin2Account>().ToString();
            _ = WebApplication.Services.GetService<Vin2Booking>().ToString();

            WebApplication.UseExceptionHandler(appError =>
            {
                appError.Run(async context =>
                {
                    var exceptionHandlerFeature = context.Features.Get<IExceptionHandlerFeature>();
                    if (context.Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                    {
                        var result = new
                        {
                            Success = false,
                            exceptionHandlerFeature.Error.Message
                        };
                        context.Response.StatusCode = 200;
                        context.Response.ContentType = "application/json; charset=utf-8";
                        await context.Response.WriteAsync(System.Text.Json.JsonSerializer.Serialize(result, new System.Text.Json.JsonSerializerOptions
                        {
                            PropertyNamingPolicy = null
                        }));
                    }
                    else
                    {
                        context.Response.Redirect($"/Error?message={exceptionHandlerFeature.Error.Message}");
                    }
                });
            });

            WebApplication.UseStaticFiles();

            WebApplication.UseRouting();

            WebApplication.UseAuthorization();

            WebApplication.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}");
            WebApplication.MapHub<Vin2Hub>("vin2-hub");

            WebApplication.Run();
        }
    }
}
