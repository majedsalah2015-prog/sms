namespace Sms.Domain.Workflow
{
    /// <summary>
    /// Per-transition reason policy (doc 05 §2). Independent of the hard rule
    /// that Reject/Return (BR-WF-010) and Cancel (BR-GLB-032) always require a
    /// reason. Reason lists arrive with the lookups framework (E-010).
    /// </summary>
    public enum ReasonPolicy : short
    {
        Optional = 1,
        Required = 2,
        RequiredFromList = 3,
    }
}
