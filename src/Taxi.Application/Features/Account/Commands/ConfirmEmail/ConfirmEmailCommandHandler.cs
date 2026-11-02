namespace Taxi.Application.Features.Account.Commands.ConfirmEmail;

using MediatR;
using Taxi.Application.Common.Interfaces;
using Taxi.Domain.Common.Results;

public class ConfirmEmailCommandHandler(IIdentityService identityService)
    : IRequestHandler<ConfirmEmailCommand, Result<Success>>
{
    private readonly IIdentityService identityService = identityService;

    public Task<Result<Success>> Handle(ConfirmEmailCommand request, CancellationToken cancellationToken) =>
        this.identityService.ConfirmEmailAsync(request.UserId, request.Token);
}
