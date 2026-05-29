using Ecommerce_Backend.Areas.Identity.Data;
using Ecommerce_Backend.Data;
using Ecommerce_Backend.Models.BusinessLayer;
using Ecommerce_Backend.Models.DatabaseLayer;
using Ecommerce_Backend.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration
    .GetConnectionString("AppDbContextConnection")
    ?? throw new InvalidOperationException(
        "Connection string 'AppDbContextConnection' not found.");

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString));

builder.Services.AddDefaultIdentity<ApplicationUser>(options =>
    options.SignIn.RequireConfirmedAccount = true)
    .AddEntityFrameworkStores<AppDbContext>();

builder.Services.AddControllersWithViews();
builder.Services.AddEndpointsApiExplorer();
//builder.Services.AddSwaggerGen();

// ? Sirf ye 3 lines — baaki kuch nahi
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<IDatabaseLayer, DataBaseLayer>();  // ? DataBaseLayer
builder.Services.AddScoped<IBusinessLayer, BusinessLayer>();

var app = builder.Build();

//if (app.Environment.IsDevelopment())
//{
//    app.UseSwagger();
//    app.UseSwaggerUI();
//}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();
app.UseAuthorization();
app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Run();