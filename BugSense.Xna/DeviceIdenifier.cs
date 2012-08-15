using System;
using System.Runtime.InteropServices;
using MonoTouch.Foundation;
using MonoTouch.UIKit;

namespace MonoTouch.Foundation
{
    public class DeviceIdentifier
    {
        public const string HardwareProperty = "hw.machine";

        public enum HardwareVersion
        {            
            iPhone,            
            iPhone3G,            
            iPhone3GS,            
            iPhone4,            
            VerizoniPhone4,            
            iPhone4S,            
            iPod1G,            
            iPod2G,   
            iPod3G,            
            iPod4G,            
            iPad,            
            iPad2WIFI,            
            iPad2WIFI24,            
            iPad2GSM,            
            iPad2CDMA,            
            iPad3WIFI,            
            iPad3GSM,            
            iPad3CDMA,           
            iPhoneSimulator,            
            iPhone4Simulator,            
            iPadSimulator,            
            Unknown            
        }
        
        [DllImport(MonoTouch.Constants.SystemLibrary)]        
        static internal extern int sysctlbyname([MarshalAs(UnmanagedType.LPStr)] string property, IntPtr output, IntPtr oldLen, IntPtr newp, uint newlen);
        
        public static HardwareVersion Version
        {
            get
            {
                var pLen = Marshal.AllocHGlobal(sizeof(int));                
                sysctlbyname(DeviceIdentifier.HardwareProperty, IntPtr.Zero, pLen, IntPtr.Zero, 0);             
                
                var length = Marshal.ReadInt32(pLen);               
                
                if (length == 0)
                {
                    Marshal.FreeHGlobal(pLen);
                    return HardwareVersion.Unknown;
                    
                }
                
                var pStr = Marshal.AllocHGlobal(length);                
                sysctlbyname(DeviceIdentifier.HardwareProperty, pStr, pLen, IntPtr.Zero, 0);                
                
                var hardwareStr = Marshal.PtrToStringAnsi(pStr);                
                var ret = HardwareVersion.Unknown;            
                
                
                switch (hardwareStr)
                {
                case "iPhone1,1":
                    ret = HardwareVersion.iPhone;
                    break;
                    
                case "iPhone1,2":
                    ret = HardwareVersion.iPhone3G;
                    break;
                    
                case "iPhone2,1":
                    ret = HardwareVersion.iPhone3GS;
                    break;
                    
                case "iPhone3,1":
                    ret = HardwareVersion.iPhone4;
                    break;
                    
                case "iPhone3,3":
                    ret = HardwareVersion.VerizoniPhone4;
                    break;
                    
                case "iPhone4,1":
                    ret = HardwareVersion.iPhone4S;
                    break;
                    
                case "iPad1,1":
                    ret = HardwareVersion.iPad;
                    break;
                    
                case "iPad2,1":
                    ret = HardwareVersion.iPad2WIFI;
                    break;
                    
                case "iPad2,2":
                    ret = HardwareVersion.iPad2GSM;
                    break;
                    
                case "iPad2,3":
                    ret = HardwareVersion.iPad2CDMA;
                    break;
                    
                case "iPad2,4":
                    ret = HardwareVersion.iPad2WIFI24;
                    break;
                    
                case "iPad3,1":
                    ret = HardwareVersion.iPad3WIFI;
                    break;
                    
                case "iPad3,2":
                    ret = HardwareVersion.iPad3GSM;
                    break;
                    
                case "iPad3,3":
                    ret = HardwareVersion.iPad3CDMA;
                    break;
                    
                case "iPod1,1":
                    ret = HardwareVersion.iPod1G;
                    break;
                    
                case "iPod2,1":
                    ret = HardwareVersion.iPod2G;
                    break;
                    
                case "iPod3,1":
                    ret = HardwareVersion.iPod3G;
                    break;
                    
                case "iPod4,1":
                    ret = HardwareVersion.iPod4G;
                    break;
                    
                case "i386":
                case "x86_64":
                    if (UIDevice.CurrentDevice.Model.Contains("iPhone"))
                    {
                        ret = UIScreen.MainScreen.Bounds.Height * UIScreen.MainScreen.Scale == 960 || 
                            UIScreen.MainScreen.Bounds.Width * UIScreen.MainScreen.Scale == 960 ? 
                            HardwareVersion.iPhone4Simulator : HardwareVersion.iPhoneSimulator;
                    }
                    else
                        ret = HardwareVersion.iPadSimulator;
                    break;
                }
                
                Marshal.FreeHGlobal(pLen);
                Marshal.FreeHGlobal(pStr);
                
                return ret;
            }
            
        }
        
    }
    
}