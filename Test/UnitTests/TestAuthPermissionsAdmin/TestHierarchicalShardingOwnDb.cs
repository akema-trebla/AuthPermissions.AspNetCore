// Copyright (c) 2025 Jon P Smith, GitHub: JonPSmith, web: http://www.thereformedprogrammer.net/
// Licensed under MIT license. See License.txt in the project root for license information.

using AuthPermissions.AdminCode.Services;
using AuthPermissions.BaseCode;
using AuthPermissions.BaseCode.DataLayer.EfCode;
using AuthPermissions.BaseCode.SetupCode;
using Test.StubClasses;
using Test.TestHelpers;
using TestSupport.EfHelpers;
using Xunit;
using Xunit.Extensions.AssertExtensions;

namespace Test.UnitTests.TestAuthPermissionsAdmin;

/// <summary>
/// Hierarchical tenants with sharding: what happens to the tenants below a top-level tenant that has its own database,
/// or that is moved to another database.
/// </summary>
public class TestHierarchicalShardingOwnDb
{
    private readonly AuthPermissionsOptions _options =
        new() { TenantType = TenantTypes.HierarchicalTenant | TenantTypes.AddSharding };

    [Fact]
    public async Task AddingAChildBelowATopTenantWithItsOwnDatabase()
    {
        //SETUP
        using var context = new AuthPermissionsDbContext(SqliteInMemory.CreateOptions<AuthPermissionsDbContext>());
        context.Database.EnsureCreated();
        await context.BulkLoadHierarchicalTenantShardingAsync();
        context.ChangeTracker.Clear();
        var service = new AuthTenantAdminService(context, _options, "en".SetupAuthPLoggingLocalizer(),
            new StubTenantChangeServiceFactory(), null);
        var company = await service.AddHierarchicalTenantAsync("Own Company", 0, null, true, "OwnCompanyDb");
        company.IsValid.ShouldBeTrue(company.GetAllErrors());

        //ATTEMPT: hasOwnDb and databaseInfoName are null, as the error message of the parameter check suggests, to use the parent values
        var child = await service.AddHierarchicalTenantAsync("Branch", company.Result.TenantId, null, null, null);

        //VERIFY
        child.IsValid.ShouldBeTrue(child.GetAllErrors());
    }

    [Fact]
    public async Task MovingATopTenantMovesItsChildrenToo()
    {
        //SETUP
        using var context = new AuthPermissionsDbContext(SqliteInMemory.CreateOptions<AuthPermissionsDbContext>());
        context.Database.EnsureCreated();
        var tenantIds = await context.BulkLoadHierarchicalTenantShardingAsync();
        context.ChangeTracker.Clear();
        var service = new AuthTenantAdminService(context, _options, "en".SetupAuthPLoggingLocalizer(),
            new StubTenantChangeServiceFactory(), null);

        //ATTEMPT: tenantIds[0] is the top-level "Company", the others are below it
        var status = await service.MoveToDifferentDatabaseAsync(tenantIds[0], false, "Other Database");

        //VERIFY
        status.IsValid.ShouldBeTrue(status.GetAllErrors());
        context.ChangeTracker.Clear();
        var tenants = context.Tenants.ToList();
        tenants.Count(x => x.DatabaseInfoName == "Other Database").ShouldEqual(
            tenants.Count(x => x.TenantFullName.StartsWith("Company")));
    }
}
