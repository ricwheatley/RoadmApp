using System.IO;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xero.NetStandard.OAuth2.Config;
using XeroNetStandardApp.Models;
using XeroNetStandardApp.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.Configure<XeroConfiguration>(builder.Configuration.GetSection("XeroConfiguration"));
builder.Services.Configure<XeroSyncOptions>(builder.Configuration.GetSection("XeroSync"));
builder.Services.AddHttpClient();
builder.Services.AddTransient<IXeroRawIngestService, XeroRawIngestService>();
builder.Services.AddTransient<TokenService>();
builder.Services.AddDistributedMemoryCache();
builder.Services.AddScoped<IPollingService, PollingService>();
builder.Services.AddSession();
builder.Services.AddMvc(options => options.EnableEndpointRouting = false);
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(@"./keys"))
    //.ProtectKeysWithDpapi()
    .SetApplicationName("RoadmApp");

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

app.UseSession();

app.UseMvc(routes =>
{
    routes.MapRoute(
        name: "default",
        template: "{controller=Home}/{action=Index}/{id?}");
});

app.Run();
