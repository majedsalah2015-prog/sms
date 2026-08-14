namespace Sms.Domain.Security
{
    /// <summary>
    /// Standard action taxonomy (doc 06 §4.1). Modules may add verbs only when
    /// none of these fit, justified in the module doc. There is deliberately no
    /// Delete — only Deactivate/Void per BR-GLB-005. SMALLINT-mapped (DB/01 §5).
    /// </summary>
    public enum ActionVerb : short
    {
        View = 1,
        Create = 2,
        Edit = 3,
        Deactivate = 4,
        Submit = 5,
        Approve = 6,
        Post = 7,
        Print = 8,
        Export = 9,
        Import = 10,
        Configure = 11,
    }
}
