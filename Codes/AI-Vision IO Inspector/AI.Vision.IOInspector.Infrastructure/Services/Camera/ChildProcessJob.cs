using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace AI.Vision.IOInspector.Infrastructure.Services.Camera
{
    /// <summary>
    /// 프로그램이 끝나면 여기에 등록된 자식 프로세스도 함께 끝나도록 묶어 둡니다.
    ///
    /// <para>
    /// ffmpeg는 별도 프로세스라 우리가 죽여야 사라집니다. 정상 종료 경로에서는
    /// StopPersistentCapture()가 정리하지만, 그 코드가 아예 실행되지 않는 경우가 있습니다.
    ///   - 네이티브 예외로 프로세스가 즉사할 때 (VLAD_HD_Inference_Mat 보호 메모리 예외 등)
    ///   - 작업 관리자에서 강제 종료할 때
    ///   - 디버깅 중 중지 버튼을 누를 때
    /// 이때 ffmpeg가 그대로 남아 카메라 스트림을 계속 붙들고 있어, 다시 실행해도
    /// NVR 연결 수가 모자라 미리보기가 안 붙는 상황이 됩니다.
    /// </para>
    ///
    /// <para>
    /// Windows Job Object에 JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE를 걸면,
    /// 이 job을 쥔 마지막 핸들이 닫히는 순간 OS가 소속 프로세스를 모두 종료합니다.
    /// 프로세스가 어떤 방식으로 사라지든 핸들은 OS가 닫아 주므로, 우리 코드가
    /// 실행될 기회를 얻지 못해도 자식 프로세스가 남지 않습니다.
    /// </para>
    ///
    /// <para>
    /// 프로그램당 하나만 쓰면 되므로 정적 인스턴스로 둡니다.
    /// job을 만들지 못하는 환경(권한 제한, 이미 다른 job에 속한 경우)에서는 조용히 넘어갑니다.
    /// 그때는 기존 정리 경로만 동작하며, 없던 기능이 하나 빠질 뿐 동작이 나빠지지는 않습니다.
    /// </para>
    /// </summary>
    public static class ChildProcessJob
    {
        private static readonly object m_oSyncRoot = new object();
        private static IntPtr m_hJob = IntPtr.Zero;
        private static bool m_bInitialized = false;
        private static string m_sLastError = string.Empty;

        /// <summary>
        /// 이 프로세스를 job에 넣습니다. 프로그램이 끝나면 함께 종료됩니다.
        /// 실패해도 예외를 던지지 않습니다. 부가 기능이므로 본 흐름을 막으면 안 됩니다.
        /// </summary>
        public static bool TryAssign(Process oProcess)
        {
            if (oProcess == null)
            {
                return false;
            }

            try
            {
                IntPtr hJob = EnsureJob();
                if (hJob == IntPtr.Zero)
                {
                    return false;
                }

                if (oProcess.HasExited)
                {
                    return false;
                }

                bool bAssigned = AssignProcessToJobObject(hJob, oProcess.Handle);
                if (!bAssigned)
                {
                    m_sLastError = "AssignProcessToJobObject 실패. Win32Error=" +
                                   Marshal.GetLastWin32Error().ToString();
                }

                return bAssigned;
            }
            catch (Exception oEx)
            {
                // 이미 끝난 프로세스면 Handle 접근에서 예외가 납니다. 무시합니다.
                m_sLastError = oEx.Message;
                return false;
            }
        }

        /// <summary>마지막 실패 사유입니다. 진단 로그에 적기 위한 것입니다.</summary>
        public static string LastError
        {
            get { return m_sLastError; }
        }

        /// <summary>job을 쓸 수 있는 상태인지입니다.</summary>
        public static bool IsAvailable
        {
            get { return EnsureJob() != IntPtr.Zero; }
        }

        private static IntPtr EnsureJob()
        {
            lock (m_oSyncRoot)
            {
                if (m_bInitialized)
                {
                    return m_hJob;
                }

                m_bInitialized = true;

                try
                {
                    IntPtr hJob = CreateJobObject(IntPtr.Zero, null);
                    if (hJob == IntPtr.Zero)
                    {
                        m_sLastError = "CreateJobObject 실패. Win32Error=" +
                                       Marshal.GetLastWin32Error().ToString();
                        return IntPtr.Zero;
                    }

                    JOBOBJECT_EXTENDED_LIMIT_INFORMATION oInfo = new JOBOBJECT_EXTENDED_LIMIT_INFORMATION();
                    oInfo.BasicLimitInformation.LimitFlags = JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE;

                    int nLength = Marshal.SizeOf(typeof(JOBOBJECT_EXTENDED_LIMIT_INFORMATION));
                    IntPtr pInfo = Marshal.AllocHGlobal(nLength);
                    try
                    {
                        Marshal.StructureToPtr(oInfo, pInfo, false);

                        bool bSet = SetInformationJobObject(
                            hJob,
                            JobObjectExtendedLimitInformation,
                            pInfo,
                            (uint)nLength);

                        if (!bSet)
                        {
                            m_sLastError = "SetInformationJobObject 실패. Win32Error=" +
                                           Marshal.GetLastWin32Error().ToString();
                            CloseHandle(hJob);
                            return IntPtr.Zero;
                        }
                    }
                    finally
                    {
                        Marshal.FreeHGlobal(pInfo);
                    }

                    // 핸들은 일부러 닫지 않고 프로그램이 끝날 때까지 들고 있습니다.
                    // 이 핸들이 닫히는 순간이 곧 자식 프로세스가 정리되는 시점입니다.
                    m_hJob = hJob;
                    return m_hJob;
                }
                catch (Exception oEx)
                {
                    m_sLastError = oEx.Message;
                    return IntPtr.Zero;
                }
            }
        }

        private const int JobObjectExtendedLimitInformation = 9;
        private const uint JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE = 0x00002000;

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr CreateJobObject(IntPtr lpJobAttributes, string lpName);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool SetInformationJobObject(
            IntPtr hJob,
            int JobObjectInfoClass,
            IntPtr lpJobObjectInfo,
            uint cbJobObjectInfoLength);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool AssignProcessToJobObject(IntPtr hJob, IntPtr hProcess);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr hObject);

        [StructLayout(LayoutKind.Sequential)]
        private struct IO_COUNTERS
        {
            public ulong ReadOperationCount;
            public ulong WriteOperationCount;
            public ulong OtherOperationCount;
            public ulong ReadTransferCount;
            public ulong WriteTransferCount;
            public ulong OtherTransferCount;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct JOBOBJECT_BASIC_LIMIT_INFORMATION
        {
            public long PerProcessUserTimeLimit;
            public long PerJobUserTimeLimit;
            public uint LimitFlags;
            public UIntPtr MinimumWorkingSetSize;
            public UIntPtr MaximumWorkingSetSize;
            public uint ActiveProcessLimit;
            public UIntPtr Affinity;
            public uint PriorityClass;
            public uint SchedulingClass;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct JOBOBJECT_EXTENDED_LIMIT_INFORMATION
        {
            public JOBOBJECT_BASIC_LIMIT_INFORMATION BasicLimitInformation;
            public IO_COUNTERS IoInfo;
            public UIntPtr ProcessMemoryLimit;
            public UIntPtr JobMemoryLimit;
            public UIntPtr PeakProcessMemoryUsed;
            public UIntPtr PeakJobMemoryUsed;
        }
    }
}
