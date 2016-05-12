using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace pdftron.PDF.Tools.Utilities
{
    public delegate void OnButtonClickedHandler(IAuthorDialog sender, bool wasOkSelected);
    public interface IAuthorDialog
    {
        // This function is here to make it clear that the control in question should bind to one of these view models.
        void SetAuthorViewModel(pdftron.PDF.Tools.Controls.ViewModels.AuthorDialogViewModel ViewModel);

        void Show();
        void Hide();
    }
}
