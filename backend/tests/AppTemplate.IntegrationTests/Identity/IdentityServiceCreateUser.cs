using AppTemplate.IntegrationTests.Data;
using AppTemplate.UseCases.Authorization;
using AppTemplate.UseCases.Interfaces;
using Ardalis.Result;

namespace AppTemplate.IntegrationTests.Identity;

public class IdentityServiceCreateUser(PostgresDatabaseFixture database)
    : BaseEfRepoTestFixture(database)
{
    private const string Password = "correct horse battery staple";

    private IIdentityService Identity => Services.GetRequiredService<IIdentityService>();

    [Fact]
    public async Task CreatesTheProfileAndAnUnconfirmedAccountWithNoRoles()
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
        account.Roles.ShouldBeEmpty();
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
            .ServiceProvider.GetRequiredService<IIdentityService>()
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
                .ServiceProvider.GetRequiredService<IIdentityService>()
                .CreateUserAsync(UserName.From("Ada"), email, Password, cancellationToken),
            second
                .ServiceProvider.GetRequiredService<IIdentityService>()
                .CreateUserAsync(UserName.From("Ada"), email, Password, cancellationToken)
        );

        results.Count(result => result.IsSuccess).ShouldBe(1);
        // Identity's own check, or the unique index when both passed it at once.
        results
            .Single(result => !result.IsSuccess)
            .Status.ShouldBeOneOf(ResultStatus.Error, ResultStatus.Conflict);
        // Identity stores the email upper-cased (UserManager.NormalizeEmail).
        var normalizedEmail = email.Value.ToUpperInvariant();
        (
            await DbContext.Users.CountAsync(
                account => account.NormalizedEmail == normalizedEmail,
                cancellationToken
            )
        ).ShouldBe(1);
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
                .ServiceProvider.GetRequiredService<IIdentityService>()
                .GetUserAsync(created.Value, cancellationToken)
        ).ShouldBeNull();
    }

    [Fact]
    public async Task AddToRolesAddsEveryRole()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var id = await CreateAsync(cancellationToken);

        var added = await Identity.AddToRolesAsync(
            id,
            [Role.Member, Role.Admin],
            cancellationToken
        );

        added.IsSuccess.ShouldBeTrue();
        (await Identity.GetUserAsync(id, cancellationToken))!.Roles.ShouldBe(
            [Role.Admin.Name, Role.Member.Name],
            ignoreOrder: true
        );
    }

    [Fact]
    public async Task AddToRolesSkipsTheRolesTheAccountHas()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var id = await CreateAsync(cancellationToken);
        await Identity.AddToRolesAsync(id, [Role.Member], cancellationToken);

        var added = await Identity.AddToRolesAsync(
            id,
            [Role.Member, Role.Admin, Role.Admin],
            cancellationToken
        );

        added.IsSuccess.ShouldBeTrue();
        (await Identity.GetUserAsync(id, cancellationToken))!.Roles.ShouldBe(
            [Role.Admin.Name, Role.Member.Name],
            ignoreOrder: true
        );
    }

    [Fact]
    public async Task AddToRolesIsNotFoundForAnUnknownUser()
    {
        var added = await Identity.AddToRolesAsync(
            UserId.From(int.MaxValue),
            [Role.Member],
            TestContext.Current.CancellationToken
        );

        added.Status.ShouldBe(ResultStatus.NotFound);
    }

    [Fact]
    public async Task MarkEmailConfirmedConfirmsWithoutSendingAnything()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var email = NewEmail();
        var id = (
            await Identity.CreateUserAsync(UserName.From("Ada"), email, Password, cancellationToken)
        ).Value;

        var confirmed = await Identity.MarkEmailConfirmedAsync(id, cancellationToken);

        confirmed.IsSuccess.ShouldBeTrue();
        (await Identity.GetUserAsync(id, cancellationToken))!.EmailConfirmed.ShouldBeTrue();
        Emails.Sent.ShouldNotContain(sent => sent.To == email.Value);
    }

    [Fact]
    public async Task MarkEmailConfirmedIsNotFoundForAnUnknownUser()
    {
        var confirmed = await Identity.MarkEmailConfirmedAsync(
            UserId.From(int.MaxValue),
            TestContext.Current.CancellationToken
        );

        confirmed.Status.ShouldBe(ResultStatus.NotFound);
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

    private async Task<UserId> CreateAsync(CancellationToken cancellationToken)
    {
        var created = await Identity.CreateUserAsync(
            UserName.From("Ada"),
            NewEmail(),
            Password,
            cancellationToken
        );
        created.IsSuccess.ShouldBeTrue();
        return created.Value;
    }
}
