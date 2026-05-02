namespace Taxi.Domain.Cars;

using Taxi.Domain.Common;
using Taxi.Domain.Common.Results;

public sealed class Car : AuditableEntity
{
    private Car()
    {
    }

    private Car(Guid id, string make, string model, int year, LocalizedText description)
        : base(id)
    {
        this.Make = make;
        this.Model = model;
        this.Year = year;
        this.Description = description;
    }

    public string Make { get; private set; } = null!;

    public string Model { get; private set; } = null!;

    public int Year { get; private set; }

    public LocalizedText Description { get; private set; } = null!;

    public static Result<Car> Create(Guid id, string make, string model, int year, string descriptionEn, string descriptionAr)
    {
        if (id == Guid.Empty)
        {
            return CarErrors.IdRequired;
        }

        if (string.IsNullOrWhiteSpace(make))
        {
            return CarErrors.MakeRequired;
        }

        if (string.IsNullOrWhiteSpace(model))
        {
            return CarErrors.ModelRequired;
        }

        if (year < 1886)
        {
            return CarErrors.InvalidYear;
        }

        if (string.IsNullOrWhiteSpace(descriptionEn) || string.IsNullOrWhiteSpace(descriptionAr))
        {
            return CarErrors.DescriptionRequired;
        }

        return new Car(id, make, model, year, new LocalizedText(descriptionEn.Trim(), descriptionAr.Trim()));
    }

    public Result<Updated> Update(string make, string model, int year, string descriptionEn, string descriptionAr)
    {
        if (string.IsNullOrWhiteSpace(make))
        {
            return CarErrors.MakeRequired;
        }

        if (string.IsNullOrWhiteSpace(model))
        {
            return CarErrors.ModelRequired;
        }

        if (year < 1886)
        {
            return CarErrors.InvalidYear;
        }

        if (string.IsNullOrWhiteSpace(descriptionEn) || string.IsNullOrWhiteSpace(descriptionAr))
        {
            return CarErrors.DescriptionRequired;
        }

        this.Make = make;
        this.Model = model;
        this.Year = year;
        this.Description = new LocalizedText(descriptionEn.Trim(), descriptionAr.Trim());

        return Result.Updated;
    }
}
