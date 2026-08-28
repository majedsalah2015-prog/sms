using Sms.Application.Common;
using Xunit;

namespace Sms.Application.Tests.Common
{
    /// <summary>
    /// The register says "محمد أحمد علي الخطيب" in one cell and this product stores four columns.
    /// Everything an imported employee is afterwards found and sorted and printed by comes out of
    /// this one function, and a mistake in it is invisible: the record looks complete, the name
    /// reads correctly on the screen when the four parts are printed back in order, and only the
    /// family-name column is quietly holding somebody's grandfather.
    /// </summary>
    public class PersonNameSplitterTests
    {
        [Fact]
        public void Four_words_are_the_quad_name_as_written()
        {
            var parts = PersonNameSplitter.Split("محمد أحمد علي الخطيب");

            Assert.Equal("محمد", parts.First);
            Assert.Equal("أحمد", parts.Father);
            Assert.Equal("علي", parts.Grandfather);
            Assert.Equal("الخطيب", parts.Family);
            Assert.True(parts.IsComplete);
        }

        /// <summary>
        /// The case the old split got wrong. Taking the words in order left the family name empty
        /// and put "الخطيب" in the father's column, so the row was refused as incomplete — which is
        /// most of an ordinary staff list.
        /// </summary>
        [Fact]
        public void Three_words_are_a_first_a_father_and_a_family()
        {
            var parts = PersonNameSplitter.Split("محمد أحمد الخطيب");

            Assert.Equal("محمد", parts.First);
            Assert.Equal("أحمد", parts.Father);
            Assert.Equal(string.Empty, parts.Grandfather);
            Assert.Equal("الخطيب", parts.Family);
            Assert.True(parts.IsComplete);
        }

        [Fact]
        public void Two_words_are_a_first_and_a_family()
        {
            var parts = PersonNameSplitter.Split("محمد الخطيب");

            Assert.Equal("محمد", parts.First);
            Assert.Equal(string.Empty, parts.Father);
            Assert.Equal(string.Empty, parts.Grandfather);
            Assert.Equal("الخطيب", parts.Family);
            Assert.True(parts.IsComplete);
        }

        /// <summary>
        /// A long name has more ancestors, not a longer surname. The family name stays the last
        /// word and the surplus collects in the grandfather's place, where it is at least true.
        /// </summary>
        [Fact]
        public void A_fifth_word_lands_with_the_grandfather_and_not_the_family()
        {
            var parts = PersonNameSplitter.Split("محمد أحمد علي حسن الخطيب");

            Assert.Equal("محمد", parts.First);
            Assert.Equal("أحمد", parts.Father);
            Assert.Equal("علي حسن", parts.Grandfather);
            Assert.Equal("الخطيب", parts.Family);
        }

        /// <summary>
        /// "عبد الله" is one name written with a space in it. Counted as two, every later part
        /// shifts a slot to the left and the record comes out plausible and wrong.
        /// </summary>
        [Fact]
        public void A_compound_first_name_is_one_part()
        {
            var parts = PersonNameSplitter.Split("عبد الله أحمد علي الخطيب");

            Assert.Equal("عبد الله", parts.First);
            Assert.Equal("أحمد", parts.Father);
            Assert.Equal("علي", parts.Grandfather);
            Assert.Equal("الخطيب", parts.Family);
        }

        [Fact]
        public void A_compound_family_name_is_one_part()
        {
            var parts = PersonNameSplitter.Split("محمد أحمد علي أبو زيد");

            Assert.Equal("محمد", parts.First);
            Assert.Equal("أحمد", parts.Father);
            Assert.Equal("علي", parts.Grandfather);
            Assert.Equal("أبو زيد", parts.Family);
        }

        [Fact]
        public void Several_compounds_in_one_name_all_hold_together()
        {
            var parts = PersonNameSplitter.Split("عبد الرحمن عبد العزيز محمد آل سعود");

            Assert.Equal("عبد الرحمن", parts.First);
            Assert.Equal("عبد العزيز", parts.Father);
            Assert.Equal("محمد", parts.Grandfather);
            Assert.Equal("آل سعود", parts.Family);
        }

        /// <summary>
        /// A particle with nothing after it is a truncated cell, not a name. It is kept as written
        /// rather than swallowed: dropping it would hide the truncation from whoever has to fix it.
        /// </summary>
        [Fact]
        public void A_particle_at_the_end_is_kept_as_it_stands()
        {
            var parts = PersonNameSplitter.Split("محمد أحمد عبد");

            Assert.Equal("محمد", parts.First);
            Assert.Equal("أحمد", parts.Father);
            Assert.Equal("عبد", parts.Family);
        }

        /// <summary>
        /// One word cannot be a person this product can store, and guessing a family name from it
        /// would be inventing one. The row is refused instead, visibly, in the preview.
        /// </summary>
        [Fact]
        public void One_word_is_incomplete_rather_than_guessed_at()
        {
            var parts = PersonNameSplitter.Split("محمد");

            Assert.Equal("محمد", parts.First);
            Assert.Equal(string.Empty, parts.Family);
            Assert.False(parts.IsComplete);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void Nothing_in_the_cell_is_nothing_out(string? cell)
        {
            var parts = PersonNameSplitter.Split(cell);

            Assert.Equal(string.Empty, parts.First);
            Assert.Equal(string.Empty, parts.Family);
            Assert.False(parts.IsComplete);
        }

        /// <summary>
        /// Excel hands back exactly what is in the cell, and what is in the cell of an Arabic
        /// register is routinely padded and double-spaced.
        /// </summary>
        [Fact]
        public void Extra_spacing_around_and_between_the_words_is_not_part_of_the_name()
        {
            var parts = PersonNameSplitter.Split("  محمد   أحمد\tعلي  الخطيب ");

            Assert.Equal("محمد", parts.First);
            Assert.Equal("أحمد", parts.Father);
            Assert.Equal("علي", parts.Grandfather);
            Assert.Equal("الخطيب", parts.Family);
        }

        /// <summary>
        /// A right-to-left mark inside the cell is invisible in Excel and would otherwise be stored
        /// as part of the name, where it stops matching the same name typed by hand.
        /// </summary>
        [Fact]
        public void Bidi_marks_do_not_become_part_of_the_name()
        {
            var rightToLeft = ((char)0x200F).ToString();
            var leftToRight = ((char)0x200E).ToString();
            var parts = PersonNameSplitter.Split(rightToLeft + "محمد" + rightToLeft + " أحمد الخطيب" + leftToRight);

            Assert.Equal("محمد", parts.First);
            Assert.Equal("أحمد", parts.Father);
            Assert.Equal("الخطيب", parts.Family);
        }

        /// <summary>
        /// Latin names go through the same door. "Al" is deliberately not treated as a particle —
        /// merging it would turn "Al Smith" into one word and lose the family name.
        /// </summary>
        [Fact]
        public void A_latin_name_splits_on_the_same_rules()
        {
            var parts = PersonNameSplitter.Split("Al Smith");

            Assert.Equal("Al", parts.First);
            Assert.Equal("Smith", parts.Family);
            Assert.True(parts.IsComplete);
        }
    }
}
