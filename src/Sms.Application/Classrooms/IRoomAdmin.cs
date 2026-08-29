using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Sms.Domain.Classrooms;
using Sms.Domain.Grades;

namespace Sms.Application.Classrooms
{
    /// <summary>doc/Modules/08 §8 "Room catalog"/"Maintenance console"/"Booking calendar" screens backing (screens deferred, the operations are core).</summary>
    public interface IRoomAdmin
    {
        Task<Building> DefineBuildingAsync(string nameAr, string nameEn, CancellationToken cancellationToken = default);

        Task<Floor> DefineFloorAsync(int buildingId, string nameAr, string nameEn, int sequenceOrder, CancellationToken cancellationToken = default);

        /// <summary>Throws <see cref="Common.Exceptions.DuplicateRoomCodeException"/> or <see cref="Common.Exceptions.InvalidRoomCapacityException"/>.</summary>
        Task<Room> DefineRoomAsync(
            int floorId, string code, string nameAr, string nameEn, int roomTypeLookupId,
            int standardCapacity, int examCapacity, GenderPolicy wingTag, CancellationToken cancellationToken = default);

        /// <summary>
        /// Floor id → the next free room code on it (<see cref="RoomCodeGenerator"/>), so the
        /// catalog screen fills the field instead of asking a registrar to invent one. The whole
        /// map in one read: the screen offers a code for every floor in its picker, and the save
        /// path resolves the same code from the same source rather than a second opinion.
        /// </summary>
        Task<IReadOnlyDictionary<int, string>> SuggestRoomCodesAsync(CancellationToken cancellationToken = default);

        Task<RoomFeature> AddFeatureAsync(int roomId, int featureLookupId, CancellationToken cancellationToken = default);

        Task<Building> UpdateBuildingAsync(int buildingId, string nameAr, string nameEn, CancellationToken cancellationToken = default);

        /// <summary>Soft-deletes (deactivates) a building; throws <see cref="Common.Exceptions.RoomInUseException"/> while it still has active floors.</summary>
        Task DeactivateBuildingAsync(int buildingId, CancellationToken cancellationToken = default);

        Task<Floor> UpdateFloorAsync(int floorId, int buildingId, string nameAr, string nameEn, int sequenceOrder, CancellationToken cancellationToken = default);

        /// <summary>Soft-deletes (deactivates) a floor; throws <see cref="Common.Exceptions.RoomInUseException"/> while it still has active rooms.</summary>
        Task DeactivateFloorAsync(int floorId, CancellationToken cancellationToken = default);

        /// <summary>Edits a room under the same rules as <see cref="DefineRoomAsync"/> (unique code BR-ROM-001, exam ≤ standard capacity BR-ROM-002).</summary>
        Task<Room> UpdateRoomAsync(
            int roomId, int floorId, string code, string nameAr, string nameEn, int roomTypeLookupId,
            int standardCapacity, int examCapacity, GenderPolicy wingTag, CancellationToken cancellationToken = default);

        /// <summary>Soft-deletes (deactivates) a room; throws <see cref="Common.Exceptions.RoomInUseException"/> while an active section uses it as default classroom or a future booking exists.</summary>
        Task DeactivateRoomAsync(int roomId, CancellationToken cancellationToken = default);

        /// <summary>BR-ROM-004: records a maintenance/reserved window — the single source of truth for room availability.</summary>
        Task<RoomAvailabilityException> SetUnavailableAsync(
            int roomId, RoomAvailabilityReason reason, DateTime startDate, DateTime endDate, string? notes = null, CancellationToken cancellationToken = default);

        /// <summary>Free-slot bookings auto-approve; a room under maintenance/reserved throws <see cref="Common.Exceptions.RoomUnavailableException"/>. Displacing an actual teaching session can't be checked yet (Timetable/M15 doesn't exist) — see doc comment.</summary>
        Task<RoomBooking> RequestBookingAsync(
            int roomId, int academicYearId, string purpose, DateTime startUtc, DateTime endUtc, int requestedByUserId, CancellationToken cancellationToken = default);
    }
}
