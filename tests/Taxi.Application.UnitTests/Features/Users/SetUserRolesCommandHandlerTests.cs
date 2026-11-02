using NSubstitute;

using Taxi.Application.Common.Interfaces;
using Taxi.Application.Common.Security;
using Taxi.Application.Features.Users.Commands.SetUserRoles;
using Taxi.Contracts.Common;
using Taxi.Domain.Common.Results;

using Xunit;

namespace Taxi.Application.UnitTests.Features.Users;

public class SetUserRolesCommandHandlerTests
{
    private const string ManagerId = "manager-1";

    private readonly IIdentityService identity = Substitute.For<IIdentityService>();
    private readonly IUser currentUser = Substitute.For<IUser>();

    public SetUserRolesCommandHandlerTests()
    {
        this.currentUser.Id.Returns(ManagerId);
        this.identity.SetRolesAsync(default!, default!).ReturnsForAnyArgs(Result.Updated);
    }

    [Fact]
    public async Task RefusesToRemoveTheManagerRoleFromTheCaller()
    {
        var result = await this.Handle(ManagerId, Roles.User);

        Assert.Equal(ErrorKind.Conflict, result.TopError.Type);
        Assert.Equal(LocalizationKeys.Account.CannotRemoveOwnManagerRole, result.TopError.Code);
        await this.identity.DidNotReceiveWithAnyArgs().SetRolesAsync(default!, default!);
    }

    [Fact]
    public async Task AllowsTheCallerToKeepManager_WhileAddingRoles()
    {
        var result = await this.Handle(ManagerId, Roles.Manager, Roles.User);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task CanonicalisesCasing_AndDropsDuplicates()
    {
        await this.Handle("someone-else", "manager", "MANAGER", "user");

        await this.identity.Received(1).SetRolesAsync(
            "someone-else",
            Arg.Is<IReadOnlyCollection<string>>(r => r.SequenceEqual(new[] { Roles.Manager, Roles.User })));
    }

    private Task<Result<Updated>> Handle(string userId, params string[] roles) =>
        new SetUserRolesCommandHandler(this.identity, this.currentUser).Handle(new SetUserRolesCommand(userId, roles), default);
}
