using System;

namespace AI.Vision.IOInspector.Application.Models
{
    /// <summary>
    /// 외부 학습 프로그램이 StandardOutput 또는 StandardError로 보낸 한 줄을 전달합니다.
    /// </summary>
    public sealed class TrainingProcessDataEventArgs : EventArgs
    {
        public TrainingProcessDataEventArgs(string data)
        {
            Data = data ?? string.Empty;
            ReceivedAt = DateTime.Now;
        }

        public string Data { get; private set; }

        public DateTime ReceivedAt { get; private set; }
    }

    /// <summary>
    /// 외부 학습 프로그램 종료 결과와 학습 완료 후 VLAD 재초기화 결과를 전달합니다.
    /// </summary>
    public sealed class TrainingProcessExitedEventArgs : EventArgs
    {
        public TrainingProcessExitedEventArgs(
            int? exitCode,
            bool completionMessageReceived,
            bool terminalErrorMessageReceived,
            bool reloadAttempted,
            bool reloadSucceeded,
            string reloadMessage,
            bool stoppedByUser)
        {
            StoppedByUser = stoppedByUser;
            ExitCode = exitCode;
            CompletionMessageReceived = completionMessageReceived;
            TerminalErrorMessageReceived = terminalErrorMessageReceived;
            ReloadAttempted = reloadAttempted;
            ReloadSucceeded = reloadSucceeded;
            ReloadMessage = reloadMessage ?? string.Empty;
        }

        public int? ExitCode { get; private set; }

        public bool CompletionMessageReceived { get; private set; }

        public bool TerminalErrorMessageReceived { get; private set; }

        public bool ReloadAttempted { get; private set; }

        public bool ReloadSucceeded { get; private set; }

        public string ReloadMessage { get; private set; }

        /// <summary>
        /// 사용자가 학습 중단 버튼으로 끝낸 종료인지입니다.
        /// 중단은 실패가 아니므로 화면 문구와 오류 처리에서 비정상 종료와 구분합니다.
        /// </summary>
        public bool StoppedByUser { get; private set; }
    }
}
