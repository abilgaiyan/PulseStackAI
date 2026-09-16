using System.Runtime.CompilerServices;
using PulseStack.Abstractions.Runtime.Realization.Application;

namespace PulseStack.Core.Runtime.Invocation.Application;

/// <summary>
/// Coordinates non-overlapping invocation ownership for concrete realized-application instances.
/// The authority is intended to be provider-owned; application association remains weak.
/// </summary>
internal sealed class ApplicationInvocationCoordinationAuthority
{
    private readonly ConditionalWeakTable<RealizedApplication, OccupancyCell> _occupancies = new();

    public bool TryAcquire(
        RealizedApplication application,
        out ApplicationInvocationOwnership? ownership)
    {
        ArgumentNullException.ThrowIfNull(application);

        // The value factory is deliberately inert. Admission is performed only after
        // ConditionalWeakTable has returned the cell installed for this exact key.
        var cell = _occupancies.GetValue(
            application,
            static _ => new OccupancyCell());

        if (!cell.TryAcquire())
        {
            ownership = null;
            return false;
        }

        ownership = new ApplicationInvocationOwnership(cell);
        return true;
    }

    internal sealed class ApplicationInvocationOwnership
    {
        private readonly OccupancyCell _cell;

        internal ApplicationInvocationOwnership(OccupancyCell cell)
        {
            _cell = cell;
        }

        public ApplicationInvocationReleaseResult Release() => _cell.Release();
    }

    internal readonly record struct ApplicationInvocationReleaseResult(
        bool Released,
        int ObservedState)
    {
        public static ApplicationInvocationReleaseResult Success =>
            new(true, OccupancyCell.Active);

        public static ApplicationInvocationReleaseResult InvariantViolation(int observedState) =>
            new(false, observedState);
    }

    internal sealed class OccupancyCell
    {
        internal const int Idle = 0;
        internal const int Active = 1;

        private int _state = Idle;

        internal bool TryAcquire() =>
            Interlocked.CompareExchange(ref _state, Active, Idle) == Idle;

        internal ApplicationInvocationReleaseResult Release()
        {
            var observed = Interlocked.CompareExchange(ref _state, Idle, Active);

            return observed == Active
                ? ApplicationInvocationReleaseResult.Success
                : ApplicationInvocationReleaseResult.InvariantViolation(observed);
        }
    }
}
