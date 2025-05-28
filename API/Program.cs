using System.Text;
using API.Data;
using API.Models;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using API.Endpoints;
using API.Services;
using API.Hubs;

var builder = WebApplication.CreateBuilder(args);


// habilitando cors
builder.Services.AddCors(
    options =>
    {
        options.AddDefaultPolicy(builder =>
        {
            builder.WithOrigins("http://localhost:4200", "https://localhost:4200")
            .AllowAnyHeader().AllowAnyMethod().AllowCredentials();
        });
    }
);


var JwtSetting = builder.Configuration.GetSection("JWTSetting");

builder.Services.AddDbContext<AppDbContext>(x => x.UseSqlite("Data Source=chat.db"));
builder.Services.AddIdentityCore<AppUser>()
.AddEntityFrameworkStores<AppDbContext>();

builder.Services.AddScoped<TokenService>();


// Autentication JWT
builder.Services.AddAuthentication(opt =>
{
    opt.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    opt.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    opt.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
}).AddJwtBearer(option =>
{
    option.SaveToken = true;
    option.RequireHttpsMetadata = false;
    option.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(
            JwtSetting.GetSection("SecurityKey").Value!)),
        ValidateIssuer = false,
        ValidateAudience = false
    };

    option.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            var accessToken = context.Request.Query["access_token"];
            var path = context.HttpContext.Request.Path;

            if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
            {
                context.Token = accessToken;
            }

            return Task.CompletedTask;
        }
    };

});

// Open Api
builder.Services.AddOpenApi();

builder.Services.AddSignalR();
var app = builder.Build();


if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// cors da api
app.UseCors(x => x.AllowAnyHeader()
.AllowAnyMethod().AllowCredentials()
.WithOrigins("http://localhost:4200", "https://localhost:4200"));

app.UseHttpsRedirection();

app.MapAccountEndpoint();
app.UseStaticFiles();
app.MapHub<ChatHub>("hubs/chat");
app.Run();

