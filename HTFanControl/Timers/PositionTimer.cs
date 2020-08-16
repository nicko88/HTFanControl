using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace HTFanControl.Timers
{
    internal abstract class PositionTimer
    {
        public abstract bool Update(TimeSpan currentPosition);
        public abstract bool Stop();
        public abstract bool TryGetNextPositions(out ReadOnlySpan<TimeSpan> nextPositions);
        public abstract ValueTask<bool> DisposeAsync(bool stop);

        protected static long Delta(TimeSpan t1, TimeSpan t2)
        {
            var deltaTicks = Subtract(t1, t2);

            if (deltaTicks >= 0)
                return deltaTicks;

            deltaTicks = unchecked(-deltaTicks);
            return deltaTicks > 0 ? deltaTicks : long.MaxValue;
        }

        protected static long Add(TimeSpan t1, TimeSpan t2)
        {
            var t1Ticks = t1.Ticks;
            var t2Ticks = t2.Ticks;
            var resultTicks = unchecked(t1Ticks + t2Ticks);

            if (t1Ticks >> 63 == t2Ticks >> 63 && t1Ticks >> 63 != resultTicks >> 63)
                return long.MaxValue;

            return resultTicks;
        }

        protected static long Subtract(TimeSpan t1, TimeSpan t2)
        {
            var t1Ticks = t1.Ticks;
            var t2Ticks = t2.Ticks;
            var resultTicks = unchecked(t1Ticks - t2Ticks);

            if (t1Ticks >> 63 != t2Ticks >> 63 && t1Ticks >> 63 != resultTicks >> 63)
                return long.MaxValue;

            return resultTicks;
        }
    }

    internal class PositionTimer<T> : PositionTimer
    {
        private readonly TimeSpan[] _positions;
        private readonly T[] _values;
        private readonly T _defaultValue;
        private readonly long _currentPositionToleranceExclusiveTicks;
        private readonly long _skipWindowTicks;
        private readonly Stopwatch _stopwatch;

        private const long TicksPerMillisecond = 10000;
        private const long SkipWindowMultiplier = 5;
        private const long ElapsedPositionToleranceTicks = 20 * TicksPerMillisecond;
        private const long MinAdjustedIntervalTicks = 60000 * TicksPerMillisecond;
        private const double AdjustmentFraction = 0.8;

        private Action<PositionTimer<T>, T> _action;
        private Timer _timer;
        private TimeSpan _startPosition;
        private int _index;
        private TimeSpan _nextPosition;
        private T _lastValue;
        private bool _invoking;
        private TaskCompletionSource<bool> _disposed;

        public PositionTimer(IEnumerable<(TimeSpan position, T value)> values, Action<PositionTimer<T>, T> action,
            int millisecondsCurrentPositionResolution, T defaultValue = default)
        {
            if (millisecondsCurrentPositionResolution <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(millisecondsCurrentPositionResolution),
                    "Must be greater than 0");
            }

            var orderedValues = values.OrderBy(v => v.position).ToList();
            var count = orderedValues.Count;
            _positions = new TimeSpan[count];
            _values = new T[count];
            TimeSpan lastPosition = default;
            var lastValue = defaultValue;
            var distinct = 0;
            for (var i = 0; i < count; i++)
            {
                var (position, value) = orderedValues[i];
                if (position == lastPosition && distinct > 0)
                {
                    lastValue = distinct > 1 ? _values[distinct - 2] : defaultValue;
                    if (EqualityComparer<T>.Default.Equals(value, lastValue))
                    {
                        lastPosition = TimeSpan.MinValue;
                        distinct--;
                    }
                    else
                    {
                        _values[distinct - 1] = value;
                        lastValue = value;
                    }
                }
                else if (!EqualityComparer<T>.Default.Equals(value, lastValue))
                {
                    _positions[distinct] = position;
                    _values[distinct] = value;
                    lastPosition = position;
                    lastValue = value;
                    distinct++;
                }
            }

            Array.Resize(ref _positions, distinct);
            Array.Resize(ref _values, distinct);

            _action = action ?? throw new ArgumentNullException(nameof(action));
            _defaultValue = defaultValue;
            _currentPositionToleranceExclusiveTicks = millisecondsCurrentPositionResolution * TicksPerMillisecond;
            _skipWindowTicks = _currentPositionToleranceExclusiveTicks * SkipWindowMultiplier;
            _stopwatch = new Stopwatch();

            InvokeTimerCallback();
        }

        public override bool Update(TimeSpan currentPosition)
        {
            lock (_positions)
            {
                if (_timer != null)
                {
                    var timerPosition = GetCurrentPosition();
                    var deltaTicks = Delta(timerPosition, currentPosition);

                    if (deltaTicks < _currentPositionToleranceExclusiveTicks)
                        return true;

                    _stopwatch.Restart();

                    if (currentPosition < timerPosition && deltaTicks <= _skipWindowTicks &&
                        Subtract(currentPosition, _startPosition) > _skipWindowTicks)
                    {
                        if (!_invoking && currentPosition < _nextPosition)
                            Change(currentPosition);
                    }
                    else
                    {
                        UpdateStateAndChangeOrInvoke(currentPosition);
                    }
                }
                else if (_action != null && _disposed == null)
                {
                    _stopwatch.Restart();

                    if (_positions.Length > 0)
                    {
                        _timer = new Timer(TimerCallback);

                        UpdateStateAndChangeOrInvoke(currentPosition);
                    }
                }
                else
                {
                    return false;
                }

                _startPosition = currentPosition;
            }

            return true;
        }

        public override bool Stop()
        {
            lock (_positions)
            {
                _stopwatch.Reset();

                if (_timer == null)
                    return _action != null && _disposed == null;

                _timer.Dispose();
                _timer = null;

                if (!_invoking && !EqualityComparer<T>.Default.Equals(_lastValue, _defaultValue))
                    InvokeTimerCallback();
            }

            return true;
        }

        public override bool TryGetNextPositions(out ReadOnlySpan<TimeSpan> nextPositions)
        {
            int nextIndex;
            lock (_positions)
            {
                if (_timer == null)
                {
                    nextPositions = default;
                    return _stopwatch.IsRunning;
                }

                var currentPosition = GetCurrentPosition();
                if (currentPosition < _nextPosition)
                {
                    nextIndex = _index;
                    if (EqualityComparer<T>.Default.Equals(_lastValue, _values[nextIndex]))
                        nextIndex++;
                }
                else if (currentPosition < _positions[0])
                {
                    nextIndex = 0;
                }
                else
                {
                    nextIndex = _index;
                    if (EqualityComparer<T>.Default.Equals(_lastValue,
                        UpdateIndexAndGetValue(currentPosition, ref nextIndex)))
                    {
                        nextIndex++;
                    }
                }
            }

            nextPositions = new ReadOnlySpan<TimeSpan>(_positions, nextIndex, _positions.Length - nextIndex);
            return true;
        }

        public override ValueTask<bool> DisposeAsync(bool stop)
        {
            lock (_positions)
            {
                if (_action == null || _disposed != null)
                    return default;

                if (stop)
                {
                    _timer?.Dispose();
                    _timer = null;
                    _stopwatch.Reset();

                    if (!_invoking)
                    {
                        if (EqualityComparer<T>.Default.Equals(_lastValue, _defaultValue))
                        {
                            _action = null;
                            return new ValueTask<bool>(true);
                        }

                        InvokeTimerCallback();
                    }
                }
                else
                {
                    _action = null;
                    _timer?.Dispose();
                    _timer = null;
                    _stopwatch.Reset();

                    if (!_invoking)
                        return new ValueTask<bool>(true);
                }

                _disposed = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            }

            return new ValueTask<bool>(_disposed.Task);
        }

        private void TimerCallback(object state)
        {
            if (state != null)
            {
                if (state != _timer)
                    return;

                lock (_positions)
                {
                    if (_timer != state || _invoking)
                        return;

                    var currentPosition = GetCurrentPosition();

                    TimeSpan elapsedPosition;
                    if (currentPosition < _nextPosition)
                    {
                        if (Subtract(_nextPosition, currentPosition) > ElapsedPositionToleranceTicks)
                        {
                            Change(currentPosition);
                            return;
                        }

                        elapsedPosition = _nextPosition;
                    }
                    else
                    {
                        elapsedPosition = currentPosition;
                    }

                    var currentValue = UpdateIndexAndGetValue(elapsedPosition, ref _index);
                    UpdateNextPosition(elapsedPosition);

                    if (EqualityComparer<T>.Default.Equals(_lastValue, currentValue))
                    {
                        if (elapsedPosition < _nextPosition)
                            Change(currentPosition);

                        return;
                    }

                    _lastValue = currentValue;
                    _invoking = true;
                }
            }

            _action?.Invoke(this, _lastValue);

            lock (_positions)
            {
                _invoking = false;

                if (_timer != null)
                {
                    var currentPosition = GetCurrentPosition();
                    if (currentPosition < _nextPosition)
                    {
                        Change(currentPosition);
                    }
                    else
                    {
                        UpdateStateAndChangeOrInvoke(currentPosition);
                    }
                }
                else if (!EqualityComparer<T>.Default.Equals(_lastValue, _defaultValue) && _action != null)
                {
                    InvokeTimerCallback();
                }
                else
                {
                    _disposed?.SetResult(true);
                }
            }
        }

        private TimeSpan GetCurrentPosition() => new TimeSpan(Add(_startPosition, _stopwatch.Elapsed));

        private void UpdateStateAndChangeOrInvoke(TimeSpan currentPosition)
        {
            if (_invoking)
            {
                _nextPosition = TimeSpan.MinValue;
            }
            else if (EqualityComparer<T>.Default.Equals(_lastValue, UpdateIndexAndGetValue(currentPosition, ref _index)))
            {
                UpdateNextPosition(currentPosition);

                if (currentPosition < _nextPosition)
                    Change(currentPosition);
            }
            else
            {
                InvokeTimerCallback();
            }
        }

        private void Change(TimeSpan currentPosition)
        {
            _timer.Change(Math.Min((long) GetInterval(currentPosition, _nextPosition).TotalMilliseconds, 4294967294),
                Timeout.Infinite);
        }

        private void InvokeTimerCallback()
        {
            if (_timer == null)
            {
                _lastValue = _defaultValue;
                _invoking = true;
                ThreadPool.QueueUserWorkItem(TimerCallback, null);
            }
            else
            {
                _nextPosition = TimeSpan.MinValue;
                _timer.Change(0, Timeout.Infinite);
            }
        }

        private T UpdateIndexAndGetValue(TimeSpan currentPosition, ref int index)
        {
            if (currentPosition >= _positions[index])
            {
                if (index == _positions.Length - 1 || currentPosition < _positions[index + 1])
                    return _values[index];

                index++;

                if (index != _positions.Length - 1 && currentPosition >= _positions[index + 1])
                    index = BinarySearch(_positions, index + 1, _positions.Length - 1, currentPosition);
            }
            else if (currentPosition < _positions[0])
            {
                index = 0;
                return _defaultValue;
            }
            else
            {
                index = BinarySearch(_positions, 1, index - 1, currentPosition);
            }

            return _values[index];
        }

        private void UpdateNextPosition(TimeSpan currentPosition)
        {
            if (_index == 0 && currentPosition < _positions[0])
            {
                _nextPosition = _positions[0];
            }
            else if (_index == _positions.Length - 1)
            {
                _nextPosition = _positions[_index];
            }
            else
            {
                _nextPosition = _positions[_index + 1];
            }
        }

        private static int BinarySearch(TimeSpan[] array, int lowIndex, int highIndex, TimeSpan value)
        {
            while (lowIndex <= highIndex)
            {
                var i = lowIndex + ((highIndex - lowIndex) >> 1);
                var order = array[i].CompareTo(value);

                if (order == 0)
                    return i;

                if (order < 0)
                {
                    lowIndex = i + 1;
                }
                else
                {
                    highIndex = i - 1;
                }
            }

            return highIndex;
        }

        private static TimeSpan GetInterval(TimeSpan startPosition, TimeSpan endPosition)
        {
            var intervalTicks = Subtract(endPosition, startPosition);

            if (intervalTicks <= MinAdjustedIntervalTicks)
                return new TimeSpan(intervalTicks);

            var adjustedIntervalTicks = (long) Math.Ceiling(intervalTicks * AdjustmentFraction);
            return adjustedIntervalTicks < MinAdjustedIntervalTicks
                ? new TimeSpan(intervalTicks)
                : new TimeSpan(adjustedIntervalTicks);
        }
    }
}
