using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace pdftron.PDF.Tools.Controls.ControlBase
{

    public delegate void ControlClosedDelegate();

    public interface ICloseableControl
    {
        event ControlClosedDelegate ControlClosed;

        void CloseControl();
    }
}
