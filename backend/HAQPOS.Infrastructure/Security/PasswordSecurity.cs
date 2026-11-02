using HAQPOS.Domain.Users;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace HAQPOS.Infrastructure.Security;

public sealed class PasswordSecurity
{
    private readonly PasswordHasher<UserAccount> hasher = new(Options.Create(new PasswordHasherOptions
    {
        CompatibilityMode = PasswordHasherCompatibilityMode.IdentityV3,
        IterationCount = 220_000
    }));
    private readonly UserAccount dummy = new();
    private readonly string dummyHash;

    public PasswordSecurity() => dummyHash = hasher.HashPassword(dummy, Guid.NewGuid().ToString("N"));
    public string Hash(UserAccount user, string password) => hasher.HashPassword(user, password);
    public PasswordVerificationResult Verify(UserAccount? user, string password) =>
        hasher.VerifyHashedPassword(user ?? dummy, user?.PasswordHash ?? dummyHash, password);
}
