using System;

namespace Sms.Application.Common.Exceptions
{
    /// <summary>BR-HLT-005 / doc §9: sent-home requires verified pickup or a documented exception.</summary>
    public class SentHomeWithoutVerifiedPickupException : InvalidOperationException
    {
        public SentHomeWithoutVerifiedPickupException(int studentId)
            : base($"Student {studentId} cannot be sent home without a verified pickup-authorized person or a documented exception (BR-HLT-005).")
        {
        }
    }

    /// <summary>BR-HLT-006 / doc §9: administration outside dosage/schedule/window requires a reason.</summary>
    public class MedicationDeviationReasonRequiredException : InvalidOperationException
    {
        public MedicationDeviationReasonRequiredException(int medicationAuthorizationId)
            : base($"Administration deviates from authorization {medicationAuthorizationId} (dose, time or date window) — a reason is required (BR-HLT-006).")
        {
        }
    }

    /// <summary>BR-HLT-004 / doc §9: campaign execution only for consented students.</summary>
    public class VaccinationConsentMissingException : InvalidOperationException
    {
        public VaccinationConsentMissingException(int campaignId, int studentId)
            : base($"Student {studentId} has no granted consent for vaccination campaign {campaignId} (BR-HLT-004).")
        {
        }
    }

    /// <summary>BR-HLT-009: only an approved-or-draft notice moves forward; sent notices are final.</summary>
    public class ExposureNoticeAlreadySentException : InvalidOperationException
    {
        public ExposureNoticeAlreadySentException(int exposureNoticeId)
            : base($"Exposure notice {exposureNoticeId} was already sent (BR-HLT-009).")
        {
        }
    }
}
