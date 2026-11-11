using AppTemplate.Infrastructure.Identity;
using AppTemplate.IntegrationTests.Data;
using AppTemplate.UseCases.Authorization;
using Ardalis.Result;

namespace AppTemplate.IntegrationTests.Identity;

public class IdentityServiceCreateUser(PostgresDatabaseFixture database)
    : BaseEfRepoTestFixture(database)
{
    private const string Password = "correct horse battery staple";

    private IdentityService Identity => Services.GetRequiredService<IdentityService>();

    [Fact]
    public async Task CreatesTheProfileAndAnUnconfirmedMemberAccount()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var email = NewEmail();

        var created = await Identity.CreateUserAsync(
            UserName.From("Ada"),
            email,
            Password,
            cancellationToken
        );

        created.IsSuccess.ShouldBeTrue();
        var account = await Identity.GetUserAsync(created.Value, cancellationToken);
        account.ShouldNotBeNull();
        account.Email.ShouldBe(email);
        account.EmailConfirmed.ShouldBeFalse();
        account.Roles.ShouldBe([Role.Member.Name]);
        (await GetRepository<User>().GetByIdAsync(created.Value, cancellationToken))!.Name.ShouldBe(
            UserName.From("Ada")
        );
    }

    [Fact]
    public async Task FailsForATakenEmailAndLeavesNoProfileBehind()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var email = NewEmail();
        await Identity.CreateUserAsync(UserName.From("Ada"), email, Password, cancellationToken);
        var profiles = await DbContext.UserProfiles.CountAsync(cancellationToken);

        using var scope = NewScope();
        var second = await scope
            .ServiceProvider.GetRequiredService<IdentityService>()
            .CreateUserAsync(UserName.From("Grace"), email, Password, cancellationToken);

        second.Status.ShouldBe(ResultStatus.Error);
        (await DbContext.UserProfiles.CountAsync(cancellationToken)).ShouldBe(profiles);
    }

    [Fact]
    public async Task FailsForAShortPasswordAndLeavesNoProfileBehind()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var profiles = await DbContext.UserProfiles.CountAsync(cancellationToken);

        var created = await Identity.CreateUserAsync(
            UserName.From("Ada"),
            NewEmail(),
            "short",
            cancellationToken
        );

        created.Status.ShouldBe(ResultStatus.Error);
        created.ValidationErrors.ShouldAllBe(error => error.Identifier == "password");
        (await DbContext.UserProfiles.CountAsync(cancellationToken)).ShouldBe(profiles);
    }

    [Fact]
    public async Task OfTwoConcurrentRegistrationsWithOneEmailOnlyOneSucceeds()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var email = NewEmail();
        using var first = NewScope();
        using var second = NewScope();

        var results = await Task.WhenAll(
            first
                .ServiceProvider.GetRequiredService<IdentityService>()
                .CreateUserAsync(UserName.From("Ada"), email, Password, cancellationToken),
            second
                .ServiceProvider.GetRequiredService<IdentityService>()
                .CreateUserAsync(UserName.From("Ada"), email, Password, cancellationToken)
        );

        results.Count(result => result.IsSuccess).ShouldBe(1);
        results.Count(result => !result.IsSuccess).ShouldBe(1);
    }

    [Fact]
    public async Task DeletingTheProfileDeletesTheAccount()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var created = await Identity.CreateUserAsync(
            UserName.From("Ada"),
            NewEmail(),
            Password,
            cancellationToken
        );
        var profile = await GetRepository<User>().GetByIdAsync(created.Value, cancellationToken);

        await GetRepository<User>().DeleteAsync(profile!, cancellationToken);

        using var scope = NewScope();
        (
            await scope
                .ServiceProvider.GetRequiredService<IdentityService>()
                .GetUserAsync(created.Value, cancellationToken)
        ).ShouldBeNull();
    }

    [Fact]
    public async Task TheMigrationsSeedTheRoles()
    {
        var roles = await DbContext
            .Roles.Select(role => new { role.Id, role.Name })
            .ToListAsync(TestContext.Current.CancellationToken);

        roles.ShouldBe(
            Role.List.Select(role => new { Id = role.Value, Name = (string?)role.Name }),
            ignoreOrder: true
        );
    }
}
