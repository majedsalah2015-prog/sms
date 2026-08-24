using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Sms.Application.Lookups;
using Sms.Domain.Common;
using Sms.Domain.Lookups;
using Sms.Infrastructure.Persistence;

namespace Sms.Infrastructure.Lookups
{
    /// <summary>
    /// doc/Modules/01 §9 "deactivation of a lookup shows usage count and requires
    /// confirmation" (§8's "usage counter before deactivate"), backing BR-SET-002:
    /// a referenced lookup value is deactivatable, never deletable. Read-only — it
    /// never saves, so it is neither of the two service shapes.
    /// <para>
    /// The references it counts are the <c>*LookupId</c> columns scattered across the
    /// domain — <c>NationalityLookupId</c>, <c>RelationshipLookupId</c>,
    /// <c>PositionLookupId</c> and the rest. They are deliberately plain int columns
    /// with no configured foreign key (see <c>StudentConfiguration</c>: nothing declares
    /// <c>HasOne&lt;LookupValue&gt;</c>), so <c>GetForeignKeys()</c> would find none of
    /// them. They are found by walking the EF model instead, because a hard-coded table
    /// of referencing columns is wrong the first time someone adds a lookup-backed field
    /// and forgets this file — and the way that wrongness shows up is the worst kind:
    /// the screen quietly reports "0 usages, safe to deactivate" about a value half the
    /// student body is using. A model walk cannot fall behind the model.
    /// </para>
    /// <para>
    /// Cost: one read per referencing column (about a dozen today), whatever the caller
    /// asks about. The batch overload exists because the per-value one on a list render
    /// multiplies by the number of rows: a real nationality list is 195 values, which
    /// would be over two thousand serial queries before the page draws.
    /// </para>
    /// </summary>
    public sealed class LookupUsageQuery : ILookupUsageQuery
    {
        /// <summary>The naming convention that <em>is</em> the schema here — see the class remarks.</summary>
        private const string LookupIdSuffix = "LookupId";

        private static readonly MethodInfo ReadReferencesMethod = typeof(LookupUsageQuery)
            .GetMethod(nameof(ReadReferencesAsync), BindingFlags.Instance | BindingFlags.NonPublic)!;

        private readonly AppDbContext _db;

        public LookupUsageQuery(AppDbContext db)
        {
            _db = db;
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<LookupUsage>> CountUsagesAsync(int lookupValueId, CancellationToken cancellationToken = default)
        {
            // A required *LookupId column on a row nobody filled in holds 0, not null, so
            // "who references value 0" would answer with every half-populated row in the
            // school. There is no LookupValue with id 0 to ask about; refuse the question
            // rather than return a confident wrong number.
            if (lookupValueId <= 0)
            {
                return Array.Empty<LookupUsage>();
            }

            var byValue = await CountUsagesAsync(new[] { lookupValueId }, cancellationToken);
            return byValue.TryGetValue(lookupValueId, out var usages) ? usages : Array.Empty<LookupUsage>();
        }

        public async Task<IReadOnlyDictionary<int, IReadOnlyList<LookupUsage>>> CountUsagesAsync(
            IReadOnlyCollection<int> lookupValueIds, CancellationToken cancellationToken = default)
        {
            var wanted = lookupValueIds.Where(id => id > 0).ToHashSet();
            if (wanted.Count == 0)
            {
                return new Dictionary<int, IReadOnlyList<LookupUsage>>();
            }

            // value id -> (entity, property) -> count
            var tally = new Dictionary<int, List<LookupUsage>>();

            foreach (var entityType in _db.Model.GetEntityTypes())
            {
                // Owned types (every LocalizedName) have no DbSet of their own and are
                // queried through their owner; shared-CLR-type entities cannot be reached
                // by Set<T>() at all. LookupValue itself is excluded because a lookup
                // pointing at a lookup is not the school's data referencing the value.
                if (entityType.IsOwned() || entityType.HasSharedClrType || entityType.ClrType == typeof(LookupValue))
                {
                    continue;
                }

                foreach (var property in entityType.GetProperties())
                {
                    if (!IsLookupReference(property))
                    {
                        continue;
                    }

                    // One read per column, not per column per value: the column's own
                    // values come back and the tallying happens here. A school's
                    // biggest referencing table is its student register, so this is
                    // one int per student — cheaper than the round-trips it replaces,
                    // and it is the same materialise-then-aggregate shape the rest of
                    // this codebase uses for its own reasons.
                    var valuesTask = (Task<List<int>>)ReadReferencesMethod
                        .MakeGenericMethod(entityType.ClrType)
                        .Invoke(this, new object[] { property.Name, property.ClrType, cancellationToken })!;

                    foreach (var group in (await valuesTask).Where(wanted.Contains).GroupBy(id => id))
                    {
                        if (!tally.TryGetValue(group.Key, out var list))
                        {
                            list = new List<LookupUsage>();
                            tally[group.Key] = list;
                        }

                        list.Add(new LookupUsage(entityType.ClrType.Name, property.Name, group.Count()));
                    }
                }
            }

            // Worst offenders first, then a stable tie-break so the prompt does not
            // reshuffle itself between two reads of the same data.
            return tally.ToDictionary(
                pair => pair.Key,
                pair => (IReadOnlyList<LookupUsage>)pair.Value
                    .OrderByDescending(u => u.Count)
                    .ThenBy(u => u.EntityName, StringComparer.Ordinal)
                    .ThenBy(u => u.PropertyName, StringComparer.Ordinal)
                    .ToList());
        }

        private static bool IsLookupReference(IProperty property)
        {
            if (!property.Name.EndsWith(LookupIdSuffix, StringComparison.Ordinal))
            {
                return false;
            }

            // int or int? only. LookupCategoryId does not match the suffix and is not a
            // reference to a value anyway.
            var clrType = Nullable.GetUnderlyingType(property.ClrType) ?? property.ClrType;
            return clrType == typeof(int);
        }

        /// <summary>
        /// Every non-null value of one <c>*LookupId</c> column in this school, reached by
        /// reflection because the entity type is only known from the model at run time.
        /// The caller tallies; this reads.
        /// </summary>
        private async Task<List<int>> ReadReferencesAsync<TEntity>(
            string propertyName, Type propertyClrType, CancellationToken cancellationToken)
            where TEntity : class
        {
            var row = Expression.Parameter(typeof(TEntity), "e");

            // EF.Property<T> rather than a compiled member access: the generic argument must
            // match the model's CLR type exactly — reading an int? column as an int does not
            // translate — and a shadow property has no PropertyInfo to bind to.
            var reference = Expression.Call(
                typeof(EF), nameof(EF.Property), new[] { propertyClrType }, row, Expression.Constant(propertyName));

            var nullable = propertyClrType != typeof(int);
            Expression predicate = nullable
                ? Expression.NotEqual(reference, Expression.Constant(null, propertyClrType))
                : Expression.Constant(true);

            // IgnoreQueryFilters is what makes a deactivated referencing row still count —
            // the operator is asking "is anything pointing at this", and a hidden row still
            // points at it. But it drops the tenant filter along with the soft-active one,
            // so the school predicate goes back on by hand: the count must never read
            // another school's rows to answer a question about this one's data.
            if (typeof(ISchoolScoped).IsAssignableFrom(typeof(TEntity)))
            {
                var schoolId = Expression.Call(
                    typeof(EF), nameof(EF.Property), new[] { typeof(int) },
                    row, Expression.Constant(nameof(ISchoolScoped.SchoolId)));

                predicate = Expression.AndAlso(predicate, Expression.Equal(schoolId, Boxed(_db.CurrentSchoolId)));
            }

            var selector = nullable
                ? Expression.Lambda<Func<TEntity, int>>(Expression.Property(reference, "Value"), row)
                : Expression.Lambda<Func<TEntity, int>>(reference, row);

            return await _db.Set<TEntity>()
                .IgnoreQueryFilters()
                .Where(Expression.Lambda<Func<TEntity, bool>>(predicate, row))
                .Select(selector)
                .ToListAsync(cancellationToken);
        }

        /// <summary>
        /// A captured value read through a member access rather than an
        /// <see cref="Expression.Constant(object)"/>, so EF lifts it into a SQL parameter
        /// exactly as it does a closure field. Inlined literals would give SQL Server a
        /// fresh ad-hoc plan for every lookup id ever confirmed.
        /// </summary>
        private static MemberExpression Boxed(int value) =>
            Expression.Property(Expression.Constant(new IntBox(value)), nameof(IntBox.Value));

        private sealed class IntBox
        {
            public IntBox(int value)
            {
                Value = value;
            }

            public int Value { get; }
        }
    }
}
