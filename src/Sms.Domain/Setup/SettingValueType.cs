namespace Sms.Domain.Setup
{
    /// <summary>How <see cref="SchoolSetting.Value"/> is parsed (doc/Modules/01 §7 "value type").</summary>
    public enum SettingValueType : short
    {
        String = 1,
        Integer = 2,
        Decimal = 3,
        Boolean = 4,
        /// <summary>Comma-separated list of codes (working days, languages…).</summary>
        CodeList = 5,
    }
}
