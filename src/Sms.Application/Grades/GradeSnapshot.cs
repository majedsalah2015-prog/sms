namespace Sms.Application.Grades
{
    public sealed class GradeSnapshot
    {
        public GradeSnapshot(int gradeLevelId, int? promotionTargetGradeLevelId, bool isGraduating)
        {
            GradeLevelId = gradeLevelId;
            PromotionTargetGradeLevelId = promotionTargetGradeLevelId;
            IsGraduating = isGraduating;
        }

        public int GradeLevelId { get; }

        public int? PromotionTargetGradeLevelId { get; }

        public bool IsGraduating { get; }
    }
}
