using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace CHNetSDK
{
    public class ImgHelper
    {
        private static Int32 m_UserID = -1;
        private static String m_ip;
        private static Int16 m_port;
        private static String m_account;
        private static String m_password;
        private static String m_savepath;

        public static bool m_InitSDK = false;
        public Int32 Findhandler = -1;
        private uint iLastErr = 0;

        public CHCNetSDK.NET_DVR_DEVICEINFO_V30 DeviceInfo;
        public CHCNetSDK.NET_DVR_FIND_PICTURE_PARAM PictureParam;

        public ImgHelper()
        {

        }

        public ImgHelper(String ip, Int16 port, String account, String password, String savepath)
        {
            m_ip = ip;
            m_port = port;
            m_account = account;
            m_password = password;
            m_savepath = savepath;
            if (!Directory.Exists(@savepath))
                Directory.CreateDirectory(@savepath);
        }

        /// <summary>
        /// 初始化SDK
        /// </summary>
        /// <returns></returns>
        public void InitSDK()
        {
            try
            {
                m_InitSDK = CHCNetSDK.NET_DVR_Init();
                if (m_InitSDK == false)
                {
                    iLastErr = CHCNetSDK.NET_DVR_GetLastError();
                    Console.WriteLine("NET_DVR_Init failed, error code = {0}", iLastErr);
                    return;
                }

                CHCNetSDK.NET_DVR_SetConnectTime(2000, 1);
                CHCNetSDK.NET_DVR_SetReconnect(10000, 1);
            }
            catch (Exception)
            {

                throw;
            }
        }

        /// <summary>
        /// 登录设备
        /// </summary>
        public void LoginSDK()
        {
            try
            {
                m_UserID = CHCNetSDK.NET_DVR_Login_V30(m_ip, m_port, m_account, m_password, ref DeviceInfo);
                if (m_UserID != 0)
                {
                    iLastErr = CHCNetSDK.NET_DVR_GetLastError();
                    Console.WriteLine("NET_DVR_Login_V30 failed, error code = {0}", iLastErr);
                    return;
                }
            }
            catch (Exception)
            {

                throw;
            }
        }

        /// <summary>
        /// 从设备内部下载图片
        /// </summary>
        /// <param name="StartTime">DateTime类开始扫描时间</param>
        /// <param name="EndTime">结束扫描时间</param>
        unsafe public void SearchAndDown(DateTime StartTime, DateTime EndTime)
        {
            try
            {
                InitSDK();
                if (m_InitSDK)
                {
                    LoginSDK();
                    if (m_UserID == 0)
                    {
                        Findhandler = getFindHandler(StartTime, EndTime);
                        if (Findhandler < 0)
                        {
                            iLastErr = CHCNetSDK.NET_DVR_GetLastError();
                            Console.WriteLine("NET_DVR_FindPicture failed, error code = {0}", iLastErr);
                            return;
                        }

                        //用于存储图片信息
                        CHCNetSDK.NET_DVR_FIND_PICTURE_V50 struSavePictrue = new CHCNetSDK.NET_DVR_FIND_PICTURE_V50();
                        CHCNetSDK.NET_DVR_PIC_PARAM temp = new CHCNetSDK.NET_DVR_PIC_PARAM();

                        bool isFinding = true;
                        int picCounter = 0;
                        int downCounter = 0;
                        while (isFinding)
                        {
                            int lFindHandler2 = CHCNetSDK.NET_DVR_FindNextPicture_V50(Findhandler, ref struSavePictrue);
                            switch (lFindHandler2)
                            {
                                case CHCNetSDK.NET_DVR_FILE_SUCCESS :
                                    if (struSavePictrue.sFileName.ToString().Contains("SNAP") && ( (int) struSavePictrue.dwFileSize) > 0) //重要
                                    {
                                        picCounter++;
                                        Console.WriteLine("Picture name ===> {0}", struSavePictrue.sFileName.ToString());
                                        //初始化变量
                                        temp.pDVRFileName = struSavePictrue.sFileName;
                                        
                                        //申请内存
                                        temp.pSavedFileBuf = Marshal.AllocHGlobal((int)struSavePictrue.dwFileSize);
                                        temp.dwBufLen = struSavePictrue.dwFileSize;
                                        temp.struAddr = struSavePictrue.struAddr;
                                        //如果获取图片信息成功
                                        if (CHCNetSDK.NET_DVR_GetPicture_V50(m_UserID, ref temp))
                                        {
                                            CHCNetSDK.NET_DVR_GetPicture(m_UserID, temp.pDVRFileName, 
                                                string.Format("{0}{1}{2}", m_savepath, temp.pDVRFileName, ".jpeg"));
                                            downCounter++;
                                        }
                                        else
                                        {
                                            iLastErr = CHCNetSDK.NET_DVR_GetLastError();
                                            Console.WriteLine("NET_DVR_GetPicture_V50 Pictrue name: [{0}] failed! erro code = {1}", 
                                                struSavePictrue.sFileName.ToString(), iLastErr);
                                        }

                                        //释放内存
                                        Marshal.FreeHGlobal(temp.pSavedFileBuf);
                                        
                                    }
                                    break;
                                case CHCNetSDK.NET_DVR_ISFINDING :
                                    break;
                                case CHCNetSDK.NET_DVR_FILE_NOFIND :
                                    Console.WriteLine("\n未找到文件！");
                                    isFinding = false;
                                    break;
                                case CHCNetSDK.NET_DVR_FILE_EXCEPTION :
                                    iLastErr = CHCNetSDK.NET_DVR_GetLastError();
                                    Console.WriteLine("\n查找文件时异常：error code: {0}", iLastErr);
                                    isFinding = false;
                                    break;
                                case CHCNetSDK.NET_DVR_NOMOREFILE :
                                default:
                                    Console.WriteLine("\n文件查找结束。共找到 {0} 张图片，下载 {1} 张图片... {2}",
                                        picCounter, downCounter, DateTime.Now.ToString());
                                    isFinding = false;
                                    break;
                            }
                        }
                    }
                }
            }
            catch (Exception)
            {

                throw;
            }
            finally
            {
                //停止查找
                if (Findhandler > 0)
                    CHCNetSDK.NET_DVR_CloseFindPicture(Findhandler);
                //注销用户
                if (m_UserID > 0)
                    CHCNetSDK.NET_DVR_Logout(m_UserID);
                //释放SDK资源
                if (m_InitSDK)
                    CHCNetSDK.NET_DVR_Cleanup();
            }
        }

        /// <summary>
        /// 返回查找句柄
        /// </summary>
        /// <param name="StartTime">DateTime类开始扫描时间</param>
        /// <param name="EndTime">结束扫描时间</param>
        /// <returns></returns>
        public Int32 getFindHandler(DateTime StartTime, DateTime EndTime)
        {
            PictureParam = new CHCNetSDK.NET_DVR_FIND_PICTURE_PARAM();
            PictureParam.lChannel = 1;
            PictureParam.byFileType = 0x25; //人脸抓拍
            PictureParam.byNeedCard = 0;

            //开始时间
            PictureParam.struStartTime.dwYear = (uint)StartTime.Year;
            PictureParam.struStartTime.dwMonth = (uint)StartTime.Month;
            PictureParam.struStartTime.dwDay = (uint)StartTime.Day;
            PictureParam.struStartTime.dwHour = (uint)StartTime.Hour;
            PictureParam.struStartTime.dwMinute = (uint)StartTime.Minute;
            PictureParam.struStartTime.dwSecond = (uint)StartTime.Second;

            //结束时间
            PictureParam.struStopTime.dwYear = (uint)EndTime.Year;
            PictureParam.struStopTime.dwMonth = (uint)EndTime.Month;
            PictureParam.struStopTime.dwDay = (uint)EndTime.Day;
            PictureParam.struStopTime.dwHour = (uint)EndTime.Hour;
            PictureParam.struStopTime.dwMinute = (uint)EndTime.Minute;
            PictureParam.struStopTime.dwSecond = (uint)EndTime.Second;

            return CHCNetSDK.NET_DVR_FindPicture(m_UserID, ref PictureParam);
        }
    }
}
