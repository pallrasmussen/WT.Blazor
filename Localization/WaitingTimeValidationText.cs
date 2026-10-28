using WT.Blazor.Models;

namespace WT.Blazor.Localization;

public static class WaitingTimeValidationText
{
    public static string Message(WaitingTimeValidationIssue issue) => issue.Error switch
    {
        WaitingTimeInputError.HospitalRequired or WaitingTimeInputError.InvalidHospital => AppText.Get("SelectHospitalValidation"),
        WaitingTimeInputError.DepartmentRequired or WaitingTimeInputError.InvalidDepartment => AppText.Get("SelectDepartmentValidation"),
        WaitingTimeInputError.NameRequired => AppText.Get("EnterNameValidation"),
        WaitingTimeInputError.NegativeWait => AppText.Get("NegativeWaitValidation"),
        WaitingTimeInputError.MaximumWait => string.Format(AppText.Get("MaximumWaitValidation"), issue.Limit),
        WaitingTimeInputError.InvalidUpperBound => AppText.Get("WaitRangeValidation"),
        WaitingTimeInputError.NameTooLong or WaitingTimeInputError.NoteTooLong
            or WaitingTimeInputError.OwnerTooLong or WaitingTimeInputError.ComparisonCodeTooLong =>
                string.Format(AppText.Get("FieldLengthValidation"), AppText.Get(issue.Field switch
                {
                    nameof(WaitingTimeInput.Name) => "SpecialtyTreatment",
                    nameof(WaitingTimeInput.Note) => "ReferralConditions",
                    _ => issue.Field
                }), issue.Limit),
        _ => AppText.Get("InvalidEstimateValidation")
    };
}
