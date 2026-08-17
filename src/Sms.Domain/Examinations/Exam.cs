using System;
using Sms.Domain.Audit;
using Sms.Domain.Common;

namespace Sms.Domain.Examinations
{
    /// <summary>
    /// core.Exam (doc/Modules/16 §7, BR-EXM-002/003): round x offering,
    /// dated. BlueprintComponentId is BR-EXM-002's joint-ownership seam —
    /// Module 16 (this entity) owns the exam event's existence/date/room;
    /// Module 17's Blueprint/BlueprintComponent (E-302) owns the weight
    /// and is where the resulting marks actually get entered/calculated
    /// (doc §7: "single marks store... one aggregate" — Examinations does
    /// not duplicate Marksheet/MarkEntry, it feeds the same one).
    /// </summary>
    [Audited(AuditTier.T2)]
    public class Exam : AuditableEntity, ISchoolScoped
    {
        public int SchoolId { get; set; }

        public int ExamRoundId { get; set; }

        public int ExamTypeId { get; set; }

        public int CurriculumOfferingId { get; set; }

        public int GradeYearProfileId { get; set; }

        public int BlueprintComponentId { get; set; }

        public DateTime Date { get; set; }

        public TimeSpan StartTime { get; set; }

        public int DurationMinutes { get; set; }
    }
}
