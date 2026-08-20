using System;

namespace Sms.Application.Common.Exceptions
{
    /// <summary>BR-ROM-001: room codes are unique per school.</summary>
    public class DuplicateRoomCodeException : InvalidOperationException
    {
        public DuplicateRoomCodeException(string code)
            : base($"A room with code '{code}' already exists for this school (BR-ROM-001).")
        {
        }
    }

    /// <summary>BR-ROM-002: exam capacity must not exceed standard capacity.</summary>
    public class InvalidRoomCapacityException : InvalidOperationException
    {
        public InvalidRoomCapacityException()
            : base("Exam capacity cannot exceed standard capacity (BR-ROM-002).")
        {
        }
    }

    /// <summary>A building/floor/room can only be removed (deactivated) while nothing active still sits under or on it.</summary>
    public class RoomInUseException : InvalidOperationException
    {
        public RoomInUseException(string reason)
            : base($"Cannot remove: {reason}.")
        {
        }
    }

    /// <summary>BR-ROM-004: the room is under maintenance or reserved for the requested window.</summary>
    public class RoomUnavailableException : InvalidOperationException
    {
        public RoomUnavailableException(int roomId)
            : base($"Room {roomId} is unavailable for the requested window (BR-ROM-004).")
        {
        }
    }
}
