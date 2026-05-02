namespace Taxi.Application.Features.Identity.Queries.RefreshTokens;

using MediatR;
using Taxi.Application.Common.Interfaces;
using Taxi.Application.Features.Identity.Dtos;
using Taxi.Domain.Common.Results;

public record RefreshTokenQuery(string ExpiredAccessToken, string RefreshToken) : IRequest<Result<TokenResponse>>;
