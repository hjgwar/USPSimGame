using Microsoft.EntityFrameworkCore;
using USPSimGame.Data;
using USPSimGame.Data.Entities;

namespace USPSimGame.Services;

public class AuthService : IAuthService
{
    private readonly IDbContextFactory<AppDbContext> _dbContextFactory;

    public AuthService(IDbContextFactory<AppDbContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    public async Task<User?> AuthenticateAsync(string username, string password)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync();
        var user = await db.Users.FirstOrDefaultAsync(u => u.Username.ToLower() == username.Trim().ToLower());

        if (user == null)
        {
            return null;
        }

        // Simple login check: if PasswordHash is empty in database, allow login with any/no password
        if (string.IsNullOrEmpty(user.PasswordHash) || user.PasswordHash == password)
        {
            return user;
        }

        return null;
    }
}
