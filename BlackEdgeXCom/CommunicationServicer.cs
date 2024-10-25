using BlackEdgeCommon.Utils.Extensions;
using NetMQ;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BlackEdgeCommon.Communication
{
    public class CommunicationServicer : IDisposable
    {
        class TimerCallbackInfo : IDisposable
        {
            private readonly NetMQPoller _poller;
            private readonly EventHandler<NetMQTimerEventArgs> _callback;
            private readonly NetMQTimer _timer;

            public TimerCallbackInfo(Action callback, TimeSpan frequency, NetMQPoller poller)
            {
                _callback = (s, e) => callback();
                _timer = new NetMQTimer(frequency) { Enable = true };
                _timer.Elapsed += _callback;
                _poller = poller;

                _poller.Add(_timer);
            }

            public void ToggleEnabled(bool enabled)
                => _timer.Enable = enabled;

            public void UpdateFrequency(TimeSpan newFrequency)
                => _timer.Interval = (int)newFrequency.TotalMilliseconds;

            public void Dispose()
            {
                _timer.Elapsed -= _callback;
                _poller.Remove(_timer);
            }
        }

        public NetMQPoller Poller { get; }

        private readonly string _description;
        private readonly ConcurrentDictionary<Guid, TimerCallbackInfo> _currentTimerCallbacks;

        public CommunicationServicer(string description = null)
        {
            _description = description ?? nameof(CommunicationServicer);
            _currentTimerCallbacks = new ConcurrentDictionary<Guid, TimerCallbackInfo>();
            Poller = new NetMQPoller();

            Poller.RunAsync(_description);
        }

        public void Dispose()
        {
            foreach (KeyValuePair<Guid, TimerCallbackInfo> kvp in _currentTimerCallbacks)
                kvp.Value.SafeDispose();

            ActionExtensions.SafeDispose(Poller);
        }

        public Guid AddTimerCallbackOnServicerThread(Action callback, TimeSpan callbackFrequency)
        {
            Guid guid = Guid.NewGuid();
            _currentTimerCallbacks[guid] = new TimerCallbackInfo(callback, callbackFrequency, Poller);
            return guid;
        }

        public void DisposeTimer(Guid guid)
        {
            if (_currentTimerCallbacks.TryRemove(guid, out TimerCallbackInfo callbackInfo))
                callbackInfo.Dispose();
        }

        public void RunOnServicerThread(Action action)
            => Poller.Run(action);
    }
}
