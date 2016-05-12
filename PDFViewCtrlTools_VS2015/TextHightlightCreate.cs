using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace pdftron.PDF.Tools
{
    class TextHightlightCreate : TextMarkupCreate
    {
        public TextHightlightCreate(PDFViewCtrl ctrl, ToolManager tMan)
            : base(ctrl, tMan)
        {
            mNextToolMode = ToolType.e_text_highlight;
            mToolMode = ToolType.e_text_highlight;
        }
    }
}
