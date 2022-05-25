using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QuickSearch.ViewModels
{
    public class TextFileViewModel : ObservableObject
    {
        public TextFileViewModel(string path)
        {
            filePath = path;
        }

        private string filePath;
        public string FilePath { get => filePath; set { SetValue(ref filePath, value); OnPropertyChanged(nameof(Text)); } }

        private bool scrollToEnd;
        public bool ScrollToEnd { get => scrollToEnd; set => SetValue(ref scrollToEnd, value); }

        public string Text { 
            get {
                try
                {
                    var lines = System.IO.File.ReadLines(FilePath).Reverse().Take(100).Reverse();
                    return string.Join("\n", lines);
                } catch (Exception)
                {

                }
                return null;
            } 
        }
    }
}
