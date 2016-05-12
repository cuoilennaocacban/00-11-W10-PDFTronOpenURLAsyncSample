using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace pdftron.PDF.Tools.Utilities
{
    public class AnalyticsHandlerBase
    {
        private static AnalyticsHandlerBase _CURRENT;
        public static AnalyticsHandlerBase CURRENT
        {
            get
            {
                if (_CURRENT == null)
                {
                    _CURRENT = new AnalyticsHandlerBase();
                }
                return _CURRENT;
            }
            set
            {
                _CURRENT = value;
            }
        }

        public virtual void AddCrashExtraData(string key, string value)
        {

        }

        public virtual void ClearCrashExtraData()
        {

        }

        public virtual bool RemoveCrashExtraData(string keyName)
        {
            return false;
        }

        public virtual void LogException(Exception ex)
        {

        }
        public virtual void LogException(Exception ex, string key, string value)
        {

        }

        public virtual void SendException(Exception ex)
        {
        }

        public virtual void SendException(Exception ex, string key, string value)
        {
        }

        public virtual void SendException(Exception ex, IDictionary<String, String> map)
        {
        }

        public virtual void LeaveBreadcrumb(string evt)
        {

        }

        public virtual void ClearBreadCrumbs()
        {

        }

        public virtual void LastActionBeforeTerminate(System.Action lastAction)
        {

        }

        public virtual void LogEvent(string tag)
        {

        }

        public virtual void SendEvent(string action)
        {
            
        }

        public virtual void SendEvent(string category, string action)
        {

        }

        public virtual void SendEvent(string category, string action, string label)
        {

        }

        public virtual void Flush()
        {
        }

        public virtual void ClearTotalCrashesNum()
        { 
        }


        public virtual void CloseSession()
        {
        }

    }
}