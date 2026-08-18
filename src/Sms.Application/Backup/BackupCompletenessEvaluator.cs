namespace Sms.Application.Backup
{
    /// <summary>Pure BR-BAK-001: a backup set is Complete only with database, attachment store, and configuration all present; a partial set is Degraded.</summary>
    public static class BackupCompletenessEvaluator
    {
        public static bool IsComplete(bool databaseIncluded, bool attachmentStoreIncluded, bool configurationIncluded)
            => databaseIncluded && attachmentStoreIncluded && configurationIncluded;
    }
}
