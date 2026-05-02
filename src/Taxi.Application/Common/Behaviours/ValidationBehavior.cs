namespace Taxi.Application.Common.Behaviours;

using FluentValidation;

using MediatR;

using Taxi.Domain.Common.Results;
using Taxi.Domain.Common.Results.Abstractions;

public class ValidationBehavior<TRequest, TResponse>(IValidator<TRequest>? validator = null)
    : IPipelineBehavior<TRequest, TResponse>
        where TRequest : IRequest<TResponse>
        where TResponse : IResult
{
    private readonly IValidator<TRequest>? validator = validator;

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken ct)
    {
        if (this.validator is null)
        {
            return await next(ct);
        }

        var validationResult = await this.validator.ValidateAsync(request, ct);

        if (validationResult.IsValid)
        {
            return await next(ct);
        }

        var errors = validationResult.Errors
            .ConvertAll(e => Error.ValidationForProperty(
                propertyName: e.PropertyName,
                code: e.ErrorMessage));

        return (dynamic)errors;
    }
}