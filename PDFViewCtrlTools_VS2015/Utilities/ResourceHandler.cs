using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace pdftron.PDF.Tools
{
    static class ResourceHandler
    {

        private static Windows.ApplicationModel.Resources.ResourceLoader _ResourceLoader =
            Windows.ApplicationModel.Resources.ResourceLoader.GetForCurrentView("pdftron.PDF.Tools/Resources");

        public static string GetString(string key)
        {
            return _ResourceLoader.GetString("pdftron_Tool_" + key);
        }
    }
}
