namespace Taxi.Application.Features.Account.Commands.ForgotPassword;

using MediatR;
using Microsoft.Extensions.Options;
using Taxi.Application.Common.Interfaces;
using Taxi.Application.Common.Settings;
using Taxi.Application.Features.Account.Emails;
using Taxi.Domain.Common.Results;

public class ForgotPasswordCommandHandler(
    IIdentityService identityService,
    IEmailSender emailSender,
    ITextLocalizer text,
    IOptions<ClientAppSettings> clientApp) : IRequestHandler<ForgotPasswordCommand, Result<Success>>
{
    private readonly IIdentityService identityService = identityService;
    private readonly IEmailSender emailSender = emailSender;
    private readonly ITextLocalizer text = text;
    private readonly ClientAppSettings clientApp = clientApp.Value;

    public async Task<Result<Success>> Handle(ForgotPasswordCommand request, CancellationToken cancellationToken)
    {
        var user = await this.identityService.FindByEmailAsync(request.Email);

        // Unconfirmed accounts get no reset: the address has never been proven to belong to them.
        if (user is null || !user.EmailConfirmed)
        {
            return Result.Success;
        }

        var tokenResult = await this.identityService.GeneratePasswordResetTokenAsync(user.UserId);
        if (tokenResult.IsError)
        {
            return tokenResult.Errors;
        }

        await this.emailSender.SendAsync(
            AccountEmails.ResetPassword(this.text, user.Email, this.clientApp.ResetPasswordUrl, tokenResult.Value),
            cancellationToken);

        return Result.Success;
    }
}
