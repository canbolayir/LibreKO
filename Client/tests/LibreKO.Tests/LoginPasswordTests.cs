using LibreKO.Network;
using Xunit;

namespace LibreKO.Tests;

public class LoginPasswordTests
{
    [Fact]
    public void AnAsciiPasswordKeepsItsRetailForm() => Assert.Equal("AQLPWT0", LoginPassword.Encode("test"));

    [Fact]
    public void LettersOutsideAsciiAreNotFoldedIntoAQuestionMark()
    {
        Assert.NotEqual(LoginPassword.Encode("?ifre"), LoginPassword.Encode("şifre"));
        Assert.NotEqual(LoginPassword.Encode("contrase?a"), LoginPassword.Encode("contraseña"));
    }
}
