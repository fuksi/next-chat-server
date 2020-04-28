using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NextChat.ChatApi.Hubs;
using NextChat.ChatApi.Services;
using Serilog;

namespace NextChat.ChatApi
{
    public class Startup
    {
        private const string NooaLabAuth0Authority = "https://nooalab.eu.auth0.com/";
        private const string ChatApiAudience = "https://api.nextchat.com";
        private const string WssApiPath = "/chatHub";
        private const string AllowAllCorsPolicy = "allowall";

        public Startup(IHostEnvironment env)
        {
            var builder = new ConfigurationBuilder()
                .SetBasePath(env.ContentRootPath)
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .AddJsonFile($"appsettings.{env.EnvironmentName}.json", optional: true)
                .AddEnvironmentVariables();

            Configuration = builder.Build();
        }

        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {

            services.AddAuthentication(options =>
            {
                options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(jwtOptions =>
            {
                jwtOptions.Authority = NooaLabAuth0Authority;
                jwtOptions.Audience = ChatApiAudience;
                jwtOptions.SaveToken = true;
                jwtOptions.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        var accessToken = context.Request.Query["access_token"];

                        Console.WriteLine(accessToken);
                        // If the request is for our hub...
                        var path = context.HttpContext.Request.Path;
                        if (!string.IsNullOrEmpty(accessToken) &&
                            (path.StartsWithSegments(WssApiPath)))
                        {
                            // Read the token out of the query string
                            context.Token = accessToken;
                        }
                        return Task.CompletedTask;
                    }
                };
            });

            services.AddControllers();

            var allowedOrigins = Configuration.GetSection("AllowedOrigins")
                .GetChildren()
                .Select(c => c.Value).ToList();
#if DEBUG
            allowedOrigins.Add("http://localhost:3000");
#endif
            services.AddCors(
                options => options.AddPolicy(AllowAllCorsPolicy, builder =>
                    {
                        builder
                            .WithOrigins(allowedOrigins.ToArray())
                            .AllowAnyHeader()
                            .AllowAnyMethod()
                            .AllowCredentials();
                    })
            );


            services.AddSingleton<IChatService, ChatService>();

            services.AddSignalR();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseSerilogRequestLogging();

            app.UseRouting();

            app.UseCors(AllowAllCorsPolicy);

            app.UseAuthentication();

            app.UseAuthorization();


            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
                endpoints.MapHub<ChatHub>(WssApiPath);
            });
        }
    }
}
