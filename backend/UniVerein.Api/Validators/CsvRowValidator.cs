using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UniVerein.Api.ApiResults;
using UniVerein.Api.Data;
using UniVerein.DAL.Entities.Enums;

namespace UniVerein.Api.Validators;

public class CsvRowValidator
{
    private readonly Dictionary<string, AcademicDegree> _academicDegreeMapping;
    private static readonly Regex IbanRegex = new(@"^[A-Z]{2}[0-9]{2}[A-Z0-9]{1,30}$", RegexOptions.Compiled);
    private static readonly Regex BicRegex = new(@"^[A-Z]{6}[A-Z0-9]{2}([A-Z0-9]{3})?$", RegexOptions.Compiled);
    private static readonly Regex EmailRegex = new(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled);

    public CsvRowValidator(Dictionary<string, AcademicDegree> academicDegreeMapping)
    {
        _academicDegreeMapping = academicDegreeMapping;
    }

    public List<ErrorResultTranslation> Validate(ImportCsvRowData row, int rowNumber)
    {
        List<ErrorResultTranslation> errors = new();

        if (row.MemberNumber == null)
            errors.Add(new() { TranslationKey = $"validator.memberNumber.required", Values = [rowNumber.ToString()] });
        else if (row.MemberNumber <= 0)
            errors.Add(new()
            {
                TranslationKey = $"validator.memberNumber.invalid",
                Values = [rowNumber.ToString(), $"{row.MemberNumber}"]
            });

        if (string.IsNullOrWhiteSpace(row.Name))
            errors.Add(new() { TranslationKey = $"validator.name.required", Values = [rowNumber.ToString()] });

        if (string.IsNullOrWhiteSpace(row.FirstName))
            errors.Add(new() { TranslationKey = $"validator.firstName.required", Values = [rowNumber.ToString()] });

        if (row.Birthday == null)
            errors.Add(new() { TranslationKey = $"validator.birthday.required", Values = [rowNumber.ToString()] });
        else if (row.Birthday > DateTimeOffset.UtcNow)
            errors.Add(new()
                { TranslationKey = $"validator.birthday.invalid", Values = [rowNumber.ToString(), $"{row.Birthday}"] });

        if (string.IsNullOrWhiteSpace(row.City))
            errors.Add(new() { TranslationKey = $"validator.city.required", Values = [rowNumber.ToString()] });

        if (string.IsNullOrWhiteSpace(row.Zip))
            errors.Add(new() { TranslationKey = $"validator.zip.required", Values = [rowNumber.ToString()] });

        if (string.IsNullOrWhiteSpace(row.StreetAndNumber))
            errors.Add(
                new() { TranslationKey = $"validator.streetAndNumber.required", Values = [rowNumber.ToString()] });

        if (row.EntryDate == null)
            errors.Add(new() { TranslationKey = $"validator.entryDate.required", Values = [rowNumber.ToString()] });

        if (string.IsNullOrWhiteSpace(row.Gender))
            errors.Add(new() { TranslationKey = $"validator.gender.required", Values = [rowNumber.ToString()] });
        else if (!Enum.TryParse<Gender>(row.Gender, ignoreCase: true, out _))
            errors.Add(new()
            {
                TranslationKey = $"validator.gender.invalid",
                Values = [rowNumber.ToString(), $"{row.Gender}", string.Join(", ", Enum.GetNames<Gender>())]
            });

        if (string.IsNullOrWhiteSpace(row.BulkMail))
            errors.Add(new() { TranslationKey = $"validator.bulkMail.required", Values = [rowNumber.ToString()] });
        else if (!Enum.TryParse<BulkMail>(row.BulkMail, ignoreCase: true, out _))
            errors.Add(new()
            {
                TranslationKey = $"validator.bulkMail.invalid",
                Values = [rowNumber.ToString(), $"{row.BulkMail}", string.Join(", ", Enum.GetNames<BulkMail>())]
            });

        if (string.IsNullOrWhiteSpace(row.EMail))
            errors.Add(new() { TranslationKey = $"validator.mail.required", Values = [rowNumber.ToString()] });
        else if (!EmailRegex.IsMatch(row.EMail))
            errors.Add(new()
                { TranslationKey = $"validator.mail.invalid", Values = [rowNumber.ToString(), $"{row.EMail}"] });

        if (!string.IsNullOrWhiteSpace(row.AcademicDegree) &&
            !_academicDegreeMapping.ContainsKey(row.AcademicDegree.ToLower()))
            errors.Add(new()
            {
                TranslationKey = $"validator.academicDegree.invalid",
                Values = [rowNumber.ToString(), $"{row.AcademicDegree}", string.Join(", ", _academicDegreeMapping.Keys)]
            });

        if (!string.IsNullOrWhiteSpace(row.TaskWithinTheClub) &&
            !Enum.TryParse<TaskWithinTheClub>(row.TaskWithinTheClub, ignoreCase: true, out _))
            errors.Add(new()
            {
                TranslationKey = $"validator.taskWithinTheClub.invalid",
                Values =
                [
                    rowNumber.ToString(), $"{row.TaskWithinTheClub}",
                    string.Join(", ", Enum.GetNames<TaskWithinTheClub>())
                ]
            });

        if (row.StudyEnd != null && row.StudyStart == null)
            errors.Add(new() { TranslationKey = $"validator.studyStart.required", Values = [rowNumber.ToString()] });

        if (row.StudyStart != null && row.StudyEnd != null && row.StudyEnd < row.StudyStart)
            errors.Add(new()
            {
                TranslationKey = $"validator.studyEnd.invalid",
                Values = [rowNumber.ToString(), $"{row.StudyEnd}", $"{row.StudyStart}"]
            });

        if (row.EntryDate != null && row.ExitDate != null && row.ExitDate < row.EntryDate)
            errors.Add(new()
            {
                TranslationKey = $"validator.entryExitDate.invalid",
                Values = [rowNumber.ToString(), $"{row.ExitDate}", $"{row.EntryDate}"]
            });

        if (!string.IsNullOrWhiteSpace(row.Iban))
        {
            string ibanNormalized = row.Iban.Replace(" ", "").ToUpper();
            if (!IbanRegex.IsMatch(ibanNormalized))
                errors.Add(new()
                    { TranslationKey = $"validator.iban.invalid", Values = [rowNumber.ToString(), $"{row.Iban}"] });
        }

        if (!string.IsNullOrWhiteSpace(row.Bic))
        {
            string bicNormalized = row.Bic.Replace(" ", "").ToUpper();
            if (!BicRegex.IsMatch(bicNormalized))
                errors.Add(new()
                    { TranslationKey = $"validator.bic.invalid", Values = [rowNumber.ToString(), $"{row.Bic}"] });
        }

        if (row.ContributionAmount != null && row.ContributionAmount < 0)
            errors.Add(new()
            {
                TranslationKey = $"validator.contributionAmount.invalid",
                Values = [rowNumber.ToString(), $"{row.ContributionAmount}"]
            });

        return errors;
    }
}