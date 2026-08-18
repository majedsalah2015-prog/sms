using Sms.Application.Backup;
using Sms.TestSupport;
using Xunit;

namespace Sms.Application.Tests.Backup
{
    public class BackupCompletenessEvaluatorTests
    {
        [Fact]
        [BusinessRule("BR-BAK-001")]
        public void Complete_only_when_all_three_components_are_present()
        {
            Assert.True(BackupCompletenessEvaluator.IsComplete(true, true, true));
        }

        [Theory]
        [InlineData(false, true, true)]
        [InlineData(true, false, true)]
        [InlineData(true, true, false)]
        [InlineData(false, false, false)]
        [BusinessRule("BR-BAK-001")]
        public void A_partial_set_is_not_complete(bool db, bool attachments, bool config)
        {
            Assert.False(BackupCompletenessEvaluator.IsComplete(db, attachments, config));
        }
    }
}
