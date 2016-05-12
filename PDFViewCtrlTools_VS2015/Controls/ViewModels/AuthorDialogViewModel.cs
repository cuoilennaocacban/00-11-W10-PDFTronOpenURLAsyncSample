using pdftron.PDF.Tools.Controls.ViewModels.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Xaml.Input;

namespace pdftron.PDF.Tools.Controls.ViewModels
{
    public class AuthorDialogViewModel : ViewModelBase
    {
        public delegate void AuthorDialogFinishedDelegate(string authorName);
        public event AuthorDialogFinishedDelegate AuthorDialogFinished;

        public AuthorDialogViewModel()
        {
            InitCommands();
        }

        #region Commands

        private void InitCommands()
        {
            AuthorNameKeyUpCommand = new RelayCommand(AuthorNameKeyUpCommandImpl);
            AuthorNameChangedCommand = new RelayCommand(AuthorNameChangedCommandImpl);
            AuthorNameOkPressedCommand = new RelayCommand(AuthorNameOkPressedCommandImpl);
            AuthorNameCancelPressedCommand = new RelayCommand(AuthorNameCancelPressedCommandImpl);
        }
        public RelayCommand AuthorNameKeyUpCommand { get; private set; }
        public RelayCommand AuthorNameChangedCommand { get; private set; }
        public RelayCommand AuthorNameOkPressedCommand { get; private set; }
        public RelayCommand AuthorNameCancelPressedCommand { get; private set; }

        private void AuthorNameKeyUpCommandImpl(object keyArgs)
        {
            KeyRoutedEventArgs args = keyArgs as KeyRoutedEventArgs;
            if (args.Key == Windows.System.VirtualKey.Enter)
            {
                AuthorEntered();
            }
            else if (args.Key == Windows.System.VirtualKey.Escape)
            {
                AuthorCanceled();
            }
        }

        private void AuthorNameChangedCommandImpl(object newAuthorName)
        {
            string authorName = newAuthorName as string;
            if (authorName != null)
            {
                AuthorName = authorName;
            }
        }

        private void AuthorNameOkPressedCommandImpl(object sender)
        {
            AuthorEntered();
        }

        private void AuthorNameCancelPressedCommandImpl(object sender)
        {
            AuthorCanceled();
        }

        #endregion Commands


        #region Properties


        private string _AuthorName = string.Empty;
        public string AuthorName
        {
            get { return _AuthorName; }
            set
            {
                if (value != null)
                {
                    if (!value.Equals(_AuthorName))
                    {
                        _AuthorName = value;
                        RaisePropertyChanged();
                        RaisePropertyChanged("DoesAuthorNameHaveContent");
                    }
                }
            }
        }

        public bool DoesAuthorNameHaveContent
        {
            get { return !string.IsNullOrWhiteSpace(AuthorName); }
        }


        #endregion Properties


        #region Implementation


        private void AuthorEntered()
        {
            RaiseAuthorFinishedEvent(AuthorName);
        }

        private void AuthorCanceled()
        {
            RaiseAuthorFinishedEvent(null);
        }

        private void RaiseAuthorFinishedEvent(string password)
        {
            if (AuthorDialogFinished != null)
            {
                AuthorDialogFinished(password);
            }
        }

        #endregion Implementation


        #region GoBack

        public void GoBack()
        {
            AuthorCanceled();
        }

        #endregion GoBack
    }
}
