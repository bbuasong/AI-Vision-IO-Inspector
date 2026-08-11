using System;
using System.Diagnostics;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace AI.Vision.IOInspector.Infrastructure.Services
{
    /// <summary>
    /// Epson Scan API(x86, 별도 프로세스)를 IO Inspector 의 '자식 프로세스'로 자동 관리합니다.
    ///
    ///   - EnsureRunningAsync(): 이미 떠 있으면 그대로 쓰고, 없으면 exe 를 실행한 뒤
    ///                           /health 가 준비될 때까지 기다립니다.
    ///   - Dispose(): 우리가 띄운 경우에만 종료합니다(외부에서 띄운 서버는 건드리지 않음).
    ///
    /// 비트니스: 이 서버는 x86 이지만 외부 exe 로 실행하므로 호출자(64bit)와 무관합니다.
    /// </summary>
    public class EpsonScanServerHost : IDisposable
    {
        private readonly EpsonScanServerOptions _options;
        private readonly HttpClient _http;
        private Process _process;
        private bool _startedByUs;
        private bool _disposed;

        public EpsonScanServerHost(EpsonScanServerOptions options)
        {
            _options = options ?? new EpsonScanServerOptions();
            _http = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
        }

        /// <summary>서버가 응답 가능한 상태가 되도록 보장합니다. 성공하면 true.</summary>
        public async Task<bool> EnsureRunningAsync(CancellationToken cancellationToken)
        {
            // 1) 이미 떠 있나? (수동 실행 / 이전 세션 잔존 포함) → 그대로 사용
            if (await IsHealthyAsync().ConfigureAwait(false))
            {
                return true;
            }

            // 2) 실행 파일 확인
            if (string.IsNullOrWhiteSpace(_options.ExecutablePath) && string.IsNullOrWhiteSpace(_options.DllPath))
            {
                throw new InvalidOperationException(
                    "Epson Scan API 실행 경로가 설정되지 않았습니다. ExecutablePath 또는 DllPath 를 지정하세요.");
            }

            // 3) 프로세스 실행
            ProcessStartInfo psi;
            if (!string.IsNullOrWhiteSpace(_options.ExecutablePath))
            {
                psi = new ProcessStartInfo
                {
                    FileName = _options.ExecutablePath,
                    WorkingDirectory = _options.WorkingDirectory ?? System.IO.Path.GetDirectoryName(_options.ExecutablePath)
                };
            }
            else
            {
                // dotnet EpsonScanApi.dll 형태로 실행
                psi = new ProcessStartInfo
                {
                    FileName = "dotnet",
                    Arguments = "\"" + _options.DllPath + "\"",
                    WorkingDirectory = _options.WorkingDirectory ?? System.IO.Path.GetDirectoryName(_options.DllPath)
                };
            }

            psi.UseShellExecute = false;     // 콘솔 창 숨기고 자식 프로세스로 묶기 위함
            psi.CreateNoWindow = true;
            psi.RedirectStandardOutput = false;
            psi.RedirectStandardError = false;

            _process = Process.Start(psi);
            _startedByUs = _process != null;

            // 4) /health 준비될 때까지 폴링
            DateTime deadline = DateTime.UtcNow.AddSeconds(_options.StartupTimeoutSeconds);
            while (DateTime.UtcNow < deadline)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (_process != null && _process.HasExited)
                {
                    throw new InvalidOperationException(
                        "Epson Scan API 프로세스가 시작 직후 종료되었습니다(ExitCode " + _process.ExitCode + "). 경로/런타임을 확인하세요.");
                }

                if (await IsHealthyAsync().ConfigureAwait(false))
                {
                    return true;
                }

                await Task.Delay(500, cancellationToken).ConfigureAwait(false);
            }

            throw new TimeoutException(
                "Epson Scan API 가 " + _options.StartupTimeoutSeconds + "초 안에 준비되지 않았습니다. (" + _options.BaseUrl + "/health 무응답)");
        }

        private async Task<bool> IsHealthyAsync()
        {
            string url = _options.BaseUrl.TrimEnd('/') + _options.HealthPath;
            try
            {
                using (HttpResponseMessage res = await _http.GetAsync(url).ConfigureAwait(false))
                {
                    return res.IsSuccessStatusCode;
                }
            }
            catch
            {
                return false; // 아직 안 떠 있음
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }
            _disposed = true;

            try
            {
                // 우리가 띄운 서버만 정리. 외부에서 띄운 건 그대로 둠.
                if (_startedByUs && _process != null && !_process.HasExited)
                {
                    _process.Kill();
                    _process.WaitForExit(3000);
                }
            }
            catch
            {
                // 종료 중 오류는 무시.
            }
            finally
            {
                if (_process != null) { _process.Dispose(); }
                _http.Dispose();
            }
        }
    }

    /// <summary>Epson Scan API 자식 프로세스 실행 설정.</summary>
    public class EpsonScanServerOptions
    {
        /// <summary>EpsonScanApi.exe 전체 경로. (publish 된 exe 권장)</summary>
        public string ExecutablePath { get; set; }

        /// <summary>exe 대신 dll 로 실행할 경우 EpsonScanApi.dll 경로. (FileName=dotnet)</summary>
        public string DllPath { get; set; }

        /// <summary>작업 디렉터리. 미지정 시 실행 파일 폴더.</summary>
        public string WorkingDirectory { get; set; }

        /// <summary>서버 주소. EpsonScanLabelService 의 BaseUrl 과 동일해야 함.</summary>
        public string BaseUrl { get; set; } = "http://127.0.0.1:8000";

        public string HealthPath { get; set; } = "/health";

        /// <summary>기동 대기 한도(초). 스캐너 초기화까지 고려해 넉넉히.</summary>
        public int StartupTimeoutSeconds { get; set; } = 30;
    }
}
