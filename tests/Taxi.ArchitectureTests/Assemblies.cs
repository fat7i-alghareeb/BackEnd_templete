using System.Reflection;

namespace Taxi.ArchitectureTests;

internal static class Assemblies
{
    public static readonly Assembly Contracts = typeof(Taxi.Contracts.Common.LocalizationKeys).Assembly;

    public static readonly Assembly Domain = typeof(Taxi.Domain.Common.Entity).Assembly;

    public static readonly Assembly Application = typeof(Taxi.Application.Common.Interfaces.IAppDbContext).Assembly;

    public static readonly Assembly Infrastructure = typeof(Taxi.Infrastructure.Data.AppDbContext).Assembly;

    public static readonly Assembly Api = typeof(Taxi.Api.Controllers.ApiController).Assembly;
}
