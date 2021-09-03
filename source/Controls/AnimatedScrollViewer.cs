using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace QuickSearch.Controls
{
    /// <summary>
    /// Interaktionslogik für AnimatedScrollViewer.xaml
    /// </summary>
    public partial class AnimatedScrollViewer : ScrollViewer
    {
        public new double HorizontalOffset
        {
            get => (double)GetValue(ScrollViewer.HorizontalOffsetProperty);
            set => base.ScrollToHorizontalOffset(value);
        }

        public static new DependencyProperty HorizontalOffsetProperty =
            DependencyProperty.Register(nameof(HorizontalOffset), typeof(double), typeof(AnimatedScrollViewer),
                new PropertyMetadata(new PropertyChangedCallback(OnHorizontalOffsetChanged)));

        private static void OnHorizontalOffsetChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
        {
            if (sender is AnimatedScrollViewer asv)
            {
                asv.HorizontalOffset = (double)e.NewValue;
            }
        }

        public new double VerticalOffset
        {
            get => (double)GetValue(ScrollViewer.VerticalOffsetProperty);
            set => base.ScrollToVerticalOffset(value);
        }

        public static new DependencyProperty VerticalOffsetProperty =
            DependencyProperty.Register(nameof(VerticalOffset), typeof(double), typeof(AnimatedScrollViewer),
                new PropertyMetadata(new PropertyChangedCallback(OnVerticalOffsetChanged)));

        private static void OnVerticalOffsetChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
        {
            if (sender is AnimatedScrollViewer asv)
            {
                asv.VerticalOffset = (double)e.NewValue;
            }
        }

        public AnimatedScrollViewer() : base()
        {
            
        }
    }
}

