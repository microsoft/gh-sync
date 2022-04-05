using Xunit;
using gh_sync;

public class AdoTests
{
    [Fact]
    public void Test1()
    {
        var connection = Ado.GetAdoConnection();
        Assert.Null(connection);
    }
}