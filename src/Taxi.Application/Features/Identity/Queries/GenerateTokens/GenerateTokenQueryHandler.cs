namespace Taxi.Application.Features.Identity.Queries.GenerateTokens;

using MediatR;
using Microsoft.Extensions.Logging;
using Taxi.Application.Common.Interfaces;
using Taxi.Application.Features.Identity.Dtos;
using Taxi.Domain.Common.Results;

public class GenerateTokenQueryHandler(
    ILogger<GenerateTokenQueryHandler> logger,
    IIdentityService identityService,
    ITokenProvider tokenProvider)
    : IRequestHandler<GenerateTokenQuery, Result<TokenResponse>>
{
    private readonly ILogger<GenerateTokenQueryHandler> logger = logger;
    private readonly IIdentityService identityService = identityService;
    private readonly ITokenProvider tokenProvider = tokenProvider;

    public async Task<Result<TokenResponse>> Handle(GenerateTokenQuery request, CancellationToken ct)
    {
        var checkPasswordResult = await this.identityService.AuthenticateAsync(request.Email, request.Password);

        if (checkPasswordResult.IsError)
        {
            this.logger.LogError("Check password error occurred for email {Email}: {ErrorDescription}", request.Email, checkPasswordResult.TopError.Description);
            return checkPasswordResult.Errors;
        }

        var generateTokenResult = await this.tokenProvider.GenerateJwtTokenAsync(checkPasswordResult.Value, ct);

        if (generateTokenResult.IsError)
        {
            this.logger.LogError("Generate token error occurred for email {Email}: {ErrorDescription}", request.Email, generateTokenResult.TopError.Description);
            return generateTokenResult.Errors;
        }

        return generateTokenResult.Value;
    }
}
