using pdftron.PDF.Annots;
using System;
using Windows.UI.Xaml.Controls;

using PDFRect = pdftron.PDF.Rect;

namespace pdftron.PDF.Tools.Utilities
{
    public class NoteDialogBase : UserControl
    {
        protected IMarkup _Annotation;
        protected PDFViewCtrl _PDFViewCtrl;
        protected ToolManager _ToolManager;
        protected int _AnnotPageNumber = 0;
        protected string _OriginalText = string.Empty;

        public bool FocusWhenOpened { get; set; }

        private bool _InitialAnnot = false;
        public bool InitialAnnot
        {
            get { return _InitialAnnot; }
            set { _InitialAnnot = value; }
        }

        public NoteDialogBase()
        {

        }

        public delegate void NoteClosedDialogHandler(bool deleted);
        public event NoteClosedDialogHandler NoteClosed = delegate { };
        protected void RaiseNoteClosedEvent(bool deleted)
        {
            if (NoteClosed != null)
            {
                NoteClosed(deleted);
            }
        }

        public NoteDialogBase(IMarkup annotation, PDFViewCtrl ctrl, ToolManager toolManager, int annotPageNumber)
        {
            _Annotation = annotation;
            _PDFViewCtrl = ctrl;
            _ToolManager = toolManager;
            _AnnotPageNumber = annotPageNumber;
            _ToolManager.mCurrentNoteDialog = this;
        }

        public virtual void CancelAndClose()
        {
            Close();
        }

        public virtual void Close()
        {
            _ToolManager.mCurrentNoteDialog = null;
        }

        protected string GetTextFromAnnot()
        {
            try
            {
                _PDFViewCtrl.DocLockRead();
                if (_Annotation != null)
                {
                    string text = _Annotation.GetContents();
                    if (text != null)
                    {
                        _OriginalText = text;
                        return _OriginalText;
                    }
                }
            }
            catch (Exception)
            {

            }
            finally
            {
                _PDFViewCtrl.DocUnlockRead();
            }

            _OriginalText = string.Empty;
            return _OriginalText;
        }

        protected void SetAnnotText(string text)
        {
            if (text != null && !InitialAnnot && text.Equals(_OriginalText))
            {
                return;
            }
            try
            {
                _PDFViewCtrl.DocLock(true);

                PDFRect rect = _Annotation.GetRect();
                rect.Normalize();

                pdftron.PDF.Annots.Popup pop = _Annotation.GetPopup();
                if (pop == null || !pop.IsValid())
                {                // We need to give this a larger rect, else it won't show up properly in adobe reader.
                    pop = pdftron.PDF.Annots.Popup.Create(_PDFViewCtrl.GetDoc().GetSDFDoc(), new PDFRect(rect.x2 + 50, rect.y2 + 50, rect.x2 + 200, rect.y2 + 200));
                    pop.SetParent(_Annotation);
                    _Annotation.SetPopup(pop);
                }

                _Annotation.SetContents(text);

                if (InitialAnnot)
                {
                    _ToolManager.CurrentTool.SetAuthor(_Annotation);
                }

                if (_InitialAnnot)
                {
                    _ToolManager.RaiseAnnotationAddedEvent(_Annotation);
                }
                else
                {
                    _ToolManager.RaiseAnnotationEditedEvent(_Annotation);
                }
            }
            catch (Exception)
            {
            }
            finally
            {
                _PDFViewCtrl.DocUnlock();
            }
        }

        protected void DeleteAnnot()
        {
            try
            {
                _PDFViewCtrl.DocLock(true);
                Page page = _PDFViewCtrl.GetDoc().GetPage(_AnnotPageNumber);
                page.AnnotRemove(_Annotation);
                _PDFViewCtrl.UpdateWithAnnot(_Annotation, _AnnotPageNumber);
                if (!_InitialAnnot)
                {
                    _ToolManager.RaiseAnnotationRemovedEvent(_Annotation);
                }
            }
            catch (Exception)
            {
            }
            finally
            {
                _PDFViewCtrl.DocUnlock();
                _Annotation = null;
            }
        }

    }
}
