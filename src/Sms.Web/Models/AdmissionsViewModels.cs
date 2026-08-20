using System;
using System.Collections.Generic;
using Sms.Domain.Admissions;
using Sms.Domain.Common;
using Sms.Domain.Grades;
using Sms.Domain.Parents;
using Sms.Domain.Schools;
using Sms.Domain.Sections;
using AdmissionApplication = Sms.Domain.Admissions.Application;

namespace Sms.Web.Models
{
    public sealed class CampaignListViewModel
    {
        public sealed record Row(AdmissionCampaign Campaign, GradeLevel Grade, GradeYearProfile Profile, int Applications, int Approved, int Registered, int Waitlisted, bool IsOpen);

        public IReadOnlyList<Row> Rows { get; set; } = Array.Empty<Row>();

        public IReadOnlyList<AcademicYear> Years { get; set; } = Array.Empty<AcademicYear>();

        public AcademicYear? Year { get; set; }

        public IReadOnlyList<(int ProfileId, GradeLevel Grade)> Profiles { get; set; } = Array.Empty<(int, GradeLevel)>();

        // form
        public int? GradeYearProfileId { get; set; }

        public DateTime? OpenDate { get; set; }

        public DateTime? CloseDate { get; set; }

        public bool RequiresAssessment { get; set; }

        public decimal? ApplicationFeeAmount { get; set; }
    }

    public sealed class PipelineBoardViewModel
    {
        public sealed record Card(AdmissionApplication Application, int AgeDays, decimal? Score, bool HasParent, bool SlaBreached);

        public sealed record Column(ApplicationStatus Status, IReadOnlyList<Card> Cards);

        public AdmissionCampaign Campaign { get; set; } = null!;

        public GradeLevel Grade { get; set; } = null!;

        public AcademicYear Year { get; set; } = null!;

        public IReadOnlyList<Column> Columns { get; set; } = Array.Empty<Column>();

        /// <summary>"board" (kanban, default) or "grid" (flat table of the same cards).</summary>
        public string ViewMode { get; set; } = "board";

        public IReadOnlyList<AdmissionCampaign> Campaigns { get; set; } = Array.Empty<AdmissionCampaign>();

        public IReadOnlyDictionary<int, string> CampaignLabels { get; set; } = new Dictionary<int, string>();

        public int ReviewSlaDays { get; set; } = 5;
    }

    public sealed class ApplicationFormViewModel
    {
        public int CampaignId { get; set; }

        public string CampaignLabel { get; set; } = string.Empty;

        public IReadOnlyList<(int Id, string Ar, string En)> Nationalities { get; set; } = Array.Empty<(int, string, string)>();

        public IReadOnlyList<Parent> Parents { get; set; } = Array.Empty<Parent>();

        public string? FirstNameAr { get; set; }

        public string? FatherNameAr { get; set; }

        public string? GrandfatherNameAr { get; set; }

        public string? FamilyNameAr { get; set; }

        public string? FirstNameEn { get; set; }

        public string? FatherNameEn { get; set; }

        public string? GrandfatherNameEn { get; set; }

        public string? FamilyNameEn { get; set; }

        public Gender Gender { get; set; } = Gender.Male;

        public DateTime? DateOfBirth { get; set; }

        public int? NationalityLookupId { get; set; }

        // parent: pick existing or quick-create (dedup by mobile)
        public int? ParentId { get; set; }

        public string? NewParentNameAr { get; set; }

        public string? NewParentNameEn { get; set; }

        public string? NewParentMobile { get; set; }

        public string? NewParentEmail { get; set; }

        public bool SubmitImmediately { get; set; } = true;
    }

    public sealed class ApplicationDetailViewModel
    {
        public AdmissionApplication Application { get; set; } = null!;

        public AdmissionCampaign Campaign { get; set; } = null!;

        public GradeLevel Grade { get; set; } = null!;

        public AcademicYear Year { get; set; } = null!;

        public string NationalityName { get; set; } = string.Empty;

        public Parent? Parent { get; set; }

        public IReadOnlyList<Parent> Parents { get; set; } = Array.Empty<Parent>();

        public IReadOnlyList<ApplicationAssessment> Assessments { get; set; } = Array.Empty<ApplicationAssessment>();

        public WaitingListEntry? WaitingListEntry { get; set; }

        public IReadOnlyList<ApplicationStatus> AllowedTransitions { get; set; } = Array.Empty<ApplicationStatus>();

        public IReadOnlyList<Section> Sections { get; set; } = Array.Empty<Section>();

        public IReadOnlyDictionary<int, int> SectionMembers { get; set; } = new Dictionary<int, int>();

        public IReadOnlyList<(int Id, string Ar, string En)> Relationships { get; set; } = Array.Empty<(int, string, string)>();

        public IReadOnlyList<(int Id, string Ar, string En)> Nationalities { get; set; } = Array.Empty<(int, string, string)>();

        public bool CanEdit => Application.Status is ApplicationStatus.Draft or ApplicationStatus.Submitted or ApplicationStatus.UnderReview or ApplicationStatus.Recommended;

        public IReadOnlyList<(string EntityType, string Action, string? Field, string? NewValue, DateTime At, int Actor)> History { get; set; } = Array.Empty<(string, string, string?, string?, DateTime, int)>();

        public bool CanRegister => Application.Status == ApplicationStatus.Approved && Application.RegisteredStudentId == null;
    }

    public sealed class WaitingListViewModel
    {
        public sealed record Row(WaitingListEntry Entry, AdmissionApplication Application, bool OfferExpired);

        public IReadOnlyList<(int ProfileId, GradeLevel Grade, AcademicYear Year)> Profiles { get; set; } = Array.Empty<(int, GradeLevel, AcademicYear)>();

        public int? ProfileId { get; set; }

        public IReadOnlyList<Row> Rows { get; set; } = Array.Empty<Row>();

        public int PlannedSeats { get; set; }

        public int Enrolled { get; set; }
    }
}
