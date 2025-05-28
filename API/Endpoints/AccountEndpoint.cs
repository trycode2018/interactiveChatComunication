using System;
using API.Common;
using API.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace API.Endpoints;

public static class AccountEndpoint
{
    public static RouteGroupBuilder MapAccountEndpoint(this WebApplication app)
    {
        var group = app.MapGroup("/api/account").WithTags("account");

        group.MapPost("/register", async
        (HttpContext context,
        UserManager<AppUser> userManager,
        [FromForm] string fullName,
        [FromForm] string email,
        [FromForm] string password,
        [FromForm] string userName) =>
        {
            var userFromDb = await userManager.FindByEmailAsync(email);

            if (userFromDb is not null)
            {
                return Results.BadRequest(Response<string>.Failure("User is already exists"));
            }
            var user = new AppUser
            {
                Email = email,
                FullName = fullName,
                UserName = userName
            };


            var result = await userManager.CreateAsync(user, password);
            if (!result.Succeeded)
            {
                return Results.BadRequest(Response<string>.Failure(result.Errors
                .Select(x => x.Description).FirstOrDefault()!));
            }

            return Results.Ok(Response<string>.Success("", "User created successfull"));
        }).DisableAntiforgery();

        return group;
    }
}
