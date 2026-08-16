using System;
using Sms.Application.Fees;
using Sms.TestSupport;
using Xunit;

namespace Sms.Application.Tests.Fees
{
    public class InvoiceHashChainBuilderTests
    {
        [Fact]
        [BusinessRule("BR-FEE-005")]
        public void Same_inputs_produce_the_same_hash()
        {
            var uuid = Guid.NewGuid().ToString();
            var postedAt = new DateTime(2027, 3, 1, 10, 0, 0, DateTimeKind.Utc);

            var first = InvoiceHashChainBuilder.ComputeHash(uuid, 1150m, postedAt, null);
            var second = InvoiceHashChainBuilder.ComputeHash(uuid, 1150m, postedAt, null);

            Assert.Equal(first, second);
        }

        [Fact]
        [BusinessRule("BR-FEE-005")]
        public void Different_previous_hash_changes_the_result_chain()
        {
            var uuid = Guid.NewGuid().ToString();
            var postedAt = new DateTime(2027, 3, 1, 10, 0, 0, DateTimeKind.Utc);

            var withoutPrevious = InvoiceHashChainBuilder.ComputeHash(uuid, 1150m, postedAt, null);
            var withPrevious = InvoiceHashChainBuilder.ComputeHash(uuid, 1150m, postedAt, "ABC123");

            Assert.NotEqual(withoutPrevious, withPrevious);
        }

        [Fact]
        [BusinessRule("BR-FEE-005")]
        public void Different_amount_changes_the_hash_detecting_a_retroactive_edit()
        {
            var uuid = Guid.NewGuid().ToString();
            var postedAt = new DateTime(2027, 3, 1, 10, 0, 0, DateTimeKind.Utc);

            var original = InvoiceHashChainBuilder.ComputeHash(uuid, 1150m, postedAt, null);
            var tampered = InvoiceHashChainBuilder.ComputeHash(uuid, 1151m, postedAt, null);

            Assert.NotEqual(original, tampered);
        }
    }
}
