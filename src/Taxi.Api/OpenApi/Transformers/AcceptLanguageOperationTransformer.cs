using System.Text.Json.Nodes;

using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

using Taxi.Contracts.Common;

namespace Taxi.Api.OpenApi.Transformers;

internal sealed class AcceptLanguageOperationTransformer : IOpenApiOperationTransformer
{
    public Task TransformAsync(
        OpenApiOperation operation,
        OpenApiOperationTransformerContext context,
        CancellationToken cancellationToken)
    {
        operation.Parameters ??= new List<IOpenApiParameter>();

        operation.Parameters.Add(new OpenApiParameter
        {
            Name = "Accept-Language",

            // The enum property in OpenAPI 2.x/vnext uses ParameterLocation
            In = ParameterLocation.Header,
            Required = false,
            Schema = new OpenApiSchema
            {
                Type = JsonSchemaType.String,
                Default = JsonValue.Create(Languages.Default),
                Enum = [.. Languages.All.Select(code => (JsonNode)JsonValue.Create(code)!)],
                Title = "Accept-Language Options"
            },
            Description = $"The preferred language for the response ({string.Join(" or ", Languages.All)})."
        });

        return Task.CompletedTask;
    }
}