﻿namespace MyBp
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Net;
    using System.Net.Http;
    using System.Reflection;
    using System.Runtime.InteropServices.ComTypes;
    using System.Threading.Tasks;
    using Client;
    using Config;
    using Microsoft.AspNetCore.Builder;
    using Microsoft.AspNetCore.Hosting;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.AspNetCore.Authentication.Cookies;
    using Microsoft.AspNetCore.Authentication.OpenIdConnect;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Localization;
    using Microsoft.Extensions.Options;
    using Microsoft.IdentityModel.Protocols.OpenIdConnect;
    using Models;
    using Refit;
    using Services;

    public static class LenusAuthenticationExtensions 
    {
        public static IServiceCollection AddLenusAuthentication(this IServiceCollection serviceCollection, IConfiguration configuration)
        {
            serviceCollection
                .AddAuthentication(o =>
                {
                    o.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                    o.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
                })
                .AddCookie(o =>
                {
                    o.AccessDeniedPath = "/error";
                    o.LogoutPath = "/Home/Logout";
                    o.LoginPath = "/Home/Login";
                })
                .AddOpenIdConnect(o =>
                {
                    configuration.GetSection("OpenIdConnect").Bind(o);
                    /* save the access token within an authentication cookie */
                    o.SaveTokens = true;
                    /* match token and cookie lifetime */
                    o.UseTokenLifetime = true;
                    
                    o.GetClaimsFromUserInfoEndpoint = true;
                    
                    /* use the authorization_code flow */
                    o.ResponseType = OpenIdConnectResponseType.Code;
                    
                    o.Events.OnRemoteFailure += ctx =>
                    {
                        ctx.Response.Redirect("/");
                        ctx.HandleResponse();
                        return Task.CompletedTask;
                    };

                    /* Mandatory scope */
                    o.Scope.Add("openid");

                    /* I want profile information (givenname, familyname) */
                    o.Scope.Add("profile");

                    /* I want to read email address */
                    o.Scope.Add("email");

                    /* I want to read blood pressure data */
                    o.Scope.Add("read.blood_pressure");
                    o.Scope.Add("read.blood_pressure.blood_pressure_systolic");
                    o.Scope.Add("read.blood_pressure.blood_pressure_diastolic");

                    /* I want to write blood pressure data */
                    o.Scope.Add("write.blood_pressure");
                    o.Scope.Add("write.blood_pressure.blood_pressure_systolic");
                    o.Scope.Add("write.blood_pressure.blood_pressure_diastolic");
                })
                ;

            return serviceCollection;
        }

        public static IServiceCollection AddLenusAuthorisation(this IServiceCollection serviceCollection)
        {
            return serviceCollection.AddAuthorization(o =>
            {
                o.AddPolicy("Query", policy => policy.RequireAuthenticatedUser());
                o.AddPolicy("Submit", policy => policy.RequireAuthenticatedUser());
            });
        }

        public static IServiceCollection AddLenusHealthClient(this IServiceCollection serviceCollection, IConfiguration configuration)
        {
            serviceCollection.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();
            serviceCollection.AddSingleton<IAccessTokenAccessor, AccessTokenAccessor>();
            serviceCollection.AddOptions();

            serviceCollection.Configure<HealthDataClientOptions>(configuration.GetSection("HealthDataClient"));

            serviceCollection.AddScoped(s =>
            {
                var options = s.GetRequiredService<IOptions<HealthDataClientOptions>>().Value;
                var client = new HttpClient(new HealthClientV2HttpClientHandler(s.GetRequiredService<IAccessTokenAccessor>()))
                {
                    BaseAddress = options.BaseUri
                };
                return RestService.For<IHealthDataClient>(client);
            });
            return serviceCollection;
        }
    }

    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddLocalization();
            services.AddMvc();

            services.AddLenusAuthentication(Configuration);
            services.AddLenusAuthorisation();
            services.AddLenusHealthClient(this.Configuration);
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
            app.UseAuthentication();
            app.UseRequestLocalization();

            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseExceptionHandler("/Error");
            }

            app.UseStaticFiles();

            var gbCulture = CultureInfo.GetCultureInfo("en-GB");
            app.UseRequestLocalization(
                new RequestLocalizationOptions()
                {
                    DefaultRequestCulture = new RequestCulture(gbCulture),
                    SupportedCultures = new[] { gbCulture },
                    SupportedUICultures = new[] { gbCulture }
                });

            app.UseMvc(routes =>
            {
                routes.MapRoute(
                    name: "default",
                    template: "{controller=Home}/{action=Index}/{id?}");
            });
        }
    }
}
