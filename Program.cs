using Ecommerce_Backend.Areas.Identity.Data;
using Ecommerce_Backend.Data;
using Ecommerce_Backend.Models;
using Ecommerce_Backend.Models.BusinessLayer;
using Ecommerce_Backend.Models.DatabaseLayer;
using Ecommerce_Backend.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;

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

// ? JWT Authentication
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = builder.Configuration["Jwt:Issuer"],
        ValidAudience = builder.Configuration["Jwt:Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(
                                     Encoding.UTF8.GetBytes(
                                         builder.Configuration["Jwt:Key"]!))
    };
});


builder.Services.AddControllers();

builder.Services.Configure<CloudinarySettings>(
    builder.Configuration.GetSection("CloudinarySettings"));

builder.Services.AddScoped<CloudinaryService>();

// ? Services
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<IJwtService, JwtService>();
builder.Services.AddScoped<IDatabaseLayer, DataBaseLayer>();
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
app.UseAuthentication();  // ? pehle
app.UseAuthorization();   // ? baad mein
app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Run();