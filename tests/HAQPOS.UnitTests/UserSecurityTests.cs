using HAQPOS.Application.Users;
using HAQPOS.Domain.Users;
using HAQPOS.Infrastructure.Security;
using Microsoft.AspNetCore.Identity;
using Xunit;

namespace HAQPOS.UnitTests;

public sealed class UserSecurityTests
{
    [Theory]
    [InlineData(" 00052143 ", "00052143")]
    [InlineData("1", "1")]
    public void EmployeeNumbersKeepLeadingZeroes(string input, string expected) =>
        Assert.Equal(expected, UserRules.EmployeeNumber(input));

    [Theory]
    [InlineData("")]
    [InlineData("123A")]
    [InlineData("１２３")]
    [InlineData("123456789012345678901")]
    public void InvalidEmployeeNumbersAreRejected(string value) =>
        Assert.Throws<UserOperationException>(() => UserRules.EmployeeNumber(value));

    [Theory]
    [InlineData("corta")]
    [InlineData("aaaaaaaaaaaaaaaaaaaa")]
    [InlineData("Mi password es muy largo")]
    [InlineData("Mi empleado 50052143 secreto")]
    public void PredictableOrShortPasswordsAreRejected(string value) =>
        Assert.Throws<UserOperationException>(() => UserRules.Password(value, "50052143"));

    [Fact]
    public void LongPassphrasesAndUnicodeAreAllowed() => UserRules.Password("Mi jardín azul tiene siete árboles", "50052143");

    [Fact]
    public void TemporaryPasswordsUseFourEasyToEnterGroupsAndAreDifferent()
    {
        var passwords = Enumerable.Range(0, 100).Select(_ => UserRules.GenerateTemporaryPassword()).ToArray();
        Assert.All(passwords, value => Assert.Matches("^[A-F0-9]{4}(-[A-F0-9]{4}){3}$", value));
        Assert.Equal(100, passwords.Distinct().Count());
    }

    [Fact]
    public void PasswordHashesAreSaltedAndNeverStoreThePassword()
    {
        var security = new PasswordSecurity();
        var user = new UserAccount();
        const string password = "Mi jardín tiene siete olivos";
        user.PasswordHash = security.Hash(user, password);
        Assert.NotEqual(security.Hash(user, password), user.PasswordHash);
        Assert.DoesNotContain(password, user.PasswordHash);
        Assert.Equal(PasswordVerificationResult.Success, security.Verify(user, password));
        Assert.Equal(PasswordVerificationResult.Failed, security.Verify(user, "otra contraseña"));
        Assert.Equal(PasswordVerificationResult.Failed, security.Verify(null, password));
    }
}
