using DesafioDotNetBaltaIO.Application.DTOs;
using DesafioDotNetBaltaIO.Application.Interfaces;
using DesafioDotNetBaltaIO.Application.Mappings;
using DesafioDotNetBaltaIO.Application.Services;
using DesafioDotNetBaltaIO.Domain.Interfaces;
using DesafioDotNetBaltaIO.Infrastructure.Context;
using DesafioDotNetBaltaIO.Infrastructure.Repositories;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using MiniValidation;
using DesafioDotNetBaltaIO.Domain.Entities;

var builder = WebApplication.CreateBuilder(args);

#region Configure services

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Desafio DotNet Balta IO",
        Description = "CRUD de IBGE",
        License = new OpenApiLicense { Name = "MIT", Url = new Uri("https://opensource.org/licenses/MIT") }
    });

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "Insert the JWT token: Bearer {your token}",
        Name = "Authorization",
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            new string[] {}
        }
    });
});

// Versioning
builder.Services.AddApiVersioning(config =>
{
    config.DefaultApiVersion = new ApiVersion(1, 0);
    config.AssumeDefaultVersionWhenUnspecified = true;
    config.ReportApiVersions = true;
});

// Dependency Injection

// Repositories
builder.Services.AddScoped<ILocationRepository, LocationRepository>();
builder.Services.AddScoped<IUserRepository, UserRepository>();
// Services
builder.Services.AddScoped<ILocationService, LocationService>();
builder.Services.AddTransient<ITokenService, TokenService>();
builder.Services.AddTransient<IUserService, UserService>();

// Configure AutoMapper
builder.Services.AddAutoMapper(typeof(LocationMappingProfile));

// Configure context
builder.Services.AddDbContext<DesafioDotNetBaltaIOContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("SqlServerDocker")));

var app = builder.Build();

#endregion

#region Configure pipelines

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "API de IBGE v1");
    });
}

app.UseHttpsRedirection();

MapActionsLogin(app);
MapActionsLocations(app);

app.Run();

#endregion

#region Enpoints Login

void MapActionsLogin(WebApplication app)
{
    app.MapPost("/login", [AllowAnonymous] async (
        UserDTO user, IUserService userService, ITokenService tokenService) =>
    {
        if (user is null)
            return Results.BadRequest("User is required");

        if (!MiniValidator.TryValidate(user, out var errors))
            return Results.ValidationProblem(errors);

        var userFromDatabase = await userService.GetByEmailAndPasswordAsync(user);

        if (userFromDatabase is null)
            return Results.BadRequest("User or password wrong");

        var tokenJwt = tokenService.CreateToken(userFromDatabase);

        return Results.Ok(tokenJwt);
    })
        .ProducesValidationProblem()
        .Produces(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .WithName("UserLogin")
        .WithTags("User");

    app.MapPost("/register", [AllowAnonymous] async (
        UserDTO user, IUserService userService) =>
    {
        if (user is null)
            return Results.BadRequest("User is required");

        if (!MiniValidator.TryValidate(user, out var errors))
            return Results.ValidationProblem(errors);

        var result = await userService.AddAsync(user);

        return result > 0
            ? Results.CreatedAtRoute("UserLogin", user)
            : Results.BadRequest("An error ocurred while saving the user");
    })
        .ProducesValidationProblem()
        .Produces<string>(StatusCodes.Status201Created)
        .Produces(StatusCodes.Status400BadRequest)
        .WithName("UserRegister")
        .WithTags("User");
}

#endregion

#region Endpoints Locations

void MapActionsLocations(WebApplication app)
{
    app.MapGet("/v1/locations", async (ILocationService service) =>
            await service.GetAsync())                
            .Produces<IEnumerable<LocationDTO>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound)
            .WithName("GetLocations")
            .WithTags("Location");

    app.MapGet("/v1/locations/city/{city}", async (
            string city, ILocationService service) =>

            await service.GetByCityAsync(city)
                is LocationDTO location
                    ? Results.Ok(location)
                    : Results.NotFound())
            .Produces<LocationDTO>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound)
            .WithName("GetLocationByCity")
            .WithTags("Location");

    app.MapGet("/v1/locations/state/{state}", async (
            string state, ILocationService service) =>

            await service.GetByStateAsync(state)
                is LocationDTO location
                    ? Results.Ok(location)
                    : Results.NotFound())
            .Produces<LocationDTO>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound)
            .WithName("GetLocationByState")
            .WithTags("Location");

    app.MapGet("/v1/locations/ibge/{ibge}", async (
            string ibge, ILocationService service) =>

            await service.GetByIbgeAsync(ibge)
                is LocationDTO location
                    ? Results.Ok(location)
                    : Results.NotFound())
            .Produces<LocationDTO>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound)
            .WithName("GetLocationByIbge")
            .WithTags("Location");

    app.MapPost("/v1/location", async (LocationDTO location, ILocationService service) =>
    {
        if (!MiniValidator.TryValidate(location, out var errors))
            return Results.ValidationProblem(errors);

        var result = await service.AddAsync(location);

        return result > 0
            ? Results.CreatedAtRoute("GetLocationByIbge", new { ibge = location.Id }, location)
            : Results.BadRequest("An error ocurred while saving the record");
    })
        .ProducesValidationProblem()
        .Produces<LocationDTO>(StatusCodes.Status201Created)
        .Produces(StatusCodes.Status400BadRequest)
        .WithName("PostLocation")
        .WithTags("Location");


    app.MapPut("/v1/location", async (
        ILocationService service, LocationDTO location) =>
    {
        if (!MiniValidator.TryValidate(location, out var errors))
            return Results.ValidationProblem(errors);

        var locationFromDatabase = await service.GetByIbgeAsync(location.Id);
        if (locationFromDatabase == null) return Results.NotFound();

        var result = await service.UpdateAsync(location);

        return result > 0
            ? Results.NoContent()
            : Results.BadRequest("An error ocurred while saving the record");
    })
        .ProducesValidationProblem()
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status400BadRequest)
        .WithName("PutLocation")
        .WithTags("Location");

    app.MapDelete("/v1/location/{ibge}", async (
        string ibge, ILocationService service) =>
    {
        var locationFromDatabase = await service.GetByIbgeAsync(ibge);
        if (locationFromDatabase == null) return Results.NotFound();

        var result = await service.RemoveAsync(ibge);

        return result > 0
            ? Results.NoContent()
            : Results.BadRequest("An error ocurred while saving the record");
    })
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status400BadRequest)
        .WithName("DeleteLocation")
        .WithTags("Location");
}

#endregion