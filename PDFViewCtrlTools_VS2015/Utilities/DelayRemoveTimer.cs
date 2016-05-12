using pdftron.PDF;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace pdftron.PDF.Tools
{
    /// <summary>
    /// This struct keeps track of everything necessary to remove canvases once a certain amount of time has expired.
    /// </summary>
    internal class DelayRemoveTimer
    {
        internal DispatcherTimer Timer;
        internal Canvas ViewerCanvas; // The canvas on which to remove an element from
        internal UIElement Target; // The element to remove
        internal Tool Tool; // needed to reference the list of Items to remove
        internal ToolManager ToolManager;

        // temporary for testing until we have a better solution
        int ticks;
        PDFViewCtrl ctrl;

        internal DelayRemoveTimer(Canvas vc, Tool tool, UIElement target, PDFViewCtrl c, ToolManager toolManager)
        {
            ViewerCanvas = vc;
            Tool = tool;
            Target = target;
            ticks = 1;
            ctrl = c;
            this.ToolManager = toolManager;

            Timer = new DispatcherTimer(); // create new timer
            Timer.Interval = TimeSpan.FromSeconds(5);
            Timer.Tick += Timer_Tick;
            Timer.Start();
        }

        private void Timer_Tick(object sender, object e)
        {
            ticks--;

            try
            {
                // Remove the overlaying shape
                if (ctrl.IsFinishedRendering(true))
                {
                    Remove();
                    this.ToolManager.mDelayRemoveTimers.Remove(this);
                }
                else if (ticks == 0)
                {
                    Remove();
                    this.ToolManager.mDelayRemoveTimers.Remove(this);
                }
            }
            catch (Exception) // this could cause problems if PDFViewCtrl is destroyed by garbage collector
            {
                
            }
        }

        // Lets the tools remove the overlaying shape, for example when the user flips pages or zooms in or out
        internal void Destroy()
        {
            Remove();
        }

        private void Remove()
        {
            if (ViewerCanvas != null && ViewerCanvas.Children.Contains(Tool))
            {
                ViewerCanvas.Children.Remove(Tool);
            }
            Timer.Stop();
        }
    }
}
