using System.Text.Json;
using System;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using System.IO;
using System.Reflection;
using System.Text;
using System.Drawing;

using OpenCvSharp;
using OpenCvSharp.Extensions;

namespace Sample_VLAD_SDK
{
    public partial class Form1 : Form
    {
        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern bool SetDllDirectory(string lpPathName);

        private const string dll_path = @"C:\SVN_LinkGenesis\FA_HDX\AI-Vision IO Inspector\Docs\00-inbox\documents\VLAD Source\VLAD_SDK - Rev3\x64\Release\VLAD_SDK.dll";

        [DllImport(dll_path)]
        extern public static long VLAD_Custom_ID_Generate(int USER_ID, int MSG_VER, int MAJ_VER, int MIN_VER);

        [DllImport(dll_path)]
        extern public static IntPtr VLAD_Custom_Registration(long custom_id, string ui_name, string root_name, string site, string modelPath, string custom_info, int gpu_id);
        [DllImport(dll_path)]
        extern public static IntPtr VLAD_HD_Registration(string ui_name, string modelPath, int gpu_id);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate void RTSP_Callback(IntPtr vlad_id, string user_name, int ui_type, int mon_idx, IntPtr display);

        [DllImport(dll_path, CallingConvention = System.Runtime.InteropServices.CallingConvention.StdCall)]
        extern public static void VLAD_Rtsp_Info_Client_Registration(IntPtr vlad_id, string url_info, string user_name, int ui_type, int mon_idx, RTSP_Callback callback);

        [DllImport(dll_path)]
        extern public static IntPtr VLAD_Inference_Mat(IntPtr vlad_id, IntPtr raw_data, float threshold, int draw_mode);
        [DllImport(dll_path)]
        extern public static int VLAD_InferenceData_Get_Valid_Count(IntPtr vlad_id, IntPtr detect_data);
        [DllImport(dll_path)]
        extern public static int VLAD_InferenceData_V1_Draw(IntPtr vlad_id, IntPtr Detect_Data, IntPtr raw_data, IntPtr Class_cnt, StringBuilder Detect_Str, string Custom_Para, IntPtr Tlv_Info, int Tlv_Size);
        [DllImport(dll_path)]
        extern public static unsafe bool VLAD_Custom_InferenceData_V1(IntPtr vlad_id, IntPtr Detect_Data, IntPtr raw_data, IntPtr Class_cnt, StringBuilder Detect_Str, string Custom_Para, IntPtr Tlv_Info, int Tlv_Size);
        [DllImport(dll_path, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        extern public static unsafe IntPtr VLAD_HD_Inference_Mat(IntPtr vlad_id, IntPtr raw_data, float threshold, StringBuilder json_Info);


        public enum SDK_USER
        {
            USER_VLAD,
            USER_STD,
            USER_CUS_STD,
            USER_SRD,
            USER_MPS,
            USER_ATS
        }

        public enum SDK_MSG
        {
            MSG_V0,
            MSG_V1,
            MSG_V2
        }

        public enum SDK_MAJ
        {
            MAJ_V0,
            MAJ_V1,
            MAJ_V2
        }

        public struct Custom_Point
        {
            public int x;
            public int y;
        }

        public struct Custom_Info_Struct
        {
            public int class_id;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string cls_name;
            public float score;
            public Custom_Point p1;
            public Custom_Point p2;
        }


        public static IntPtr Vlad_id;
        public static int GPU_ID = 0;
        public static string Model_Path = "D:/USB/models/C62/output/export/CLS_INCRES_V2_Ver2";
        public static string url_info = "rtsp://210.99.70.120:1935/live/cctv001.stream";
        public static PictureBox PB;

        public static string json_data = @"
            {
                ""partNo"": ""01100-51430"",
                ""viewName"": 1,
                ""scoreThreshold"": 95.00,
                ""dimensions"": {
                    ""width"": 0.00,
                    ""depth"": 0.00,
                    ""height"": 0.00
                },
                ""measurementPoints"": [
                    {
                      ""indexNo"": 1,
                      ""nominalValue"": 150.00,
                      ""toleranceMin"": -0.50,
                      ""toleranceMax"": 0.50,
                      ""x1"": 120.50,
                      ""y1"": 240.00,
                      ""x2"": 360.50,
                      ""y2"": 240.00
                    }
              ]
            }";
        public Form1()
        {
            InitializeComponent();
            PB = this.pictureBox1;

            long custom_id = VLAD_Custom_ID_Generate((int)SDK_USER.USER_CUS_STD, (int)SDK_MSG.MSG_V1, (int)SDK_MAJ.MAJ_V1, GPU_ID);
            string para = "{\"MODEL\":0,\"CAM\":0}";
            Vlad_id = VLAD_HD_Registration("CUSTOM", Model_Path, GPU_ID);

            string path = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            SetDllDirectory(path); // DLL 탐색 경로를 현재 실행 폴더로 고정
            //VLAD_Rtsp_Info_Client_Registration(Vlad_id, url_info, "HD", 7, 0, RTSP_Frame_Proc);
        }

        public static void RTSP_Frame_Proc(IntPtr vlad_id, string user_name, int ui_type, int mon_idx, IntPtr display)
        {
            using (Mat mat = new Mat(1080, 1920, MatType.CV_8UC3, display))
            {
                float threshold;
                IntPtr detect_data;
                int[] class_lst;
                int valid_cnt;

                threshold = 0.1f;
                detect_data = VLAD_Inference_Mat(vlad_id, mat.CvPtr, threshold, 0);
                        
                valid_cnt = VLAD_InferenceData_Get_Valid_Count(vlad_id, detect_data);
                if (valid_cnt <= 0)
                {
                    Cv2.WaitKey(30);
                    return;
                }
                int tlv_size = Marshal.SizeOf(typeof(Custom_Info_Struct));
                IntPtr tlv_info = Marshal.AllocHGlobal(tlv_size * valid_cnt);
                StringBuilder detect_str = new StringBuilder(16384);
                VLAD_Custom_InferenceData_V1(Vlad_id, detect_data, mat.CvPtr, IntPtr.Zero, detect_str, null, tlv_info, tlv_size);

                string jsonOutput = detect_str.ToString().Trim();
                Console.WriteLine(jsonOutput);

                Marshal.FreeHGlobal(tlv_info);

                if (Form1.PB != null)                
                    Form1.PB.Image = BitmapConverter.ToBitmap(mat);
                Cv2.WaitKey(30);
            }
        }

        private void button1_Click(object sender, EventArgs e)
        {
            if (Vlad_id == IntPtr.Zero)
            {
                MessageBox.Show("Vlad_id가 0입니다. VLAD_HD_Registration이 실패한 상태로 보입니다 (Model_Path 확인 필요: " + Model_Path + "). 이 상태로 VLAD_HD_Inference_Mat을 호출하면 AccessViolationException이 발생하므로 중단합니다.");
                return;
            }

            Mat im = Cv2.ImRead(@"C:\SVN_LinkGenesis\FA_HDX\AI-Vision IO Inspector\Codes\AI-Vision IO Inspector\DB\History\20260611\16\K26\01100-51430_BOLT-STUD_Front_165654967.png");
            StringBuilder jsonBuffer = new StringBuilder(8192);
            jsonBuffer.Append(json_data);
            VLAD_HD_Inference_Mat(Vlad_id, im.CvPtr, 0.1f, jsonBuffer);
            Console.WriteLine("----------------------------------------------------");
            Console.WriteLine(jsonBuffer.ToString());
            Form1.PB.Image = BitmapConverter.ToBitmap(im);

        }
    }
}
