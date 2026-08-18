using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading;
using AI.Vision.IOInspector.Domain.Enums;
using AI.Vision.IOInspector.Domain.Models;

namespace AI.Vision.IOInspector.Infrastructure.Services.Camera
{
    /// <summary>
    /// 채널별 상시 연결(<see cref="PersistentRtspFrameGrabber"/>)을 한곳에서 관리합니다.
    ///
    /// 어느 채널을 상시 연결로 돌릴지는 설정으로 정합니다.
    /// 한 채널만 켜고 나머지는 기존 방식으로 두면 <b>같은 조건에서 효과를 비교</b>할 수 있어,
    /// 전체 전환 전에 안전하게 확인할 수 있습니다.
    ///
    /// 설정값 형식 (CFG\VladRuntimeSettings.json 의 PersistentCaptureChannels)
    ///   ""            사용하지 않습니다. 기존 방식 그대로입니다. (기본값)
    ///   "Top"         Top 채널만 상시 연결합니다.
    ///   "Top,Thickness"  쉼표로 여러 채널을 지정합니다.
    ///   "ALL"         6채널 전부 상시 연결합니다.
    /// </summary>
    public class PersistentCaptureRegistry : IDisposable
    {
        private const string AllChannelsKeyword = "ALL";
        private const string LatestFrameFolderName = "LatestFrames";

        private readonly object m_oSyncRoot = new object();
        private readonly Dictionary<ImageViewType, PersistentRtspFrameGrabber> m_oGrabbers =
            new Dictionary<ImageViewType, PersistentRtspFrameGrabber>();
        private readonly string m_sRootPath;
        private readonly FfmpegToolLocator m_oFfmpegToolLocator;

        private string m_sConfiguredChannels = string.Empty;

        public PersistentCaptureRegistry(string sRootPath)
        {
            m_sRootPath = sRootPath;
            m_oFfmpegToolLocator = new FfmpegToolLocator(sRootPath);
        }

        /// <summary>
        /// 설정에 지정된 채널의 상시 연결을 시작합니다.
        /// 지정되지 않은 채널은 기존 방식(검사 시 새 연결)을 그대로 씁니다.
        /// </summary>
        public void Start(IList<CameraChannelConfig> oChannels, string sConfiguredChannels)
        {
            m_sConfiguredChannels = sConfiguredChannels == null ? string.Empty : sConfiguredChannels.Trim();

            if (string.IsNullOrWhiteSpace(m_sConfiguredChannels))
            {
                RtspCaptureLog.WritePersistent(m_sRootPath, "-", "DISABLED",
                    "PersistentCaptureChannels가 비어 있어 상시 연결을 사용하지 않습니다.");
                return;
            }

            string sFfmpegPath = m_oFfmpegToolLocator.FindFfmpegPath();
            if (string.IsNullOrWhiteSpace(sFfmpegPath))
            {
                RtspCaptureLog.WritePersistent(m_sRootPath, "-", "DISABLED",
                    m_oFfmpegToolLocator.BuildMissingRuntimeMessage());
                return;
            }

            if (oChannels == null)
            {
                return;
            }

            foreach (CameraChannelConfig oChannel in oChannels)
            {
                if (oChannel == null || !oChannel.IsEnabled)
                {
                    continue;
                }

                if (!IsPersistentTarget(oChannel.ViewType))
                {
                    continue;
                }

                string sRtspUrl = RtspUrlBuilder.Build(oChannel);
                if (string.IsNullOrWhiteSpace(sRtspUrl))
                {
                    RtspCaptureLog.WritePersistent(m_sRootPath, oChannel.DisplayName, "SKIP",
                        "RTSP URL이 비어 있어 상시 연결을 시작하지 않습니다.");
                    continue;
                }

                StartChannel(oChannel, sRtspUrl, sFfmpegPath);
            }
        }

        private void StartChannel(CameraChannelConfig oChannel, string sRtspUrl, string sFfmpegPath)
        {
            lock (m_oSyncRoot)
            {
                if (m_oGrabbers.ContainsKey(oChannel.ViewType))
                {
                    return;
                }

                string sLatestFramePath = BuildLatestFramePath(oChannel);
                PersistentRtspFrameGrabber oGrabber = new PersistentRtspFrameGrabber(
                    m_sRootPath,
                    oChannel.DisplayName,
                    sRtspUrl,
                    sFfmpegPath,
                    sLatestFramePath);

                m_oGrabbers[oChannel.ViewType] = oGrabber;
                oGrabber.Open();
            }
        }

        /// <summary>
        /// 이 채널이 상시 연결 대상인지 알려줍니다.
        /// </summary>
        public bool IsPersistentChannel(ImageViewType eViewType)
        {
            lock (m_oSyncRoot)
            {
                return m_oGrabbers.ContainsKey(eViewType);
            }
        }

        /// <summary>
        /// 상시 연결이 보관 중인 최신 프레임을 검사용 경로로 복사합니다.
        /// 상시 연결 대상이 아니거나, 검사 요청 시각 이후의 새 프레임이 오지 않으면 false를 돌려주며,
        /// 호출자는 기존 방식(새 연결 캡처)으로 넘어가야 합니다.
        /// </summary>
        /// <param name="dtRequestedAt">검사를 요청한 시각입니다. 이 시각 이후의 프레임만 사용합니다.</param>
        public bool TryGrabLatest(
            ImageViewType eViewType,
            string sOutputFilePath,
            DateTime dtRequestedAt,
            out DateTime dtFrameCapturedAt,
            out string sMessage)
        {
            dtFrameCapturedAt = DateTime.MinValue;
            sMessage = string.Empty;

            PersistentRtspFrameGrabber oGrabber;
            lock (m_oSyncRoot)
            {
                if (!m_oGrabbers.TryGetValue(eViewType, out oGrabber))
                {
                    sMessage = "상시 연결 대상이 아닙니다.";
                    return false;
                }
            }

            return oGrabber.TryGrabLatest(sOutputFilePath, dtRequestedAt, out dtFrameCapturedAt, out sMessage);
        }

        /// <summary>
        /// 현재 상시 연결 상태를 한 줄로 요약합니다. 진단용입니다.
        /// </summary>
        public string BuildStatusSummary()
        {
            lock (m_oSyncRoot)
            {
                if (m_oGrabbers.Count == 0)
                {
                    return "상시 연결 없음";
                }

                List<string> oParts = new List<string>();
                foreach (KeyValuePair<ImageViewType, PersistentRtspFrameGrabber> oPair in m_oGrabbers)
                {
                    oParts.Add(
                        oPair.Key.ToString() + "=" +
                        (oPair.Value.IsRunning ? "실행중" : "중지") +
                        "(재기동 " + oPair.Value.RestartCount.ToString(CultureInfo.InvariantCulture) + "회)");
                }

                return string.Join(", ", oParts.ToArray());
            }
        }

        public void Stop()
        {
            List<PersistentRtspFrameGrabber> oTargets = new List<PersistentRtspFrameGrabber>();
            lock (m_oSyncRoot)
            {
                foreach (PersistentRtspFrameGrabber oGrabber in m_oGrabbers.Values)
                {
                    oTargets.Add(oGrabber);
                }
                m_oGrabbers.Clear();
            }

            if (oTargets.Count == 0)
            {
                return;
            }

            // 채널을 하나씩 순서대로 닫으면 각 채널의 스레드 종료 대기가 누적되어
            // 6채널이면 종료에 수 초가 걸립니다. 프로그램 종료가 그만큼 느려지므로
            // 먼저 모든 채널에 중지 신호를 보내고, 그다음에 함께 기다립니다.
            foreach (PersistentRtspFrameGrabber oGrabber in oTargets)
            {
                oGrabber.RequestStop();
            }

            List<Thread> oCloseThreads = new List<Thread>();
            foreach (PersistentRtspFrameGrabber oGrabber in oTargets)
            {
                PersistentRtspFrameGrabber oTarget = oGrabber;
                Thread oThread = new Thread(delegate() { oTarget.Close(); });
                oThread.IsBackground = true;
                oThread.Start();
                oCloseThreads.Add(oThread);
            }

            foreach (Thread oThread in oCloseThreads)
            {
                // 타임아웃 없이 기다리지 않습니다. 한 채널이 멈춰도 프로그램이 닫혀야 합니다.
                oThread.Join(4000);
            }
        }

        public void Dispose()
        {
            Stop();
        }

        /// <summary>
        /// 설정 문자열에 이 방향이 포함되는지 확인합니다.
        /// </summary>
        private bool IsPersistentTarget(ImageViewType eViewType)
        {
            if (string.Equals(m_sConfiguredChannels, AllChannelsKeyword, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            string[] oTokens = m_sConfiguredChannels.Split(',');
            foreach (string sToken in oTokens)
            {
                if (string.Equals(sToken.Trim(), eViewType.ToString(), StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 최신 프레임을 보관할 경로입니다. 검사 결과 폴더와 섞이지 않도록 별도 폴더에 둡니다.
        /// 채널마다 파일 하나만 유지되므로 용량이 늘지 않습니다.
        /// </summary>
        private string BuildLatestFramePath(CameraChannelConfig oChannel)
        {
            string sFolderPath = Path.Combine(
                ProjectDataRootResolver.Resolve(m_sRootPath),
                "DB",
                LatestFrameFolderName);
            Directory.CreateDirectory(sFolderPath);

            // 검사 이미지 저장 확장자가 .png이므로 최신 프레임도 png로 만듭니다.
            //
            // 예전에는 jpg로 만들어 .png 이름으로 복사했습니다. 그러면 내용은 JPEG인데 이름은 png라
            // 외부 도구가 오해하고, 무엇보다 JPEG 손실 압축을 한 번 거친 이미지로 검사하게 됩니다.
            // 검사 정확도에 직접 영향을 주므로 처음부터 무손실 png로 받습니다.
            return Path.Combine(sFolderPath, oChannel.ViewType.ToString() + ".png");
        }
    }
}
