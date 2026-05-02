using MediatR;

using Taxi.Application.Features.Identity.Dtos;
using Taxi.Domain.Common.Results;

namespace Taxi.Application.Features.Identity.Queries.GenerateTokens;

public record GenerateTokenQuery(
    string Email,
    string Password) : IRequest<Result<TokenResponse>>;