namespace Taxi.Application.Features.Account.Commands.ResendConfirmation;

using MediatR;
using Microsoft.Extensions.Options;
using Taxi.Application.Common.Interfaces;
using Taxi.Application.Common.Settings;
using Taxi.Application.Features.Account.Emails;
using Taxi.Domain.Common.Results;

public class ResendConfirmationCommandHandler(
    IIdentityService identityService,
    IEmailSender emailSender,
    ITextLocalizer text,
    IOptions<ClientAppSettings> clientApp) : IRequestHandler<ResendConfirmationCommand, Result<Success>>
{
    private readonly IIdentityService identityService = identityService;
    private readonly IEmailSender emailSender = emailSender;
    private readonly ITextLocalizer text = text;
    private readonly ClientAppSettings clientApp = clientApp.Value;

    public async Task<Result<Success>> Handle(ResendConfirmationCommand request, CancellationToken cancellationToken)
    {
        var user = await this.identityService.FindByEmailAsync(request.Email);

        if (user is null || user.EmailConfirmed)
        {
            return Result.Success;
        }

        var tokenResult = await this.identityService.GenerateEmailConfirmationTokenAsync(user.UserId);
        if (tokenResult.IsError)
        {
            return tokenResult.Errors;
        }

        await this.emailSender.SendAsync(
            AccountEmails.ConfirmEmail(this.text, user.Email, this.clientApp.ConfirmEmailUrl, user.UserId, tokenResult.Value),
            cancellationToken);

        return Result.Success;
    }
}
