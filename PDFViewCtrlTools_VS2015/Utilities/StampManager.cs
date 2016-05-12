using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using pdftron.PDF;

using UIPoint = Windows.Foundation.Point;
using PDFPoint = pdftron.PDF.Point;
using Windows.Foundation;

namespace pdftron.PDF.Tools.Utilities
{

    /// <summary>
    /// This class manages all the stamps the user has.
    /// For the initial version, it only manages a signature.
    /// </summary>
    class StampManager
    {
        private static string SIGNATURE_FILE_NAME = "SignatureFile.CompleteReader";
        private static PDFDoc _StampDoc;

        public StampManager()
        {
            GetStampDoc();
        }

        private PDFDoc GetStampDoc()
        {
            if (_StampDoc == null)
            {
                try
                {
                    _StampDoc = new PDFDoc(System.IO.Path.Combine(Windows.Storage.ApplicationData.Current.LocalFolder.Path, SIGNATURE_FILE_NAME));
                }
                catch (Exception)
                {
                    _StampDoc = new PDFDoc();
                }
            }
            return _StampDoc;
        }

        public async System.Threading.Tasks.Task<bool> HasDefaultSignature()
        {
            Windows.Storage.IStorageItem signatureFile = null;
            try
            {
                signatureFile = await Windows.Storage.ApplicationData.Current.LocalFolder.GetItemAsync(SIGNATURE_FILE_NAME);
            }
            catch (System.Exception)
            {
            }
           
            if (signatureFile != null)
            {
                PDFDoc doc = GetStampDoc();
                doc.LockRead();
                if (doc.GetPageCount() > 0)
                {
                    doc.UnlockRead();
                    return true;
                }
                doc.UnlockRead();
            }
            return false;
        }

        public async System.Threading.Tasks.Task<Page> GetDefaultSignature()
        {
            Windows.Storage.IStorageItem signatureFile = null;
            try
            {
                signatureFile = await Windows.Storage.ApplicationData.Current.LocalFolder.GetItemAsync(SIGNATURE_FILE_NAME);
            }
            catch (System.Exception)
            {
            }

            if (signatureFile != null)
            {
                PDFDoc doc = GetStampDoc();
                Page page = null;
                doc.LockRead();
                if (doc.GetPageCount() > 0)
                {
                    page = doc.GetPage(1);
                    
                }
                doc.UnlockRead();
                return page;
            }
            return null;
        }

        public async Task DeleteDefaultSignatureFile()
        {
            Windows.Storage.IStorageItem signatureFile = null;
            try
            {
                signatureFile = await Windows.Storage.ApplicationData.Current.LocalFolder.GetItemAsync(SIGNATURE_FILE_NAME).AsTask().ConfigureAwait(false);
            }
            catch (System.Exception)
            {
            }
           
            if (signatureFile != null)
            {
                PDFDoc doc = GetStampDoc();
                try
                {
                    doc.Lock();
                    doc.PageRemove(doc.GetPageIterator(1));
                }
                catch (Exception)
                {
                }
                finally
                {
                    doc.Unlock();
                }

                try
                {
                    await doc.SaveAsync(System.IO.Path.Combine(Windows.Storage.ApplicationData.Current.LocalFolder.Path, SIGNATURE_FILE_NAME), SDF.SDFDocSaveOptions.e_remove_unused);
                }
                catch (Exception)
                {
                    // failed to save doc
                }

            }
        }


        public async Task<pdftron.PDF.Page> CreateSignatureAsync(List<List<UIPoint>> listOfPointLists, Windows.UI.Color strokeColor,
            double leftmost, double rightmost, double topmost, double bottommost, bool makeDefault = false, bool useStylus = false)
        {
            PDFDoc doc = await CreateDocumentAsync(listOfPointLists, strokeColor, leftmost, rightmost, topmost, bottommost, makeDefault, useStylus).ConfigureAwait(false);
            return doc.GetPage(1);
        }




        private async Task<pdftron.PDF.PDFDoc> CreateDocumentAsync(List<List<UIPoint>> listOfPointLists, Windows.UI.Color strokeColor,
            double leftmost, double rightmost, double topmost, double bottommost, bool makeDefault, bool useStylus = false)
        {
            PDFDoc doc = null;

            try
            {
                // create a new page with a buffer of 20 on each side.
                if (makeDefault)
                {
                    doc = GetStampDoc();
                    doc.Lock();
                    if (doc.GetPageCount() > 0)
                    {
                        doc.PageRemove(doc.GetPageIterator(1));
                    }
                }
                else
                {
                    doc = new PDFDoc();
                    doc.Lock();
                }
                Page page = doc.PageCreate(new Rect(0, 0, rightmost - leftmost + 40, bottommost - topmost + 40));
                doc.PagePushBack(page);


                // create the annotation in the middle of the page.
                pdftron.PDF.Annots.Ink ink = pdftron.PDF.Annots.Ink.Create(doc.GetSDFDoc(), new Rect(20, 20, rightmost - leftmost + 20, bottommost - topmost + 20));
                AnnotBorderStyle bs = ink.GetBorderStyle();
                bs.width = 2;
                ink.SetBorderStyle(bs);

                if (useStylus)
                {
                    ink.SetSmoothing(false);
                }

                // Shove the points to the ink annotation
                int i = 0;
                Point pdfp = new Point();
                foreach (List<UIPoint> pointList in listOfPointLists)
                {
                    int j = 0;
                    foreach (UIPoint p in pointList)
                    {
                        pdfp.x = p.X - leftmost + 20;
                        pdfp.y = bottommost - p.Y + 20;
                        ink.SetPoint(i, j, pdfp);
                        j++;
                    }
                    i++;
                }

                double r = strokeColor.R / 255.0;
                double g = strokeColor.G / 255.0;
                double b = strokeColor.B / 255.0;
                ink.SetColor(new ColorPt(r, g, b));
                ink.RefreshAppearance();

                // Make the page crop box the same as the annotation bounding box, so that there's no gaps.
                Rect newBoundRect = ink.GetRect();
                page.SetCropBox(newBoundRect);

                ink.RefreshAppearance();

                page.AnnotPushBack(ink);
            }
            catch (System.Exception)
            {
            }
            finally
            {
                if (doc != null)
                {
                    doc.Unlock();
                }
            }

            if (null != doc && makeDefault)
            {
                await doc.SaveAsync(System.IO.Path.Combine(Windows.Storage.ApplicationData.Current.LocalFolder.Path, SIGNATURE_FILE_NAME), SDF.SDFDocSaveOptions.e_remove_unused).AsTask().ConfigureAwait(false);
            }

            return doc;
        }
    }
}
