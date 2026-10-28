namespace WT.Blazor.Models;

public enum WaitingTimeInputError
{
    HospitalRequired, InvalidHospital, DepartmentRequired, InvalidDepartment,
    NameRequired, NameTooLong, InvalidStatus, InvalidEndpoint, InvalidUnit,
    NegativeWait, MaximumWait, InvalidUpperBound, OwnerTooLong, ComparisonCodeTooLong, NoteTooLong
}

public sealed record WaitingTimeValidationIssue(WaitingTimeInputError Error, string Field, int? Limit = null)
{
    public ArgumentException ToException() => Error switch
    {
        WaitingTimeInputError.InvalidHospital or WaitingTimeInputError.InvalidDepartment
            or WaitingTimeInputError.InvalidStatus or WaitingTimeInputError.InvalidEndpoint
            or WaitingTimeInputError.InvalidUnit or WaitingTimeInputError.NegativeWait
            or WaitingTimeInputError.MaximumWait or WaitingTimeInputError.InvalidUpperBound =>
                new ArgumentOutOfRangeException(Field, $"Invalid waiting-time input: {Error}."),
        _ => new ArgumentException($"Invalid waiting-time input: {Error}.", Field)
    };
}

public static class WaitingTimeValidation
{
    public static WaitingTimeInput Normalize(WaitingTimeInput input) => input with
    {
        Name = input.Name?.Trim() ?? "",
        Note = TrimOptional(input.Note),
        ResponsibleDepartment = TrimOptional(input.ResponsibleDepartment),
        ComparisonCode = TrimOptional(input.ComparisonCode)?.ToUpperInvariant(),
        Department = input.Hospital == Hospital.Landssjukrahusid ? input.Department : null,
        WaitValue = input.EstimateStatus == EstimateStatus.Available ? input.WaitValue : 0,
        WaitUpperValue = input.EstimateStatus == EstimateStatus.Available ? input.WaitUpperValue : null
    };

    public static WaitingTimeValidationIssue? Validate(WaitingTimeInput input)
    {
        if (input.Hospital is null) return new(WaitingTimeInputError.HospitalRequired, nameof(input.Hospital));
        if (!Enum.IsDefined(input.Hospital.Value)) return new(WaitingTimeInputError.InvalidHospital, nameof(input.Hospital));
        if (input.Hospital == Hospital.Landssjukrahusid && input.Department is null)
            return new(WaitingTimeInputError.DepartmentRequired, nameof(input.Department));
        if (input.Department is { } department && !Enum.IsDefined(department))
            return new(WaitingTimeInputError.InvalidDepartment, nameof(input.Department));
        input = Normalize(input);
        if (input.Name.Length == 0) return new(WaitingTimeInputError.NameRequired, nameof(input.Name));
        if (input.Name.Length > FieldLimits.WaitingTimeName) return new(WaitingTimeInputError.NameTooLong, nameof(input.Name), FieldLimits.WaitingTimeName);
        if (!Enum.IsDefined(input.EstimateStatus)) return new(WaitingTimeInputError.InvalidStatus, nameof(input.EstimateStatus));
        if (!Enum.IsDefined(input.ReferralEndpoint)) return new(WaitingTimeInputError.InvalidEndpoint, nameof(input.ReferralEndpoint));
        if (!Enum.IsDefined(input.Unit)) return new(WaitingTimeInputError.InvalidUnit, nameof(input.Unit));
        if (input.WaitValue < 0) return new(WaitingTimeInputError.NegativeWait, nameof(input.WaitValue));
        if (input.WaitValue > WaitDuration.MaximumValue(input.Unit))
            return new(WaitingTimeInputError.MaximumWait, nameof(input.WaitValue), WaitDuration.MaximumValue(input.Unit));
        if (input.WaitUpperValue is { } upper && (upper < input.WaitValue || upper > WaitDuration.MaximumValue(input.Unit)))
            return new(WaitingTimeInputError.InvalidUpperBound, nameof(input.WaitUpperValue));
        if (input.ResponsibleDepartment?.Length > FieldLimits.ResponsibleDepartment) return new(WaitingTimeInputError.OwnerTooLong, nameof(input.ResponsibleDepartment), FieldLimits.ResponsibleDepartment);
        if (input.ComparisonCode?.Length > FieldLimits.ComparisonCode) return new(WaitingTimeInputError.ComparisonCodeTooLong, nameof(input.ComparisonCode), FieldLimits.ComparisonCode);
        if (input.Note?.Length > FieldLimits.Note) return new(WaitingTimeInputError.NoteTooLong, nameof(input.Note), FieldLimits.Note);
        return null;
    }

    public static WaitingTimeInput NormalizeAndValidate(WaitingTimeInput input)
    {
        if (Validate(input) is { } issue) throw issue.ToException();
        return Normalize(input);
    }

    private static string? TrimOptional(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();
}
