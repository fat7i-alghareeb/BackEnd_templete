using System.Net.Http.Json;
using System.Text.Json;

namespace Taxi.Api.IntegrationTests.Infrastructure;

/// <summary>Reads RFC 7807 bodies without depending on the MVC ProblemDetails types.</summary>
public static class ProblemJson
{
    public static async Task<JsonElement> ReadJsonAsync(this HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<JsonElement>();

    public static async Task<string> TitleAsync(this HttpResponseMessage response) =>
        (await response.ReadJsonAsync()).GetProperty("title").GetString()!;

    public static async Task<string[]> FieldErrorsAsync(this HttpResponseMessage response, string field) =>
        (await response.ReadJsonAsync()).GetProperty("errors").GetProperty(field)
            .EnumerateArray().Select(e => e.GetString()!).ToArray();
}
