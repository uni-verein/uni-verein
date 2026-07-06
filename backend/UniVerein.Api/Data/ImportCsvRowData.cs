using System;
using CsvHelper.Configuration.Attributes;

namespace UniVerein.Api.Data;

public class ImportCsvRowData
{
    [Name("Member number")] 
    public int? MemberNumber { get; set; }
    [Name("Gender")] 
    public string? Gender { get; set; }
    [Name("Name")] 
    public string? Name { get; set; }
    [Name("First name")] 
    public string? FirstName { get; set; }
    [Name("Middle name")] 
    public string? MiddleName { get; set; }
    [Name("Birthday")] 
    public DateTimeOffset? Birthday { get; set; }
    [Name("Phone nummer")] 
    public string? PhoneNummer { get; set; }
    [Name("Bulk mail")] 
    public string? BulkMail { get; set; }
    [Name("Mail")]
    public string? EMail { get; set; }
    [Name("Street and number")] 
    public string? StreetAndNumber { get; set; }
    [Name("ZIP code")] 
    public string? Zip { get; set; }
    [Name("City")]
    public string? City { get; set; }
    [Name("Country code")]
    public string? CountryCode { get; set; }
    [Name("Study start")] 
    public DateTimeOffset? StudyStart { get; set; }
    [Name("Study end")] 
    public DateTimeOffset? StudyEnd { get; set; }
    [Name("Academic degree")] 
    public string? AcademicDegree { get; set; }
    [Name("Course of study")] 
    public string? CourseOfStudy { get; set; }
    [Name("Task within the club")] 
    public string? TaskWithinTheClub { get; set; }
    [Name("Member category")] 
    public string? MemberCategory { get; set; }
    [Name("Entry date")] 
    public DateTimeOffset? EntryDate { get; set; }
    [Name("Exit date")] 
    public DateTimeOffset? ExitDate { get; set; }
    [Name("IBAN")] 
    public string? Iban { get; set; }
    [Name("BIC")] 
    public string? Bic { get; set; }
    [Name("Sepa consent date")] 
    public DateTimeOffset? SepaConsentDate { get; set; }
    [Name("Contribution amount")] 
    public int? ContributionAmount { get; set; }
}