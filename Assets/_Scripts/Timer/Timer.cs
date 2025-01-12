using System.Threading.Tasks;
using System.Threading;
using System;



namespace Xonix
{
    /// <summary>
    /// Simple disposable timer
    /// </summary>
    public class Timer : IDisposable
    {
        private const int MillisecondsValue = 1000;


        public event Action OnTimerEnded;
        public event Action OnTickPassed;

        private CancellationTokenSource _timerCountingCancellationTokenSource;

        private readonly float _tickDurationSeconds;
        private readonly float _targetCountOfTicks;

        private int _passedTicks = 0;



        public float TicksLeft => _targetCountOfTicks - _passedTicks;



        public Timer(float tickDureationInSeconds, float countOfTicks)
        {
            _tickDurationSeconds = tickDureationInSeconds;
            _targetCountOfTicks = countOfTicks;

            _timerCountingCancellationTokenSource = new CancellationTokenSource();

            XonixGame.OnGameOver += Dispose;

            PauseMenu.OnPause += PauseTimer;
            PauseMenu.OnResume += ResumeTimer;
        }

        public Timer(float currentTickDurationSeconds)
        {
            _tickDurationSeconds = currentTickDurationSeconds;
            _targetCountOfTicks = float.PositiveInfinity;

            _timerCountingCancellationTokenSource = new CancellationTokenSource();

            XonixGame.OnGameOver += Dispose;

            PauseMenu.OnPause += PauseTimer;
            PauseMenu.OnResume += ResumeTimer;
        }



        public async Task Start()
        {
            while (_passedTicks < _targetCountOfTicks && !_timerCountingCancellationTokenSource.IsCancellationRequested)
            {
                await Task.Delay((int)(_tickDurationSeconds * MillisecondsValue), _timerCountingCancellationTokenSource.Token);

                _passedTicks++;
                OnTickPassed?.Invoke();
            }

            OnTimerEnded?.Invoke();
        }

        public void PauseTimer()
        {
            CancelTickCounting();
        }

        public void ResumeTimer()
        {
            ResetToken();

#pragma warning disable CS4014 // ��� ��� ���� ����� �� ���������, ���������� ������������� ������ ������������ �� ��� ���, ���� ����� �� ����� ��������
            Start();
#pragma warning restore CS4014 // ��� ��� ���� ����� �� ���������, ���������� ������������� ������ ������������ �� ��� ���, ���� ����� �� ����� ��������
        }

        private void CancelTickCounting()
        {
            if (!_timerCountingCancellationTokenSource.IsCancellationRequested)
                _timerCountingCancellationTokenSource.Cancel();
        }

        private void ResetToken()
        {
            _timerCountingCancellationTokenSource.Dispose();
            _timerCountingCancellationTokenSource = new CancellationTokenSource();
        }

        public void Dispose()
        {
            CancelTickCounting();

            PauseMenu.OnPause -= PauseTimer;
            PauseMenu.OnResume -= ResumeTimer;
        }
    }
}
