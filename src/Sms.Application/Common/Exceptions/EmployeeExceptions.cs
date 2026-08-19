using System;
using Sms.Domain.Employees;

namespace Sms.Application.Common.Exceptions
{
    /// <summary>BR-EMP-001/008: the requested status pair isn't a legal move.</summary>
    public class InvalidEmployeeStatusTransitionException : InvalidOperationException
    {
        public InvalidEmployeeStatusTransitionException(EmployeeStatus from, EmployeeStatus to)
            : base($"Employee status cannot move from '{from}' to '{to}' (BR-EMP-001).")
        {
        }
    }

    /// <summary>BR-EMP-003: the requested contract status pair isn't a legal move.</summary>
    public class InvalidContractStatusTransitionException : InvalidOperationException
    {
        public InvalidContractStatusTransitionException(ContractStatus from, ContractStatus to)
            : base($"Contract status cannot move from '{from}' to '{to}' (BR-EMP-003).")
        {
        }
    }

    /// <summary>BR-EMP-003: contract dates must not overlap an existing contract for the same employee.</summary>
    public class OverlappingContractException : InvalidOperationException
    {
        public OverlappingContractException(int employeeId)
            : base($"A contract already covers part of this date range for employee {employeeId} (BR-EMP-003).")
        {
        }
    }

    /// <summary>Only Draft contracts may be edited — active/terminated contracts are immutable documents (BR-EMP-003).</summary>
    public class ContractNotEditableException : InvalidOperationException
    {
        public ContractNotEditableException(int contractId, ContractStatus status)
            : base($"Contract {contractId} is '{status}' and can no longer be edited (BR-EMP-003).")
        {
        }
    }

    /// <summary>BR-EMP-002: an org unit can only be deleted/re-parented while its tree and assignment history allow it.</summary>
    public class OrgUnitInUseException : InvalidOperationException
    {
        public OrgUnitInUseException(int orgUnitId, string reason)
            : base($"Org unit {orgUnitId} is in use: {reason}.")
        {
        }
    }
}
