using AdHocReporting.Domain.Enums;
using AdHocReporting.Infrastructure.Services;
using FluentAssertions;

namespace AdHocReporting.Tests.Services;

public sealed class SqlSafetyValidatorTests
{
    [Fact]
    public void ValidateDefinition_ShouldAllowSelectQuery()
    {
        var action = () => SqlSafetyValidator.ValidateDefinition(DatasourceType.Query, "SELECT Id, Name FROM dbo.Employees");
        action.Should().NotThrow();
    }

    [Fact]
    public void ValidateDefinition_ShouldAllowJoinQueryWithTrailingSemicolon()
    {
        var action = () => SqlSafetyValidator.ValidateDefinition(
            DatasourceType.Query,
            "SELECT M.SalesDate, M.CustomerName, d.SalesID FROM SalesDetail d INNER JOIN SalesMaster M ON M.SalesID = d.SalesID;");

        action.Should().NotThrow();
    }

    [Fact]
    public void ValidateDefinition_ShouldAllowCteQueryWithTrailingSemicolon()
    {
        var action = () => SqlSafetyValidator.ValidateDefinition(
            DatasourceType.Query,
            "WITH SalesCte AS (SELECT SalesID, CustomerName FROM SalesMaster) SELECT SalesID, CustomerName FROM SalesCte;");

        action.Should().NotThrow();
    }

    [Fact]
    public void ValidateDefinition_ShouldRejectDelete()
    {
        var action = () => SqlSafetyValidator.ValidateDefinition(DatasourceType.Query, "SELECT * FROM dbo.Employees; DELETE FROM dbo.Employees");
        action.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void ValidateDefinition_ShouldRejectNonSelectQuery()
    {
        var action = () => SqlSafetyValidator.ValidateDefinition(DatasourceType.Query, "UPDATE dbo.Employees SET Name='x'");
        action.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void ValidateAgentSelectQuery_ShouldRejectMultipleStatements()
    {
        var action = () => SqlSafetyValidator.ValidateAgentSelectQuery("SELECT * FROM src; DELETE FROM src");
        action.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void ValidateAgentSelectQuery_ShouldAllowTrailingSemicolon()
    {
        var action = () => SqlSafetyValidator.ValidateAgentSelectQuery("SELECT * FROM src;");

        action.Should().NotThrow();
    }
}

