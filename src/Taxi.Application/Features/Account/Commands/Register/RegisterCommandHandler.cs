namespace Taxi.Application.Features.Account.Commands.Register;

using MediatR;
using Microsoft.Extensions.Options;
using Taxi.Application.Common.Interfaces;
using Taxi.Application.Common.Security;
using Taxi.Application.Common.Settings;
using Taxi.Application.Features.Account.Emails;
using Taxi.Domain.Common.Results;

public class RegisterCommandHandler(
    IIdentityService identityService,
    IEmailSender emailSender,
    ITextLocalizer text,
    IOptions<ClientAppSettings> clientApp) : IRequestHandler<RegisterCommand, Result<Success>>
{
    private readonly IIdentityService identityService = identityService;
    private readonly IEmailSender emailSender = emailSender;
    private readonly ITextLocalizer text = text;
    private readonly ClientAppSettings clientApp = clientApp.Value;

    public async Task<Result<Success>> Handle(RegisterCommand request, CancellationToken cancellationToken)
    {
        // Password policy first, for every caller: if it ran only for new emails, a weak password
        // would answer 400 for a new address and 202 for an existing one.
        var passwordResult = await this.identityService.ValidatePasswordAsync(request.Password, nameof(request.Password));
        if (passwordResult.IsError)
        {
            return passwordResult.Errors;
        }

        var existing = await this.identityService.FindByEmailAsync(request.Email);
        if (existing is not null)
        {
            // Tell the owner of the address instead of the caller.
            await this.emailSender.SendAsync(AccountEmails.AlreadyRegistered(this.text, existing.Email), cancellationToken);
            return Result.Success;
        }

        var createResult = await this.identityService.CreateUserAsync(request.Email, request.Password, Roles.User);
        if (createResult.IsError)
        {
            return createResult.Errors;
        }

        var userId = createResult.Value;

        var tokenResult = await this.identityService.GenerateEmailConfirmationTokenAsync(userId);
        if (tokenResult.IsError)
        {
            return tokenResult.Errors;
        }

        await this.emailSender.SendAsync(
            AccountEmails.ConfirmEmail(this.text, request.Email, this.clientApp.ConfirmEmailUrl, userId, tokenResult.Value),
            cancellationToken);

        return Result.Success;
    }
}
