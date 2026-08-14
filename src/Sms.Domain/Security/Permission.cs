using Sms.Domain.Common;

namespace Sms.Domain.Security
{
    /// <summary>
    /// sec.Permission — the global catalog of (Module, Screen, Action) triples
    /// (doc 06 §4). Product-defined, not tenant data: no SchoolId.
    /// </summary>
    public class Permission : AuditableEntity
    {
        public string ModuleCode { get; set; } = string.Empty;

        public string ScreenCode { get; set; } = string.Empty;

        public ActionVerb Action { get; set; }
    }
}
