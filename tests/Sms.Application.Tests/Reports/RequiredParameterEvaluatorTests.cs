using Sms.Application.Reports;
using Sms.TestSupport;
using Xunit;

namespace Sms.Application.Tests.Reports
{
    public class RequiredParameterEvaluatorTests
    {
        [Fact]
        public void ParseRequiredKeys_handles_null_and_blank()
        {
            Assert.Empty(RequiredParameterEvaluator.ParseRequiredKeys(null));
            Assert.Empty(RequiredParameterEvaluator.ParseRequiredKeys("   "));
        }

        [Fact]
        public void ParseRequiredKeys_splits_trims_and_drops_empties()
        {
            var keys = RequiredParameterEvaluator.ParseRequiredKeys(" SchoolId ,, TermId,SectionId ");

            Assert.Equal(new[] { "SchoolId", "TermId", "SectionId" }, keys);
        }

        [Fact]
        public void FindMissing_is_case_insensitive_and_reports_only_absent_keys()
        {
            var required = new[] { "SchoolId", "TermId" };

            var missing = RequiredParameterEvaluator.FindMissing(required, new[] { "schoolid" });

            Assert.Equal(new[] { "TermId" }, missing);
        }

        [Fact]
        public void FindMissing_is_empty_when_all_keys_supplied()
        {
            var required = new[] { "SchoolId", "TermId" };

            var missing = RequiredParameterEvaluator.FindMissing(required, new[] { "SchoolId", "TermId" });

            Assert.Empty(missing);
        }
    }
}
